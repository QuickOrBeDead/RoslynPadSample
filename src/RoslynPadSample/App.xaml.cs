namespace RoslynPadSample
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Threading.Tasks;
    using System.Windows;
    using System.Windows.Threading;

    using RoslynPadSample.ViewModels;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public event Action<Exception> UnhandledException;
        
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            Current.DispatcherUnhandledException += OnUnhandledDispatcherException;
            AppDomain.CurrentDomain.UnhandledException += CurrentDomainOnUnhandledException;
            TaskScheduler.UnobservedTaskException += TaskSchedulerOnUnobservedTaskException;

            var mainWindow = new MainWindow { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            mainWindow.SourceInitialized += (_, _) => mainWindow.WindowState = WindowState.Maximized;
            mainWindow.DataContext = new MainViewModel { SourceCode = GetEmbeddedResourceText("SourceCode.csx") };
            mainWindow.Show();
        }

        private void OnUnhandledDispatcherException(object sender, DispatcherUnhandledExceptionEventArgs args)
        {
            HandleException(args.Exception);
            args.Handled = true;
        }

        private void TaskSchedulerOnUnobservedTaskException(object sender, UnobservedTaskExceptionEventArgs args)
        {
            HandleException(args.Exception.Flatten().InnerException);
        }

        private void CurrentDomainOnUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            HandleException((Exception)args.ExceptionObject);
        }

        private void HandleException(Exception exception)
        {
            if (exception is OperationCanceledException)
            {
                return;
            }

            var aggregateException = exception as AggregateException;
            UnhandledException?.Invoke(aggregateException?.Flatten() ?? exception);
        }

        private static string GetEmbeddedResourceText(string resourcePath)
        {
            if (string.IsNullOrWhiteSpace(resourcePath))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(resourcePath));
            }

            var assembly = Assembly.GetExecutingAssembly();
            var resourceName = $"{assembly.GetName().Name}.Resources.{resourcePath}";

            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    throw new InvalidOperationException($"Resource not found: {resourceName}");
                }

                using (StreamReader reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }
    }
}
