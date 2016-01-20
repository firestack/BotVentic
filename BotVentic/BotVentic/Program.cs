using BotVentic.Json;
using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace BotVentic
{
    class Program
    {
        // DictEmotes <EmoteCode, { emote_id, emote_type }>
        public static ConcurrentDictionary<string, string[]> DictEmotes { get; private set; } = new ConcurrentDictionary<string, string[]>();
        public static string BttvTemplate { get; private set; }


        public static HashSet<string> FFZEmoteSets = new HashSet<string>();
        public static HashSet<string> FFZChannelNames = new HashSet<string>();
        public static string SaveFileName = "./Save.json";

        public static int EditThreshold
        {
            get
            {
                return Config.EditThreshold;
            }
        }
        public static int EditMax
        {
            get
            {
                return Config.EditMax;
            }
        }

        private static DiscordClient Client { get; set; }
        private static Config Config { get; set; }

        private static object _lock = new object();
        private static bool UpdatingEmotes = false;


        static void Main(string[] args)
        {
            FFZEmoteSets.Add("3"); // Global endpoint
            FFZEmoteSets.Add("4330"); // Weird new globals



            Console.WriteLine("Version " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString());

            if (File.Exists("config.json"))
            {
                using (StreamReader sr = new StreamReader("config.json"))
                {
                    Config = JsonConvert.DeserializeObject<Config>(sr.ReadToEnd());
                }

            }
            else
            {
                Console.WriteLine("No config file present!");
                System.Threading.Thread.Sleep(4000);
                return;
            }

            Console.WriteLine("Started!");

            Task emoteUpdate = UpdateAllEmotesAsync();

            Console.WriteLine("Emotes acquired!");

            Client = new DiscordClient(new DiscordClientConfig());

            Client.MessageCreated += MessageHandler.HandleIncomingMessage;
            Client.MessageUpdated += MessageHandler.HandleEdit;
            Client.Disconnected += HandleDisconnect;


            Connect();
            emoteUpdate.Wait();
        }

        private static void HandleDisconnect(object sender, DisconnectedEventArgs e)
        {
            Console.WriteLine("Disconnected... Attempting to reconnect in 2 seconds.");
            Thread.Sleep(2000);
            Connect();
        }

        private static void Connect()
        {
            Client.Run(async () =>

            {
                Console.WriteLine("Connecting...");
                try
                {
                    await Client.Connect(Config.Email, Config.Password);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.ToString());
                    return;
                }
                Console.WriteLine("Connected!");

                
            });
        }

        /// <summary>
        /// Update the list of all emoticons
        /// </summary>
        public static async Task UpdateAllEmotesAsync()
        {
            lock (_lock)
            {
                if (UpdatingEmotes)
                    return;
                else
                    UpdatingEmotes = true;
            }
            DictEmotes.Clear();
            await UpdateFFZEmotes();
            await UpdateBttvEmotes();
            await UpdateTwitchEmotes();
            UpdatingEmotes = false;
        }

        /// <summary>
        /// Update the list of emoticons
        /// </summary>
        private static async Task UpdateTwitchEmotes()
        {

            var emotes = JsonConvert.DeserializeObject<EmoticonImages>(await RequestAsync("https://api.twitch.tv/kraken/chat/emoticon_images"));


            if (emotes == null || emotes.Emotes == null)
            {
                Console.WriteLine("Error loading twitch emotes!");
                return;
            }

            foreach (var em in emotes.Emotes)
            {
                DictEmotes[em.Code] = new string[] { "" + em.Id, "twitch" };
            }
        }

        /// <summary>
        /// Update list of betterttv emoticons
        /// </summary>
        private static async Task UpdateBttvEmotes()
        {

            DictEmotes["(ditto)"] = new string[] { "554da1a289d53f2d12781907", "bttv" };


            var emotes = JsonConvert.DeserializeObject<BttvEmoticonImages>(await RequestAsync("https://api.betterttv.net/2/emotes"));


            if (emotes == null || emotes.Template == null || emotes.Emotes == null)
            {
                Console.WriteLine("Error loading bttv emotes");
                return;
            }

            BttvTemplate = emotes.Template;

            foreach (var em in emotes.Emotes)
            {
                DictEmotes[em.Code] = new string[] { "" + em.Id, "bttv" };
            }
        }


        /// <summary>
        /// Update the list of FrankerFaceZ emoticons
        /// </summary>
        private static async Task UpdateFFZEmotes()
        {

            foreach (var EmoteSetId in FFZEmoteSets)

            {

                var emotes = JsonConvert.DeserializeObject<FFZEmoteiconSet>(await RequestAsync("http://api.frankerfacez.com/v1/set/" + EmoteSetId));

                if (emotes == null || emotes.Set == null)
                {
                    Console.WriteLine("Error loading ffz emotes");
                    return;
                }
                foreach (var emote in emotes.Set.Emotes)
                {
                    if (emote != null)
                    {
                        try
                        {
                            // Find Largest key
                            string LargestKey = "1";
                            foreach(var URL in emote.EmoteLinks)
                            {
                                if (int.Parse(URL.Key) > int.Parse(LargestKey))
                                {
                                    LargestKey = URL.Key;
                                }
                            }
                            //DictEmotes.Add(emote.Code, new string[] { emote.EmoteLinks[LargestKey], "ffz" });
                            DictEmotes[emote.Code] = new string[] { emote.EmoteLinks[LargestKey], "ffz" };
                        }
                        catch { }
                    }
                }

                //foreach (var set in emotes.Set.Emotes)
                //{
                //    if (set != null )
                //    {
                //        foreach (var em in set.Emotes)
                //        {
                //            try
                //            {
                //                DictEmotes.Add(em.Code, new string[] { "" + em.Id, "ffz" });
                //            }
                //            catch { }
                //        }
                //    }
                //}
            }
            
        }

        public static async Task<int> AddFFZEmotes(string[] channels)
        {
            int totalEmotesRequested = 0;
            foreach (var channel in channels)
            {
                Console.WriteLine("Joining FFZ Channel: " + channel);

                string RequestString = await RequestAsync("https://api.frankerfacez.com/v1/_room/" + channel);

                if (RequestString == "") // If it is assume 404
                    continue; // That FFZ Channel didn't exist. try next channel

                // Confirmed channel exists
                FFZChannelNames.Add(channel);

                var id = JsonConvert.DeserializeObject<FFZRoom>(RequestString);

                if (!FFZEmoteSets.Contains(id.Room.Set.ToString()))
                {
                    FFZEmoteSets.Add(id.Room.Set.ToString());
                }
                
                var emotes = JsonConvert.DeserializeObject<FFZEmoteiconSet>(await RequestAsync("http://api.frankerfacez.com/v1/set/" + id.Room.Set.ToString()));
                totalEmotesRequested += emotes.Set.Emotes.Count;
            }
            
            SaveConfig();
            UpdateFFZEmotes();

            return totalEmotesRequested;

        }

        public static void SaveConfig()
        {
            Save SaveData = new Save();
            SaveData.FFZChannelNames = FFZChannelNames;
            SaveData.ChannelDefines = MessageHandler.ChannelDefines;
            SaveData.FFZChannelSetIDs = FFZEmoteSets;

            File.WriteAllText(SaveFileName, JsonConvert.SerializeObject(SaveData));
        }

        public static void LoadConfig()
        {
            if (!File.Exists(SaveFileName)) { return; }
            var SavedData = JsonConvert.DeserializeObject<Save>(File.ReadAllText(SaveFileName));

            FFZChannelNames = SavedData.FFZChannelNames;
            FFZEmoteSets = SavedData.FFZChannelSetIDs;
            MessageHandler.ChannelDefines = SavedData.ChannelDefines;
        }


        /// <summary>
        /// Get URL
        /// </summary>
        /// <param name="uri">URL to request</param>
        /// <returns>Response body</returns>
        public static async Task<string> RequestAsync(string uri)
        {
            WebRequest request = WebRequest.Create(uri);
            // 30 seconds max, mainly because of emotes
            request.Timeout = 15000;

            // Change our user agent string to something more informative
            ((HttpWebRequest) request).UserAgent = "BotVentic/1.0";
            try
            {
                string data;
                using (WebResponse response = await request.GetResponseAsync())
                {
                    using (System.IO.Stream stream = response.GetResponseStream())
                    {
                        System.IO.StreamReader reader = new System.IO.StreamReader(stream);
                        data = reader.ReadToEnd();
                    }
                }
                return data;
            }
            catch (Exception)
            {

                return "";
            }
        }
    }
}
