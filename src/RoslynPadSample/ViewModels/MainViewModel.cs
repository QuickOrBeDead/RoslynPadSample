namespace RoslynPadSample.ViewModels
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Linq;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Windows.Input;

    using Microsoft.CodeAnalysis;
    using Microsoft.CodeAnalysis.Diagnostics;

    using RoslynPad.Roslyn;

    using RoslynPadSample.Build;
    using RoslynPadSample.Commands;

    public sealed class MainViewModel : ViewModelBase
    {
        private string _sourceCode;
        private ExecutionHost _executionHost;
        private IList<DotNetSdkInfo> _dotNetSdkList;
        private DotNetSdkInfo _dotNetSdk;

        private bool _restoreCompleted;

        public event Action<string> OutputMessage;

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

        public ICommand RunCommand { get; }

        public IList<DotNetSdkInfo> DotNetSdkList
        {
            get => _dotNetSdkList;
            set
            {
                if (ReferenceEquals(_dotNetSdkList, value))
                {
                    return;
                }

                _dotNetSdkList = value;
                OnPropertyChanged();
            }
        }

        public DotNetSdkInfo DotNetSdk
        {
            get => _dotNetSdk;
            set
            {
                if (ReferenceEquals(_dotNetSdk, value))
                {
                    return;
                }

                _dotNetSdk = value;
                OnPropertyChanged();

                if (value != null)
                {
                    InitExecutionHost();
                }
            }
        }

        public MainViewModel()
        {
            RoslynHost = new RoslynHost(
                new[] { Assembly.Load("RoslynPad.Editor.Windows"), Assembly.Load("RoslynPad.Roslyn.Windows") },
                RoslynHostReferences.NamespaceDefault);

            Id = Guid.NewGuid().ToString("N");
            BuildPath = Path.Combine(Path.GetTempPath(), "RoslynPadSample", "build", Id);

            if (!Directory.Exists(BuildPath))
            {
                Directory.CreateDirectory(BuildPath);
            }

            RunCommand = new RelayCommand(RunCodeAsync, _ => DotNetSdk != null && _restoreCompleted);
            DotNetSdkList = new DotNetSdkFinder().GetDotNetSdkList();
        }

        private async Task RunCodeAsync()
        {
            var code = (await RoslynHost.GetDocument(DocumentId).GetTextAsync().ConfigureAwait(false)).ToString();
            await _executionHost.ExecuteAsync(code).ConfigureAwait(false);
        }

        public void Init(DocumentId documentId)
        {
            DocumentId = documentId;

            DotNetSdk = DotNetSdkList.FirstOrDefault();
        }

        private void InitExecutionHost()
        {
            _executionHost = new ExecutionHost(
                new BuildInfo { DotNetSdkInfo = DotNetSdk, BuildPath = BuildPath },
                RoslynHost);
            _executionHost.RestoreMessage += s => OutputMessage?.Invoke(s);
            _executionHost.ConsoleMessage += s => OutputMessage?.Invoke(s);
            _executionHost.RestoreCompleted += OnExecutionHost_OnRestoreCompleted;
            _executionHost.Restore();
        }

        private void OnExecutionHost_OnRestoreCompleted(MetadataReference[] metadataReferences, AnalyzerFileReference[] analyzerReferences)
        {
            var document = RoslynHost.GetDocument(DocumentId);
            if (document == null)
            {
                throw new InvalidOperationException($"Document not found for documentId: {DocumentId}");
            }

            var project = document.Project.WithMetadataReferences(metadataReferences).WithAnalyzerReferences(analyzerReferences);
            document = project.GetDocument(DocumentId);

            if (document == null)
            {
                throw new InvalidOperationException($"Document not found for documentId: {DocumentId}");
            }

            RoslynHost.UpdateDocument(document);

            _restoreCompleted = true;
        }
    }
}
