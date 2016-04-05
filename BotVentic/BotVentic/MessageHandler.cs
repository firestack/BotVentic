using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Threading.Tasks;

namespace BotVentic
{
    class MessageHandler
    {


        private static MessageEventArgs GE;
        private static MessageUpdatedEventArgs GEE;
        public static Dictionary<ulong, Dictionary<string, string>> ChannelDefines = new Dictionary<ulong, Dictionary<string, string>>();
        private static ConcurrentQueue<Message[]> BotReplies = new ConcurrentQueue<Message[]>();
        private static Dictionary<ulong, ulong> LastHandledMessageOnChannel = new Dictionary<ulong, ulong>();

        public static async void HandleIncomingMessage(object client, MessageEventArgs e)
        {
            GE = e;
            if (e != null && e.Message != null && !e.Message.IsAuthor)
            {
                string server = e.Message.Server == null ? "1-1" : e.Message.Server.Name;
                string channel = e.Message.Channel == null ? "NULL" : e.Message.Channel.Name;
                string user = e.Message.User == null ? "?" : e.Message.User.Name;
                string rawtext = e.Message.RawText ?? "";
                Console.WriteLine("[{0}][{3}][Message] {1}: {2}", server, user, rawtext, channel);
                string reply = null;
                string[] words = rawtext.Split(' ');

                // Private message, check for invites
                if (e.Server == null)
                {
                    string[] inviteWords = new string[words.Length];

                    // support legacy "invite [link]" syntax
                    if (words[0] == "invite")
                    {
                        if (words.Length >= 2)
                        {
                            Array.Copy(words, 1, inviteWords, 0, words.Length - 1);
                        }
                        else
                        {
                            await SendReply(client, e.Message, e.Message.Channel.Id, e.Message.Id, "Missing invite link");
                        }
                    }

                    else
                        Array.Copy(words, inviteWords, words.Length);

                    if (inviteWords.Length >= 1 && !inviteWords[0].StartsWith("!"))
                    {
                        try
                        {

                            var invite = await ((DiscordClient) client).GetInvite(inviteWords[0]);
                            await invite.Accept();
                            await SendReply(client, e.Message, e.Message.Channel.Id, e.Message.Id, "Joined!");

                        }
                        catch /*(Exception ex)*/
                        {

                            //Console.WriteLine(ex.ToString());
                            await SendReply(client, e.Message, e.Message.Channel.Id, e.Message.Id, "Failed to join \"" + inviteWords[0] + "\"! Please double-check that the invite is valid and has not expired. If the issue persists, open an issue on the repository. !source for link.");

                        }
                    }
                }

                reply = await HandleCommands(reply, words, e);


                if (reply == null)
                    reply = HandleEmotesAndConversions(reply, words);

                if (!string.IsNullOrWhiteSpace(reply))
                {
                    await SendReply(client, e.Message, e.Message.Channel.Id, e.Message.Id, reply);
                }
            }
        }

        public static async void HandleEdit(object client, MessageUpdatedEventArgs e)
        {

            GEE = e;
            // Don't handle own message or any message containing embeds that was *just* replied to
            if (e != null && e.Before != null && !e.Before.IsAuthor && ((e.Before.Embeds != null && e.Before.Embeds.Length == 0) || !IsMessageLastRepliedTo(e.Before.Channel.Id, e.Before.Id)))
            {
                if (LastHandledMessageOnChannel.ContainsKey(e.Before.Channel.Id))
                    LastHandledMessageOnChannel.Remove(e.Before.Channel.Id);


                bool calcDate = (DateTime.Now - e.Before.Timestamp).Minutes < DiscordBot.EditThreshold;
                string server = e.Before.Server == null ? "1-1" : e.Before.Server.Name;
                string user = e.Before.User == null ? "?" : e.Before.User.Name;
                string rawtext = e.Before.RawText ?? "";
                Console.WriteLine(string.Format("[{0}][Edit] {1}: {2}", server, user, rawtext));

                string reply = null;
                string[] words = rawtext.Split(' ');
                

                reply = await HandleCommands(reply, words, GE);


                if (reply == null)
                {
                    reply = HandleEmotesAndConversions(reply, words);
                }

                if (!string.IsNullOrWhiteSpace(reply) && calcDate)
                {
                    Message botRelation = GetExistingBotReplyOrNull(e.Before.Id);
                    if (botRelation == null)
                    {
                        await SendReply(client, e.After, e.After.Channel.Id, e.After.Id, reply);
                    }
                    else if (botRelation != null)
                    {
                        try
                        {

                            await botRelation.Edit(reply);

                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                        }
                    }
                }
            }
        }


        private static async Task SendReply(object client, Message message, ulong channelId, ulong messageId, string reply)
        {
            try
            {
                LastHandledMessageOnChannel[channelId] = messageId;
                Message x = await ((DiscordClient) client).GetChannel(channelId).SendMessage(reply);
                AddBotReply(x, message);

            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static bool IsMessageLastRepliedTo(ulong channelId, ulong messageId)
        {
            return (LastHandledMessageOnChannel.ContainsKey(channelId) && LastHandledMessageOnChannel[channelId] == messageId);
        }

        private static string HandleEmotesAndConversions(string reply, string[] words)
        {
            for (int i = words.Length - 1; i >= 0; --i)
            {
                string word = words[i];
                bool found = false;
                if (word.StartsWith("#"))
                {
                    string code = word.Substring(1, word.Length - 1);
                    found = IsWordEmote(code, ref reply);
                }
                else if (word.StartsWith(":") && word.EndsWith(":") && word.Length > 2)
                {
                    string code = word.Substring(1, word.Length - 2);
                    found = IsWordEmote(code, ref reply, false);
                }

                //@TODO: Check this later. See what a Null server would be
                else if (GE.Server.Id != null && ChannelDefines.ContainsKey(GE.Server.Id))
                {
                    if (ChannelDefines[GE.Server.Id].ContainsKey(word))
                    {
                        reply = ChannelDefines[GE.Server.Id][word] + (reply == "" ? "" : "\n") + reply;
                    }

                }
                if (found)
                    break;

                switch (word)
                {
                    case "C":
                        if (i >= 1)
                        {
                            int celsius;
                            if (Int32.TryParse(words[i - 1], out celsius))
                            {
                                reply = celsius + " \u00b0C = " + (celsius * 9 / 5 + 32) + " \u00b0F";
                            }
                        }
                        break;
                    case "F":
                        if (i >= 1)
                        {
                            int fahrenheit;
                            if (Int32.TryParse(words[i - 1], out fahrenheit))
                            {
                                reply = fahrenheit + " \u00b0F = " + ((fahrenheit - 32) * 5 / 9) + " \u00b0C";
                            }
                        }
                        break;
                }
            }

            return reply;
        }


        private static bool IsWordEmote(string code, ref string reply, bool caseSensitive = true)
        {
            Func<string, string, bool> emoteComparer = (first, second) => { return caseSensitive ? (first == second) : (first.ToLower() == second.ToLower()); };
            bool found = false;
            string[] emote_info;

            if (DiscordBot.DictEmotes.TryGetValue(code, out emote_info))
            {
                found = true;
                reply = GetEmoteUrl(emote_info);
            }
            else
            {
                foreach (var emote in DiscordBot.DictEmotes.Keys)
                {
                    if (emoteComparer(code, emote))
                    {
                        reply = GetEmoteUrl(DiscordBot.DictEmotes[emote]);
                        found = true;
                        break;
                    }
                }
            }
            return found;
        }

        private static string GetEmoteUrl(string[] emote_info)
        {
            string reply = "";
            switch (emote_info[1])
            {
                case "twitch":
                    reply = "http://emote.3v.fi/2.0/" + emote_info[0] + ".png"; break;
                case "bttv":
                    reply = "https:" + DiscordBot.BttvTemplate.Replace("{{id}}", emote_info[0]).Replace("{{image}}", "2x"); break;
                case "ffz":
                    reply = "https:" + emote_info[0]; break;
            }

            return reply;
        }


        private static async Task<string> HandleCommands(string reply, string[] words, MessageEventArgs e)
        {
            if (words == null || words.Length < 0)
                return "An error occurred.";

            switch (words[0])
            {
                case "!stream":
                    if (words.Length > 1)
                    {
                        string json = await DiscordBot.RequestAsync("https://api.twitch.tv/kraken/streams/" + words[1].ToLower() + "?stream_type=all");
                        if (json != null)
                        {
                            var streams = JsonConvert.DeserializeObject<Json.Streams>(json);
                            if (streams != null)
                            {
                                if (streams.Stream == null)
                                {
                                    reply = "The channel is currently *offline*";
                                }
                                else
                                {
                                    long ticks = DateTime.UtcNow.Ticks - streams.Stream.CreatedAt.Ticks;
                                    TimeSpan ts = new TimeSpan(ticks);
                                    reply = "**[" + NullToEmpty(streams.Stream.Channel.DisplayName) + "]**" + (streams.Stream.Channel.IsPartner ? @"\*" : "") + " " + (streams.Stream.IsPlaylist ? "(Playlist)" : "")
                                        + "\n**Title**: " + NullToEmpty(streams.Stream.Channel.Status).Replace("*", @"\*")
                                        + "\n**Game:** " + NullToEmpty(streams.Stream.Game) + "\n**Viewers**: " + streams.Stream.Viewers
                                        + "\n**Uptime**: " + ts.ToString(@"d' day" + (ts.Days == 1 ? "" : "s") + @" 'hh\:mm\:ss")
                                        + "\n**Quality**: " + streams.Stream.VideoHeight + "p" + Math.Ceiling(streams.Stream.FramesPerSecond);
                                }
                            }
                        }
                    }
                    else
                    {
                        reply = "**Usage:** !stream channel";
                    }
                    break;
                case "!channel":
                    if (words.Length > 1)
                    {
                        string json = await DiscordBot.RequestAsync("https://api.twitch.tv/kraken/channels/" + words[1].ToLower());
                        if (json != null)
                        {
                            var channel = JsonConvert.DeserializeObject<Json.Channel>(json);
                            if (channel != null && channel.DisplayName != null)
                            {
                                reply = "**[" + NullToEmpty(channel.DisplayName) + "]**"
                                    + "\n**Partner**: " + (channel.IsPartner ? "Yes" : "No")
                                    + "\n**Title**: " + NullToEmpty(channel.Status).Replace("*", @"\*")
                                    + "\n**Registered**: " + NullToEmpty(channel.Registered.ToString("yyyy-MM-dd HH:mm")) + " UTC"
                                    + "\n**Followers**: " + channel.Followers;
                            }
                        }
                    }
                    else
                    {
                        reply = "**Usage:** !channel channel";
                    }
                    break;
                case "!source":
                    reply = "https://github.com/firestack/BotVentic";
                    break;
                case "!frozen":
                    if (words.Length >= 2 && words[1] != "pizza")
                        break;
                    // Fall through to frozenpizza
                    goto case "!frozenpizza";
                case "!frozenpizza":
                    reply = "*starts making a frozen pizza*";
                    break;
                case "!update":
                    if (words.Length > 1)
                    {
                        switch (words[1])
                        {
                            case "emotes":
                                await DiscordBot.UpdateAllEmotesAsync();
                                reply = "*updated list of known emotes*";
                                break;
                        }
                    }
                    break;
                case "!joinffz":
                    
                    bool bUserHasBotRole = false;
                    if (e.Channel.IsPrivate || !UserHasRole("BotMaker")) { break;}
                    
                    if (!bUserHasBotRole) { break; }//Leave switch statement
                    int totalEmotes = await DiscordBot.AddFFZEmotes(words.ToList().GetRange(1, words.Length - 1).ToArray());
                    reply = String.Format("({0}) New FFZ Emotes Added", totalEmotes);
                    
                    break;

                case "#define":
                    if (!e.Channel.IsPrivate)
                    {

                        if (!UserHasRole("BotMaker") || words.Length < 3) { break; }//Leave switch statement
                        if (!ChannelDefines.ContainsKey(e.Server.Id))
                        {
                            ChannelDefines[e.Server.Id] = new Dictionary<string, string>();
                        }

                        ChannelDefines[e.Server.Id][words[1]] = String.Join(" ", words.ToList().GetRange(2, words.Length - 2).ToArray());
                        DiscordBot.SaveConfig();
                    }

                    break;
                case "#undef":
                    if (!e.Channel.IsPrivate)
                    {
                        if (!UserHasRole("BotMaker") || words.Length < 1 || !ChannelDefines.ContainsKey(e.Server.Id)) { break; }//Leave switch statement
                        ChannelDefines[e.Server.Id].Remove(words[1]);
                    }

                    break;

                case "#list":
                    if(!e.Channel.IsPrivate && UserHasRole("BotMaker") && ChannelDefines.ContainsKey(e.Server.Id))
                    {
                        reply = "Current Defines: ```";
                        foreach(var kvp in ChannelDefines[e.Server.Id])
                        {
                            reply += String.Format("#{0}:\t{1}\n", kvp.Key, kvp.Value);
                        }
                        reply += "```";
                    }
                    break; 

                case "!listffz":
                    reply = "I am using emotes from these channels: ```" + String.Join(", ", DiscordBot.FFZChannelNames) + "```";
                    break;

                case "#VERSION":
                    reply = "Version " + System.Reflection.Assembly.GetExecutingAssembly().GetName().Version.ToString() + "\nModified by bomb" ;
                    break;

                case "#game":
                    if (!UserHasRole("BotMaker") || words.Length < 1) { break; }//Leave switch statement
                    DiscordBot.Client.SetGame(String.Join(" ",  words.ToList().GetRange(1, words.Length - 1)));

                    break;
            }

            return reply;
        }

        private static void AddBotReply(Message bot, Message user)
        {
            while (BotReplies.Count > DiscordBot.EditMax)
            {
                Message[] dummy;
                BotReplies.TryDequeue(out dummy);
            }
            BotReplies.Enqueue(new Message[] { bot, user });
        }

        private enum MessageIndex
        {
            BotReply,
            UserMessage
        }

        private static Message GetExistingBotReplyOrNull(ulong id)
        {
            foreach (var item in BotReplies)
            {
                if (item[(int)MessageIndex.UserMessage].Id == id)
                {
                    return item[(int)MessageIndex.BotReply];
                }
            }
            return null;
        }

        private static string NullToEmpty(string str)
        {
            return (str == null) ? "" : str;
        }

        private static bool UserHasRole(string RoleName)
        {
            if (GE.Channel.IsPrivate)
                return false;

            bool bRoleFlag = false;
            foreach (var Role in GE.User.Roles)
            {
                if (Role.Name == RoleName)
                {
                    bRoleFlag = true;
                    break;//Leave foreach
                }
            }
            return bRoleFlag;
        }
    }
}
