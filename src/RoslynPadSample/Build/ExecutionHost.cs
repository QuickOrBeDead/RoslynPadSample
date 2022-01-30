namespace RoslynPadSample.Build
{
    using System;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Text;
    using System.Threading;
    using System.Threading.Tasks;
    using System.Xml.Linq;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.CSharp;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Scripting;

    using RoslynPad.Roslyn;
    using RoslynPad.Roslyn.Scripting;

    public sealed class ExecutionHost
    {
        private readonly BuildInfo _buildInfo;
        private readonly RoslynHost _roslynHost;

        private readonly IAnalyzerAssemblyLoader _analyzerAssemblyLoader;

        private Task _restoreTask;
        private CancellationTokenSource _restoreCts;

        private MetadataReference[] _metadataReferences;

        public event Action<string> RestoreMessage;
        public event Action<string> BuildMessage;
        public event Action<string> ConsoleMessage;
        public event Action<MetadataReference[], AnalyzerFileReference[]> RestoreCompleted;

        public ExecutionHost(BuildInfo buildInfo, RoslynHost roslynHost)
        {
            _buildInfo = buildInfo ?? throw new ArgumentNullException(nameof(buildInfo));
            _roslynHost = roslynHost ?? throw new ArgumentNullException(nameof(roslynHost));
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

                _metadataReferences = references.Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => _roslynHost.CreateMetadataReference(r)).ToArray();

                var analyzerReferences = analyzers.Where(r => !string.IsNullOrWhiteSpace(r))
                    .Select(r => new AnalyzerFileReference(r, _analyzerAssemblyLoader)).ToArray();

                RestoreMessage?.Invoke("Restore completed successfully...");
                RestoreCompleted?.Invoke(_metadataReferences, analyzerReferences);
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

            using var executeCts = new CancellationTokenSource();
            var cancellationToken = executeCts.Token;

            var scriptOptions = ScriptOptions.Default;
            if (_metadataReferences != null)
            {
                scriptOptions = scriptOptions.WithReferences(_metadataReferences);
            }

            var script = new ScriptRunner(
                code,
                parseOptions: _roslynHost.ParseOptions as CSharpParseOptions,
                outputKind: OutputKind.ConsoleApplication,
                platform: Platform.AnyCpu,
                references: scriptOptions.MetadataReferences,
                usings: scriptOptions.Imports,
                filePath: scriptOptions.FilePath,
                workingDirectory: _buildInfo.BuildPath,
                metadataResolver: scriptOptions.MetadataResolver,
                optimizationLevel: OptimizationLevel.Release,
                checkOverflow: true,
                allowUnsafe: true);

            var assemblyPath = Path.Combine(_buildInfo.BuildPath, "bin", "rpSampleProject.dll");

            var diagnostics = await script.SaveAssembly(assemblyPath, cancellationToken).ConfigureAwait(false);
            
            // TODO: send diagnostics messages to UI Console

            BuildMessage?.Invoke($"{assemblyPath} assembly save ended.");

            var errorDiagnostics = diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
            if (errorDiagnostics.Any())
            {
                // TODO: send error diagnostics messages to UI Console

                BuildMessage?.Invoke("Build FAILED.");

                return;
            }

            BuildMessage?.Invoke("Build succeeded.");

            await RunProcess(assemblyPath, cancellationToken);
        }

        private async Task RunProcess(string assemblyPath, CancellationToken cancellationToken)
        {
            using (var process = new Process
                                     {
                                         StartInfo = new ProcessStartInfo
                                                         {
                                                             FileName = _buildInfo.DotNetSdkInfo.DotNetExe,
                                                             Arguments =
                                                                 $"\"{assemblyPath}\" --pid {Environment.ProcessId}",
                                                             WorkingDirectory = Path.GetDirectoryName(assemblyPath),
                                                             CreateNoWindow = true,
                                                             UseShellExecute = false,
                                                             RedirectStandardOutput = true,
                                                             RedirectStandardError = true,
                                                             RedirectStandardInput = true,
                                                             StandardOutputEncoding = Encoding.UTF8,
                                                             StandardErrorEncoding = Encoding.UTF8
                                                         }
                                     })
            {
                await using (cancellationToken.Register(() =>
                    {
                        try
                        {
                            process?.Kill();
                        }
                        catch
                        {
                            // Empty
                        }
                    }))
                {
                    if (process.Start())
                    {
                        await Task.WhenAll(
                            Task.Run(() => ReadProcessStream(process.StandardOutput), cancellationToken),
                            Task.Run(() => ReadProcessStream(process.StandardError), cancellationToken));
                    }
                }
            }
        }

        private async Task ReadProcessStream(StreamReader reader)
        {
            while (!reader.EndOfStream)
            {
                var line = await reader.ReadLineAsync().ConfigureAwait(false);
                if (line != null)
                {
                    ConsoleMessage?.Invoke(line);
                }
            }
        }
    }
}
