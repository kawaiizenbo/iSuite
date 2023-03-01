using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace iSuite
{
    /// <summary>
    /// Interaction logic for AppsPage.xaml
    /// </summary>
    public partial class AppsPage : UserControl
    {
        public static string selectedBundleID = "";
        public static string ipaPath = "";
        public static List<DeviceApp> apps = new List<DeviceApp>();
        public AppsPage()
        {
            InitializeComponent();
        }

        private void refreshAppListButton_Click(object sender, RoutedEventArgs e)
        {
            Task.Run(new Action(GetAppsThread));
            installedAppsListView.ItemsSource = null;
            installedAppsListView.ItemsSource = apps;
            appInstallStatusListBox.Items.Add("Refreshed.");
            var border = (Border)VisualTreeHelper.GetChild(appInstallStatusListBox, 0);
            var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
            scrollViewer.ScrollToBottom();
        }

        private async void installNewAppButton_Click(object sender, RoutedEventArgs e)
        {
            var openIPAFile = new Microsoft.Win32.OpenFileDialog();
            openIPAFile.FileName = "app";
            openIPAFile.DefaultExt = ".ipa";
            openIPAFile.Filter = "iOS Apps (.ipa)|*.ipa";

            openIPAFile.ShowDialog();

            ipaPath = openIPAFile.FileName;

            appInstallStatusListBox.Items.Clear();

            appInstallStatusListBox.Items.Add($"Attempting install of {ipaPath}");
            await Task.Run(new Action(InstallAppThread));
        }

        private void removeSelectedAppButton_Click(object sender, RoutedEventArgs e)
        {
            selectedBundleID = ((DeviceApp)installedAppsListView.SelectedItem).CFBundleIdentifier;

            appInstallStatusListBox.Items.Add($"Attempting removal of of {selectedBundleID}");
            var border = (Border)VisualTreeHelper.GetChild(appInstallStatusListBox, 0);
            var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
            scrollViewer.ScrollToBottom();
            Task.Run(new Action(RemoveAppThread));
        }

        private void InstallAppThread()
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ideviceinstaller.exe",
                    Arguments = $"-u {MainWindow.deviceUDID} --install \"{ipaPath}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            while (!proc.StandardOutput.EndOfStream && !proc.StandardError.EndOfStream)
            {
                string line = proc.StandardOutput.ReadLine();
                if (line == null || line.Trim() == "") line = proc.StandardError.ReadLine();
                Dispatcher.Invoke(() =>
                {
                    appInstallStatusListBox.Items.Add(line);
                    var border = (Border)VisualTreeHelper.GetChild(appInstallStatusListBox, 0);
                    var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
                    scrollViewer.ScrollToBottom();
                });
            }
            Dispatcher.Invoke(() =>
            {
                appInstallStatusListBox.Items.Add($"Process ended with code {proc.ExitCode} {(proc.ExitCode == 0 ? "(Success)" : "")}");
                var border = (Border)VisualTreeHelper.GetChild(appInstallStatusListBox, 0);
                var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
                scrollViewer.ScrollToBottom();
                Task.Run(new Action(GetAppsThread));
                installedAppsListView.ItemsSource = null;
                installedAppsListView.ItemsSource = apps;
            });
        }

        private void RemoveAppThread()
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ideviceinstaller.exe",
                    Arguments = $"-u {MainWindow.deviceUDID} --uninstall \"{selectedBundleID}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            while (!proc.StandardOutput.EndOfStream && !proc.StandardError.EndOfStream)
            {
                string line = proc.StandardOutput.ReadLine();
                if (line == null || line.Trim() == "") line = proc.StandardError.ReadLine();
                Dispatcher.Invoke(() =>
                {
                    appInstallStatusListBox.Items.Add(line);
                    var border = (Border)VisualTreeHelper.GetChild(appInstallStatusListBox, 0);
                    var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
                    scrollViewer.ScrollToBottom();
                });
            }
            Dispatcher.Invoke(() =>
            {
                appInstallStatusListBox.Items.Add($"Process ended with code {proc.ExitCode} {(proc.ExitCode == 0 ? "(Success)" : "")}");
                var border = (Border)VisualTreeHelper.GetChild(appInstallStatusListBox, 0);
                var scrollViewer = (ScrollViewer)VisualTreeHelper.GetChild(border, 0);
                scrollViewer.ScrollToBottom();
                Task.Run(new Action(GetAppsThread));
                installedAppsListView.ItemsSource = null;
                installedAppsListView.ItemsSource = apps;
            });
        }

        public void GetAppsThread()
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ideviceinstaller.exe",
                    Arguments = $"-u {MainWindow.deviceUDID} -l",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            apps.Clear();
            while (!proc.StandardOutput.EndOfStream)
            {
                string line = proc.StandardOutput.ReadLine();
                if (line == null || line.Trim() == "" || line.Contains("CFBundleIdentifier, CFBundleVersion, CFBundleDisplayName")) continue;
                apps.Add(new DeviceApp()
                {
                    CFBundleIdentifier = line.Split(',')[0],
                    CFBundleVersion = line.Split(',')[1].Trim().Replace("\"", ""),
                    CFBundleDisplayName = line.Split(',')[2].Trim().Replace("\"", ""),
                });
            }
            if (!proc.HasExited) proc.Kill();
            
            Dispatcher.Invoke(() =>
            {
                installedAppsListView.ItemsSource = apps;
            });
        }
    }
}
