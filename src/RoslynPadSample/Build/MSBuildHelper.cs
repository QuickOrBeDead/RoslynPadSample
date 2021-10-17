namespace RoslynPadSample.Build
{
    using System;
    using System.Xml.Linq;

    internal static class MSBuildHelper
    {
        public const string ReferencesFile = "references.txt";
        public const string AnalyzersFile = "analyzers.txt";

        public static XDocument CreateCsproj(string targetFramework) =>
            new XDocument(
                new XElement(
                    "Project",
                    ImportSdkProject("Microsoft.NET.Sdk", "Sdk.props"),
                    BuildProperties(targetFramework),
                    ImportSdkProject("Microsoft.NET.Sdk", "Sdk.targets"),
                    CoreCompileTarget()));

        private static XElement BuildProperties(string targetFramework)
        {
            var group = new XElement(
                "PropertyGroup",
                new XElement("TargetFramework", targetFramework),
                new XElement("OutputType", "Exe"),
                new XElement("OutputPath", "bin"),
                new XElement("UseAppHost", false),
                new XElement("AppendTargetFrameworkToOutputPath", false),
                new XElement("AppendRuntimeIdentifierToOutputPath", false),
                new XElement("CopyBuildOutputToOutputDirectory", false),
                new XElement("GenerateAssemblyInfo", false));

            if (!targetFramework.Contains("core", StringComparison.OrdinalIgnoreCase))
            {
                group.Add(new XElement("FrameworkPathOverride", @"$(WinDir)\Microsoft.NET\Framework\v4.0.30319"));
            }

            return group;
        }

        private static XElement CoreCompileTarget() =>
            new XElement(
                "Target",
                new XAttribute("Name", "CoreCompile"),
                WriteLinesToFile(ReferencesFile, "@(ReferencePathWithRefAssemblies)"),
                WriteLinesToFile(AnalyzersFile, "@(Analyzer)"));

        private static XElement WriteLinesToFile(string file, string lines) =>
            new XElement(
                "WriteLinesToFile",
                new XAttribute("File", file),
                new XAttribute("Lines", lines),
                new XAttribute("Overwrite", true));

        private static XElement ImportSdkProject(string sdk, string project) =>
            new XElement(
                "Import",
                new XAttribute("Sdk", sdk),
                new XAttribute("Project", project));
    }
}