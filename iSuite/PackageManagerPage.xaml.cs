using ICSharpCode.SharpZipLib.BZip2;
using ICSharpCode.SharpZipLib.GZip;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
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
    public partial class PackageManagerPage : UserControl
    {
        public PackageManagerPage()
        {
            InitializeComponent();
        }

        public void LoadLanguage()
        {
            reposGroupBox.Content = (string)MainWindow.languageTable["repos"];
            addRepoButton.Content = (string)MainWindow.languageTable["addRepo"];
            removeSelectedRepoButton.Content = (string)MainWindow.languageTable["removeSelectedRepo"];
            packagesLVGB.Header = (string)MainWindow.languageTable["packages"];
            packageNameColumn.Header = (string)MainWindow.languageTable["packageName"];
            packageIdColumn.Header = (string)MainWindow.languageTable["packageID"];
            developerColumn.Header = (string)MainWindow.languageTable["developer"];
            versionColumn.Header = (string)MainWindow.languageTable["version"];
        }

        private void addRepoButton_Click(object sender, RoutedEventArgs e)
        {
            string repo = addRepoTextBox.Text;
            repo = repo.Trim();
            if (repo == "" || repo == null) return;
            if (!repo.StartsWith("http://") && !repo.StartsWith("https://")) repo = "http://" + repo;
            if (!repo.EndsWith("/")) repo += "/";
            repoListBox.Items.Add(repo);
            MainWindow.options.PackageManagerRepos.Add(repo);
            File.WriteAllText(MainWindow.dataLocation + "options.json", JsonConvert.SerializeObject(MainWindow.options));
            addRepoTextBox.Text = null;
        }

        private void repoListBox_MouseDoubleClick(object sender, MouseButtonEventArgs e)
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
                FileStream packagesBz2Decompressed = File.Create(MainWindow.dataLocation + "temp/Packages");
                BZip2.Decompress(packagesBz2, packagesBz2Decompressed, true);
            }
            catch (Exception)
            {
                try
                {
                    MemoryStream packagesGz = new MemoryStream(webClient.DownloadData(link + "Packages.bz2"));
                    FileStream packagesGzDecompressed = File.Create(MainWindow.dataLocation + "temp/Packages");
                    GZip.Decompress(packagesGz, packagesGzDecompressed, true);
                }
                catch (Exception)
                {
                    try
                    {
                        webClient.DownloadFile(link + "Packages", MainWindow.dataLocation + "temp/Packages");
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
            foreach (string s in rx.Split(File.ReadAllText(MainWindow.dataLocation + "temp/Packages")))
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
            packages.RemoveAll(IsEmptyPackage);
            packagesListView.ItemsSource = packages;
            packagesLVGB.Header = link;
        }

        private static bool IsEmptyPackage(DebPackage package)
        {
            return package == new DebPackage();
        }

        private void removeSelectedRepoButton_Click(object sender, RoutedEventArgs e)
        {
            repoListBox.Items.Remove(repoListBox.SelectedItem);
        }

        private void packagesListView_MouseDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            var saveDebFile = new Microsoft.Win32.SaveFileDialog
            {
                FileName = $"{((DebPackage)packagesListView.SelectedItem).Package}-{((DebPackage)packagesListView.SelectedItem).Version}.deb",
                DefaultExt = ".deb",
                Filter = "DPKG Packages|*.deb"
            };

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
    }
}
