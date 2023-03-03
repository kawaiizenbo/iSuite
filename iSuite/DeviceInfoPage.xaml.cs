using iMobileDevice.iDevice;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
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
    public partial class DeviceInfoPage : UserControl
    {
        bool sensitiveInfoShown = true;
        public DeviceInfoPage()
        {
            InitializeComponent();
        }

        public void LoadLanguage()
        {
            deviceInfoGroupBox.Header = (string)MainWindow.languageTable["deviceInformation"];
            deviceStorageGroupBox.Header = (string)MainWindow.languageTable["deviceStorage"];
            powerOffDeviceButton.Content = (string)MainWindow.languageTable["powerOff"];
            rebootDeviceButton.Content = (string)MainWindow.languageTable["reboot"];
            recoveryModeToggleButton.Content = (string)MainWindow.languageTable["enterRecovery"];
            sensitiveInfoToggleButton.Content = (string)MainWindow.languageTable["hideSensitiveInfo"];
            openiDeviceLogButton.Content = (string)MainWindow.languageTable["openSyslog"];
            attributeColumn.Header = (string)MainWindow.languageTable["attribute"];
            valueColumn.Header = (string)MainWindow.languageTable["value"];
        }

        public void UpdateControls()
        {
            try 
            { 
                mainGroupBox.Header = Util.GetLockdowndStringKey(MainWindow.lockdownHandle, null, "MarketingName"); 
            }
            catch 
            { 
                try 
                { 
                    WebClient wc = new WebClient();
                    mainGroupBox.Header = JObject.Parse(wc.DownloadString(
                        new Uri($"{MainWindow.options.APISource}/device/{MainWindow.deviceIdentifier}.json")
                    ))["name"];
                    wc.Dispose();
                }
                catch
                {
                    mainGroupBox.Header = MainWindow.deviceIdentifier;
                }
            }

            systemStorageLabel.Content = $"{(string)MainWindow.languageTable["system"]} ({Util.FormatBytes(MainWindow.deviceSystemCapacity)} {(string)MainWindow.languageTable["total"]})";
            dataStorageLabel.Content = $"{(string)MainWindow.languageTable["data"]} ({Util.FormatBytes(MainWindow.deviceDataCapacity)} {(string)MainWindow.languageTable["total"]})";

            systemStorageFreeLabel.Content = $"{(string)MainWindow.languageTable["available"]} ({Util.FormatBytes(MainWindow.deviceSystemAvailable)})";
            dataStorageFreeLabel.Content = $"{(string)MainWindow.languageTable["available"]} ({Util.FormatBytes(MainWindow.deviceDataAvailable)})";

            systemStorageProgressBar.Maximum = (int)(MainWindow.deviceSystemCapacity / 10000000);
            dataStorageProgressBar.Maximum = (int)(MainWindow.deviceDataCapacity / 10000000);

            systemStorageProgressBar.Value = (int)((MainWindow.deviceSystemCapacity - MainWindow.deviceSystemAvailable) / 10000000);
            dataStorageProgressBar.Value = (int)((MainWindow.deviceDataCapacity - MainWindow.deviceDataAvailable) / 10000000);

            deviceInfoListView.ItemsSource = MainWindow.deviceInfo;
        }

        public void SetRecovery()
        {
            mainGroupBox.Header = (string)MainWindow.languageTable["recoveryMode"];
            recoveryModeToggleButton.Content = (string)MainWindow.languageTable["exitRecovery"];
            powerOffDeviceButton.IsEnabled = false;
            rebootDeviceButton.IsEnabled = false;
        }

        public void SetDFU()
        {
            mainGroupBox.Header = (string)MainWindow.languageTable["dfuMode"];
            recoveryModeToggleButton.Content = "-------";
            recoveryModeToggleButton.IsEnabled = false;
            powerOffDeviceButton.IsEnabled = false;
            rebootDeviceButton.IsEnabled = false;
        }

        public void SetBarColors(SolidColorBrush color)
        {
            systemStorageProgressBar.Foreground = color;
            dataStorageProgressBar.Foreground = color;
        }

        private void powerOffDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            using (Process p = new Process())
            {
                p.StartInfo.FileName = "idevicediagnostics.exe";
                p.StartInfo.Arguments = "shutdown";
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.WaitForExit();
            }
        }

        private void recoveryModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.normalConnected)
            {
                MainWindow.lockdown.lockdownd_enter_recovery(MainWindow.lockdownHandle);
            }
            else
            {
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = "irecovery.exe";
                    p.StartInfo.Arguments = "-n";
                    p.StartInfo.CreateNoWindow = true;
                    p.Start();
                    p.WaitForExit();
                }
            }
        }

        private void rebootDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            using (Process p = new Process())
            {
                p.StartInfo.FileName = "idevicediagnostics.exe";
                p.StartInfo.Arguments = "restart";
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.WaitForExit();
            }
        }
        private void sensitiveInfoToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (sensitiveInfoShown)
            {
                deviceInfoListView.ItemsSource = MainWindow.stDeviceInfo;
                sensitiveInfoShown = false;
                sensitiveInfoToggleButton.Content = (string)MainWindow.languageTable["showSensitiveInfo"];
            }
            else
            {
                deviceInfoListView.ItemsSource = MainWindow.deviceInfo;
                sensitiveInfoShown = true;
                sensitiveInfoToggleButton.Content = (string)MainWindow.languageTable["hideSensitiveInfo"];
            }
        }

        private void openiDeviceLogButton_Click(object sender, RoutedEventArgs e)
        {
            using (Process p = new Process())
            {
                p.StartInfo.FileName = "idevicesyslog.exe";
                p.StartInfo.Arguments = "-u" + MainWindow.deviceUDID;
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.WaitForExit();
            }
        }

        private void deviceInfoListView_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (deviceInfoListView.SelectedItem == null) return;
            KeyValuePair<string, string> item = (KeyValuePair<string, string>)deviceInfoListView.SelectedItem;
            Clipboard.SetText(item.Value);
        }
    }
}
