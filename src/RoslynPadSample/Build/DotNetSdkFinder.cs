namespace RoslynPadSample.Build
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;

    using NuGet.Versioning;

    public sealed class DotNetSdkFinder
    {
        public IList<DotNetSdkInfo> GetDotNetSdkList()
        {
            var (dotNetExe, dotNetSdkPath) = FindNetCoreSdkPath();
            return GetDotNetSdkList(dotNetSdkPath, dotNetExe);
        }

        private static IList<DotNetSdkInfo> GetDotNetSdkList(string sdkPath, string dotNetExe)
        {
            var dictionary = new Dictionary<NuGetVersion, (string tfm, string version)>();

            foreach (var directory in Directory.EnumerateDirectories(sdkPath))
            {
                var versionName = Path.GetFileName(directory);
                if (NuGetVersion.TryParse(versionName, out var version) && version.Major > 1)
                {

                    dictionary.Add(version, ($"{(version.Major >= 5 ? "net" : "netcoreapp")}{version.Major}.{version.Minor}", versionName));
                }
            }

            return dictionary.OrderBy(x => x.Key.IsPrerelease)
                .ThenByDescending(x => x.Key)
                .Select(x => new DotNetSdkInfo(x.Value.tfm, x.Value.version, dotNetExe))
                .ToList();
        }

        private static (string dotnetExe, string sdkPath) FindNetCoreSdkPath()
        {
            var programW6432Path = Environment.GetEnvironmentVariable("ProgramW6432");
            if (string.IsNullOrWhiteSpace(programW6432Path))
            {
                throw new InvalidOperationException("ProgramW6432 environment variable could not be found");
            }

            var dotnetPaths = new[] { Path.Combine(programW6432Path, "dotnet") };
            var sdkPath = (from path in dotnetPaths
                                   let fullPath = Path.Combine(path, "sdk")
                                   where Directory.Exists(fullPath)
                                   select fullPath).FirstOrDefault();

            if (string.IsNullOrWhiteSpace(sdkPath))
            {
                throw new InvalidOperationException("DotNet Core Sdk path could not be found");
            }

            var dotnetExe = Path.GetFullPath(Path.Combine(sdkPath, "..", "dotnet.exe"));
            if (File.Exists(dotnetExe))
            {
                return (dotnetExe, sdkPath);
            }

            throw new InvalidOperationException("DotNet Core Sdk path could not be found");
        }
    }
}
