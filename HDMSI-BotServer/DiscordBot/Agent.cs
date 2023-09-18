using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using NLog;
using NLog.Web;

using MultiServerIntegrateBot.Model;
using MultiServerIntegrateBot.Enum;

namespace MultiServerIntegrateBot.DiscordBot
{
    /// <summary>
    /// BotAgentクラス
    /// 
    /// </summary>
    public class Agent
    {
        /// <summary>
        /// ロガー
        /// </summary>
        private static Logger logger = NLogBuilder.ConfigureNLog("nlog.config").GetCurrentClassLogger();

        /// <summary>
        /// 
        /// </summary>
        public string Id { get; set; }

        public Dictionary<string, string> SayDictionary = new Dictionary<string, string>();

        /// <summary>
        /// 無処理コンストラクター
        /// </summary>
        public Agent()
        {

        }

        /// <summary>
        /// 初期化用コンストラクター
        /// </summary>
        /// <param name="json"></param>
        public Agent(string json)
        {
            Load(JToken.Parse(json));
        }

        /// <summary>
        /// 文字列化メソッド
        /// </summary>
        /// <returns></returns>
        public override string ToString()
        {
            return ToJson().ToString(Formatting.Indented);
        }

        /// <summary>
        /// JSONにパースするメソッド
        /// </summary>
        /// <returns></returns>
        public JToken ToJson()
        {
            JTokenStack stack = new JTokenStack(new JObject());

            stack.Set("say", writeDictionary(SayDictionary));

            return stack.Pop();
        }

        /// <summary>
        /// 読み取り用メソッド
        /// </summary>
        /// <param name="root"></param>
        public void Load(JToken root)
        {
            JTokenStack stack = new JTokenStack(root);
            SayDictionary = readDictionary((JObject)stack.Get("say"));
            stack.Pop();
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public string Say(string key)
        {
            string value;
            if (SayDictionary.TryGetValue(key, out value))
            {
                return value;
            }
            else
            {
                Utils.Log(LogSeverity.Warning, GetType(), "Missing translation for saying \"" + key + "'\"");
                return key;
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="string"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string Say(string @string, params object[] args)
        {
            return string.Format(Say(@string), args);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="string"></param>
        /// <returns></returns>
        public string Say(BotString @string)
        {
            return Say(@string.ToString());
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="string"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        public string Say(BotString @string, params object[] args)
        {
            return string.Format(Say(@string), args);
        }

        /// <summary>
        /// JOSNのパラメーター読み取りメソッド
        /// </summary>
        /// <param name="node"></param>
        /// <returns></returns>
        private static Dictionary<string, string> readDictionary(JObject node)
        {
            var result = new Dictionary<string, string>();
            foreach (var entry in node)
            {
                result.Add(entry.Key, (string)entry.Value);
            }
            return result;
        }

        /// <summary>
        /// JOSNのパラメーター書き取りメソッド
        /// </summary>
        /// <param name="entries"></param>
        /// <returns></returns>
        private static JObject writeDictionary(Dictionary<string, string> entries)
        {
            var result = new JObject();
            foreach (var entry in entries)
            {
                result.Add(entry.Key, entry.Value);
            }
            return result;
        }
    }
}
