using System;
using System.IO;
using System.Windows;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using System.Net.Http;
using System.IO.Compression;
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
        private static readonly HttpClient _httpClient = new HttpClient();
        private static Mutex? _mutex;

        private string versionFile = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "version.md");
        private string appName = "Fancy Five";
        
        private string configDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "Nickis Launcher");
        private string configPath;
        private LauncherConfig config = new LauncherConfig();
        private Forms.NotifyIcon trayIcon = null!;

        public MainWindow()
        {
            // Sicherstellen, dass nur eine Instanz läuft
            _mutex = new Mutex(true, "FancyFiveLauncherMutex", out bool createdNew);
            if (!createdNew)
            {
                System.Windows.MessageBox.Show("Der Launcher läuft bereits im Hintergrund!", "Information");
                System.Windows.Application.Current.Shutdown();
                return;
            }

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
                if (IsGameRunning())
                {
                    System.Windows.MessageBox.Show("Bitte schließe FiveM und GTA V, bevor du Mods installierst!", "Prozess gefunden", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                InstallProgress.Visibility = Visibility.Visible;
                InstallProgress.IsIndeterminate = true;

                if (string.IsNullOrEmpty(config.FiveMPath) || !Directory.Exists(config.FiveMPath)) throw new Exception("FiveM Pfad ist ungültig!");
                if (string.IsNullOrEmpty(config.GTAVPath) || !Directory.Exists(config.GTAVPath)) throw new Exception("GTA V Pfad ist ungültig!");

                // Zustände auf dem UI-Thread erfassen
                bool installGrafik = CheckGrafik.IsChecked == true;
                bool installSound = CheckSound.IsChecked == true;
                bool installSky = CheckSky.IsChecked == true;
                bool installAurora = CheckAurora.IsChecked == true;

                // 1. Grafikmod
                if (installGrafik)
                {
                    await DownloadAndExtract("https://store6.gofile.io/download/web/95212982-8ae9-41d5-802f-906551aafd4e/citizen.zip", config.FiveMPath, "Grafikmod");
                }

                // 2. SoundMod
                if (installSound)
                {
                    string sfxTarget = Path.Combine(config.GTAVPath, "x64", "audio", "sfx");
                    BackupFile(sfxTarget, "RESIDENT.rpf");
                    BackupFile(sfxTarget, "WEAPONS_PLAYER.rpf");
                    await DownloadAndExtract("https://cold4.gofile.io/download/web/d39e5a51-1c2a-4186-aadc-93984a20eb2c/RESIDENT.zip", sfxTarget, "SoundMod");
                }

                // 3. Addons
                string modsFolder = Path.Combine(config.FiveMPath, "mods");
                if (!Directory.Exists(modsFolder)) Directory.CreateDirectory(modsFolder);

                if (installSky)
                {
                    await DownloadAndExtract("https://store6.gofile.io/download/web/b39d2da8-2678-47f8-b8ef-c59d52ce120c/nw_sky.zip", modsFolder, "Sky Mod");
                }
                
                if (installAurora)
                {
                    await DownloadAndExtract("https://store6.gofile.io/download/web/885bcc1c-b802-4e95-926a-29218098c236/nw_aurora.zip", modsFolder, "Aurora Mod");
                }

                await Task.Run(() => {
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

        private async Task DownloadAndExtract(string url, string targetDir, string statusName)
        {
            StatusText.Text = $"Lade {statusName} herunter...";
            string tempFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString() + ".zip");
            
            var response = await _httpClient.GetAsync(url);
            response.EnsureSuccessStatusCode();
            using (var fs = new FileStream(tempFile, FileMode.Create))
                await response.Content.CopyToAsync(fs);

            StatusText.Text = $"Installiere {statusName}...";
            await Task.Run(() => ZipFile.ExtractToDirectory(tempFile, targetDir, true));
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }

        private bool IsGameRunning()
        {
            return Process.GetProcessesByName("FiveM").Any() || 
                   Process.GetProcessesByName("GTA5").Any() || 
                   Process.GetProcessesByName("FiveM_ChromeBrowser").Any();
        }

        private async void BtnUninstall_Click(object sender, RoutedEventArgs e)
        {
            try {
                if (IsGameRunning())
                {
                    System.Windows.MessageBox.Show("Bitte schließe FiveM und GTA V, bevor du Mods entfernst!", "Prozess gefunden", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                StatusText.Text = "Deinstalliere Komponenten...";
                InstallProgress.Visibility = Visibility.Visible;
                InstallProgress.IsIndeterminate = true;

                // Zustände auf dem UI-Thread erfassen, bevor der Hintergrund-Task startet
                bool uninstallGrafik = CheckGrafik.IsChecked == true;
                bool uninstallSound = CheckSound.IsChecked == true;
                bool uninstallSky = CheckSky.IsChecked == true;
                bool uninstallAurora = CheckAurora.IsChecked == true;

                await Task.Run(() => {
                    // Grafikmod entfernen
                    if (uninstallGrafik)
                    {
                        string target = Path.Combine(config.FiveMPath, "citizen");
                        if (Directory.Exists(target)) Directory.Delete(target, true);
                    }

                    // SoundMod entfernen & Backup wiederherstellen
                    if (uninstallSound)
                    {
                        string sfxTarget = Path.Combine(config.GTAVPath, "x64", "audio", "sfx");
                        RestoreFile(sfxTarget, "RESIDENT.rpf");
                        RestoreFile(sfxTarget, "WEAPONS_PLAYER.rpf");
                    }

                    // Addons entfernen
                    if (uninstallSky)
                        DeleteFile(Path.Combine(config.FiveMPath, "mods", "nw_sky.rpf"));

                    if (uninstallAurora)
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