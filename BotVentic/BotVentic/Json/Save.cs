using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;

namespace BotVentic.Json
{
    class Save
    {
        [JsonProperty("FFZChannelNames")]
        public HashSet<String> FFZChannelNames { get; set; }

        [JsonProperty("FFZChannelIds")]
        public HashSet<String> FFZChannelSetIDs { get; set; }

        [JsonProperty("ChannelDefine")]
        public Dictionary<string, Dictionary<string, string>> ChannelDefines { get; set; }
    }
}
