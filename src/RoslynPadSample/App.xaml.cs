namespace RoslynPadSample
{
    using System;
    using System.IO;
    using System.Reflection;
    using System.Windows;

    using RoslynPadSample.ViewModels;

    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            var mainWindow = new MainWindow { WindowStartupLocation = WindowStartupLocation.CenterScreen };
            mainWindow.SourceInitialized += (_, _) => mainWindow.WindowState = WindowState.Maximized;
            mainWindow.DataContext = new MainViewModel { SourceCode = GetEmbeddedResourceText("SourceCode.csx") };
            mainWindow.Show();
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
