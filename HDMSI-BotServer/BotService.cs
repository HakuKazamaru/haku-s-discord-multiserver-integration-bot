using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

using Microsoft.Extensions.Hosting;

using NLog;
using NLog.Web;

namespace MultiServerIntegrateBot
{
    public class BotService : BackgroundService
    {
        /// <summary>
        /// ロガー
        /// </summary>
        private static Logger logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();

        /// <summary>
        /// サービス開始時処理
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public override async Task StartAsync(CancellationToken ct)
        {
            logger.Info("サービスが開始されました。");
            await base.StartAsync(ct);
        }

        /// <summary>
        /// サービス終了時処理
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        public override async Task StopAsync(CancellationToken ct)
        {
            logger.Info("サービスが終了しました。");
            await base.StopAsync(ct);
        }

        /// <summary>
        /// メイン処理
        /// </summary>
        /// <param name="ct"></param>
        /// <returns></returns>
        protected override async Task ExecuteAsync(CancellationToken ct)
        {
            logger.Info("メイン処理の実行を開始します。");

            try
            {
                string configPath = Program.configPath;
                string agentsPath = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "agents");
                string commandsPath = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "commands.dll");

                var bot = new DiscordBot.Bot();

                try {
                    if (await bot.Init(configPath, agentsPath, commandsPath))
                    {
                        await bot.Start();
                        await Task.Delay(-1);
                    }
                }
                finally {
                    bot.Dispose();
                }
            }
            finally
            {
                logger.Info("メイン処理が終了しました。");
            }
        }

    }
}
