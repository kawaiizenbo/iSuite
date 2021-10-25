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

    // options.json
    public class Options
    {
        public bool enableverbosealerts { get; set; }
        public string[] packageManagerRepos { get; set; }
    }
}
