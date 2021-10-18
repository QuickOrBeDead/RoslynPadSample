namespace RoslynPadSample.Build
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Scripting;

    using RoslynPad.Roslyn;

    public sealed class ExecutionHost
    {
        private readonly BuildInfo _buildInfo;

        private readonly RoslynHost _roslynHost;
        private readonly DocumentId _documentId;

        private ScriptOptions _scriptOptions;

        private readonly IAnalyzerAssemblyLoader _analyzerAssemblyLoader;

        private Task _restoreTask;
        private CancellationTokenSource _restoreCts;

        public event Action<string> RestoreMessage;
        public event Action<MetadataReference[], AnalyzerFileReference[]> RestoreCompleted;

        public ExecutionHost(BuildInfo buildInfo, RoslynHost roslynHost, DocumentId documentId)
        {
            _buildInfo = buildInfo ?? throw new ArgumentNullException(nameof(buildInfo));
            _roslynHost = roslynHost ?? throw new ArgumentNullException(nameof(roslynHost));
            _documentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
            _analyzerAssemblyLoader = _roslynHost.GetService<IAnalyzerAssemblyLoader>();
        }

        public void Restore()
        {
            if (_restoreCts != null)
            {
                _restoreCts.Cancel();
                _restoreCts.Dispose();
            }

            var restoreCts = new CancellationTokenSource();
            _restoreTask = RestoreAsync(GetCurrentRestoreTask(), restoreCts.Token);
            _restoreCts = restoreCts;
        }

        private async Task RestoreAsync(Task previousRestoreTask, CancellationToken cancellationToken)
        {
            try
            {
                await previousRestoreTask.ConfigureAwait(false);
            }
            catch
            {
                // Empty
            }

            try
            {
                await BuildGlobalJsonAsync(cancellationToken).ConfigureAwait(false);
                var csprojPath = await BuildCsprojAsync(cancellationToken).ConfigureAwait(false);

                var errorsPath = Path.Combine(_buildInfo.BuildPath, "errors.log");
                File.Delete(errorsPath);

                cancellationToken.ThrowIfCancellationRequested();

                using var restoreProcess = RunProcess(
                    _buildInfo.DotNetSdkInfo.DotNetExe,
                    _buildInfo.BuildPath,
                    $"build -nologo -p:nugetinteractive=true -flp:errorsonly;logfile=\"{errorsPath}\" \"{csprojPath}\"",
                    cancellationToken);

                await ReadRestoreProcessStandardOutputAsync(restoreProcess).ConfigureAwait(false);
                await restoreProcess.WaitForExitAsync(cancellationToken).ConfigureAwait(false);

                if (restoreProcess.ExitCode != 0)
                {
                    await ReadRestoreProcessErrorAsync(restoreProcess, errorsPath, cancellationToken).ConfigureAwait(false);

                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();

                var references = await ReadBuildFileLinesAsync(MSBuildHelper.ReferencesFile, cancellationToken).ConfigureAwait(false);
                var analyzers = await ReadBuildFileLinesAsync(MSBuildHelper.AnalyzersFile, cancellationToken).ConfigureAwait(false);

                var metadataReferences = references.Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => _roslynHost.CreateMetadataReference(r)).ToArray();

                var analyzerReferences = analyzers.Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => new AnalyzerFileReference(r, _analyzerAssemblyLoader)).ToArray();

                // _scriptOptions = _scriptOptions.WithReferences(metadataReferences);
                RestoreMessage?.Invoke("Restore completed successfully...");
                RestoreCompleted?.Invoke(metadataReferences, analyzerReferences);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                RestoreMessage?.Invoke($"Restore completed with error: {ex.Message}");
            }
        }

        private async Task ReadRestoreProcessErrorAsync(
            Process restoreProcess,
            string errorsPath,
            CancellationToken cancellationToken)
        {
            try
            {
                var errors = await File.ReadAllLinesAsync(errorsPath, cancellationToken).ConfigureAwait(false);
                if (errors.Length > 0)
                {
                    for (var i = 0; i < errors.Length; i++)
                    {
                        RestoreMessage?.Invoke(errors[i]);
                    }
                }
                else
                {
                    await ReadRestoreProcessStandardErrorAsync(restoreProcess).ConfigureAwait(false);
                }
            }
            catch (FileNotFoundException)
            {
                await ReadRestoreProcessStandardErrorAsync(restoreProcess).ConfigureAwait(false);
            }
        }

        private async Task ReadRestoreProcessStandardOutputAsync(Process restoreProcess)
        {
            string line;
            while ((line = await restoreProcess.StandardOutput.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    RestoreMessage?.Invoke(line.Trim());
                }
            }
        }

        private async Task ReadRestoreProcessStandardErrorAsync(Process restoreProcess)
        {
            string line;
            while ((line = await restoreProcess.StandardError.ReadLineAsync().ConfigureAwait(false)) != null)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    RestoreMessage?.Invoke(line.Trim());
                }
            }
        }

        private Task GetCurrentRestoreTask() => _restoreTask ?? Task.CompletedTask;

        private Task BuildGlobalJsonAsync(CancellationToken cancellationToken)
        {
            return File.WriteAllTextAsync(
                Path.Combine(_buildInfo.BuildPath, "global.json"),
                $"{{\r\n  \"sdk\": {{\r\n    \"version\": \"{_buildInfo.DotNetSdkInfo.FrameworkVersion}\"\r\n  }}\r\n}}",
                cancellationToken);
        }

        private async Task<string> BuildCsprojAsync(CancellationToken cancellationToken)
        {
            var csproj = MSBuildHelper.CreateCsproj(_buildInfo.DotNetSdkInfo.TargetFrameworkMoniker);
            var csprojPath = Path.Combine(_buildInfo.BuildPath, "rpSampleProject.csproj");

            using (var fs = new FileStream(csprojPath, FileMode.Create))
            {
                await csproj.SaveAsync(fs, SaveOptions.None, cancellationToken).ConfigureAwait(false);
            }
           
            return csprojPath;
        }

        private static Process RunProcess(string path, string workingDirectory, string arguments, CancellationToken cancellationToken)
        {
            var process = new Process
                              {
                                  StartInfo = new ProcessStartInfo
                                                  {
                                                      FileName = path,
                                                      WorkingDirectory = workingDirectory,
                                                      Arguments = arguments,
                                                      RedirectStandardOutput = true,
                                                      RedirectStandardError = true,
                                                      CreateNoWindow = true,
                                                      UseShellExecute = false,
                                                  }
                              };

            using var _ = cancellationToken.Register(() =>
                {
                    try
                    {
                        process.Kill();
                    }
                    catch
                    {
                        // Empty
                    }
                });

            process.Start();
            return process;
        }

        private Task<string[]> ReadBuildFileLinesAsync(string file, CancellationToken cancellationToken)
        {
            return File.ReadAllLinesAsync(Path.Combine(_buildInfo.BuildPath, file), cancellationToken);
        }

        public async Task ExecuteAsync(string code)
        {
            await GetCurrentRestoreTask().ConfigureAwait(false);
        }
    }
}
