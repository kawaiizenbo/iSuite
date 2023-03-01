using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.GZip;

using iMobileDevice;
using iMobileDevice.Afc;
using iMobileDevice.iDevice;
using iMobileDevice.Lockdown;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace iSuite
{
    public partial class MainWindow : Window
    {
        public static IiDeviceApi idevice;
        public static ILockdownApi lockdown;
        public static IAfcApi afc;
        public static iDeviceHandle deviceHandle;
        public static LockdownClientHandle lockdownHandle;
        public static LockdownServiceDescriptorHandle lockdownServiceHandle;
        public static AfcClientHandle afcHandle;

        public static bool onlineFlag = true;

        public static bool shouldStopDetecting = false;

        public static bool normalConnected = false;
        public static bool recoveryConnected = false;
        public static bool dfuConnected = false;

        public static string dataLocation = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/iSuite/";

        public static OptionsJson options;

        public static Dictionary<string, string> deviceInfo = new Dictionary<string, string>();
        public static Dictionary<string, string> stDeviceInfo = new Dictionary<string, string>();
        
        public static ulong deviceUniqueChipID = 0; // ecid
        public static string deviceUDID;
        public static string deviceIdentifier;

        // storage related device info
        public static ulong deviceDiskCapacity = 0;
        public static ulong deviceSystemCapacity = 0;
        public static ulong deviceSystemAvailable = 0;
        public static ulong deviceDataCapacity = 0;
        public static ulong deviceDataAvailable = 0;

        bool dragging = false;
        Point startPoint;
        System.Windows.Forms.Timer ProbeTimer = new System.Windows.Forms.Timer();

        public MainWindow()
        {
            NativeLibraries.Load();
            InitializeComponent();
            idevice = LibiMobileDevice.Instance.iDevice;
            lockdown = LibiMobileDevice.Instance.Lockdown;
            afc = LibiMobileDevice.Instance.Afc;
            ProbeTimer.Enabled = false;
            ProbeTimer.Interval = 500;
            ProbeTimer.Tick += new EventHandler(Probe);

            // Load settings
            if (!Directory.Exists(dataLocation))
            {
                Directory.CreateDirectory(dataLocation);
            }
            if (!Directory.Exists(dataLocation + "temp/"))
            {
                Directory.CreateDirectory(dataLocation + "temp/");
            }
            if (!Directory.Exists(dataLocation + "bin/"))
            {
                Directory.CreateDirectory(dataLocation + "bin/");
            }
            if (!File.Exists(dataLocation + "/options.json"))
            {
                File.WriteAllText(dataLocation + "/options.json", JsonConvert.SerializeObject(new OptionsJson()));
            }
            options = JsonConvert.DeserializeObject<OptionsJson>(File.ReadAllText(dataLocation + "/options.json"));
            usb.Source = Util.BitmapToImageSource(iSuite.Resources.usb);
        }

        private void DeviceDetectorThread()
        {
            while (!shouldStopDetecting)
            {
                int count = 0;

                iDeviceError ret = idevice.idevice_get_device_list(out ReadOnlyCollection<string> udids, ref count);

                if (ret == iDeviceError.NoDevice || udids.Count == 0)
                {
                    Process process = new Process();
                    process.StartInfo.FileName = "irecovery.exe";
                    process.StartInfo.Arguments = "-q";
                    process.StartInfo.CreateNoWindow = true;
                    process.StartInfo.UseShellExecute = false;
                    process.StartInfo.RedirectStandardOutput = true;
                    process.Start();
                    Thread.Sleep(1000);
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                    if (process.ExitCode == 0)
                    {
                        string text = process.StandardOutput.ReadToEnd();
                        if (text.Contains("Recovery"))
                        {
                            recoveryConnected = true;
                            dfuConnected = false;
                            normalConnected = false;
                            shouldStopDetecting = true;
                        }
                        else if (text.Contains("DFU"))
                        {
                            dfuConnected = true;
                            recoveryConnected = false;
                            normalConnected = false;
                            shouldStopDetecting = true;
                        }
                    }
                }
                else
                {
                    deviceUDID = udids[0];
                    ret.ThrowOnError();
                    idevice.idevice_new(out deviceHandle, udids[0]);
                    lockdown.lockdownd_client_new_with_handshake(deviceHandle, out lockdownHandle, "iSuite");
                    lockdown.lockdownd_start_service(lockdownHandle, "com.apple.afc", out lockdownServiceHandle);
                    lockdownHandle.Api.Afc.afc_client_new(deviceHandle, lockdownServiceHandle, out afcHandle);
                    dfuConnected = false;
                    recoveryConnected = false;
                    normalConnected = true;
                    shouldStopDetecting = true;
                }
                Thread.Sleep(1000);
            }
        }

        private void Probe(object sender, EventArgs e)
        {
            int count = 0;

            iDeviceError ret = idevice.idevice_get_device_list(out ReadOnlyCollection<string> udids, ref count);

            if (ret == iDeviceError.NoDevice || udids.Count == 0)
            {
                Process process = new Process();
                process.StartInfo.FileName = "irecovery.exe";
                process.StartInfo.Arguments = "-q";
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.Start();
                Thread.Sleep(1000);
                if (!process.HasExited)
                {
                    process.Kill();
                }
                if (process.ExitCode == 0)
                {
                    string text = process.StandardOutput.ReadToEnd();
                    if (text.Contains("Recovery"))
                    {
                        recoveryConnected = true;
                        dfuConnected = false;
                        normalConnected = false;
                        shouldStopDetecting = true;
                    }
                    else if (text.Contains("DFU"))
                    {
                        dfuConnected = true;
                        recoveryConnected = false;
                        normalConnected = false;
                        shouldStopDetecting = true;
                    }
                    else
                    {
                        ResetAll();
                    }
                }
                else
                {
                    ResetAll();
                }
            }
        }

        private async void ResetAll()
        {
            mainTabControl.Visibility = Visibility.Hidden;

            deviceDiskCapacity = 0;
            deviceSystemCapacity = 0;
            deviceDataCapacity = 0;
            deviceSystemAvailable = 0;
            deviceDataAvailable = 0;
            deviceInfo = new Dictionary<string, string>();
            stDeviceInfo = new Dictionary<string, string>();
            deviceUniqueChipID = 0;
            deviceUDID = "0";
            deviceHandle = null;
            lockdownHandle = null;
            lockdownServiceHandle = null;
            afcHandle = null;
            normalConnected = false;
            recoveryConnected = false;
            dfuConnected = false;
            AFCPage.afcPath = "/";
            RestorePage.ipswPath = "";
            shouldStopDetecting = false;

            await Task.Run(new Action(DeviceDetectorThread));
            await Init();
        }

        private async Task Init()
        {
            waitingForDeviceLabel.Content = "Device Found";
            ensureTrustedLabel.Content = "Obtaining device data...";
            if (normalConnected)
            {
                // get device info
                stDeviceInfo["Name"] = deviceInfo["Name"] = Util.GetLockdowndStringKey(lockdownHandle, null, "DeviceName");
                deviceIdentifier = stDeviceInfo["Product Identifier"] = deviceInfo["Product Identifier"] = Util.GetLockdowndStringKey(lockdownHandle, null, "ProductType");
                stDeviceInfo["Model Number"] = deviceInfo["Model Number"] = Util.GetLockdowndStringKey(lockdownHandle, null, "ModelNumber") + 
                    Util.GetLockdowndStringKey(lockdownHandle, null, "RegionInfo");
                stDeviceInfo["Board Configuration"] = deviceInfo["Board Configuration"] = Util.GetLockdowndStringKey(lockdownHandle, null, "HardwareModel");
                stDeviceInfo["Architecture"] = deviceInfo["Architecture"] = Util.GetLockdowndStringKey(lockdownHandle, null, "CPUArchitecture");
                stDeviceInfo["iOS Version"] = deviceInfo["iOS Version"] = Util.GetLockdowndStringKey(lockdownHandle, null, "ProductVersion");
                stDeviceInfo["iOS Build"] = deviceInfo["iOS Build"] = Util.GetLockdowndStringKey(lockdownHandle, null, "BuildVersion");
                try
                {
                    stDeviceInfo["Baseband Version"] = deviceInfo["Baseband Version"] = Util.GetLockdowndStringKey(lockdownHandle, null, "BasebandVersion");
                }
                catch(Exception) { }
                deviceInfo["Serial Number"] = Util.GetLockdowndStringKey(lockdownHandle, null, "SerialNumber");
                deviceUniqueChipID = Util.GetLockdowndUlongKey(lockdownHandle, null, "UniqueChipID");
                deviceInfo["ECID"] = string.Format("{0:X}", deviceUniqueChipID);
                try
                {
                    if (Util.GetLockdowndStringKey(lockdownHandle, null, "InternationalMobileEquipmentIdentity") == null)
                    {
                        deviceInfo["MEID"] = Util.GetLockdowndStringKey(lockdownHandle, null, "MobileEquipmentIdentifier");
                    }
                    else
                    {
                        deviceInfo["IMEI"] = Util.GetLockdowndStringKey(lockdownHandle, null, "InternationalMobileEquipmentIdentity");
                    }
                    deviceInfo["Phone Number"] = Util.GetLockdowndStringKey(lockdownHandle, null, "PhoneNumber");
                }
                catch(Exception) { }
                deviceInfo["UDID"] = deviceUDID;
                deviceInfo["Wi-Fi MAC Address"] = Util.GetLockdowndStringKey(lockdownHandle, null, "WiFiAddress").ToUpper();
                stDeviceInfo["Activation Status"] = deviceInfo["Activation Status"] = Util.GetLockdowndStringKey(lockdownHandle, null, "ActivationState");
                try
                {
                    // ios 3 and lower doent have this
                    stDeviceInfo["Display Resolution"] = deviceInfo["Display Resolution"] = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.mobile.iTunes", "ScreenWidth").ToString() + "x" +
                        Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.mobile.iTunes", "ScreenHeight").ToString();
                }
                catch(Exception) { }

                // storage
                deviceDiskCapacity = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalDiskCapacity");
                deviceSystemCapacity = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalSystemCapacity");
                deviceDataCapacity = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalDataCapacity");
                deviceSystemAvailable = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalSystemAvailable");
                deviceDataAvailable = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalDataAvailable");
                stDeviceInfo["Disk Capacity"] = deviceInfo["Disk Capacity"] = Util.FormatBytes(deviceDiskCapacity);

                // battery
                stDeviceInfo["Current Charge"] = deviceInfo["Current Charge"] = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.mobile.battery", "BatteryCurrentCapacity").ToString() + "%";
                DeviceInfoPage.UpdateControls();

                // get apps
                await Task.Run(new Action(AppsPage.GetAppsThread));
                AppsPage.installedAppsListView.ItemsSource = AppsPage.apps;

                // refresh firmware
                //refreshFirmwareButton_Click(null, null);

                // afc
                AFCPage.Init();
            }
            else if (recoveryConnected)
            {
                DeviceInfoPage.SetRecovery();

                // awful
                using (Process p = new Process())
                {
                    p.StartInfo.FileName = "irecovery.exe";
                    p.StartInfo.Arguments = "-q";
                    p.StartInfo.CreateNoWindow = true;
                    p.StartInfo.UseShellExecute = false;
                    p.StartInfo.RedirectStandardOutput = true;
                    p.Start();
                    while (!p.StandardOutput.EndOfStream)
                    {
                        string line = p.StandardOutput.ReadLine();
                        if (line.StartsWith("ECID: "))
                        {
                            deviceInfo["ECID"] = line.Remove(0, 5).Trim().TrimStart('0').TrimStart('x').TrimStart('0').ToUpper();
                        }
                        else if (line.StartsWith("SRNM: "))
                        {
                            deviceInfo["Serial Number"] = line.Remove(0, 5).Trim();
                        }
                    }
                }

                DeviceInfoPage.UpdateControls();

                appsTab.IsEnabled = false;
                fileSystemTab.IsEnabled = false;
                jailbreakTab.IsEnabled = false;
                restoreTab.IsEnabled = true;
            }
            else if (dfuConnected)
            {
                DeviceInfoPage.SetDFU();

                appsTab.IsEnabled = false;
                fileSystemTab.IsEnabled = false;
                jailbreakTab.IsEnabled = false;
                restoreTab.IsEnabled = false;
            }
            if (!recoveryConnected && !normalConnected && !dfuConnected)
            {
                // hide things that wont work without a device
                deviceInfoTab.IsEnabled = false;
                appsTab.IsEnabled = false;
                fileSystemTab.IsEnabled = false;
                jailbreakTab.IsEnabled = false;
                restoreTab.IsEnabled = false;
                mainTabControl.SelectedItem = settingsTab;
            }

            mainTabControl.Visibility = Visibility.Visible;
            ProbeTimer.Enabled = true;
        }

        #region Settings Tab
        private void resetSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            options = new OptionsJson();
            ApiTextBox.Text = options.APISource;
            darkModeCheckBox.IsChecked = options.DarkMode;
            tempDataLocTextBox.Text = options.TempDataLocation;
            languageComboBox.SelectedIndex = options.Language;
            colorSchemeComboBox.SelectedIndex = options.ColorScheme;
            File.WriteAllText(dataLocation + "options.json", JsonConvert.SerializeObject(options));
        }

        private void creditsButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("iSuite by KawaiiZenbo\nBased on LibiMobileDevice\nIPSW API by ipsw.me\nJailbreak API by littlebyte", "About iSuite");
        }

        private void saveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            options.APISource = ApiTextBox.Text;
            options.DarkMode = (bool)darkModeCheckBox.IsChecked;
            options.TempDataLocation = tempDataLocTextBox.Text;
            options.Language = languageComboBox.SelectedIndex;
            options.ColorScheme = colorSchemeComboBox.SelectedIndex;
            File.WriteAllText(dataLocation + "/options.json", JsonConvert.SerializeObject(options));
        }

        private void darkModeCheckBox_Checked(object sender, RoutedEventArgs e)
        {

        }

        private void colorSchemeComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            switch (colorSchemeComboBox.SelectedIndex)
            {
                case 0:
                    window.Background = SystemParameters.WindowGlassBrush;
                    DeviceInfoPage.SetBarColors((SolidColorBrush)SystemParameters.WindowGlassBrush);
                    topBarGrid.Background = SystemParameters.WindowGlassBrush;
                    break;
                case 1:
                    window.Background = new SolidColorBrush(Colors.DodgerBlue);
                    DeviceInfoPage.SetBarColors(new SolidColorBrush(Colors.DodgerBlue));
                    topBarGrid.Background = new SolidColorBrush(Colors.DodgerBlue);
                    break;
                case 2:
                    window.Background = new SolidColorBrush(Colors.Chartreuse);
                    DeviceInfoPage.SetBarColors(new SolidColorBrush(Colors.Chartreuse));
                    topBarGrid.Background = new SolidColorBrush(Colors.Chartreuse);
                    break;
                case 3:
                    window.Background = new SolidColorBrush(Colors.MediumOrchid);
                    DeviceInfoPage.SetBarColors(new SolidColorBrush(Colors.MediumOrchid));
                    topBarGrid.Background = new SolidColorBrush(Colors.MediumOrchid);
                    break;
                case 4:
                    window.Background = new SolidColorBrush(Colors.Coral);
                    DeviceInfoPage.SetBarColors(new SolidColorBrush(Colors.Coral));
                    topBarGrid.Background = new SolidColorBrush(Colors.Coral);
                    break;
                case 5:
                    window.Background = new SolidColorBrush(Colors.Pink);
                    DeviceInfoPage.SetBarColors(new SolidColorBrush(Colors.Pink));
                    topBarGrid.Background = new SolidColorBrush(Colors.Pink);
                    break;
                default:
                    break;
            }
        }
        #endregion

        #region Window Functions
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            mainTabControl.Visibility = Visibility.Hidden;

            // set options controls to options
            ApiTextBox.Text = options.APISource;
            tempDataLocTextBox.Text = options.TempDataLocation;
            darkModeCheckBox.IsChecked = options.DarkMode;
            languageComboBox.SelectedIndex = options.Language;
            colorSchemeComboBox.SelectedIndex = options.ColorScheme;

            // check for them until you dont need to check for them anymore
            await Task.Run(new Action(DeviceDetectorThread));
            await Init();
        }

        private async void continueWithoutDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            shouldStopDetecting = true;
            await Init();
        }

        private void closeButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void topBarGrid_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            dragging = true;
            startPoint = e.GetPosition(this.topBarGrid);
        }

        private void topBarGrid_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            dragging = false;
        }

        private void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            if (dragging)
            {
                Point p = PointToScreen(e.GetPosition(this.topBarGrid));
                window.Left = p.X - this.startPoint.X;
                window.Top = p.Y - this.startPoint.Y;
            }
        }

        private void minimizeButton_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }
        #endregion
    }
}
