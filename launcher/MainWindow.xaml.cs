using System;
using System.IO;
using System.Windows;
using System.Diagnostics;
using Microsoft.Win32;
using System.Threading.Tasks;

namespace NickisWorldLauncher
{
    public partial class MainWindow : Window
    {
        private string fivemPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FiveM", "FiveM.app");
        private string modSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "fivem-data");
        private string versionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "version.md");
        private string appName = "NickisWorldLauncher";

        public MainWindow()
        {
            InitializeComponent();
            LoadLocalVersion();
            CheckAutostartStatus();
        }

        private void LoadLocalVersion()
        {
            if (File.Exists(versionFile))
                VersionText.Text = "Version: " + File.ReadAllText(versionFile).Trim();
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            try {
                StatusText.Text = "Installiere Dateien...";
                InstallProgress.Visibility = Visibility.Visible;
                InstallProgress.IsIndeterminate = true;

                await Task.Run(() => {
                    if (!Directory.Exists(fivemPath)) throw new Exception("FiveM nicht gefunden!");
                    
                    // Simpler Copy-Befehl (rekursiv)
                    foreach (string dirPath in Directory.GetDirectories(modSource, "*", SearchOption.AllDirectories))
                        Directory.CreateDirectory(dirPath.Replace(modSource, fivemPath));

                    foreach (string newPath in Directory.GetFiles(modSource, "*.*", SearchOption.AllDirectories))
                        File.Copy(newPath, newPath.Replace(modSource, fivemPath), true);
                });

                StatusText.Text = "Installation erfolgreich!";
                MessageBox.Show("Grafik-Mod wurde erfolgreich installiert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex) {
                MessageBox.Show("Fehler: " + ex.Message);
                StatusText.Text = "Fehler bei der Installation";
            }
            finally {
                InstallProgress.Visibility = Visibility.Hidden;
            }
        }

        private void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            // Hier würdest du normalerweise eine URL abrufen (z.B. von GitHub)
            // string remoteVersion = new WebClient().DownloadString("URL_ZU_DEINER_VERSION_DATEI");
            MessageBox.Show("Suche nach Updates... (Feature muss mit Server-URL verknüpft werden)", "Update-Check");
        }

        private void BtnStartFiveM_Click(object sender, RoutedEventArgs e)
        {
            string exePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FiveM", "FiveM.exe");
            if (File.Exists(exePath)) Process.Start(exePath);
            else MessageBox.Show("FiveM.exe nicht gefunden!");
        }

        private void CheckAutostartStatus()
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            AutostartCheck.IsChecked = rk.GetValue(appName) != null;
        }

        private void Autostart_Changed(object sender, RoutedEventArgs e)
        {
            RegistryKey rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (AutostartCheck.IsChecked == true)
                rk.SetValue(appName, Process.GetCurrentProcess().MainModule.FileName);
            else
                rk.DeleteValue(appName, false);
        }
    }
}