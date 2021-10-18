namespace RoslynPadSample
{
    using System;
    using System.IO;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Threading;

    using Avalon.Windows.Controls;

    using RoslynPad.Editor;

    using RoslynPadSample.ViewModels;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private MarkerMargin _errorMargin;
        private MainViewModel _viewModel;

        public MainWindow()
        {
            InitializeComponent();

            Loaded += MainWindow_Loaded;
            ((App)Application.Current).UnhandledException += MainViewModel_UnhandledException;
        }

        private void MainViewModel_UnhandledException(Exception ex)
        {
            Dispatcher.InvokeAsync(
                () =>
                    {
                        TaskDialog.ShowInline(
                            this,
                            "Unhandled Exception",
                            $"{ex.GetType().Name}: {ex.Message}",
                            string.Empty,
                            TaskDialogButtons.Close);
                    },
                DispatcherPriority.Background);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _viewModel = (MainViewModel)DataContext;
            _viewModel.OutputMessage += s =>
                {
                    Dispatcher.InvokeAsync(
                        () =>
                            {
                                OutputText.AppendText($"{DateTime.Now:dd/MM/yyyy HH:mm:ss.fff} - {s}{Environment.NewLine}");
                                OutputText.ScrollToEnd();
                            },
                        DispatcherPriority.Background);
                };

            _errorMargin = new MarkerMargin { Visibility = Visibility.Collapsed, MarkerImage = TryFindResource("ExceptionMarker") as ImageSource, Width = 10 };
            CodeEditor.TextArea.LeftMargins.Insert(0, _errorMargin);

            var documentId = CodeEditor.Initialize(
                _viewModel.RoslynHost,
                new ClassificationHighlightColors(),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RoslynDebugger"),
                _viewModel.SourceCode);

            _viewModel.Init(documentId);
        }

        private void Editor_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.InvokeAsync(() => CodeEditor.Focus(), DispatcherPriority.Background);
        }
    }
}
