namespace RoslynPadSample.Build
{
    using System;

    public sealed class DotNetSdkInfo
    {
        public string Name { get; }

        public string TargetFrameworkMoniker { get; }

        public string FrameworkVersion { get; }

        public string DotNetExe { get; }

        public DotNetSdkInfo(string targetFrameworkMoniker, string frameworkVersion, string dotNetExe)
        {
            if (string.IsNullOrWhiteSpace(targetFrameworkMoniker))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(targetFrameworkMoniker));
            }

            if (string.IsNullOrWhiteSpace(frameworkVersion))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(frameworkVersion));
            }

            if (string.IsNullOrWhiteSpace(dotNetExe))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(dotNetExe));
            }

            Name = ".NET Core";
            TargetFrameworkMoniker = targetFrameworkMoniker;
            FrameworkVersion = frameworkVersion;
            DotNetExe = dotNetExe;
        }

        public override string ToString() => $"{Name} {FrameworkVersion}";
    }
}