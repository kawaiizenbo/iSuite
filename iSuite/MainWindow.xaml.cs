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
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

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

        private bool shouldStopDetecting = false;

        private bool normalConnected = false;
        private bool recoveryConnected = false;
        private bool dfuConnected = false;

        private string dataLocation = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/iSuite/";

        private OptionsJson options;
        private JailbreakJson jailbreaks;

        private string ipaPath;
        private string afcPath = "/";
        private string ipswPath;

        private Dictionary<string, string> deviceInfo = new();
        private List<DeviceApp> apps = new();
        private ulong deviceUniqueChipID = 0; // ecid
        private string deviceUUID;

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
            if (!File.Exists($"{dataLocation}bin/futurerestore-v194.exe"))
            {
                File.WriteAllBytes($"{dataLocation}bin/futurerestore-v194.exe", iSuite.Resources.futurerestore_v194);
            }
            options = JsonConvert.DeserializeObject<OptionsJson>(File.ReadAllText(dataLocation + "options.json"));
            if (options.packageManagerRepos == null)
            {
                options.packageManagerRepos = new() { "http://repo.kawaiizenbo.me/", "http://cydia.invoxiplaygames.uk/" };
            }
            usb.Source = Util.BitmapToImageSource(iSuite.Resources.usb);
            LoadSettingsToControls();
        }
        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            mainTabControl.Visibility = Visibility.Hidden;

            // murder
            using (Process p = new())
            {
                p.StartInfo.FileName = "taskkill";
                p.StartInfo.Arguments = "/f /im iTunes.exe";
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.WaitForExit();
            }

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
                    process.StartInfo.FileName = "runtimes/win-x86/native/irecovery.exe";
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
                    deviceUUID = udids[0];
                    ret.ThrowOnError();
                    idevice.idevice_new(out deviceHandle, udids[0]).ThrowOnError();
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

            using (HttpClient wc = new())
            {
                File.WriteAllText(dataLocation + "fws.json", await wc.GetStringAsync(options.fwjsonsource));
                fws = JObject.Parse(await File.ReadAllTextAsync(dataLocation + "fws.json"));
            }

            if (normalConnected)
            {
                // get device info
                deviceInfo["Name"] = Util.GetLockdowndStringKey(lockdownHandle, null, "DeviceName");
                deviceInfo["Identifier"] = Util.GetLockdowndStringKey(lockdownHandle, null, "ProductType");
                deviceInfo["Model Number"] = Util.GetLockdowndStringKey(lockdownHandle, null, "ModelNumber") + Util.GetLockdowndStringKey(lockdownHandle, null, "RegionInfo");
                deviceInfo["Board Config"] = Util.GetLockdowndStringKey(lockdownHandle, null, "HardwareModel");
                deviceInfo["Version"] = Util.GetLockdowndStringKey(lockdownHandle, null, "ProductVersion");
                deviceInfo["Build"] = Util.GetLockdowndStringKey(lockdownHandle, null, "BuildVersion");
                try
                {
                    deviceInfo["Baseband Ver."] = Util.GetLockdowndStringKey(lockdownHandle, null, "BasebandVersion");
                }
                catch { }
                deviceUniqueChipID = Util.GetLockdowndUlongKey(lockdownHandle, null, "UniqueChipID");
                deviceInfo["Serial Number"] = Util.GetLockdowndStringKey(lockdownHandle, null, "SerialNumber");
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
                catch { }
                deviceInfo["MAC Address"] = Util.GetLockdowndStringKey(lockdownHandle, null, "WiFiAddress").ToUpper();
                deviceInfo["UUID"] = deviceUUID;
                deviceInfo["Activated"] = Util.GetLockdowndStringKey(lockdownHandle, null, "ActivationState");

                deviceTotalDiskCapacity = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalDiskCapacity");
                deviceTotalSystemCapacity = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalSystemCapacity");
                deviceTotalDataCapacity = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalDataCapacity");
                deviceTotalSystemAvailable = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalSystemAvailable");
                deviceTotalDataAvailable = Util.GetLockdowndUlongKey(lockdownHandle, "com.apple.disk_usage", "TotalDataAvailable");

                deviceInfoGroupBox.Header = fws["devices"][deviceInfo["Identifier"]]["name"];
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

                // get apps
                await Task.Run(new Action(GetAppsThread));
                installedAppsListView.ItemsSource = apps;

                // load fs to listbox
                firmwareListView.ItemsSource = fws["devices"][deviceInfo["Identifier"]]["firmwares"];

                // afc
                afcPathTextBox.Text = afcPath;
                try
                {
                    afc.afc_read_directory(afcHandle, afcPath, out ReadOnlyCollection<string> afcDirectory).ThrowOnError();
                    afcListBox.ItemsSource = afcDirectory;
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
                using (Process p = new())
                {
                    p.StartInfo.FileName = "runtimes/win-x86/native/irecovery.exe";
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

                appsTab.Visibility = Visibility.Hidden;
                fileSystemTab.Visibility = Visibility.Hidden;
                jailbreakTab.Visibility = Visibility.Hidden;
                restoreTab.Visibility = Visibility.Hidden;
            }
            else if (dfuConnected)
            {
                deviceInfoGroupBox.Header = "DFU Mode";
                recoveryModeToggleButton.Content = "-------";
                recoveryModeToggleButton.IsEnabled = false;
                powerOffDeviceButton.IsEnabled = false;
                rebootDeviceButton.IsEnabled = false;

                appsTab.Visibility = Visibility.Hidden;
                fileSystemTab.Visibility = Visibility.Hidden;
                jailbreakTab.Visibility = Visibility.Hidden;
                restoreTab.Visibility = Visibility.Hidden;
            }
            if (!recoveryConnected && !normalConnected && !dfuConnected)
            {
                // hide things that wont work without a device
                deviceInfoTab.Visibility = Visibility.Hidden;
                appsTab.Visibility = Visibility.Hidden;
                fileSystemTab.Visibility = Visibility.Hidden;
                jailbreakTab.Visibility = Visibility.Hidden;
                restoreTab.Visibility = Visibility.Hidden;

                mainTabControl.SelectedItem = settingsTab;
            }

            mainTabControl.Visibility = Visibility.Visible;
        }

        private void LoadSettingsToControls()
        {
            themeSettingComboBox.SelectedItem = options.theme;
            fwJsonSourceTextBox.Text = options.fwjsonsource;
            foreach(string s in options.packageManagerRepos)
            {
                repoListBox.Items.Add(s);
            }
            
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

        private void InstallAppThread()
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "runtimes/win-x86/native/ideviceinstaller.exe",
                    Arguments = $"-u {deviceUUID} --install \"{ipaPath}\"",
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
                });
            }
            if (!proc.HasExited) proc.Kill();
        }

        private void GetAppsThread()
        {
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "runtimes/win-x86/native/ideviceinstaller.exe",
                    Arguments = $"-u {deviceUUID} -l",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
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

        private async void refreshFirmwareButton_Click(object sender, RoutedEventArgs e)
        {
            // oh boy hope this doesnt ever go down
            using (HttpClient wc = new())
            {
                fws = JObject.Parse(await wc.GetStringAsync(options.fwjsonsource));
            }
            firmwareListView.ItemsSource = fws["devices"][deviceInfo["Identifier"]]["firmwares"];
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
            if (!selectedFW.signed)
            {
                restoreStatusListBox.Items.Add("This firmware is (probably) not signed, restoring will most likely fail.");
            }
            if (!File.Exists($"{dataLocation}IPSW/{selectedFW.filename}"))
            {
                restoreStatusListBox.Items.Add($"Downloading iOS {selectedFW.version} for {deviceInfo["Identifier"]}");
                using (WebClient wc = new())
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
            MessageBox.Show("Please restart the application.");
        }

        private async void continueWithoutDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            await Init();
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
            Clipboard.SetText(((KeyValuePair<string, string>)deviceInfoListView.SelectedItem).Value);
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

        private async void repoListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (repoListBox.SelectedItem.ToString() == null) return;
            string link = repoListBox.SelectedItem.ToString();
            HttpClient webClient = new();
            // headers because some repos are 'interesting'
            webClient.DefaultRequestHeaders.Add("X-Machine", "iPod4,1");
            webClient.DefaultRequestHeaders.Add("X-Unique-ID", "0000000000000000000000000000000000000000");
            webClient.DefaultRequestHeaders.Add("X-Firmware", "6.1");
            webClient.DefaultRequestHeaders.Add("User-Agent", "Telesphoreo APT-HTTP/1.0.999");
            // Attempt to download packages file (try/catch hell)
            try
            {
                Stream packagesBz2 = await webClient.GetStreamAsync(link + "Packages.bz2");
                FileStream packagesBz2Decompressed = File.Create(dataLocation + "temp/Packages");
                BZip2.Decompress(packagesBz2, packagesBz2Decompressed, true);
            }
            catch (Exception)
            {
                try
                {
                    Stream packagesGz = await webClient.GetStreamAsync(link + "Packages.gz");
                    FileStream packagesGzDecompressed = File.Create(dataLocation + "temp/Packages");
                    GZip.Decompress(packagesGz, packagesGzDecompressed, true);
                }
                catch (Exception)
                {
                    try
                    {
                        using (StreamWriter outputFile = new StreamWriter(dataLocation + "temp/Packages"))
                        {
                            outputFile.WriteLine(await webClient.GetStreamAsync(link + "Packages"));
                        }
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
            foreach (string s in File.ReadAllText(dataLocation + "temp/Packages").Split("\n\n"))
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

        private async void packagesListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
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

            using (HttpClient wc = new())
            {
                using (StreamWriter outputFile = new StreamWriter(saveDebPath))
                {
                    outputFile.WriteLine(await wc.GetStreamAsync(link));
                }
            }
        }

        private void powerOffDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            using (Process p = new())
            {
                p.StartInfo.FileName = "runtimes/win-x86/native/idevicediagnostics.exe";
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
                using (Process p = new())
                {
                    p.StartInfo.FileName = "runtimes/win-x86/native/irecovery.exe";
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
            Process.Start("https://kawaiizenbo.me/abtis.html");
        }

        private void rebootDeviceButton_Click(object sender, RoutedEventArgs e)
        {
            using (Process p = new())
            {
                p.StartInfo.FileName = "runtimes/win-x86/native/idevicediagnostics.exe";
                p.StartInfo.Arguments = "restart";
                p.StartInfo.CreateNoWindow = true;
                p.Start();
                p.WaitForExit();
            }
            MessageBox.Show("Please restart the application.");
        }

        private void Window_ContentRendered(object sender, EventArgs e)
        {
            MinWidth = 1000;
            MinHeight = 700;
        }

        private async void refreshAppListButton_Click(object sender, RoutedEventArgs e)
        {
            await Task.Run(new Action(GetAppsThread));
            installedAppsListView.ItemsSource = apps;
        }

        private void afcGoButton_Click(object sender, RoutedEventArgs e)
        {
            afcPath = afcPathTextBox.Text;
            try
            {
                afc.afc_read_directory(afcHandle, afcPath, out ReadOnlyCollection<string> afcDirectory).ThrowOnError();
                afcListBox.ItemsSource = afcDirectory;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void afcListBox_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if ((string)afcListBox.SelectedItem == ".") return;
            try
            {
                afc.afc_read_directory(afcHandle, afcPath + $"/{afcListBox.SelectedItem}/", out ReadOnlyCollection<string> afcDirectory).ThrowOnError();
                if ((string)afcListBox.SelectedItem == "..")
                {

                }
                afcPath += $"{afcListBox.SelectedItem}/";
                afcListBox.ItemsSource = afcDirectory;
                afcPathTextBox.Text = afcPath;
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

            string afcUploadFile = openAfcUploadFile.FileName;

        }
    }
}
