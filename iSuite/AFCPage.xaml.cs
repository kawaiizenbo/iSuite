using iMobileDevice.Afc;
using iMobileDevice.iDevice;
using iMobileDevice.Lockdown;

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace iSuite
{
    public partial class AFCPage : UserControl
    {
        public string afcPath = "/";
        public AFCPage()
        {
            InitializeComponent();
        }

        public void LoadLanguage()
        {
            afcGoButton.Content = MainWindow.languageTable["go"];
            afcRefreshButton.Content = MainWindow.languageTable["refresh"];
            afcUploadFileButton.Content = MainWindow.languageTable["uploadFile"];
            afcMKDirButton.Content = MainWindow.languageTable["mkdir"];
            afcDownloadFileButton.Content = MainWindow.languageTable["downloadFile"];
            afcDeleteSelectedButton.Content = MainWindow.languageTable["deleteSelected"];
            afcConnectAfc2Button.Content = MainWindow.languageTable["connectAFC2"];
        }

        public void Init()
        {
            afcPathTextBox.Text = afcPath;
            try
            {
                MainWindow.afc.afc_read_directory(MainWindow.afcHandle, afcPath, out ReadOnlyCollection<string> afcDirectory).ThrowOnError();
                afcItemsListBox.ItemsSource = afcDirectory;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void afcGoButton_Click(object sender, RoutedEventArgs e)
        {
            afcPath = afcPathTextBox.Text;
            try
            {
                MainWindow.afc.afc_read_directory(MainWindow.afcHandle, afcPath, out ReadOnlyCollection<string> afcDirectory).ThrowOnError();
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
                MainWindow.afc.afc_read_directory(MainWindow.afcHandle, afcPath + $"/{afcItemsListBox.SelectedItem}/", out ReadOnlyCollection<string> afcDirectory).ThrowOnError();
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
            MainWindow.afc.afc_file_open(MainWindow.afcHandle, afcPath + "/" + afcUploadFileName, AfcFileMode.FopenRw, ref handle);
            byte[] array = File.ReadAllBytes(afcUploadFilePath);
            uint bytesWritten = 0U;
            MainWindow.afc.afc_file_write(MainWindow.afcHandle, handle, array, (uint)array.Length, ref bytesWritten);
            MainWindow.afc.afc_file_close(MainWindow.afcHandle, handle);
            afcRefreshButton_Click(sender, e);
        }

        private void afcRefreshButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MainWindow.afc.afc_read_directory(MainWindow.afcHandle, afcPath, out ReadOnlyCollection<string> afcDirectory).ThrowOnError();
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
            f.Title = (string)MainWindow.languageTable["makeDirectory"];
            f.LabelText = (string)MainWindow.languageTable["enterNewDirectoryName"];
            f.ShowDialog();
            MainWindow.afc.afc_make_directory(MainWindow.afcHandle, afcPath + "/" + f.TextBoxContents);
            afcRefreshButton_Click(sender, e);
        }

        private void afcConnectAfc2Button_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // idk this causes issues sometimes blame libusbmuxd
                MainWindow.lockdown.lockdownd_start_service(MainWindow.lockdownHandle, "com.apple.afc2", out MainWindow.lockdownServiceHandle).ThrowOnError();
                MainWindow.lockdownHandle.Api.Afc.afc_client_new(MainWindow.deviceHandle, MainWindow.lockdownServiceHandle, out MainWindow.afcHandle).ThrowOnError();
                afcPath = "/";
                afcPathTextBox.Text = afcPath;
                afcRefreshButton_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, (string)MainWindow.languageTable["afc2ConnectFailed"]);
            }
        }

        private void afcDeleteSelectedButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                MainWindow.afc.afc_remove_path(MainWindow.afcHandle, afcPath + $"/{afcItemsListBox.SelectedItem}").ThrowOnError();
                afcRefreshButton_Click(sender, e);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, (string)MainWindow.languageTable["failedToDelete"]);
            }
        }

        private void afcDownloadFileButton_Click(object sender, RoutedEventArgs e)
        {
            var saveAfcFile = new Microsoft.Win32.SaveFileDialog
            {
                FileName = afcItemsListBox.SelectedItem.ToString()
            };

            saveAfcFile.ShowDialog();

            string afcSaveFilePath = saveAfcFile.FileName;
            string afcFilePath = afcPath + "/" + afcItemsListBox.SelectedItem.ToString();
            MainWindow.afc.afc_get_file_info(MainWindow.afcHandle, afcFilePath, out ReadOnlyCollection<string> infoListr);
            List<string> infoList = new List<string>(infoListr.ToArray());
            long fileSize = Convert.ToInt64(infoList[infoList.FindIndex(x => x == "st_size") + 1]);

            ulong fileHandle = 0;
            MainWindow.afc.afc_file_open(MainWindow.afcHandle, afcFilePath, AfcFileMode.FopenRdonly, ref fileHandle);

            FileStream fileStream = File.Create(afcSaveFilePath);
            const int bufferSize = 4194304;
            for (int i = 0; i < fileSize / bufferSize + 1; i++)
            {
                uint bytesRead = 0;

                long remainder = fileSize - i * bufferSize;
                int currBufferSize = remainder >= bufferSize ? bufferSize : (int)remainder;
                byte[] currBuffer = new byte[currBufferSize];

                if ((MainWindow.afc.afc_file_read(MainWindow.afcHandle, fileHandle, currBuffer, Convert.ToUInt32(currBufferSize), ref bytesRead))
                    != AfcError.Success)
                {
                    MainWindow.afc.afc_file_close(MainWindow.afcHandle, fileHandle);
                }

                fileStream.Write(currBuffer, 0, currBufferSize);
            }

            fileStream.Close();
        }
    }
}
