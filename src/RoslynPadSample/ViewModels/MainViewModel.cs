namespace RoslynPadSample.ViewModels
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Reflection;

    using Microsoft.CodeAnalysis;

    using RoslynPad.Roslyn;

    using RoslynPadSample.Build;

    public sealed class MainViewModel : ViewModelBase
    {
        private string _sourceCode;

        public string SourceCode
        {
            get => _sourceCode;
            set
            {
                if (ReferenceEquals(_sourceCode, value))
                {
                    return;
                }

                _sourceCode = value;
                OnPropertyChanged();
            }
        }

        public string BuildPath { get; }

        public string Id { get; }

        public RoslynHost RoslynHost { get; }

        public DocumentId DocumentId { get; private set; }

        private ExecutionHost _executionHost;

        public MainViewModel()
        {
            RoslynHost = new RoslynHost(
                new[]
                    {
                        Assembly.Load("RoslynPad.Editor.Windows"), 
                        Assembly.Load("RoslynPad.Roslyn.Windows")
                    },
                RoslynHostReferences.NamespaceDefault);

            Id = Guid.NewGuid().ToString("N");
            BuildPath = Path.Combine(Path.GetTempPath(), "RoslynPadSample", "build", Id);

            if (!Directory.Exists(BuildPath))
            {
                Directory.CreateDirectory(BuildPath);
            }
        }

        public void Init(DocumentId documentId)
        {
            DocumentId = documentId;

            _executionHost = new ExecutionHost(RoslynHost, documentId, BuildPath, Enumerable.Empty<string>());
            _executionHost.Restore();
        }
    }
}
