using iMobileDevice;
using iMobileDevice.Lockdown;
using iMobileDevice.Plist;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace iSuite
{
    public class Util
    {
        public Util()
        {
            NativeLibraries.Load();
        }

        public static string FormatBytes(ulong bytes)
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

        public static string CalculateMD5(string fileName)
        {
            using (var md5 = MD5.Create())
            {
                using (var stream = File.OpenRead(fileName))
                {
                    var hash = md5.ComputeHash(stream);
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
        }

        public static string RemoveIllegalFileNameChars(string input, string replacement = "")
        {
            var regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());
            var r = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)));
            return r.Replace(input, replacement);
        }

        public static string GetLockdowndStringKey(LockdownClientHandle lockdownHandle, string domain, string key)
        {
            ILockdownApi lockdown = LibiMobileDevice.Instance.Lockdown;
            try
            {
                lockdown.lockdownd_get_value(lockdownHandle, domain, key, out PlistHandle temp).ThrowOnError();
                temp.Api.Plist.plist_get_string_val(temp, out string s);
                Debug.WriteLine($"{domain}.{key}");
                return s;
            }
            catch (Exception)
            {
                throw new LockdownException(LockdownError.MissingKey);
            }
        }

        public static ulong GetLockdowndUlongKey(LockdownClientHandle lockdownHandle, string domain, string key)
        {
            ILockdownApi lockdown = LibiMobileDevice.Instance.Lockdown;
            ulong u = 0;
            lockdown.lockdownd_get_value(lockdownHandle, domain, key, out PlistHandle temp).ThrowOnError();
            temp.Api.Plist.plist_get_uint_val(temp, ref u);
            Debug.WriteLine($"{domain}.{key}");
            return u;
        }

        public static BitmapImage BitmapToImageSource(System.Drawing.Bitmap bitmap)
        {
            using (MemoryStream memory = new MemoryStream())
            {
                bitmap.Save(memory, System.Drawing.Imaging.ImageFormat.Bmp);
                memory.Position = 0;
                BitmapImage bitmapimage = new BitmapImage();
                bitmapimage.BeginInit();
                bitmapimage.StreamSource = memory;
                bitmapimage.CacheOption = BitmapCacheOption.OnLoad;
                bitmapimage.EndInit();
                return bitmapimage;
            }
        }
    }
}
