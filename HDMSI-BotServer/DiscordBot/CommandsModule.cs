using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net.NetworkInformation;
using System.Reflection;
using System.Security;
using System.Text;
using System.Threading.Tasks;

using Discord;
using Discord.Commands;
using Discord.WebSocket;

using MultiServerIntegrateBot.Enum;
using MultiServerIntegrateBot.Model;

namespace MultiServerIntegrateBot.DiscordBot
{
    public class CommandsModule : ModuleBase<MultiServerIntegrateBot.Model.CommandContext>
    {
        public const string ERR_PERMISSIONS_REQUIRED = "ERR:RequirePermissions";
        public const string ERR_REQUIRE_GUILD_CONTEXT = "ERR:RequireGuildContext";

        /// <summary>
        /// key must:
        /// 1. be unique (to identify a command)
        /// 2. not contain spaces (to separate entries)
        /// 3. not start with permission symbols - !, *, ~ (to work as permission key)
        /// </summary>
        /// <param name="command"></param>
        /// <returns></returns>
        public static string GetCommandKey(CommandInfo command)
        {
            var path = new Stack<string>();
            path.Push(command.Name);
            for (var mod = command.Module; mod != null; mod = mod.Parent)
            {
                path.Push(mod.Name);
            }

            return string.Join(".", path) + "(" + string.Join(",", command.Parameters.Select(p => p.Name)) + ")";
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="userId"></param>
        /// <param name="commandKey"></param>
        /// <returns></returns>
        protected Permission CheckUserPermissions(ulong userId, string commandKey)
        {
            if (userId == Context.Bot.AppInfo.Owner.Id)
            {
                return Permission.Accept;
            }
            if (Context.Guild != null)
            {
                Permission rolePermission = checkRolePermissions(userId, commandKey, 0);
                if (rolePermission != Permission.Default)
                {
                    return rolePermission;
                }
            }

            return Context.Bot.Config.commandsUserPermissions.Check(userId, commandKey, Context.Bot.Config.commandsDefaultPermissions);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="roleId"></param>
        /// <param name="commandKey"></param>
        /// <returns></returns>
        protected Permission CheckRolePermissions(ulong roleId, string commandKey)
        {
            return checkRolePermissions(roleId, commandKey, 1);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="entityId"></param>
        /// <param name="commandKey"></param>
        /// <param name="entityType"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentOutOfRangeException"></exception>
        private Permission checkRolePermissions(ulong entityId, string commandKey, int entityType)
        {
            PermissionDictionary<ulong> rolePermissions;
            if (Context.Bot.Config.commandsRolePermissions.TryGetValue(Context.Guild.Id, out rolePermissions))
            {
                IEnumerable<SocketRole> roles;
                switch (entityType)
                {
                    case 0:
                        {
                            var user = Context.Guild.GetUser(entityId);
                            roles = user.Roles.OrderByDescending(r => r.Position);
                            break;
                        }
                    case 1:
                        {
                            var role_ = Context.Guild.GetRole(entityId);
                            roles = Context.Guild.Roles.OrderByDescending(r => r.Position).Where(r => r.Position <= role_.Position);
                            break;
                        }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
                foreach (var role in roles)
                {
                    Permission rolePermission = rolePermissions.Check(role.Id, commandKey, Context.Bot.Config.commandsDefaultPermissions);
                    if (rolePermission != Permission.Default)
                    {
                        return rolePermission;
                    }
                }
            }
            return Permission.Default;
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="string"></param>
        /// <returns></returns>
        protected string Say(BotString @string)
        {
            return Context.Bot.BotAgent.Say(@string);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="string"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        protected string Say(BotString @string, params object[] args)
        {
            return Context.Bot.BotAgent.Say(@string, args);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="string"></param>
        /// <returns></returns>
        protected string Say(string @string)
        {
            return Context.Bot.BotAgent.Say(@string);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="string"></param>
        /// <param name="args"></param>
        /// <returns></returns>
        protected string Say(string @string, params object[] args)
        {
            return Context.Bot.BotAgent.Say(@string, args);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        protected override void BeforeExecute(CommandInfo command)
        {
            CheckPermissions(command);
            CheckGuildContext(command);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        /// <exception cref="InvalidOperationException"></exception>
        private void CheckPermissions(CommandInfo command)
        {
            var dpc = command.Attributes.OfType<DisablePermissionCheckAttribute>().SingleOrDefault();
            bool checkPermissions = dpc == null;
            if (!checkPermissions)
            {
                return;
            }

            if (CheckUserPermissions(Context.Message.Author.Id, GetCommandKey(command)) != Permission.Accept)
            {
                throw new InvalidOperationException(ERR_PERMISSIONS_REQUIRED);
            }
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="command"></param>
        /// <exception cref="InvalidOperationException"></exception>
        private void CheckGuildContext(CommandInfo command)
        {
            var rgc = command.Attributes.OfType<RequireGuildContextAttribute>().SingleOrDefault();
            bool requiresGuildContext = rgc != null && rgc.RequiresGuildContext;

            if (requiresGuildContext && Context.Guild == null)
            {
                throw new InvalidOperationException(ERR_REQUIRE_GUILD_CONTEXT);
            }
        }
    }
}
