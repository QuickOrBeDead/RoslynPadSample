namespace RoslynPadSample
{
    using System;
    using System.IO;
    using System.Linq;
    using System.Threading;
    using System.Windows;
    using System.Windows.Media;
    using System.Windows.Threading;

    using Avalon.Windows.Controls;

    using NuGet.Packaging;

    using RoslynPad.Editor;

    using RoslynPadSample.Controls;
    using RoslynPadSample.Models;
    using RoslynPadSample.ViewModels;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
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
                            $"{ex.GetType().Name}: {ex.Message}{Environment.NewLine}{ex}",
                            string.Empty,
                            TaskDialogButtons.Close);
                    },
                DispatcherPriority.Background);
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            CodeEditor.TextArea.SelectionBrush = Brushes.Yellow;
            CodeEditor.TextArea.SelectionForeground = Brushes.Black;

            _viewModel = (MainViewModel)DataContext;
            _viewModel.OutputMessage += s =>
                {
                    Dispatcher.InvokeAsync(() => WriteOutput(s, OutputTextType.Output, "-"), DispatcherPriority.Background);
                };
            _viewModel.ConsoleMessage += s =>
                {
                    Dispatcher.InvokeAsync(() => WriteOutput(s, OutputTextType.Console), DispatcherPriority.Background);
                };

            var documentId = CodeEditor.Initialize(
                _viewModel.RoslynHost,
                new ClassificationHighlightColors(),
                Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "RoslynDebugger"),
                _viewModel.SourceCode);

            _viewModel.Init(documentId);

            Runtime.Debugger.Notified += (spanStart, spanLength, variables) =>
                {
                    Dispatcher.InvokeAsync(
                        () =>
                            {
                                _viewModel.Variables.Clear();
                                _viewModel.Variables.AddRange(
                                    variables.Select(
                                        x => new VariableModel
                                                 {
                                                     Name = x.Name, Value = Convert.ToString(x.Value)
                                                 }));

                                CodeEditor.Select(spanStart, spanLength);
                            },
                        DispatcherPriority.Background);

                    Thread.Sleep(1000);
                };
        }

        public void WriteOutput(string text, OutputTextType textType = OutputTextType.Output, string separator = ">")
        {
            var doc = OutputText.Document;
            var startOffset = doc.TextLength;
            doc.Insert(doc.TextLength, $"{DateTime.Now:dd/MM/yyyy HH:mm:ss.fff} {separator} {text}{Environment.NewLine}");
            var endOffset = doc.TextLength;

            var colorizer = new OffsetColorizer(GetColor(textType)) { StartOffset = startOffset, EndOffset = endOffset };
            OutputText.TextArea.TextView.LineTransformers.Add(colorizer);

            OutputText.ScrollToEnd();
        }

        private Color GetColor(OutputTextType textType)
        {
            switch (textType)
            {
                case OutputTextType.Output:
                    return Colors.MediumBlue;
                case OutputTextType.Console:
                    return Colors.White;
                default:
                    return Colors.White;
            }
        }

        private void Editor_OnLoaded(object sender, RoutedEventArgs e)
        {
            Dispatcher.InvokeAsync(() => CodeEditor.Focus(), DispatcherPriority.Background);
        }
    }
}