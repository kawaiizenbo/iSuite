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

    // options.json
    public class OptionsJson
    {
        public string fwjsonsource { get; set; } = "https://api.appledb.dev/main.json";
        public List<string> packageManagerRepos { get; set; } 
    }

}
