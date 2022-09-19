using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Security.Permissions;

namespace iSuite
{
    public class DeviceApp
    {
        public string CFBundleIdentifier { get; set; }
        public string CFBundleVersion { get; set; }
        public string CFBundleDisplayName { get; set; }
    }

    public class Firmware
    {
        public string identifier { get; set; }
        public string version { get; set; }
        public string buildid { get; set; }
        public string sha1sum { get; set; }
        public string md5sum { get; set; }
        public long filesize { get; set; }
        public string url { get; set; }
        public DateTime releasedate { get; set; }
        public DateTime uploaddate { get; set; }
        public bool signed { get; set; }
    }

    public class DebPackage
    {
        public string Package { get; set; }
        public string Version { get; set; }
        public string Architecture { get; set; }
        public string Maintainer { get; set; }
        public string Conflicts { get; set; }
        public string Filename { get; set; }
        public string Size { get; set; }
        public string MD5Sum { get; set; }
        public string SHA1 { get; set; }
        public string SHA256 { get; set; }
        public string Section { get; set; }
        public string Description { get; set; }
        public string Author { get; set; }
        public string Name { get; set; }
    }

    // options.json
    public class OptionsJson
    {
        public string IPSWApiSource { get; set; } = "https://api.ipsw.me/v4/";
        public string JailbreakAPISource { get; set; } = "https://api.appledb.dev/";
        public string TempDataLocation { get; set; } = $"{Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)}/iSuite/temp";
        public int ColorScheme { get; set; } = 0;
        public bool DarkMode { get; set; } = false;
        public int Language { get; set; } = 0;
        public List<string> PackageManagerRepos { get; set; } = new List<string>() { "http://repo.kawaiizenbo.me/" };
    }

}
