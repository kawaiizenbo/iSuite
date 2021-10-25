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
    public class Options
    {
        public string theme { get; set; }
        public string[] packageManagerRepos { get; set; }
    }
}
