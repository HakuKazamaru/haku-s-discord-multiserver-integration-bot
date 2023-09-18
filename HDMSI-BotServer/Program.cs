using System.Diagnostics;
using System.Reflection;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.WindowsServices;
using Microsoft.Extensions.Hosting.Systemd;

using NLog;
using NLog.Extensions;

using NLog.Extensions.Logging;
using NLog.Web;
using Windows.Services.Maps;

namespace MultiServerIntegrateBot
{
    /// <summary>
    /// メインクラス
    /// </summary>
    public static class Program
    {
        /// <summary>
        /// 設定ファイルのパス
        /// </summary>
        public static string configPath;

        /// <summary>
        /// メインメソッド
        /// </summary>
        /// <param name="args"></param>
        public static void Main(string[] args)
        {
            /// <summary>
            /// ロガー
            /// </summary>
            var logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();
            logger.Info("========== App Start! ==================================================");

            try
            {
                Console.Title = Assembly.GetExecutingAssembly().GetName().Name;

                switch (args.Length)
                {
                    case 0:
                        configPath = Path.Combine(Directory.GetParent(Assembly.GetExecutingAssembly().Location).FullName, "config.json");
                        break;
                    case 1:
                    default:
                        configPath = args[0];
                        break;
                }


                if (true) {
                    IHost host = CreateHostBuilderForWindows(args).Build();
                    host.Run();
                }
                else {
                    IHost host = CreateHostBuilderForLinux(args).Build();
                    host.Run();
                }
            }
            catch (Exception ex)
            {
                logger.Error(ex, "例外が発生したためにプログラムを停止しました。");
                throw;
            }
            finally
            {
                NLog.LogManager.Shutdown();
            }

            logger.Info("========== App End!   ==================================================");
        }

        /// <summary>
        /// ASP.NET Core 設定(Windows)
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static IHostBuilder CreateHostBuilderForWindows(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .UseWindowsService(options =>
                {
                    // サービス名
                    options.ServiceName = nameof(BotService); 
                })
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .ConfigureServices(services => {
                    services.AddSingleton<BotService>();
                    services.AddHostedService<BotService>();
                })
                .ConfigureLogging((hostContext, logging) => { })
                .UseNLog();
        }

        /// <summary>
        /// ASP.NET Core 設定(Linux)
        /// </summary>
        /// <param name="args"></param>
        /// <returns></returns>
        public static IHostBuilder CreateHostBuilderForLinux(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .UseSystemd()
                .ConfigureAppConfiguration((hostingContext, config) =>
                {
                    config.SetBasePath(Path.GetDirectoryName(Process.GetCurrentProcess().MainModule.FileName));
                    config.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);
                })
                .ConfigureServices(services => {
                    services.AddSingleton<BotService>();
                    services.AddHostedService<BotService>();
                })
                .ConfigureLogging((hostContext, logging) => { })
                .UseNLog();
        }
    }
}
