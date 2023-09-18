using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using MultiServerIntegrateBot.DiscordBot;

namespace MultiServerIntegrateBot.Model
{
    public class CommandContext : ICommandContext
    {
        public Bot Bot { get; private set; }
        public SocketUserMessage Message { get; private set; }
        public SocketGuild Guild { get; private set; }
        public DiscordSocketClient Discord => Bot.Discord;

        IDiscordClient ICommandContext.Client => Bot.Discord;
        IGuild ICommandContext.Guild => Guild;
        IMessageChannel ICommandContext.Channel => Message.Channel;
        IUser ICommandContext.User => Message.Author;
        IUserMessage ICommandContext.Message => Message;

        public CommandContext(Bot bot, SocketUserMessage message, SocketGuild guild)
        {
            this.Bot = bot;
            this.Message = message;
            this.Guild = guild;
        }
    }
}
