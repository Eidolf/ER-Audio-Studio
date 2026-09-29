using System;
using System.IO;
using System.Windows;
using ErAudioTool.UI;

namespace ErAudioTool
{
    public class App : Application
    {
        [STAThread]
        public static int Main(string[] args)
        {
            try
            {
                var app = new App();
                DarkThemeStyles.ApplyToApplication(app);
                var window = new MainWindow();
                return app.Run(window);
            }
            catch (Exception ex)
            {
                string log = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "startup_error.log");
                File.WriteAllText(log, ex.ToString());
                MessageBox.Show("Fehler beim Starten:\n\n" + ex.Message, "ER Audio", MessageBoxButton.OK, MessageBoxImage.Error);
                return 1;
            }
        }
    }
}
