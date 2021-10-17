namespace RoslynPadSample.Build
{
    using System;
    using System.Collections.Generic;
    using System.Collections.Immutable;
    using System.Diagnostics;
    using System.IO;
    using System.Linq;
    using System.Runtime.InteropServices;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;
    using Microsoft.CodeAnalysis.Scripting;
    using NuGet.Versioning;

    using RoslynPad.Roslyn;

    public sealed class ExecutionHost
    {
        private readonly RoslynHost _roslynHost;

        private readonly DocumentId _documentId;

        private readonly string _buildPath;

        private ScriptOptions _scriptOptions;

        private readonly string _dotNetExe;
        private readonly string _dotNetSdkPath;
        private readonly string _dotNetTargetFrameworkMoniker;
        private readonly string _dotNetSdkVersion;

        private readonly IAnalyzerAssemblyLoader _analyzerAssemblyLoader;

        public ExecutionHost(RoslynHost roslynHost, DocumentId documentId, string buildPath, IEnumerable<string> imports)
        {
            if (imports == null)
            {
                throw new ArgumentNullException(nameof(imports));
            }

            if (string.IsNullOrWhiteSpace(buildPath))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(buildPath));
            }

            _roslynHost = roslynHost ?? throw new ArgumentNullException(nameof(roslynHost));
            _documentId = documentId ?? throw new ArgumentNullException(nameof(documentId));
            _analyzerAssemblyLoader = _roslynHost.GetService<IAnalyzerAssemblyLoader>();

            _buildPath = buildPath;
            _scriptOptions = ScriptOptions.Default.WithImports(imports);

            (_dotNetExe, _dotNetSdkPath) = FindNetCore();
            (_dotNetTargetFrameworkMoniker, _dotNetSdkVersion) = GetCoreSdkVersionInfo(_dotNetSdkPath);
        }

        public void Restore()
        {
            BuildGlobalJson();
            var csprojPath = BuildCsproj();

            var errorsPath = Path.Combine(_buildPath, "errors.log");
            File.Delete(errorsPath);

            using var result = RunProcess(_dotNetExe, _buildPath, $"build -nologo -p:nugetinteractive=true -flp:errorsonly;logfile=\"{errorsPath}\" \"{csprojPath}\"");

            result.WaitForExit();

            var references = ReadBuildFileLines(MSBuildHelper.ReferencesFile);
            var analyzers = ReadBuildFileLines(MSBuildHelper.AnalyzersFile);

            var metadataReferences = references
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => _roslynHost.CreateMetadataReference(r))
                .ToArray();

            var analyzerReferences = analyzers
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .Select(r => new AnalyzerFileReference(r, _analyzerAssemblyLoader))
                .ToArray();

            _scriptOptions = _scriptOptions.WithReferences(metadataReferences);

            var document = _roslynHost.GetDocument(_documentId);
            if (document == null)
            {
                throw new InvalidOperationException($"Document not found for documentId: {_documentId}");
            }

            var project = document.Project;

            project = project
                .WithMetadataReferences(metadataReferences)
                .WithAnalyzerReferences(analyzerReferences);

            document = project.GetDocument(_documentId);

            _roslynHost.UpdateDocument(document);
        }

        private void BuildGlobalJson()
        {
            var global = $"{{\r\n  \"sdk\": {{\r\n    \"version\": \"{_dotNetSdkVersion}\"\r\n  }}\r\n}}";
            File.WriteAllText(Path.Combine(_buildPath, "global.json"), global);
        }

        private string BuildCsproj()
        {
            var csproj = MSBuildHelper.CreateCsproj(_dotNetTargetFrameworkMoniker);
            var csprojPath = Path.Combine(_buildPath, "rpSampleProject.csproj");

            csproj.Save(csprojPath);
            return csprojPath;
        }

        private static (string tfm, string name) GetCoreSdkVersionInfo(string sdkPath)
        {
            var dictionary = new Dictionary<NuGetVersion, (string tfm, string name)>();

            foreach (var directory in Directory.EnumerateDirectories(sdkPath))
            {
                var versionName = Path.GetFileName(directory);
                if (NuGetVersion.TryParse(versionName, out var version) && version.Major > 1)
                {
                    dictionary.Add(version, ($"netcoreapp{version.Major}.{version.Minor}", versionName));
                }
            }

            return dictionary.OrderBy(c => c.Key.IsPrerelease)
                              .ThenByDescending(c => c.Key)
                              .Select(c => c.Value)
                              .FirstOrDefault();
        }

        private static (string dotnetExe, string sdkPath) FindNetCore()
        {
            string[] dotnetPaths;
            string dotnetExe;
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                dotnetPaths = new[] { Path.Combine(Environment.GetEnvironmentVariable("ProgramW6432"), "dotnet") };
                dotnetExe = "dotnet.exe";
            }
            else
            {
                dotnetPaths = new[] { "/usr/share/dotnet", "/usr/local/share/dotnet" };
                dotnetExe = "dotnet";
            }

            var sdkPath = (from path in dotnetPaths
                           let fullPath = Path.Combine(path, "sdk")
                           where Directory.Exists(fullPath)
                           select fullPath).FirstOrDefault();

            if (sdkPath != null)
            {
                dotnetExe = Path.GetFullPath(Path.Combine(sdkPath, "..", dotnetExe));
                if (File.Exists(dotnetExe))
                {
                    return (dotnetExe, sdkPath);
                }
            }

            return (string.Empty, string.Empty);
        }

        private static Process RunProcess(string path, string workingDirectory, string arguments)
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

            process.Start();
            return process;
        }

        private string[] ReadBuildFileLines(string file)
        {
            var path = Path.Combine(_buildPath, file);
            return File.ReadAllLines(path);
        }
    }
}
