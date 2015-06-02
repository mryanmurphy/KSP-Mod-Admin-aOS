using System.Collections.Generic;
using Newtonsoft.Json;

namespace KSPMODAdmin.Core.Utils.Ckan
{
    public class CkanInstallInfo
    {
        public string file = string.Empty;
        public string find = string.Empty;
        public string install_to = string.Empty;
        [JsonConverter(typeof(JsonSingleOrArrayConverter<string>))]
        public List<string> filter = new List<string>();
        [JsonConverter(typeof(JsonSingleOrArrayConverter<string>))]
        public List<string> filter_regexp = new List<string>();
    }
}