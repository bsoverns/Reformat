using System.Configuration;
using System.Data;
using System.Windows;
using System.Windows.Threading;

namespace WpfReformat
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            // Last line of defense: show the error instead of closing the window and losing the user's text.
            DispatcherUnhandledException += (object s, DispatcherUnhandledExceptionEventArgs e) =>
            {
                MessageBox.Show($"Something went wrong:\n\n{e.Exception.Message}", "Reformat",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                e.Handled = true;
            };
        }
    }

}
