using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

namespace MultiServerIntegrateBot.Model
{
    /// <summary>
    /// 
    /// </summary>
    public class JTokenStack : Stack<JToken>
    {
        /// <summary>
        /// 無処理コンストラクター
        /// </summary>
        public JTokenStack()
        {

        }

        /// <summary>
        /// 初期化用コンストラクター
        /// </summary>
        /// <param name="root"></param>
        public JTokenStack(JToken root)
        {
            Push(root);
        }

        /// <summary>
        /// プッシュメソッド
        /// </summary>
        /// <param name="childName"></param>
        public void Push(string childName)
        {
            var token = Peek();
            var child = token[childName];
            Push(child);
        }

        /// <summary>
        /// プッシュメソッド
        /// </summary>
        /// <param name="childName"></param>
        public void PushNew(string childName)
        {
            var token = Peek();
            var child = new JObject();
            token[childName] = child;
            Push(child);
        }

        /// <summary>
        /// ゲッター
        /// </summary>
        /// <param name="childName"></param>
        /// <returns></returns>
        /// <exception cref="IOException"></exception>
        public JToken Get(string childName)
        {
            var token = Peek();
            var child = token[childName];
            if (child == null)
            {
                throw new IOException("\"" + childName + "\" not found in " + token.Path);
            }
            return child;
        }

        /// <summary>
        /// セッター
        /// </summary>
        /// <param name="childName"></param>
        /// <param name="value"></param>
        public void Set(string childName, JToken value)
        {
            var token = Peek();
            token[childName] = value;
        }

    }
}
