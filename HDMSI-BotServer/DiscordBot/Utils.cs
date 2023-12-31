﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using Newtonsoft.Json.Linq;

using NLog;
using NLog.Web;

namespace MultiServerIntegrateBot.DiscordBot
{
    public static class Utils
    {
        /// <summary>
        /// ロガー
        /// </summary>
        private static Logger logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();

        // public resources
        public static readonly Color color_info = new Color(0xf0ff40);

        public static readonly string link_botRepository = "https://github.com/BinkanSalaryman/Discord-Audio-Stream-Bot";
        public static readonly string link_discordApplications = "https://discordapp.com/developers/applications/me";
        public static readonly string link_discordDeveloperMode = "https://support.discordapp.com/hc/en-us/articles/206346498-Where-can-I-find-my-User-Server-Message-ID-";
        public static readonly string link_discordAuthorize = "https://discordapp.com/oauth2/authorize?client_id={0}&scope=bot&permissions=0";
        public static readonly string link_twitchDummyStream = "http://twitch.tv/0";

        // private use
        private const int logMessagePad = 18;
        private static readonly string logNewLine = "\n" + string.Format("{0,-" + logMessagePad + "}", "");
        private static readonly string logExceptionStart;
        private static readonly string logExceptionEnd;

        /// <summary>
        /// コンストラクター
        /// </summary>
        static Utils()
        {
            StringBuilder sb = new StringBuilder();

            {
                string field = " Cause ";
                float pad = (logMessagePad - field.Length) / 2f;

                sb.Clear();
                for (int i = 0; i < Math.Ceiling(pad); i++)
                {
                    sb.Append('-');
                }
                sb.Append(field);
                for (int i = 0; i < Math.Floor(pad); i++)
                {
                    sb.Append('-');
                }
                logExceptionStart = sb.ToString();
            }

            {
                sb.Clear();
                for (int i = 0; i < logMessagePad; i++)
                {
                    sb.Append('-');
                }
                logExceptionEnd = sb.ToString();
            }
        }

        /// <summary>
        /// GUIDの検索
        /// </summary>
        /// <param name="discord">BOTクライアント</param>
        /// <param name="text">GUID</param>
        /// <returns></returns>
        public static IEnumerable<SocketGuild> ParseGuild(DiscordSocketClient discord, string text)
        {
            ulong guildId;
            if (ulong.TryParse(text, out guildId))
            {
                return new[] { discord.GetGuild(guildId) };
            }
            return discord.Guilds.Where(g => g.Name == text);
        }

        /// <summary>
        /// ユーザーの検索（ユーザーID）
        /// </summary>
        /// <param name="discord">BOTクライアント</param>
        /// <param name="text">ユーザーID</param>
        /// <returns></returns>
        public static SocketUser ParseUser(DiscordSocketClient discord, string text)
        {
            ulong userId;
            if (MentionUtils.TryParseUser(text, out userId))
            {
                return discord.GetUser(userId);
            }
            int discriminatorIdx = text.IndexOf('#');
            if (text.StartsWith("@") && discriminatorIdx >= 0)
            {
                string username = text.Substring(1, discriminatorIdx - 1);
                string discriminator = text.Substring(discriminatorIdx + 1);
                return discord.GetUser(username, discriminator);
            }
            if (ulong.TryParse(text, out userId))
            {
                return discord.GetUser(userId);
            }
            return null;
        }

        /// <summary>
        /// ユーザーの検索（ロールID）
        /// </summary>
        /// <param name="discord">BOTクライアント</param>
        /// <param name="text">ロールID</param>
        /// <param name="guild">GUID</param>
        /// <returns></returns>
        public static IEnumerable<SocketUser> ParseUsers(DiscordSocketClient discord, string text, SocketGuild guild)
        {
            if (guild != null)
            {
                ulong roleId;
                if (MentionUtils.TryParseRole(text, out roleId))
                {
                    return guild.Users.Where(u => u.Roles.Any(r => r.Id == roleId));
                }
            }
            ulong channelId;
            if (MentionUtils.TryParseChannel(text, out channelId))
            {
                return discord.GetChannel(channelId).Users;
            }
            var user = ParseUser(discord, text);
            return user != null ? new[] { user } : null;
        }

        /// <summary>
        /// ロール検索
        /// </summary>
        /// <param name="guild">GUID</param>
        /// <param name="text">ロール名</param>
        /// <returns></returns>
        public static SocketRole ParseRole(SocketGuild guild, string text)
        {
            ulong roleId;
            if (MentionUtils.TryParseRole(text, out roleId))
            {
                return guild.GetRole(roleId);
            }
            return null;
        }

        /// <summary>
        /// ユーザーステータスの変換
        /// </summary>
        /// <param name="status">ステータス名</param>
        /// <returns></returns>
        public static UserStatus? ParseUserStatus(string status)
        {
            switch (status.ToLowerInvariant())
            {
                case "online":
                    return UserStatus.Online;
                case "idle":
                    return UserStatus.Idle;
                case "afk":
                    return UserStatus.AFK;
                case "dnd":
                    return UserStatus.DoNotDisturb;
                case "invisible":
                    return UserStatus.Invisible;
                case "offline":
                    return UserStatus.Offline;
                default:
                    return null;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="severity"></param>
        /// <param name="source"></param>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(LogSeverity severity, Type source, string message, Exception ex = null)
        {
            Log(severity, source.Name, message, ex);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="severity"></param>
        /// <param name="source"></param>
        /// <param name="message"></param>
        /// <param name="ex"></param>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static void Log(LogSeverity severity, string source, string message, Exception ex = null)
        {
            Log(new LogMessage(severity, source, message, ex));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        public static void Log(LogMessage message)
        {
#if !DEBUG
            if(message.Severity == LogSeverity.Debug) {
                return;
            }
#endif
            var fg = Console.ForegroundColor;

            // message
            Console.ForegroundColor = getLogForeground(LogSeverity.Verbose);
            Console.Write("{0,-8} {1,-8} ",
                DateTime.Now.ToLocalTime().ToLongTimeString(),
                message.Source
            );
            logger.Debug(string.Format("{0,-8} {1,-8} ",
                DateTime.Now.ToLocalTime().ToLongTimeString(),
                message.Source));
            Console.ForegroundColor = getLogForeground(message.Severity);
            Console.WriteLine(message.Message.Replace("\n", logNewLine));
            logger.Debug(message.Message.Replace("\n", logNewLine));

            // exception
            if (message.Exception != null)
            {
                Console.ForegroundColor = getLogForeground(LogSeverity.Verbose);
                Console.WriteLine(logExceptionStart);
                logger.Debug(logExceptionStart);
                Console.ForegroundColor = getLogForeground(LogSeverity.Error);
                Console.WriteLine(message.Exception);
                logger.Error(message.Exception);
                Console.ForegroundColor = getLogForeground(LogSeverity.Verbose);
                Console.WriteLine(logExceptionEnd);
                logger.Debug(logExceptionEnd);
            }

            Console.ForegroundColor = fg;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="severity"></param>
        /// <returns></returns>
        /// <exception cref="InvalidOperationException"></exception>
        private static ConsoleColor getLogForeground(LogSeverity severity)
        {
            switch (severity)
            {
                case LogSeverity.Debug:
                    return ConsoleColor.DarkGray;
                case LogSeverity.Verbose:
                    return ConsoleColor.Gray;
                case LogSeverity.Info:
                    return ConsoleColor.White;
                case LogSeverity.Warning:
                    return ConsoleColor.Yellow;
                case LogSeverity.Error:
                    return ConsoleColor.Red;
                case LogSeverity.Critical:
                    return ConsoleColor.Magenta;
                default:
                    throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        /// <param name="access"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static unsafe UnmanagedMemoryStream OpenBuffer(IntPtr buffer, int length, FileAccess access)
        {
            return new UnmanagedMemoryStream((byte*)buffer, length, length, access);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="buffer"></param>
        /// <param name="index"></param>
        /// <param name="littleEndian"></param>
        /// <returns></returns>
        public static short GetInt16(byte[] buffer, int index, bool littleEndian)
        {
            short result = 0;
            if (littleEndian)
            {
                for (int i = 0; i < sizeof(short); i++)
                {
                    result |= (short)(buffer[index + i] << (i * 8));
                }
            }
            else
            {
                for (int i = 0; i < sizeof(short); i++)
                {
                    result |= (short)(buffer[index + i] << (sizeof(short) - i * 8));
                }
            }
            return result;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="value"></param>
        /// <param name="buffer"></param>
        /// <param name="index"></param>
        /// <param name="littleEndian"></param>
        public static void GetBytes(short value, byte[] buffer, int index, bool littleEndian)
        {
            if (littleEndian)
            {
                for (int i = 0; i < sizeof(short); i++)
                {
                    buffer[index + i] = (byte)((value >> (i * 8)) & 0xFF);
                }
            }
            else
            {
                for (int i = 0; i < sizeof(short); i++)
                {
                    buffer[index + i] = (byte)((value >> (sizeof(short) - i * 8)) & 0xFF);
                }
            }
        }
    }
}
