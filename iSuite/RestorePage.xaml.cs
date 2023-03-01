using Newtonsoft.Json.Linq;
using Newtonsoft.Json;

using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace iSuite
{
    public partial class RestorePage : UserControl
    {
        public string ipswPath = "";
        public RestorePage()
        {
            InitializeComponent();
        }

        private void RestoreThread()
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = $"idevicerestore.exe",
                    Arguments = $"--erase -u -y {MainWindow.deviceUDID} \"{ipswPath}\"",
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
                    restoreStatusListBox.Items.Add(line);
                });
            }
        }

        private void refreshFirmwareButton_Click(object sender, RoutedEventArgs e)
        {
            using (WebClient wc = new WebClient())
            {
                firmwareListView.ItemsSource = JObject.Parse(wc.DownloadString(new Uri($"{MainWindow.options.APISource}/device/{MainWindow.deviceIdentifier}?type=ipsw")))["firmwares"];
            }
        }

        private async void restoreFirmwareButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Restoring your device will erase ALL information\nPlease ensure that you are signed out of iCloud/Find My is disabled\nContinue?", "WARNING!", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            if (firmwareListView.SelectedItem == null)
            {
                restoreStatusListBox.Items.Add("Please select a firmware");
                return;
            }
            Firmware selectedFW = JsonConvert.DeserializeObject<Firmware>(((JObject)firmwareListView.SelectedItem).ToString());
            if (selectedFW.version.StartsWith("1.") || selectedFW.version.StartsWith("2."))
            {
                MessageBox.Show("Restoring to iOS 1 and 2 is not supported.", "Error!");
                return;
            }
            if (!File.Exists($"{MainWindow.options.TempDataLocation}/{selectedFW.identifier}-{selectedFW.buildid}.ipsw"))
            {
                restoreStatusListBox.Items.Add($"Downloading iOS {selectedFW.version} for {MainWindow.deviceInfo["Product Identifier"]}");
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFile(new Uri(selectedFW.url), $"{MainWindow.options.TempDataLocation}/{selectedFW.identifier}-{selectedFW.buildid}.ipsw");
                }
                restoreStatusListBox.Items.Add($"Download complete.");
            }
            if (Util.CalculateMD5($"{MainWindow.options.TempDataLocation}/{selectedFW.identifier}-{selectedFW.buildid}.ipsw") != selectedFW.md5sum)
            {
                restoreStatusListBox.Items.Add("File hash verification failed.");
                return;
            }
            ipswPath = $"{MainWindow.options.TempDataLocation}/{selectedFW.identifier}-{selectedFW.buildid}.ipsw";
            await Task.Run(new Action(RestoreThread));
        }
    }
}
