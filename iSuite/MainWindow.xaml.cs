using iMobileDevice;
using iMobileDevice.iDevice;
using iMobileDevice.Lockdown;
using iMobileDevice.Plist;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Net;
using System.Windows;
using System.Windows.Controls;

namespace iSuite
{
    public partial class MainWindow : Window
    {
        private readonly IiDeviceApi idevice;
        private readonly ILockdownApi lockdown;
        private iDeviceHandle deviceHandle;
        private LockdownClientHandle lockdownHandle;

        private JObject fws;

        private readonly string dataLocation = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/iSuite/";

        private OptionsJson options;

        // every info string ever
        private Dictionary<string, string> deviceInfo = new Dictionary<string, string>();
        private string deviceName; // DeviceName
        private string deviceSerialNumber;
        private string deviceProductVersion;
        private string deviceBuildVersion;
        private ulong deviceUniqueChipID = 0;
        private string deviceECID; // UniqueChipID converted to hex then string
        private string deviceProductType;
        private string deviceHardwareModel;
        private string deviceModelNumber;
        private string deviceColor;

        // storage related ones
        private ulong deviceTotalDiskCapacity = 0;
        private ulong deviceTotalSystemCapacity = 0;
        private ulong deviceTotalDataCapacity = 0;
        private ulong deviceTotalSystemAvailable = 0;
        private ulong deviceTotalDataAvailable = 0;

        public MainWindow()
        {
            NativeLibraries.Load();
            InitializeComponent();
            idevice = LibiMobileDevice.Instance.iDevice;
            lockdown = LibiMobileDevice.Instance.Lockdown;

            // Load settings
            if (!Directory.Exists(dataLocation))
            {
                Directory.CreateDirectory(dataLocation);
            }
            if (!Directory.Exists(dataLocation + "IPSW/"))
            {
                Directory.CreateDirectory(dataLocation + "IPSW/");
            }
            if (!File.Exists(dataLocation + "options.json"))
            {
                File.WriteAllText(dataLocation + "options.json", JsonConvert.SerializeObject(new OptionsJson()));
            }
            options = JsonConvert.DeserializeObject<OptionsJson>(File.ReadAllText(dataLocation + "options.json"));
            LoadSettingsToControls();
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

            return string.Format("{0:0.##} {1}", dblSByte, Suffix[i]);
        }

        private void Init()
        {
            // only detects devices in normal mode, ill work on recovery and dfu later
            int zero = 0;

            iDeviceError ret = idevice.idevice_get_device_list(out ReadOnlyCollection<string> udids, ref zero);

            if (ret == iDeviceError.NoDevice || udids.Count == 0)
            {
                return;
            }

            using (WebClient wc = new WebClient())
            {
                fws = JObject.Parse(wc.DownloadString(options.fwjsonsource));
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
            lockdown.lockdownd_get_device_name(lockdownHandle, out deviceName).ThrowOnError();
            lockdown.lockdownd_get_value(lockdownHandle, null, "SerialNumber", out PlistHandle temp).ThrowOnError();
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

            List<ListViewItem> deviceInfoListViewItems = new List<ListViewItem>();
            ListViewItem tempi = new ListViewItem();
            tempi.ContentTemplate = (DataTemplate)FindName("DiDataTemplate");

            deviceInfoListView.ItemsSource = deviceInfoListViewItems;

            mainTabControl.Visibility = Visibility.Visible;
        }

        private void LoadSettingsToControls()
        {
            themeSettingComboBox.SelectedItem = options.theme;
            fwJsonSourceTextBox.Text = options.fwjsonsource;
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
                fws = JObject.Parse(wc.DownloadString(options.fwjsonsource));
            }
            List<ListViewItem> compatibleFws = new List<ListViewItem>();
            foreach (JObject f in fws["devices"][deviceProductType]["firmwares"])
            {
                ListViewItem item = new ListViewItem();
                f["version"] = $"{f["version"]} ({f["buildid"]})";
                item.Content = f;
                item.ContentTemplate = (DataTemplate)FindName("FwDataTemplate");
                compatibleFws.Add(item);
            }
            firmwareListView.ItemsSource = compatibleFws;
        }

        private void restoreFirmwareButton_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Restoring your device will erase ALL information\nPlease ensure that you are signed out of iCloud/Find My is disabled\nContinue?", "WARNING!", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            if (firmwareListView.SelectedItem == null)
            {
                MessageBox.Show("Please select a firmware");
                return;
            }
            Firmware selectedFW = JsonConvert.DeserializeObject<Firmware>(((JObject)((ListViewItem)firmwareListView.SelectedItem).Content).ToString());
            if (selectedFW.version.StartsWith("1.") || selectedFW.version.StartsWith("2."))
            {
                MessageBox.Show("Restoring to iOS 1 and 2 is not supported.", "Error!");
                return;
            }
            if (!selectedFW.signed && (deviceProductType.StartsWith("iPhone1,") || deviceProductType.StartsWith("iPod1,")))
            {
                if (MessageBox.Show("This firmware is (probably) not signed, restoring will most likely fail.\nContinue?", "WARNING!", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
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

        private void resetSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            options = new OptionsJson();
            File.WriteAllText(dataLocation + "options.json", JsonConvert.SerializeObject(options));
            LoadSettingsToControls();
        }

        private void saveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            options.theme = (string)themeSettingComboBox.SelectedItem;
            options.fwjsonsource = fwJsonSourceTextBox.Text;
            File.WriteAllText(dataLocation + "options.json", JsonConvert.SerializeObject(options));
        }

        private void deviceInfoListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            Clipboard.SetText(((DeviceInfoElement)((ListViewItem)deviceInfoListView.SelectedItem).Content).value);
        }
    }
}
