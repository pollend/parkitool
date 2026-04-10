using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace Parkitool
{
    public class ParkitectConfiguration
    {
        public static ParkitectConfiguration LoadConfiguration(String file)
        {
            return JsonConvert.DeserializeObject<ParkitectConfiguration>(File.ReadAllText(file));
        }

        [JsonProperty(PropertyName = "name")] public String Name { get; set; }

        [JsonProperty(PropertyName = "folder")]
        public String Folder { get; set; }

        [JsonProperty(PropertyName = "version")]
        public String Version { get; set; }

        [JsonProperty(PropertyName = "workshop")]
        public String Workshop { get; set; }

        [JsonProperty(PropertyName = "author")]
        public String Author { get; set; }

        [JsonProperty(PropertyName = "description")]
        public String Description { get; set; }

        [JsonProperty(PropertyName = "preview")]
        public String Preview { get; set; }

        [JsonProperty(PropertyName = "include")]
        public List<String> Include { get; set; }

        [JsonProperty(PropertyName = "assemblies")]
        public List<String> Assemblies { get; set; } = new List<string>();

        [JsonProperty(PropertyName = "additionalAssemblies")]
        public List<String> AdditionalAssemblies { get; set; } = new List<string>();

        [JsonProperty(PropertyName = "assets")]
        public List<String> Assets { get; set; }

        [JsonProperty(PropertyName = "sources")]
        public List<String> Sources { get; set; }

        [JsonProperty(PropertyName = "packages")]
        public Dictionary<String, String> Packages { get; set; } = new Dictionary<String, String>();

    }
}
