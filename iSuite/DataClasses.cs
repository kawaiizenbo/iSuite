using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace iSuite
{
    public class DeviceApp
    {
        public string CFBundleIdentifier { get; set; }
        public string CFBundleVersion { get; set; }
        public string CFBundleDisplayName { get; set; }
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

    public class Firmware
    {
        public string version { get; set; }
        public string buildid { get; set; }
        public string sha1sum { get; set; }
        public string md5sum { get; set; }
        public object size { get; set; }
        public DateTime releasedate { get; set; }
        public DateTime uploaddate { get; set; }
        public string url { get; set; }
        public bool signed { get; set; }
        public string filename { get; set; }
    }

    // options.json
    public class OptionsJson
    {
        public string theme { get; set; } = "Teal";
        public string fwjsonsource { get; set; } = "https://api.ipsw.me/v2.1/firmwares.json/condensed";
        public List<string> packageManagerRepos { get; set; } 
    }

    // jailbreak.json (why does this exist???)
    public class JailbreakJson
    {
        public Jailbreak[] jailbreaks { get; set; }
    }

    public class Jailbreak
    {
        public string name { get; set; }
        public string authors { get; set; }
        public string displayCompat { get; set; }
        public string[] internalCompat { get; set; }
        public string type { get; set; }
        public string site { get; set; }
    }
}
