using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

using NLog;
using NLog.Web;

namespace MultiServerIntegrateBot
{
    /// <summary>
    /// ASP関連ライブラリー初期化クラス
    /// </summary>
    public class Startup
    {
        /// <summary>
        /// ロガー
        /// </summary>
        private static Logger logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();

        /// <summary>
        /// appsetting.json読み込み用
        /// </summary>
        public IConfiguration Configuration { get; }

        /// <summary>
        /// コンストラクター
        /// </summary>
        /// <param name="configuration"></param>
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="services"></param>
        public void ConfigureServices(IServiceCollection services)
        {
            logger.Info("========== Start! ==================================================");

            logger.Info("==========  End!  ==================================================");
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="app"></param>
        /// <param name="env"></param>
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            logger.Info("========== Start! ==================================================");

            logger.Info("==========  End!  ==================================================");
        }
    }
}
