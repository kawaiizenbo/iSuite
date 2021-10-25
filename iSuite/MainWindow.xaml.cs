using iMobileDevice;
using iMobileDevice.iDevice;
using iMobileDevice.Lockdown;
using iMobileDevice.Plist;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Timers;
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
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        IiDeviceApi idevice;
        ILockdownApi lockdown;
        iDeviceHandle deviceHandle;
        LockdownClientHandle lockdownHandle;

        JObject fws;

        // every info string ever
        string deviceName; // DeviceName
        string deviceSerialNumber;
        string deviceProductVersion;
        string deviceBuildVersion;
        ulong deviceUniqueChipID = 0;
        string deviceECID; // UniqueChipID converted to hex then string
        string deviceProductType;
        string deviceHardwareModel;
        string deviceModelNumber;
        string deviceColor;

        // storage related ones
        ulong deviceTotalDiskCapacity = 0;
        ulong deviceTotalSystemCapacity = 0;
        ulong deviceTotalDataCapacity = 0;
        ulong deviceTotalSystemAvailable = 0;
        ulong deviceTotalDataAvailable = 0;

        public MainWindow()
        {
            NativeLibraries.Load();
            InitializeComponent();
            idevice = LibiMobileDevice.Instance.iDevice;
            lockdown = LibiMobileDevice.Instance.Lockdown;

            // Load settings

        }
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            mainTabControl.Visibility = Visibility.Hidden;
            waitingForDeviceLabel.Margin = new Thickness(10, 10, 0, 0);
            ensureTrustedLabel.Margin = new Thickness(10, 68, 0, 0);
            refreshDevicesButton.Margin = new Thickness(10, 0, 0, 10);
            continueWithoutDeviceButton.Margin = new Thickness(65, 0, 0, 10);

            // check once
            Init();
        }

        // got this off stackoverflow lmao it works i guess
        private static string FormatBytes(ulong bytes)
        {
            string[] Suffix = { "B", "KB", "MB", "GB", "TB" };
            int i;
            double dblSByte = bytes;
            for (i = 0; i < Suffix.Length && bytes >= 1000; i++, bytes /= 1000)
            {
                dblSByte = bytes / 1000.0;
            }

            return String.Format("{0:0.##} {1}", dblSByte, Suffix[i]);
        }

        private void Init()
        {
            // only detects devices in normal mode, ill work on recovery and dfu later
            ReadOnlyCollection<string> udids;
            int zero = 0;

            var ret = idevice.idevice_get_device_list(out udids, ref zero);

            if (ret == iDeviceError.NoDevice || udids.Count == 0)
            {
                return;
            }

            using (WebClient wc = new WebClient())
            {
                fws = JObject.Parse(wc.DownloadString("https://api.ipsw.me/v2.1/firmwares.json/condensed"));
            }

            ret.ThrowOnError();
            idevice.idevice_new(out deviceHandle, udids[0]).ThrowOnError();
            lockdown.lockdownd_client_new_with_handshake(deviceHandle, out lockdownHandle, "iSuite").ThrowOnError();

            // make the ugly large text go away
            waitingForDeviceLabel.Visibility = Visibility.Hidden;
            ensureTrustedLabel.Visibility = Visibility.Hidden;
            refreshDevicesButton.Visibility = Visibility.Hidden;
            continueWithoutDeviceButton.Visibility = Visibility.Hidden;

            // get device info
            PlistHandle temp;
            lockdown.lockdownd_get_device_name(lockdownHandle, out deviceName).ThrowOnError();
            lockdown.lockdownd_get_value(lockdownHandle, null, "SerialNumber", out temp).ThrowOnError();
            temp.Api.Plist.plist_get_string_val(temp, out deviceSerialNumber);

            lockdown.lockdownd_get_value(lockdownHandle, null, "ProductVersion", out temp).ThrowOnError();
            temp.Api.Plist.plist_get_string_val(temp, out deviceProductVersion);

            lockdown.lockdownd_get_value(lockdownHandle, null, "ProductType", out temp).ThrowOnError();
            temp.Api.Plist.plist_get_string_val(temp, out deviceProductType);

            lockdown.lockdownd_get_value(lockdownHandle, null, "BuildVersion", out temp).ThrowOnError();
            temp.Api.Plist.plist_get_string_val(temp, out deviceBuildVersion);

            lockdown.lockdownd_get_value(lockdownHandle, null, "UniqueChipID", out temp).ThrowOnError();
            temp.Api.Plist.plist_get_uint_val(temp, ref deviceUniqueChipID);

            lockdown.lockdownd_get_value(lockdownHandle, null, "DeviceColor", out temp).ThrowOnError();
            temp.Api.Plist.plist_get_string_val(temp, out deviceColor);

            lockdown.lockdownd_get_value(lockdownHandle, null, "HardwareModel", out temp).ThrowOnError();
            temp.Api.Plist.plist_get_string_val(temp, out deviceHardwareModel);

            lockdown.lockdownd_get_value(lockdownHandle, null, "ModelNumber", out temp).ThrowOnError();
            temp.Api.Plist.plist_get_string_val(temp, out deviceModelNumber);

            lockdown.lockdownd_get_value(lockdownHandle, "com.apple.disk_usage", "TotalDiskCapacity", out temp).ThrowOnError();
            temp.Api.Plist.plist_get_uint_val(temp, ref deviceTotalDiskCapacity);

            lockdown.lockdownd_get_value(lockdownHandle, "com.apple.disk_usage", "TotalSystemCapacity", out temp).ThrowOnError();
            temp.Api.Plist.plist_get_uint_val(temp, ref deviceTotalSystemCapacity);

            lockdown.lockdownd_get_value(lockdownHandle, "com.apple.disk_usage", "TotalDataCapacity", out temp).ThrowOnError();
            temp.Api.Plist.plist_get_uint_val(temp, ref deviceTotalDataCapacity);

            lockdown.lockdownd_get_value(lockdownHandle, "com.apple.disk_usage", "TotalSystemAvailable", out temp).ThrowOnError();
            temp.Api.Plist.plist_get_uint_val(temp, ref deviceTotalSystemAvailable);

            lockdown.lockdownd_get_value(lockdownHandle, "com.apple.disk_usage", "TotalDataAvailable", out temp).ThrowOnError();
            temp.Api.Plist.plist_get_uint_val(temp, ref deviceTotalDataAvailable);

            temp.Dispose();

            deviceECID = string.Format("{0:X}", deviceUniqueChipID);

            deviceInfoGroupBox.Header = fws["devices"][deviceProductType]["name"];
            deviceStorageGroupBox.Header = $"Device Storage ({FormatBytes(deviceTotalDiskCapacity)} Total)";

            systemStorageLabel.Content = $"System ({FormatBytes(deviceTotalSystemAvailable)} Total)";
            dataStorageLabel.Content = $"Data ({FormatBytes(deviceTotalDataAvailable)} Total)";

            systemStorageFreeLabel.Content = $"{FormatBytes(deviceTotalSystemCapacity)} Free";
            dataStorageFreeLabel.Content = $"{FormatBytes(deviceTotalDataCapacity)} Free";

            systemStorageProgressBar.Maximum = (int)(deviceTotalSystemCapacity / 10000000);
            dataStorageProgressBar.Maximum = (int)(deviceTotalDataCapacity / 10000000);

            systemStorageProgressBar.Value = (int)((deviceTotalSystemCapacity - deviceTotalSystemAvailable) / 10000000);
            dataStorageProgressBar.Value = (int)((deviceTotalDataCapacity - deviceTotalDataAvailable) / 10000000);

            //deviceInfoListBox.Items.Add(deviceSerialNumber);
            //deviceInfoListBox.Items.Add(deviceECID);
            //deviceInfoListBox.Items.Add($"{deviceProductVersion} ({deviceBuildVersion})");
            //deviceInfoListBox.Items.Add(deviceProductType);
            //deviceInfoListBox.Items.Add(deviceModelNumber);
            //deviceInfoListBox.Items.Add(deviceHardwareModel);
            //deviceInfoListBox.Items.Add(deviceColor);

            mainTabControl.Visibility = Visibility.Visible;
        }

        private void refreshDevicesButton_Click(object sender, RoutedEventArgs e)
        {
            Init();
        }

        private void installNewAppButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void refreshFirmwareButton_Click(object sender, RoutedEventArgs e)
        {
            // oh boy hope this doesnt ever go down
            using (WebClient wc = new WebClient())
            {
                fws = JObject.Parse(wc.DownloadString("https://api.ipsw.me/v2.1/firmwares.json/condensed"));
            }
            JObject hate = fws;      
            List<ListViewItem> compatibleFws = new List<ListViewItem>();
            foreach (JObject f in hate["devices"][deviceProductType]["firmwares"])
            {
                ListViewItem item = new ListViewItem();
                f["version"] = $"{f["version"]} ({f["buildid"]})";
                item.Content = f;
                item.ContentTemplate = (DataTemplate)this.FindName("FwDataTemplate");
                compatibleFws.Add(item);
            }
            firmwareListView.ItemsSource = compatibleFws;
            hate = null;
        }

        private void restoreFirmwareButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult result = MessageBox.Show("Restoring your device will erase ALL information\nPlease ensure that you are signed out of iCloud/Find My is disabled\nContinue?", "WARNING!", MessageBoxButton.YesNo);
            if (result != MessageBoxResult.Yes) return;
            if (firmwareListView.SelectedItem == null) 
            {
                MessageBox.Show("Please select a firmware");
                return;
            }
        }

        private void continueWithoutDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            // hide things that wont work without a device
            waitingForDeviceLabel.Visibility = Visibility.Hidden;
            ensureTrustedLabel.Visibility = Visibility.Hidden;
            refreshDevicesButton.Visibility = Visibility.Hidden;
            continueWithoutDeviceButton.Visibility = Visibility.Hidden;

            deviceInfoTab.Visibility = Visibility.Hidden;
            appsTab.Visibility = Visibility.Hidden;
            fileSystemTab.Visibility = Visibility.Hidden;
            jailbreakTab.Visibility = Visibility.Hidden;
            restoreTab.Visibility = Visibility.Hidden;

            mainTabControl.SelectedItem = settingsTab;

            mainTabControl.Visibility = Visibility.Visible;
        }
    }
}
