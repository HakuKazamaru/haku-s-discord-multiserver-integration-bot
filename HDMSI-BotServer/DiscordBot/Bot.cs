using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Resources;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.Net;
using Discord.Rest;
using Discord.WebSocket;

using ManagedBass;

using NLog;
using NLog.Fluent;
using NLog.Web;

using Windows.ApplicationModel;

using MultiServerIntegrateBot.Common;
using MultiServerIntegrateBot.Model;
using MultiServerIntegrateBot.Enum;
using Microsoft.Extensions.DependencyInjection;
using System.Globalization;
using Discord.Audio;

namespace MultiServerIntegrateBot.DiscordBot
{
    public class Bot : IDisposable
    {
        /// <summary>
        /// ロガー
        /// </summary>
        private static Logger logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();

        /// <summary>
        /// 設定管オブジェクト
        /// </summary>
        public Config Config { get; private set; }

        /// <summary>
        /// BOTエージェント
        /// </summary>
        public Agent BotAgent { get; private set; }

        /// <summary>
        /// DiscordBotクライアントオブジェクト
        /// </summary>
        public DiscordSocketClient Discord { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public RestApplication AppInfo { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        public Assembly CommandsAssembly { get; private set; }


        /// <summary>
        /// 設定ファイルパス
        /// </summary>
        private string configPath;
        /// <summary>
        /// エージェントパス
        /// </summary>
        private string agentsPath;
        /// <summary>
        /// コマンドパス
        /// </summary>
        private string commandsPath;

        /// <summary>
        /// BOTコマンド受付用
        /// </summary>
        private CommandService commandService;
        /// <summary>
        /// 
        /// </summary>
        private WinApi.HandlerRoutine consoleCtrlHandler;

        #region ボイスチャット関連
        #region 定数関連
        /// <summary>
        /// 音声サンプリングレート
        /// </summary>
        public const int SAMPLE_RATE = 48000;
        /// <summary>
        /// サンプルサイズ
        /// </summary>
        public const int SAMPLE_SIZE = sizeof(short);
        /// <summary>
        /// 音声チャンネル数
        /// </summary>
        public const int CHANNEL_COUNT = 2;
        /// <summary>
        /// フレームバッファー
        /// </summary>
        public const int FRAME_SAMPLES = 20 * (SAMPLE_RATE / 1000);
        #endregion
        /// <summary>
        /// 
        /// </summary>
        private ConcurrentDictionary<ulong, VoiceSet> voiceSets = new ConcurrentDictionary<ulong, VoiceSet>();

        /// <summary>
        /// 
        /// </summary>
        private readonly object record_lock = new object();
        /// <summary>
        /// 
        /// </summary>
        private int recordDevice;
        /// <summary>
        /// 
        /// </summary>
        private int recordChannel;
        /// <summary>
        /// 
        /// </summary>
        private RecordProcedure recordProc;

        /// <summary>
        /// 
        /// </summary>
        private readonly object playback_lock = new object();
        /// <summary>
        /// 
        /// </summary>
        private int playbackDevice;
        /// <summary>
        /// 
        /// </summary>
        private int playbackChannel;
        /// <summary>
        /// 
        /// </summary>
        private StreamProcedure playProc;
        /// <summary>
        /// 
        /// </summary>
        private bool firstPlayProcCall;
        /// <summary>
        /// 
        /// </summary>
        private MemoryQueue playbackQueue;
        #endregion

        /// <summary>
        /// 
        /// </summary>
        public static IServiceProvider Services { get; private set; }
        /// <summary>
        /// 
        /// </summary>
        private static IServiceCollection _services = new ServiceCollection();

        /// <summary>
        /// コンストラクター
        /// </summary>
        public Bot()
        {
            AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
            CultureInfo.DefaultThreadCurrentCulture = CultureInfo.InvariantCulture;
            CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;
        }

        /// <summary>
        /// BOT初期化処理
        /// </summary>
        /// <param name="configPath">設定ファイルパス</param>
        /// <param name="agentsPath">エージェントパス</param>
        /// <param name="commandsPath">コマンドパス</param>
        /// <returns></returns>
        public async Task<bool> Init(string configPath, string agentsPath, string commandsPath)
        {
            logger.Info("========== Start! ==================================================");

            this.configPath = configPath;
            this.agentsPath = agentsPath;
            this.commandsPath = commandsPath;

            // Discordクライアントの生成
            Discord = new DiscordSocketClient();

            // 各種イベントハンドの設定
            Discord.JoinedGuild += Discord_JoinedGuild;
            Discord.LeftGuild += Discord_LeftGuild;
            Discord.MessageReceived += Discord_MessageReceived;
            Discord.Log += Discord_Log;
            Discord.Ready += Discord_Ready;

            commandService = new CommandService();
            commandService.Log += Discord_Log;

            try
            {
                CommandsAssembly = Assembly.LoadFile(Path.GetFullPath(commandsPath));
                await commandService.AddModulesAsync(CommandsAssembly, Services);
            }
            catch (Exception ex)
            {
                Discord_Log(LogSeverity.Error, "Failed to load commands assembly at \"" + commandsPath + "\"", ex);
                return false;
            }

            // install commands
            ModifyServices(x => x
                .AddSingleton(Discord)
                .AddSingleton(commandService)
            );

            // load config
            if (!ReloadConfig())
            {
                return false;
            }

            // export embedded bot agent as reference
            if (!Directory.Exists(agentsPath))
            {
                Directory.CreateDirectory(agentsPath);
            }
            File.Copy(Path.Combine(agentsPath, "tomoko.json"), Path.Combine(Path.GetDirectoryName(commandsPath), @"Resources\tomoko.json"));

            // load agent
            if (!ReloadAgent())
            {
                return false;
            }

            // capture console exit
            consoleCtrlHandler = new WinApi.HandlerRoutine(consoleCtrl);
            WinApi.SetConsoleCtrlHandler(consoleCtrlHandler, true);

            // init record device
            if (Config.speakEnabled)
            {
                recordDevice = -1;
                if (Config.speakRecordingDevice != null)
                {
                    int i = 0;
                    bool result = true;
                    List<DeviceInfo> devices = new List<DeviceInfo>();

                    while (result)
                    {
                        ManagedBass.DeviceInfo deviceInfo;
                        result = Bass.GetDeviceInfo(i, out deviceInfo);
                        if (result) { devices.Add(deviceInfo); }
                        i++;
                    }

                    for (int j = 0; j < devices.Count; j++)
                    {
                        if (devices[j].Name == Config.speakRecordingDevice)
                        {
                            recordDevice = j;
                            break;
                        }
                    }
                    if (recordDevice < 0)
                    {
                        IEnumerable<string> devicesList = devices.Select(d => d.Name);
                        Discord_Log(LogSeverity.Error, "Recording device \"" + Config.speakRecordingDevice + "\" not found.\nAvailable recording devices:\n * " + string.Join("\n * ", devicesList) + "\n");
                        return false;
                    }
                }

                if (!Bass.RecordInit(recordDevice))
                {
                    Discord_Log(LogSeverity.Error, "Failed to init recording device: " + Bass.LastError.ToString());
                    return false;
                }
                recordProc = new RecordProcedure(recordDevice_audioReceived);
            }

            // init playback device
            if (Config.listenEnabled)
            {
                playbackDevice = -1;
                if (Config.listenPlaybackDevice != null)
                {

                    int i = 0;
                    bool result = true;
                    List<DeviceInfo> devices = new List<DeviceInfo>();

                    while (result)
                    {
                        ManagedBass.DeviceInfo deviceInfo;
                        result = Bass.GetDeviceInfo(i, out deviceInfo);
                        if (result) { devices.Add(deviceInfo); }
                        i++;
                    }

                    for (int j = 0; j < devices.Count; i++)
                    {
                        if (devices[j].Name == Config.listenPlaybackDevice)
                        {
                            playbackDevice = j;
                            break;
                        }
                    }
                    if (playbackDevice < 0)
                    {
                        IEnumerable<string> devicesList = devices.Select(d => d.Name);
                        Discord_Log(LogSeverity.Error, "Playback device \"" + Config.listenPlaybackDevice + "\" not found.\nAvailable playback devices:\n * " + string.Join("\n * ", devicesList) + "\n");
                        return false;
                    }
                }

                if (!Bass.Init(playbackDevice, SAMPLE_RATE, DeviceInitFlags.Default, IntPtr.Zero))
                {
                    Discord_Log(LogSeverity.Error, "Failed to init playback device: " + Bass.LastError.ToString());
                    return false;
                }
                playProc = new StreamProcedure(playbackDevice_audioRequested);
            }

            logger.Info("==========  End!  ==================================================");
            return true;
        }

        /// <summary>
        /// BOT開始処理
        /// </summary>
        /// <returns></returns>
        public async Task Start()
        {
            if (Config == null)
            {
                Discord_Log(LogSeverity.Error, "Missing configuration, please fill config.json and try again.");
                await Task.Delay(-1);
            }
            if (Config.botToken == null)
            {
                Discord_Log(LogSeverity.Warning, "No bot token is set! Please create an application, create a bot user; then update the configuration file / enter data in the console.");
                queryBotToken();
            }

            switch (Discord.LoginState)
            {
                case LoginState.LoggedIn:
                case LoginState.LoggingIn:
                    await Stop();
                    break;
            }

        // log in & start
        L_login:
            try
            {
                await Discord.LoginAsync(TokenType.Bot, Config.botToken);
                await Discord.StartAsync();
            }
            catch (HttpException ex) when (ex.HttpCode == HttpStatusCode.Unauthorized)
            {
                Discord_Log(LogSeverity.Error, "Bot token is invalid", ex);
                queryBotToken();
                goto L_login;
            }
            catch (Exception ex)
            {
                Discord_Log(LogSeverity.Error, "Login failed.", ex);
                goto L_login;
            }
        }

        /// <summary>
        /// BOT停止処理
        /// </summary>
        /// <returns></returns>
        public async Task Stop()
        {
            Discord_Log(LogSeverity.Info, "Stopping");

            await LeaveVoiceAll();

            // stop discord client
            await Discord.StopAsync();
        }

        /// <summary>
        /// 設定ファイルリロード
        /// </summary>
        /// <returns></returns>
        public bool ReloadConfig()
        {
            if (File.Exists(configPath))
            {
                try
                {
                    Config = new Config(File.ReadAllText(configPath));
                }
                catch (Exception ex)
                {
                    Discord_Log(LogSeverity.Error, "Failed to parse configuration", ex);
                    return false;
                }
            }
            else
            {
                Discord_Log(LogSeverity.Info, "Configuration file not found, writing prototype to " + configPath);
                Config = new Config();
                // fill default default permissions
                foreach (var command in commandService.Commands)
                {
                    var dp = command.Attributes.OfType<DefaultPermissionAttribute>().SingleOrDefault();
                    if (dp != null)
                    {
                        switch (dp.DefaultPermission)
                        {
                            case Permission.Accept:
                                Config.commandsDefaultPermissions.Add(CommandsModule.GetCommandKey(command));
                                break;
                            case Permission.Reject:
                                Config.commandsDefaultPermissions.Add("!" + CommandsModule.GetCommandKey(command));
                                break;
                        }
                    }
                }
            }
            File.WriteAllText(configPath, Config.ToString());
            return true;
        }

        /// <summary>
        /// 設定ファイル更新
        /// </summary>
        /// <returns></returns>
        public bool UpdateConfig()
        {
            try
            {
                File.WriteAllText(configPath, Config.ToString());
                return true;
            }
            catch (Exception ex)
            {
                Discord_Log(LogSeverity.Error, "Failed to save file at \"" + configPath + "\"", ex);
                return false;
            }
        }

        /// <summary>
        /// Botエージェント再起動
        /// </summary>
        /// <returns></returns>
        public bool ReloadAgent()
        {
            string filename = Path.HasExtension(Config.commandsBotAgent) ? Config.commandsBotAgent : Path.ChangeExtension(Config.commandsBotAgent, "json");
            string path = Path.Combine(agentsPath, filename);

            try
            {
                BotAgent = new Agent(File.ReadAllText(path));
                return true;
            }
            catch (Exception ex)
            {
                Discord_Log(LogSeverity.Error, "Failed to load bot agent file at \"" + path + "\"", ex);
                return false;
            }
        }

        /// <summary>
        /// テキストメッセージ受信イベントハンドラー
        /// </summary>
        /// <param name="messageParam"></param>
        /// <returns></returns>
        private async Task Discord_MessageReceived(SocketMessage messageParam)
        {
            bool isCommand = false, isReceiveChannel = false;
            int argPos = 0;
            var message = messageParam as SocketUserMessage;
            if (message == null || message.Author.Id == Discord.CurrentUser.Id)
            {
                return;
            }

            isCommand = message.HasMentionPrefix(Discord.CurrentUser, ref argPos) || message.Channel is IDMChannel;
            if (!isCommand)
            {
                return;
            }

            Discord_Log(LogSeverity.Verbose, message.Author.Username + "#" + message.Author.Discriminator + ": " + message.Content.Substring(argPos));

            //コマンド実行チャンネルの確認
            foreach (ulong chanelid in Config.commandReceiveChanels)
            {
                var chatchannnel = Discord.GetChannel(chanelid) as SocketTextChannel;
                if (((SocketTextChannel)message.Channel).Equals(chatchannnel))
                {
                    isReceiveChannel = true;
                    break;
                }
            }
            if (!isReceiveChannel)
            {
                return;
            }
            Task.Run(async () => await RunCommand(message, argPos));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async Task Discord_Log(LogMessage message)
        {
            Utils.Log(message);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="message"></param>
        /// <returns></returns>
        private async void Discord_Log(LogSeverity severity, string message, Exception ex = null)
        {
            Utils.Log(severity, "Bot", message, ex);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        private async Task Discord_Ready()
        {
            Console.Title = Assembly.GetExecutingAssembly().GetName().Name + " (" + Discord.CurrentUser.Username + "#" + Discord.CurrentUser.Discriminator + ")";
            AppInfo = await Discord.GetApplicationInfoAsync();

            if (Discord.Guilds.Count == 0)
            {
                showNoGuildFound();
            }

            if (Config.voiceAutoJoinVoiceChannels != null)
            {
                foreach (var entry in Config.voiceAutoJoinVoiceChannels)
                {
                    var guild = Discord.GetGuild(entry.Key);
                    if (guild == null)
                    {
                        Discord_Log(LogSeverity.Error, "Auto-join voice: Failed to resolve guild with id " + entry.Key + ". Please check that this guild exists and that the bot user is authorized (it should appear in the member list).");
                        continue;
                    }
                    var voiceChannel = guild.GetVoiceChannel(entry.Value);
                    if (voiceChannel == null)
                    {
                        Discord_Log(LogSeverity.Error, "Auto-join voice: Failed to resolve voice channel with id " + entry.Value + ". Please check that this channel exists in the guild \"" + guild.Name + "\" and that it is a voice channel.");
                        continue;
                    }
                    await JoinVoice(voiceChannel);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guild"></param>
        /// <returns></returns>
        private async Task Discord_JoinedGuild(SocketGuild guild)
        {
            logger.Info("========== Start! ==================================================");
            if (Discord.Guilds.Count == 1)
            {

            }
            logger.Info("==========  End!  ==================================================");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guild"></param>
        /// <returns></returns>
        private async Task Discord_LeftGuild(SocketGuild guild)
        {
            logger.Info("========== Start! ==================================================");
            if (Discord.Guilds.Count == 0)
            {
                showNoGuildFound();
            }
            logger.Info("==========  End!  ==================================================");
        }

        /// <summary>
        /// コマンド実行イベントハンドラ
        /// </summary>
        /// <param name="message"></param>
        /// <param name="argPos"></param>
        /// <param name="guild"></param>
        /// <returns></returns>
        public async Task RunCommand(SocketUserMessage message, int argPos, SocketGuild guild = null)
        {
            // run command
            var context = new MultiServerIntegrateBot.Model.CommandContext(this, message, guild ?? (message.Channel as SocketGuildChannel)?.Guild);
            IResult result = await commandService.ExecuteAsync(context, argPos, Services);

            if (!result.IsSuccess)
            {
                switch (result.Error)
                {
                    case CommandError.Exception:
                        switch (result.ErrorReason)
                        {
                            case CommandsModule.ERR_REQUIRE_GUILD_CONTEXT:
                                await message.Channel.SendMessageAsync(BotAgent.Say(BotString.warning_guildContextRequired));
                                break;
                            case CommandsModule.ERR_PERMISSIONS_REQUIRED:
                                await message.Channel.SendMessageAsync(BotAgent.Say(BotString.warning_permissionsRequired));
                                break;
                            default:
                                Discord_Log(LogSeverity.Error, "Command error: " + result.ErrorReason);
                                await message.Channel.SendMessageAsync(BotAgent.Say(BotString.error_exception));
                                break;
                        }
                        break;
                    case CommandError.UnknownCommand:
                        await message.Channel.SendMessageAsync(BotAgent.Say(BotString.error_unknownCommand));
                        break;
                    case CommandError.BadArgCount:
                        await message.Channel.SendMessageAsync(BotAgent.Say(BotString.error_badArgCount));
                        break;
                    default:
                        await message.Channel.SendMessageAsync(BotAgent.Say(BotString.error_reason, result.ErrorReason));
                        break;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="voiceChannel"></param>
        /// <returns></returns>
        public async Task JoinVoice(SocketVoiceChannel voiceChannel)
        {
            VoiceSet voiceSet;
            if (voiceSets.TryGetValue(voiceChannel.Guild.Id, out voiceSet))
            {
                await LeaveVoice(voiceChannel.Guild.Id);
            }
            voiceSet = new VoiceSet();
            voiceSets.TryAdd(voiceChannel.Guild.Id, voiceSet);

            // join voice channel
            try
            {
                voiceSet.audioClient = await voiceChannel.ConnectAsync();
            }
            catch (Exception ex)
            {
                Discord_Log(LogSeverity.Error, "Failed to connect to voice channel", ex);
                return;
            }

            if (Config.speakEnabled)
            {
                // create speak stream
                voiceSet.speakStream = voiceSet.audioClient.CreatePCMStream(Config.speakAudioType, Config.speakBitRate ?? voiceChannel.Bitrate, Config.speakBufferMillis);

                // start recording
                if (recordChannel == 0 || Bass.ChannelIsActive(recordChannel) != PlaybackState.Playing)
                {
                    if (recordChannel == 0)
                    {
                        recordChannel = Bass.RecordStart(SAMPLE_RATE, CHANNEL_COUNT, BassFlags.RecordPause, recordProc, IntPtr.Zero);
                    }
                    Bass.ChannelPlay(recordChannel, false);

                    await Discord.SetGameAsync(Config.speakRecordingDevice ?? "Default Recording Device", Utils.link_twitchDummyStream, ActivityType.Playing);
                }
            }

            if (Config.listenEnabled)
            {
                // create listen streams
                foreach (var user in voiceChannel.Users)
                {
                    voiceSet.listenStreams.TryAdd(user.Id, user.AudioStream);
                }
                voiceSet.audioClient.StreamCreated += async (userId, listenStream) => voiceSet.listenStreams.TryAdd(userId, listenStream);
                voiceSet.audioClient.StreamDestroyed += async (userId) => { AudioInStream s; voiceSet.listenStreams.TryRemove(userId, out s); };

                // start playback
                if (playbackChannel == 0 || Bass.ChannelIsActive(playbackChannel) != PlaybackState.Playing)
                {
                    if (playbackChannel == 0)
                    {
                        playbackChannel = Bass.CreateStream(SAMPLE_RATE, CHANNEL_COUNT, BassFlags.Default, playProc, IntPtr.Zero);
                    }
                    firstPlayProcCall = true;
                    Bass.ChannelPlay(playbackChannel, false);
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guildId"></param>
        /// <returns></returns>
        public async Task LeaveVoice(ulong guildId)
        {
            VoiceSet voiceSet;
            if (!voiceSets.TryRemove(guildId, out voiceSet))
            {
                return;
            }

            // disconnect audio streams
            if (voiceSet.speakStream != null)
            {
                voiceSet.speakStream.Close();
            }
            foreach (var listenStream in voiceSet.listenStreams)
            {
                if (listenStream.Value != null)
                {
                    listenStream.Value.Close();
                }
            }

            // leave voice chat
            await voiceSet.audioClient.StopAsync();
            voiceSet.audioClient.Dispose();

            if (voiceSets.Count == 0)
            {
                // stop recording
                if (recordChannel != 0 && Bass.ChannelIsActive(recordChannel) != PlaybackState.Stopped)
                {
                    Bass.ChannelStop(recordChannel);
                    Bass.StreamFree(recordChannel);
                    recordChannel = 0;
                }
                await Discord.SetGameAsync("", Utils.link_twitchDummyStream, ActivityType.Playing);

                // stop playback
                if (playbackChannel != 0 && Bass.ChannelIsActive(playbackChannel) != PlaybackState.Stopped)
                {
                    Bass.ChannelStop(playbackChannel);
                    Bass.StreamFree(playbackChannel);
                    playbackChannel = 0;
                }
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <returns></returns>
        public async Task LeaveVoiceAll()
        {
            foreach (var guildId in voiceSets.Keys)
            {
                await LeaveVoice(guildId);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="guildId"></param>
        /// <returns></returns>
        public bool IsInVoice(ulong guildId)
        {
            return voiceSets.ContainsKey(guildId);
        }

        /// <summary>
        /// 
        /// </summary>
        private void queryBotToken()
        {
            Process.Start(Utils.link_discordApplications);

            Console.ForegroundColor = ConsoleColor.White;
            Console.WriteLine("Please enter bot token (My Apps > New App > APP BOT USER > Token):");

            Console.ForegroundColor = ConsoleColor.Green;
            Console.Write("$ ");
            Console.ForegroundColor = ConsoleColor.White;
            Config.botToken = Console.ReadLine();
            UpdateConfig();
        }

        /// <summary>
        /// 
        /// </summary>
        private void showNoGuildFound()
        {
            Discord_Log(LogSeverity.Warning, "No guild found! Please authorize/invite the bot to a guild.");
            Process.Start(string.Format(Utils.link_discordAuthorize, Discord.CurrentUser.Id));
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="type"></param>
        /// <returns></returns>
        private bool consoleCtrl(WinApi.CtrlTypes type)
        {
            switch (type)
            {
                case WinApi.CtrlTypes.CTRL_BREAK_EVENT:
                case WinApi.CtrlTypes.CTRL_CLOSE_EVENT:
                case WinApi.CtrlTypes.CTRL_C_EVENT:
                case WinApi.CtrlTypes.CTRL_LOGOFF_EVENT:
                case WinApi.CtrlTypes.CTRL_SHUTDOWN_EVENT:
                default:
                    Stop().GetAwaiter().GetResult();
                    Dispose();
                    break;
            }
            return true;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        private bool recordDevice_audioReceived(int handle, IntPtr buffer, int length, IntPtr user)
        {
            try
            {
                lock (record_lock)
                {
                    foreach (var voiceSet in voiceSets)
                    {
                        if (voiceSet.Value.speakStream == null)
                        {
                            continue;
                        }

                        var self = Discord.GetGuild(voiceSet.Key).GetUser(Discord.CurrentUser.Id);
                        if (self == null)
                        {
                            continue;
                        }

                        // send audio to discord voice
                        using (var stream = Utils.OpenBuffer(buffer, length, FileAccess.Read))
                        {
                            stream.CopyTo(voiceSet.Value.speakStream);
                        }
                    }
                    return true;
                }
            }
            catch (OperationCanceledException)
            {
                Discord_Log(LogSeverity.Debug, "Audio recording canceled");
                return false;
            }
            catch (Exception ex)
            {
                Discord_Log(LogSeverity.Error, "Error in audio recording", ex);
                return false;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="handle"></param>
        /// <param name="buffer"></param>
        /// <param name="length"></param>
        /// <param name="user"></param>
        /// <returns></returns>
        private int playbackDevice_audioRequested(int handle, IntPtr buffer, int length, IntPtr user)
        {
            if (firstPlayProcCall)
            {
                // first call is synchronous, following calls are asynchronous.
                firstPlayProcCall = false;
                if (playbackQueue == null)
                {
                    playbackQueue = new MemoryQueue();
                }
                else
                {
                    playbackQueue.Clear();
                }
                return 0;
            }

            try
            {
                lock (playback_lock)
                {
                    // read audio from users we're listening to
                    var frames = new List<RTPFrame>();
                    foreach (var voiceSet in voiceSets)
                    {
                        var guild = Discord.GetGuild(voiceSet.Key);
                        foreach (var listenStream in voiceSet.Value.listenStreams)
                        {
                            if (listenStream.Value == null)
                            {
                                continue;
                            }

                            var sender = guild.GetUser(listenStream.Key);
                            if (sender == null || sender.IsMuted || sender.IsSelfMuted || sender.IsSuppressed)
                            {
                                continue;
                            }

                            Discord_Log(LogSeverity.Debug, "listen:" + sender.Nickname + " frames[" + listenStream.Value.AvailableFrames + "]");
                            for (int f = 0; f < listenStream.Value.AvailableFrames; f++)
                            {
                                var frame = listenStream.Value.ReadFrameAsync(CancellationToken.None).GetAwaiter().GetResult();
                                if (frame.Missed)
                                {
                                    Discord_Log(LogSeverity.Debug, "RTP frame missed");
                                }
                                frames.Add(frame);
                            }
                        }
                    }

                    // mix audio
                    frames.Sort((o1, o2) => (int)(o1.Timestamp - o2.Timestamp));
                    using (var stream = playbackQueue.AsStream(FileAccess.Write))
                    {
                        mixRTPFrames(frames, stream);
                    }

                    // send audio to playback device
                    using (var stream = Utils.OpenBuffer(buffer, length, FileAccess.Write))
                    {
                        playbackQueue.Dequeue(stream, Math.Min(Math.Max(0, playbackQueue.Length), length));
                        return (int)stream.Position;
                    }
                }
            }
            catch (OperationCanceledException)
            {
                Discord_Log(LogSeverity.Debug, "Audio playback canceled");
                return -1;
            }
            catch (Exception ex)
            {
                Discord_Log(LogSeverity.Error, "Error in audio playback", ex);
                return -1;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sortedFrames"></param>
        /// <param name="stream"></param>
        private static void mixRTPFrames(IReadOnlyList<RTPFrame> sortedFrames, Stream stream)
        {
            if (sortedFrames.Count == 0)
            {
                return;
            }

            uint startTimestamp = sortedFrames[0].Timestamp;
            uint endTimestamp = sortedFrames[sortedFrames.Count - 1].Timestamp;

            byte[] sampleBuffer = new byte[SAMPLE_SIZE];
            for (int f = 0; f <= (endTimestamp - startTimestamp) / FRAME_SAMPLES; f++)
            {
                for (int s = 0; s < FRAME_SAMPLES; s++)
                {
                    int sample = 0;

                    foreach (var frame in sortedFrames)
                    {
                        if (!frame.Missed && (frame.Timestamp - startTimestamp) / FRAME_SAMPLES == f)
                        {
                            sample += Utils.GetInt16(frame.Payload, s * SAMPLE_SIZE, false);
                        }
                    }

                    Utils.GetBytes((short)Math.Min(Math.Max(short.MinValue, sample), short.MaxValue), sampleBuffer, 0, false);
                    stream.Write(sampleBuffer, 0, SAMPLE_SIZE);
                }
            }
        }


        /// <summary>
        /// 
        /// </summary>
        /// <param name="modify"></param>
        public static void ModifyServices(Action<IServiceCollection> modify)
        {
            modify(_services);
            Services = _services.BuildServiceProvider();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            Discord_Log(LogSeverity.Critical, "An unhandled exception has occured", e.ExceptionObject as Exception);
            Console.WriteLine("Press any key . . .");
            // Console.ReadKey();
            Environment.Exit(0);
        }

        /// <summary>
        /// デスコンストラクター
        /// </summary>
        public void Dispose()
        {
            if (Discord != null)
            {
                try
                {
                    Discord.Dispose();
                }
                finally
                {
                    Discord = null;
                }
            }
            if (recordChannel != 0)
            {
                Bass.StreamFree(recordChannel);
            }
            if (playbackChannel != 0)
            {
                Bass.StreamFree(playbackChannel);
            }
            Bass.Free();
            Bass.RecordFree();

        }
    }
}
