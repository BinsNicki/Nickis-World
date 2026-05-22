using System;
using System.IO;
using System.Windows;
using System.Diagnostics;
using Microsoft.Win32;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Forms = System.Windows.Forms;

namespace NickisWorldLauncher
{
    public class LauncherConfig
    {
        public string InstalledVersion { get; set; } = "Keine";
        public string FiveMPath { get; set; } = string.Empty;
        public string GTAVPath { get; set; } = string.Empty;
        public DateTime LastUpdate { get; set; }
    }

    public partial class MainWindow : Window
    {
        private string modSource = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "fivem-data");
        private string versionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "version.md");
        private string appName = "Fancy Five";
        
        private string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Nickis Launcher");
        private string configPath;
        private LauncherConfig config = new LauncherConfig();
        private Forms.NotifyIcon trayIcon = null!;

        public MainWindow()
        {
            InitializeComponent();
            configPath = Path.Combine(configDir, "config.json");
            
            SetupTrayIcon();
            LoadConfig();
            LoadLocalVersion();
            CheckAutostartStatus();

            // Erstmalige Pfadabfrage
            if (string.IsNullOrEmpty(config.FiveMPath) || string.IsNullOrEmpty(config.GTAVPath))
            {
                SetupPaths();
            }

            // Autostart Minimierung prüfen
            if (Environment.GetCommandLineArgs().Contains("--autostart"))
            {
                this.Hide();
            }
        }

        private void SetupPaths()
        {
            System.Windows.MessageBox.Show("Willkommen! Bitte wähle zuerst deinen FiveM App-Daten Ordner und deinen GTA V Hauptordner aus.", "Ersteinrichtung");

            var folderDialog = new OpenFolderDialog();
            
            folderDialog.Title = "Wähle deinen FiveM Application Data Ordner (z.B. FiveM.app)";
            if (folderDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(folderDialog.FolderName))
            {
                config.FiveMPath = folderDialog.FolderName;
            }
            else { System.Windows.Application.Current.Shutdown(); return; }

            folderDialog.Title = "Wähle deinen GTA V Hauptordner (wo die GTA5.exe liegt)";
            if (folderDialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(folderDialog.FolderName))
            {
                config.GTAVPath = folderDialog.FolderName;
            }
            else { System.Windows.Application.Current.Shutdown(); return; }

            SaveConfig();
        }

        private void SetupTrayIcon()
        {
            trayIcon = new Forms.NotifyIcon();
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logo.ico");
            if (File.Exists(iconPath)) trayIcon.Icon = new System.Drawing.Icon(iconPath);
            
            trayIcon.Visible = true;
            trayIcon.Text = "Fancy Five Launcher";
            trayIcon.DoubleClick += (s, e) => { this.Show(); this.WindowState = WindowState.Normal; };
            
            var contextMenu = new Forms.ContextMenuStrip();
            contextMenu.Items.Add("Öffnen", null, (s, e) => { this.Show(); this.WindowState = WindowState.Normal; });
            contextMenu.Items.Add("Beenden", null, (s, e) => { trayIcon.Dispose(); System.Windows.Application.Current.Shutdown(); });
            trayIcon.ContextMenuStrip = contextMenu;
        }

        private void LoadLocalVersion()
        {
            string currentVer = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "1.0.0";
            VersionText.Text = $"Installierte Version: {config.InstalledVersion} | Verfügbar: {currentVer}";
        }

        private async void BtnInstall_Click(object sender, RoutedEventArgs e)
        {
            try {
                StatusText.Text = "Installiere Dateien...";
                InstallProgress.Visibility = Visibility.Visible;
                InstallProgress.IsIndeterminate = true;

                await Task.Run(() => {
                    if (string.IsNullOrEmpty(config.FiveMPath) || !Directory.Exists(config.FiveMPath)) throw new Exception("FiveM Pfad ist ungültig oder nicht gesetzt!");
                    if (string.IsNullOrEmpty(config.GTAVPath) || !Directory.Exists(config.GTAVPath)) throw new Exception("GTA V Pfad ist ungültig oder nicht gesetzt!");
                    
                    // 1. Grafikmod
                    if (CheckGrafik.Dispatcher.Invoke(() => CheckGrafik.IsChecked == true))
                    {
                        string source = Path.Combine(modSource, "citizen");
                        if (Directory.Exists(source)) CopyDirectory(source, Path.Combine(config.FiveMPath, "citizen"));
                    }

                    // 2. SoundMod
                    if (CheckSound.Dispatcher.Invoke(() => CheckSound.IsChecked == true))
                    {
                        string soundSource = Path.Combine(modSource, "sound");
                        string sfxTarget = Path.Combine(config.GTAVPath, "x64", "audio", "sfx");
                        if (Directory.Exists(soundSource) && Directory.Exists(sfxTarget))
                        {
                            BackupFile(sfxTarget, "RESIDENT.rpf");
                            BackupFile(sfxTarget, "WEAPONS_PLAYER.rpf");
                            foreach (string file in Directory.GetFiles(soundSource, "*.rpf"))
                                File.Copy(file, Path.Combine(sfxTarget, Path.GetFileName(file)), true);
                        }
                    }

                    // 3. Addons (Sky & Aurora)
                    string modsFolder = Path.Combine(config.FiveMPath, "mods");
                    Directory.CreateDirectory(modsFolder);

                    if (CheckSky.Dispatcher.Invoke(() => CheckSky.IsChecked == true))
                        InstallAddon("nw_sky.rpf", modsFolder);
                    
                    if (CheckAurora.Dispatcher.Invoke(() => CheckAurora.IsChecked == true))
                        InstallAddon("nw_aurora.rpf", modsFolder);

                    // Config aktualisieren
                    config.InstalledVersion = File.Exists(versionFile) ? File.ReadAllText(versionFile).Trim() : "1.0.0";
                    config.LastUpdate = DateTime.Now;
                    SaveConfig();
                });

                StatusText.Text = "Installation erfolgreich!";
                System.Windows.MessageBox.Show("Grafik-Mod wurde erfolgreich installiert!", "Erfolg", MessageBoxButton.OK, MessageBoxImage.Information);
                LoadLocalVersion();
            }
            catch (Exception ex) {
                System.Windows.MessageBox.Show("Fehler: " + ex.Message);
                StatusText.Text = "Fehler bei der Installation";
            }
            finally {
                InstallProgress.Visibility = Visibility.Hidden;
            }
        }

        private void InstallAddon(string fileName, string targetFolder)
        {
            string sourceFile = Path.Combine(modSource, "mods", fileName);
            if (File.Exists(sourceFile))
                File.Copy(sourceFile, Path.Combine(targetFolder, fileName), true);
        }

        private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            try {
                StatusText.Text = "Deinstalliere Komponenten...";
                InstallProgress.Visibility = Visibility.Visible;
                InstallProgress.IsIndeterminate = true;

                await Task.Run(() => {
                    // Grafikmod entfernen
                    if (CheckGrafik.Dispatcher.Invoke(() => CheckGrafik.IsChecked == true))
                    {
                        string target = Path.Combine(config.FiveMPath, "citizen");
                        if (Directory.Exists(target)) Directory.Delete(target, true);
                    }

                    // SoundMod entfernen & Backup wiederherstellen
                    if (CheckSound.Dispatcher.Invoke(() => CheckSound.IsChecked == true))
                    {
                        string sfxTarget = Path.Combine(config.GTAVPath, "x64", "audio", "sfx");
                        RestoreFile(sfxTarget, "RESIDENT.rpf");
                        RestoreFile(sfxTarget, "WEAPONS_PLAYER.rpf");
                    }

                    // Addons entfernen
                    if (CheckSky.Dispatcher.Invoke(() => CheckSky.IsChecked == true))
                        DeleteFile(Path.Combine(config.FiveMPath, "mods", "nw_sky.rpf"));

                    if (CheckAurora.Dispatcher.Invoke(() => CheckAurora.IsChecked == true))
                        DeleteFile(Path.Combine(config.FiveMPath, "mods", "nw_aurora.rpf"));
                });

                StatusText.Text = "Deinstallation abgeschlossen!";
                System.Windows.MessageBox.Show("Ausgewählte Komponenten wurden entfernt.", "Deinstalliert");
            }
            catch (Exception ex) {
                System.Windows.MessageBox.Show("Fehler bei Deinstallation: " + ex.Message);
            }
            finally {
                InstallProgress.Visibility = Visibility.Hidden;
            }
        }

        private void RestoreFile(string directory, string fileName)
        {
            string filePath = Path.Combine(directory, fileName);
            string backupPath = filePath + ".bak";
            if (File.Exists(backupPath))
            {
                if (File.Exists(filePath)) File.Delete(filePath);
                File.Move(backupPath, filePath);
            }
        }

        private void DeleteFile(string path)
        {
            if (File.Exists(path)) File.Delete(path);
        }

        private void BackupFile(string directory, string fileName)
        {
            string filePath = Path.Combine(directory, fileName);
            string backupPath = filePath + ".bak";
            if (File.Exists(filePath) && !File.Exists(backupPath))
            {
                File.Copy(filePath, backupPath);
            }
        }

        private void CopyDirectory(string sourceDir, string targetDir)
        {
            Directory.CreateDirectory(targetDir);
            foreach (var file in Directory.GetFiles(sourceDir))
                File.Copy(file, Path.Combine(targetDir, Path.GetFileName(file)), true);
            foreach (var directory in Directory.GetDirectories(sourceDir))
                CopyDirectory(directory, Path.Combine(targetDir, Path.GetFileName(directory)));
        }

        private void BtnCheckUpdates_Click(object sender, RoutedEventArgs e)
        {
            // Hier würdest du normalerweise eine URL abrufen (z.B. von GitHub)
            string onlineVersion = "1.0.1"; // Beispielwert
            if (onlineVersion != config.InstalledVersion)
            {
                trayIcon.ShowBalloonTip(3000, "Update verfügbar!", $"Eine neue Version ({onlineVersion}) der Grafik-Mod ist bereit.", Forms.ToolTipIcon.Info);
            }
            else System.Windows.MessageBox.Show("Deine Mod ist auf dem neuesten Stand.");
        }

        private void BtnStartFiveM_Click(object sender, RoutedEventArgs e)
        {
            string exePath = Path.Combine(Path.GetDirectoryName(config.FiveMPath) ?? "", "FiveM.exe");
            if (File.Exists(exePath)) Process.Start(exePath);
            else System.Windows.MessageBox.Show("FiveM.exe konnte im Pfad nicht gefunden werden!");
        }

        private void LoadConfig()
        {
            if (File.Exists(configPath))
            {
                string json = File.ReadAllText(configPath);
                config = JsonSerializer.Deserialize<LauncherConfig>(json) ?? config;
            }
        }

        private void SaveConfig()
        {
            if (!Directory.Exists(configDir)) Directory.CreateDirectory(configDir);
            string json = JsonSerializer.Serialize(config);
            File.WriteAllText(configPath, json);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Statt schließen nur verstecken
            e.Cancel = true;
            this.Hide();
            trayIcon.ShowBalloonTip(2000, "Fancy Five", "Launcher läuft im Hintergrund weiter.", Forms.ToolTipIcon.Info);
        }

        private void CheckAutostartStatus()
        {
            using RegistryKey? rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", false);
            AutostartCheck.IsChecked = rk?.GetValue(appName) != null;
        }

        private void Autostart_Changed(object sender, RoutedEventArgs e)
        {
            using RegistryKey? rk = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (AutostartCheck.IsChecked == true)
                rk?.SetValue(appName, $"\"{Process.GetCurrentProcess().MainModule?.FileName}\" --autostart");
            else
                rk?.DeleteValue(appName, false);
        }
    }
}