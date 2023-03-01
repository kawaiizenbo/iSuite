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

        public void UpdateControls()
        {
            try 
            { 
                deviceInfoGroupBox.Header = Util.GetLockdowndStringKey(MainWindow.lockdownHandle, null, "MarketingName"); 
            }
            catch 
            { 
                try 
                { 
                    WebClient wc = new WebClient();
                    deviceInfoGroupBox.Header = JObject.Parse(wc.DownloadString(
                        new Uri($"{MainWindow.options.APISource}/device/{MainWindow.deviceIdentifier}.json")
                    ))["name"];
                    wc.Dispose();
                }
                catch
                {
                    deviceInfoGroupBox.Header = MainWindow.deviceIdentifier;
                }
            }
            

            deviceStorageGroupBox.Header = $"Device Storage ({Util.FormatBytes(MainWindow.deviceDiskCapacity)} Total)";

            systemStorageLabel.Content = $"System ({Util.FormatBytes(MainWindow.deviceSystemCapacity)} Total)";
            dataStorageLabel.Content = $"Data ({Util.FormatBytes(MainWindow.deviceDataCapacity)} Total)";

            systemStorageFreeLabel.Content = $"{Util.FormatBytes(MainWindow.deviceSystemAvailable)} Free";
            dataStorageFreeLabel.Content = $"{Util.FormatBytes(MainWindow.deviceDataAvailable)} Free";

            systemStorageProgressBar.Maximum = (int)(MainWindow.deviceSystemCapacity / 10000000);
            dataStorageProgressBar.Maximum = (int)(MainWindow.deviceDataCapacity / 10000000);

            systemStorageProgressBar.Value = (int)((MainWindow.deviceSystemCapacity - MainWindow.deviceSystemAvailable) / 10000000);
            dataStorageProgressBar.Value = (int)((MainWindow.deviceDataCapacity - MainWindow.deviceDataAvailable) / 10000000);

            deviceInfoListView.ItemsSource = MainWindow.deviceInfo;
        }

        public void SetRecovery()
        {
            deviceInfoGroupBox.Header = "Recovery Mode";
            recoveryModeToggleButton.Content = "Exit Recovery";
            powerOffDeviceButton.IsEnabled = false;
            rebootDeviceButton.IsEnabled = false;
        }

        public void SetDFU()
        {
            deviceInfoGroupBox.Header = "DFU Mode";
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
                sensitiveInfoToggleButton.Content = "Show sensitive info";
            }
            else
            {
                deviceInfoListView.ItemsSource = MainWindow.deviceInfo;
                sensitiveInfoShown = true;
                sensitiveInfoToggleButton.Content = "Hide sensitive info";
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
