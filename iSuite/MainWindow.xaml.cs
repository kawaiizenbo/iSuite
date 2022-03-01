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
        private IiDeviceApi idevice;
        private ILockdownApi lockdown;
        private IAfcApi afc;
        private iDeviceHandle deviceHandle;
        private LockdownClientHandle lockdownHandle;
        private LockdownServiceDescriptorHandle lockdownServiceHandle;
        private AfcClientHandle afcHandle;

        private JObject fws;
        private bool onlineFlag = true;

        private bool shouldStopDetecting = false;

        private bool normalConnected = false;
        private bool recoveryConnected = false;
        private bool dfuConnected = false;

        private string dataLocation = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/iSuite/";

        private OptionsJson options;

        private string ipaPath;
        private string afcPath = "/";
        private string ipswPath;
        private string selectedBundleID;

        private Dictionary<string, string> deviceInfo = new Dictionary<string, string>();
        private Dictionary<string, string> batteryInfo = new Dictionary<string, string>();
        private List<DeviceApp> apps = new List<DeviceApp>();
        private ulong deviceUniqueChipID = 0; // ecid
        private string deviceUDID;

        // storage related device info
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
            afc = LibiMobileDevice.Instance.Afc;

            // Load settings
            if (!Directory.Exists(dataLocation))
            {
                Directory.CreateDirectory(dataLocation);
            }
            if (!Directory.Exists(dataLocation + "IPSW/"))
            {
                Directory.CreateDirectory(dataLocation + "IPSW/");
            }
            if (!Directory.Exists(dataLocation + "temp/"))
            {
                Directory.CreateDirectory(dataLocation + "temp/");
            }
            if (!Directory.Exists(dataLocation + "bin/"))
            {
                Directory.CreateDirectory(dataLocation + "bin/");
            }
            if (!File.Exists(dataLocation + "options.json"))
            {
                File.WriteAllText(dataLocation + "options.json", JsonConvert.SerializeObject(new OptionsJson()));
            }
            options = JsonConvert.DeserializeObject<OptionsJson>(File.ReadAllText(dataLocation + "options.json"));
            if (options.packageManagerRepos == null)
            {
                options.packageManagerRepos = new List<string>() { "http://repo.kawaiizenbo.me/" };
            }
            usb.Source = Util.BitmapToImageSource(iSuite.Resources.usb);
        }
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            mainTabControl.Visibility = Visibility.Hidden;

            // check for them until you dont need to check for them anymore
            await Task.Run(new Action(DeviceDetectorThread));
            await Init();
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

        private async Task Init()
        {
            if (normalConnected)
            {
                // get device info
                deviceInfo["Name"] = Util.GetLockdowndStringKey(lockdownHandle, null, "DeviceName");
                deviceInfo["Identifier"] = Util.GetLockdowndStringKey(lockdownHandle, null, "ProductType");
                deviceInfo["Model Number"] = Util.GetLockdowndStringKey(lockdownHandle, null, "ModelNumber") + 
                    Util.GetLockdowndStringKey(lockdownHandle, null, "RegionInfo");
                deviceInfo["Board Config"] = Util.GetLockdowndStringKey(lockdownHandle, null, "HardwareModel");
                deviceInfo["Architecture"] = Util.GetLockdowndStringKey(lockdownHandle, null, "CPUArchitecture");
                deviceInfo["Version"] = Util.GetLockdowndStringKey(lockdownHandle, null, "ProductVersion");
                deviceInfo["Build"] = Util.GetLockdowndStringKey(lockdownHandle, null, "BuildVersion");
                try
                {
                    deviceInfo["Baseband Ver."] = Util.GetLockdowndStringKey(lockdownHandle, null, "BasebandVersion");
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
                deviceInfo["WiFI MAC Address"] = Util.GetLockdowndStringKey(lockdownHandle, null, "WiFiAddress").ToUpper();
                deviceInfo["Activated"] = Util.GetLockdowndStringKey(lockdownHandle, null, "ActivationState");
                try
                {
                    // ios 3 and lower doent have this
                    deviceInfo["Resolution"] = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.mobile.iTunes", "ScreenWidth").ToString() + "x" +
                        Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.mobile.iTunes", "ScreenHeight").ToString();
                }
                catch(Exception) { }

                // storage
                deviceTotalDiskCapacity = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalDiskCapacity");
                deviceTotalSystemCapacity = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalSystemCapacity");
                deviceTotalDataCapacity = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalDataCapacity");
                deviceTotalSystemAvailable = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalSystemAvailable");
                deviceTotalDataAvailable = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalDataAvailable");

                // battery
                batteryInfo["Current Charge"] = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.mobile.battery", "BatteryCurrentCapacity").ToString() + "%";


                // put them on the controls
                try { deviceInfoGroupBox.Header = Util.GetLockdowndStringKey(lockdownHandle, null, "MarketingName"); }
                catch (Exception) { deviceInfoGroupBox.Header = deviceInfo["Identifier"]; }

                deviceStorageGroupBox.Header = $"Device Storage ({Util.FormatBytes(deviceTotalDiskCapacity)} Total)";

                systemStorageLabel.Content = $"System ({Util.FormatBytes(deviceTotalSystemCapacity)} Total)";
                dataStorageLabel.Content = $"Data ({Util.FormatBytes(deviceTotalDataCapacity)} Total)";

                systemStorageFreeLabel.Content = $"{Util.FormatBytes(deviceTotalSystemAvailable)} Free";
                dataStorageFreeLabel.Content = $"{Util.FormatBytes(deviceTotalDataAvailable)} Free";

                systemStorageProgressBar.Maximum = (int)(deviceTotalSystemCapacity / 10000000);
                dataStorageProgressBar.Maximum = (int)(deviceTotalDataCapacity / 10000000);

                systemStorageProgressBar.Value = (int)((deviceTotalSystemCapacity - deviceTotalSystemAvailable) / 10000000);
                dataStorageProgressBar.Value = (int)((deviceTotalDataCapacity - deviceTotalDataAvailable) / 10000000);

                deviceInfo["Capacity"] = Util.FormatBytes(deviceTotalDiskCapacity);

                deviceInfoListView.ItemsSource = deviceInfo;
                batteryInfoListView.ItemsSource = batteryInfo;

                // get apps
                await Task.Run(new Action(GetAppsThread));
                installedAppsListView.ItemsSource = apps;

                // load fs to listbox (do not)
                //firmwareListView.ItemsSource = fws["devices"][deviceInfo["Identifier"]]["firmwares"];

                // afc
                afcPathTextBox.Text = afcPath;
                try
                {
                    afc.afc_read_directory(afcHandle, afcPath, out ReadOnlyCollection<string> afcDirectory).ThrowOnError();
                    afcItemsListBox.ItemsSource = afcDirectory;
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message);
                }
            }
            else if (recoveryConnected)
            {
                deviceInfoGroupBox.Header = "Recovery Mode";
                recoveryModeToggleButton.Content = "Exit Recovery";
                powerOffDeviceButton.IsEnabled = false;
                rebootDeviceButton.IsEnabled = false;

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

                deviceInfoListView.ItemsSource = deviceInfo;

                appsTab.IsEnabled = false;
                fileSystemTab.IsEnabled = false;
                //jailbreakTab.IsEnabled = false;
                //restoreTa.IsEnabled = false;
            }
            else if (dfuConnected)
            {
                deviceInfoGroupBox.Header = "DFU Mode";
                recoveryModeToggleButton.Content = "-------";
                recoveryModeToggleButton.IsEnabled = false;
                powerOffDeviceButton.IsEnabled = false;
                rebootDeviceButton.IsEnabled = false;

                appsTab.IsEnabled = false;
                fileSystemTab.IsEnabled = false;
                //jailbreakTab.IsEnabled = false;
                //restoreTab.IsEnabled = false;
            }
            if (!recoveryConnected && !normalConnected && !dfuConnected)
            {
                // hide things that wont work without a device
                deviceInfoTab.IsEnabled = false;
                appsTab.IsEnabled = false;
                fileSystemTab.IsEnabled = false;
                //jailbreakTab.IsEnabled = false;
                //restoreTab.IsEnabled = false;
            }

            mainTabControl.Visibility = Visibility.Visible;
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
                    Arguments = $"-u {deviceUDID} --install \"{ipaPath}\"",
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
                    Arguments = $"-u {deviceUDID} --uninstall \"{selectedBundleID}\"",
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

        private void GetAppsThread()
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "ideviceinstaller.exe",
                    Arguments = $"-u {deviceUDID} -l",
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
        }
        private void RestoreThread()
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = $"{dataLocation}bin/futurerestore-v194.exe",
                    Arguments = $"--latest-baseband \"{ipswPath}\"",
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
                File.WriteAllText(dataLocation + "fws.json", wc.DownloadString(new Uri(options.fwjsonsource)));
                fws = JObject.Parse(File.ReadAllText(dataLocation + "fws.json"));
            }
            firmwareListView.ItemsSource = fws["devices"][deviceInfo["Identifier"]]["firmwares"];
        }

        private async void restoreFirmwareButton_Click(object sender, RoutedEventArgs e)
        {/*
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
            if (!File.Exists($"{dataLocation}IPSW/{selectedFW.filename}"))
            {
                restoreStatusListBox.Items.Add($"Downloading iOS {selectedFW.version} for {deviceInfo["Identifier"]}");
                using (WebClient wc = new WebClient())
                {
                    wc.DownloadFile(new Uri(selectedFW.url), $"{dataLocation}IPSW/{selectedFW.filename}");
                }
                restoreStatusListBox.Items.Add($"Download complete.");
            }
            if (Util.CalculateMD5($"{dataLocation}IPSW/{selectedFW.filename}") != selectedFW.md5sum)
            {
                restoreStatusListBox.Items.Add("File hash verification failed.");
                return;
            }
            ipswPath = $"{dataLocation}IPSW/{selectedFW.filename}";
            await Task.Run(new Action(RestoreThread));
            MessageBox.Show("Please restart the application.");*/
        }
        private async void continueWithoutDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            await Init();
        }

        private void resetSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            options = new OptionsJson();
            File.WriteAllText(dataLocation + "options.json", JsonConvert.SerializeObject(options));
        }

        private void saveSettingsButton_Click(object sender, RoutedEventArgs e)
        {
            File.WriteAllText(dataLocation + "options.json", JsonConvert.SerializeObject(options));
        }

        private void addRepoButton_Click(object sender, RoutedEventArgs e)
        {
            string repo = addRepoTextBox.Text;
            repo = repo.Trim();
            if (repo == "" || repo == null) return;
            if (!repo.StartsWith("http://") && !repo.StartsWith("https://")) repo = "http://" + repo;
            if (!repo.EndsWith("/")) repo += "/";
            repoListBox.Items.Add(repo);
            options.packageManagerRepos.Add(repo);
            File.WriteAllText(dataLocation + "options.json", JsonConvert.SerializeObject(options));
            addRepoTextBox.Text = null;
        }

        private void repoListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (repoListBox.SelectedItem.ToString() == null) return;
            string link = repoListBox.SelectedItem.ToString();
            WebClient webClient = new WebClient();
            // headers because some repos are 'interesting'
            webClient.Headers.Add("X-Machine", "iPod4,1");
            webClient.Headers.Add("X-Unique-ID", "0000000000000000000000000000000000000000");
            webClient.Headers.Add("X-Firmware", "6.1");
            webClient.Headers.Add("User-Agent", "Telesphoreo APT-HTTP/1.0.999");
            // Attempt to download packages file (try/catch hell)
            try
            {
                MemoryStream packagesBz2 = new MemoryStream(webClient.DownloadData(link + "Packages.bz2"));
                FileStream packagesBz2Decompressed = File.Create(dataLocation + "temp/Packages");
                BZip2.Decompress(packagesBz2, packagesBz2Decompressed, true);
            }
            catch (Exception)
            {
                try
                {
                    MemoryStream packagesGz = new MemoryStream(webClient.DownloadData(link + "Packages.bz2"));
                    FileStream packagesGzDecompressed = File.Create(dataLocation + "temp/Packages");
                    GZip.Decompress(packagesGz, packagesGzDecompressed, true);
                }
                catch (Exception)
                {
                    try
                    {
                        webClient.DownloadFile(link + "Packages", dataLocation + "temp/Packages");
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
            }
            Thread.Sleep(500);
            // Clean list of package links, names, and versions
            List<DebPackage> packages = new List<DebPackage>();
            Regex rx = new Regex(@"\n\n+", RegexOptions.Compiled);
            foreach (string s in rx.Split(File.ReadAllText(dataLocation + "temp/Packages")))
            {
                string packageID = "";
                string version = "";
                string arch = "";
                string fileName = "";
                string md5 = "";
                string author = "";
                string name = "";
                foreach (string s2 in s.Split('\n'))
                {
                    if (s2.StartsWith("Package: "))
                    {
                        packageID = s2.Remove(0, 8).Trim();
                    }
                    else if (s2.StartsWith("Version: "))
                    {
                        version = s2.Remove(0, 8).Trim();
                    }
                    else if (s2.StartsWith("Architecture: "))
                    {
                        arch = s2.Remove(0, 13).Trim();
                    }
                    else if (s2.StartsWith("Filename: "))
                    {
                        fileName = s2.Remove(0, 9).Trim();
                    }
                    else if (s2.StartsWith("MD5sum: "))
                    {
                        md5 = s2.Remove(0, 7).Trim();
                    }
                    else if (s2.StartsWith("Author: "))
                    {
                        author = s2.Remove(0, 7).Trim();
                    }
                    else if (s2.StartsWith("Name: "))
                    {
                        name = s2.Remove(0, 5).Trim();
                    }
                }
                packages.Add(new DebPackage()
                {
                    Package = packageID,
                    Version = version,
                    Author = author,
                    Name = name,
                    MD5Sum = md5,
                    Architecture = arch,
                    Filename = fileName
                });
            }
            // remove last one because bunch of line feeds at the end
            packages.RemoveAt(packages.Count - 1);
            packagesListView.ItemsSource = packages;
            packagesLVGB.Header = link;
        }

        private void removeSelectedRepoButton_Click(object sender, RoutedEventArgs e)
        {
            repoListBox.Items.Remove(repoListBox.SelectedItem);
        }

        private void packagesListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var saveDebFile = new Microsoft.Win32.SaveFileDialog();
            saveDebFile.FileName = $"{((DebPackage)packagesListView.SelectedItem).Package}-{((DebPackage)packagesListView.SelectedItem).Version}.deb";
            saveDebFile.DefaultExt = ".deb";
            saveDebFile.Filter = "Debian/APT Packages|*.deb";

            saveDebFile.ShowDialog();

            string saveDebPath = saveDebFile.FileName;

            string link = ((DebPackage)packagesListView.SelectedItem).Filename;

            if (!((DebPackage)packagesListView.SelectedItem).Filename.StartsWith("http://"))
            {
                link = packagesLVGB.Header + link;
            }
            using (WebClient wc = new WebClient())
            {
                wc.DownloadFileAsync(new Uri(link), saveDebPath);
            }
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
            MessageBox.Show("Please restart the application.");
        }

        private void recoveryModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (normalConnected)
            {
                lockdown.lockdownd_enter_recovery(lockdownHandle);
                MessageBox.Show("Please restart the application.");
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
                MessageBox.Show("Please restart the application.");
            }
        }

        private void aboutButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("iSuite by KawaiiZenbo\nBased on LibiMobileDevice and ", "About iSuite");
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
            MessageBox.Show("Please restart the application.");
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

        private void afcGoButton_Click(object sender, RoutedEventArgs e)
        {
            afcPath = afcPathTextBox.Text;
            try
            {
                afc.afc_read_directory(afcHandle, afcPath, out ReadOnlyCollection<string> afcDirectory).ThrowOnError();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void afcListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (afcItemsListBox.SelectedItem.ToString() == ".") return;
            try
            {
                afc.afc_read_directory(afcHandle, afcPath + $"/{afcItemsListBox.SelectedItem}/", out ReadOnlyCollection<string> afcDirectory).ThrowOnError();
                if (afcItemsListBox.SelectedItem.ToString() == "..")
                {
                    afcPath = afcPath.Replace('/' + afcPath.TrimEnd('/').Split('/').Last(), "");
                }
                else
                {
                    afcPath += $"{afcItemsListBox.SelectedItem}/";
                }
                afcPathTextBox.Text = afcPath;
                afcItemsListBox.ItemsSource = null;
                afcItemsListBox.ItemsSource = afcDirectory;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void afcUploadFileButton_Click(object sender, RoutedEventArgs e)
        {
            var openAfcUploadFile = new Microsoft.Win32.OpenFileDialog();

            openAfcUploadFile.ShowDialog();

            if (openAfcUploadFile.FileName == "") return;

            string afcUploadFilePath = openAfcUploadFile.FileName;

            string afcUploadFileName = afcUploadFilePath.Split('\\').Last();

            ulong handle = 0UL;
            afc.afc_file_open(afcHandle, afcPath + "/" + afcUploadFileName, AfcFileMode.FopenRw, ref handle);
            byte[] array = File.ReadAllBytes(afcUploadFilePath);
            uint bytesWritten = 0U;
            afc.afc_file_write(afcHandle, handle, array, (uint)array.Length, ref bytesWritten);
            afc.afc_file_close(afcHandle, handle);
            afcRefreshButton_Click(sender, e);
        }

        private void afcRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                afc.afc_read_directory(afcHandle, afcPath, out ReadOnlyCollection<string> afcDirectory).ThrowOnError();
                afcItemsListBox.ItemsSource = null;
                afcItemsListBox.ItemsSource = afcDirectory;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void afcMKDirButton_Click(object sender, RoutedEventArgs e)
        {
            GenericSingleInputForm f = new GenericSingleInputForm();
            f.Title = "Make Directory";
            f.LabelText = "Please enter the name for the new directory.";
            f.ShowDialog();
            afc.afc_make_directory(afcHandle, afcPath + "/" + f.TextBoxContents);
            afcRefreshButton_Click(sender, e);
        }

        private void afcConnectAfc2Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // idk this causes issues sometimes blame libusbmuxd
                lockdown.lockdownd_start_service(lockdownHandle, "com.apple.afc2", out lockdownServiceHandle).ThrowOnError();
                lockdownHandle.Api.Afc.afc_client_new(deviceHandle, lockdownServiceHandle, out afcHandle).ThrowOnError();
                afcPath = "/";
                afcPathTextBox.Text = afcPath;
                afcRefreshButton_Click(sender, e);
            }
            catch(Exception ex)
            {
                MessageBox.Show(ex.Message, "Could not connect to afc2");
            }
        }

        private void afcDeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                afc.afc_remove_path(afcHandle, afcPath + $"/{afcItemsListBox.SelectedItem}").ThrowOnError();
                afcRefreshButton_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Could not delete object");
            }
        }

        private void afcDownloadFileButton_Click(object sender, RoutedEventArgs e)
        {
            var saveAfcFile = new Microsoft.Win32.SaveFileDialog();
            saveAfcFile.FileName = afcItemsListBox.SelectedItem.ToString();

            saveAfcFile.ShowDialog();

            string afcSaveFilePath = saveAfcFile.FileName;
            string afcFilePath = afcPath + "/" + afcItemsListBox.SelectedItem.ToString();
            afc.afc_get_file_info(afcHandle, afcFilePath, out ReadOnlyCollection<string> infoListr);
            List<string> infoList = new List<string>(infoListr.ToArray());
            long fileSize = Convert.ToInt64(infoList[infoList.FindIndex(x => x == "st_size") + 1]);

            ulong fileHandle = 0;
            afc.afc_file_open(afcHandle, afcFilePath, AfcFileMode.FopenRdonly, ref fileHandle);

            FileStream fileStream = File.Create(afcSaveFilePath);
            const int bufferSize = 4194304;
            for (int i = 0; i < fileSize / bufferSize + 1; i++)
            {
                uint bytesRead = 0;

                long remainder = fileSize - i * bufferSize;
                int currBufferSize = remainder >= bufferSize ? bufferSize : (int)remainder;
                byte[] currBuffer = new byte[currBufferSize];

                if ((afc.afc_file_read(afcHandle, fileHandle, currBuffer, Convert.ToUInt32(currBufferSize), ref bytesRead))
                    != AfcError.Success)
                {
                    afc.afc_file_close(afcHandle, fileHandle);
                }

                fileStream.Write(currBuffer, 0, currBufferSize);
            }

            fileStream.Close();
        }
    }
}
