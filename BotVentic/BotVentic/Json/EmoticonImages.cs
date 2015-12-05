using System.Collections.Generic;
using Newtonsoft.Json;

namespace BotVentic.Json
{
    class Emoticon
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }
    }

    class EmoticonImages
    {
        [JsonProperty("emoticons")]
        public List<Emoticon> Emotes { get; set; }
    }

    class BttvEmoticon
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("code")]
        public string Code { get; set; }
    }

    class BttvEmoticonImages
    {
        [JsonProperty("emotes")]
        public List<BttvEmoticon> Emotes { get; set; }

        [JsonProperty("urlTemplate")]
        public string Template { get; set; }
    }
    
    class FFZLinks
    {
        [JsonProperty("next")]
        public string Next { get; set; }
    }

    class FFZEmoticon
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("urls")]
        public Dictionary<string, string> EmoteLinks { get; set; }

        [JsonProperty("name")]
        public string Code { get; set; }
    }

    class FFZEmoticonImages
    {
        [JsonProperty("_links")]
        public FFZLinks Links { get; set; }

        [JsonProperty("emoticons")]
        public List<FFZEmoticon> Emotes { get; set; }
    }

    class FFZEmoticonSetsAPIGLOBALENDPOINT
    {
        [JsonProperty("sets")]
        public Dictionary<string, FFZEmoticonImages> Sets { get; set; }
    }

    class FFZEmoteiconSet
    {
        [JsonProperty("set")]
        public FFZEmoticonImages Set { get; set; }

        public int status;
    }

    class FFZRoomData
    {
        [JsonProperty("set")]
        public int Set { get; set; }
    }

    class FFZRoom
    {
        [JsonProperty("room")]
        public FFZRoomData Room { get; set; }
    }

}
