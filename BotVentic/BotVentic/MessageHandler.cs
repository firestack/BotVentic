﻿using Discord;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;

namespace BotVentic
{
    class MessageHandler
    {
        private static Dictionary<Message, Message> BotReplies = new Dictionary<Message, Message>();
        private static Dictionary<string, string> LastHandledMessageOnChannel = new Dictionary<string, string>();

        public static async void HandleIncomingMessage(object client, MessageEventArgs e)
        {
            
            if (e != null && e.Message != null && !e.Message.IsAuthor)
            {
                string server = e.Message.Server == null ? "1-1" : e.Message.Server.Name;
                string user = e.Message.User == null ? "?" : e.Message.User.Name;
                Console.WriteLine("[{0}][Message] {1}: {2}", server, user, e.Message.RawText);
                string reply = null;
                string[] words = e.Message.RawText.Split(' ');

                if (words[0] == "invite" && words.Length >= 2)
                {
                    try
                    {
                        await ((DiscordClient)client).AcceptInvite(words[1]);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.ToString());
                    }
                }

                reply = HandleCommands(reply, words, e);

                if (reply == null)
                    reply = HandleEmotesAndConversions(reply, words);

                if (!String.IsNullOrWhiteSpace(reply))
                {
                    LastHandledMessageOnChannel[e.Message.ChannelId] = e.MessageId;
                    await SendReply(client, e, reply);
                }
            }
        }

        public static async void HandleEdit(object client, MessageEventArgs e)
        {
            // Don't handle own message or any message containing embeds that was *just* replied to
            if (e != null && e.Message != null && !e.Message.IsAuthor && (e.Message.Embeds.Length == 0 || !IsMessageLastRepliedTo(e)))
            {
                if (LastHandledMessageOnChannel.ContainsKey(e.Message.ChannelId))
                    LastHandledMessageOnChannel.Remove(e.Message.ChannelId);

                bool calcDate = (DateTime.Now - e.Message.Timestamp).Minutes < Program.EditThreshold;
                string server = e.Message.Server == null ? "1-1" : e.Message.Server.Name;
                string user = e.Message.User == null ? "?" : e.Message.User.Name;
                Console.WriteLine(String.Format("[{0}][Edit] {1}: {2}", server, user, e.Message.RawText));
                string reply = null;
                string[] words = e.Message.RawText.Split(' ');

                reply = HandleCommands(reply, words, e);

                if (reply == null)
                {
                    reply = HandleEmotesAndConversions(reply, words);
                }

                if (!String.IsNullOrWhiteSpace(reply) && calcDate)
                {
                    Message botRelation = GetExistingBotReplyOrNull(e.Message.Id);
                    if (botRelation == null)
                    {
                        await SendReply(client, e, reply);
                    }
                    else if (botRelation != null)
                    {
                        try
                        {
                            await ((DiscordClient)client).EditMessage(botRelation, text: reply);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.ToString());
                        }
                    }
                }
            }
        }

        private static async System.Threading.Tasks.Task SendReply(object client, MessageEventArgs e, string reply)
        {
            try
            {
                Message[] x = await ((DiscordClient)client).SendMessage(e.Message.ChannelId, reply);
                AddBotReply(x[0], e.Message);
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
            }
        }

        private static bool IsMessageLastRepliedTo(MessageEventArgs e)
        {
            return (LastHandledMessageOnChannel.ContainsKey(e.Message.ChannelId) && LastHandledMessageOnChannel[e.Message.ChannelId] == e.MessageId);
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

            if (Program.DictEmotes.TryGetValue(code, out emote_info))
            {
                found = true;
                reply = GetEmoteUrl(emote_info);
            }
            else
            {
                foreach (var emote in Program.DictEmotes.Keys)
                {
                    if (emoteComparer(code, emote))
                    {
                        reply = GetEmoteUrl(Program.DictEmotes[emote]);
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
                    reply = "https:" + Program.BttvTemplate.Replace("{{id}}", emote_info[0]).Replace("{{image}}", "2x"); break;
                case "ffz":
                    reply = "https:" + emote_info[0]; break;
            }

            return reply;
        }

        private static string HandleCommands(string reply, string[] words, MessageEventArgs e)
        {
            if (words == null || words.Length < 0)
                return "An error occurred.";

            switch (words[0])
            {
                case "!stream":
                    if (words.Length > 1)
                    {
                        string json = Program.Request("https://api.twitch.tv/kraken/streams/" + words[1].ToLower() + "?stream_type=all");
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
                        string json = Program.Request("https://api.twitch.tv/kraken/channels/" + words[1].ToLower());
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
                    reply = "https://github.com/3ventic/BotVentic";
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
                                Program.UpdateAllEmotes();
                                reply = "*updated list of known emotes*";
                                break;
                        }
                    }
                    break;
                case "!joinffz":
                    
                    bool bUserHasBotRole = false;
                    if (!e.Channel.IsPrivate)
                    {
                        foreach (var Role in e.Member.Roles)
                        {
                            if (Role.Name == "BotMaker")
                            {
                                bUserHasBotRole = true;
                                break;//Leave foreach
                            }
                        }

                        if (!bUserHasBotRole) { break; }//Leave switch statment
                        int totalEmotes = Program.AddFFZEmotes(words.ToList().GetRange(1, words.Length - 1).ToArray());
                        reply = String.Format("({0}) New FFZ Emotes Added", totalEmotes);
                    }
                    
                    break;
            }

            return reply;
        }

        private static void AddBotReply(Message bot, Message user)
        {
            if (BotReplies.Count > Program.EditMax)
            {
                BotReplies.Remove(BotReplies.Keys.ElementAt(0));
            }
            BotReplies.Add(bot, user);
        }

        private static Message GetExistingBotReplyOrNull(string id)
        {
            foreach (KeyValuePair<Message, Message> item in BotReplies)
            {
                if (item.Value.Id == id)
                {
                    return item.Key;
                }
            }
            return null;
        }

        private static string NullToEmpty(string str)
        {
            return (str == null) ? "" : str;
        }
    }
}
