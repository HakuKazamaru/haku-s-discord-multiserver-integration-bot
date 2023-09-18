using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord.Audio;

namespace MultiServerIntegrateBot.Model
{
    /// <summary>
    /// ボイスチャット関連情報管理クラス
    /// </summary>
    public class VoiceSet
    {
        public IAudioClient audioClient;
        public AudioOutStream speakStream;
        public ConcurrentDictionary<ulong, AudioInStream> listenStreams = new ConcurrentDictionary<ulong, AudioInStream>();
    }
}
