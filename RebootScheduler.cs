using System.Collections.Generic;
using System;
using Newtonsoft.Json;
using Oxide.Core.Plugins;

namespace Oxide.Plugins
{
    [Info("RebootScheduler", "HandyS11", "1.0.0")]
    [Description("Restart your Rust server on schedule or when updates comes out")]
    internal sealed class RebootScheduler : RustPlugin
    {
        #region Fields

        private Configuration _config;

        #endregion

        #region Permission

        private static class Permission
        {
            public const string Admin = "rebootscheduler.admin";
        }

        #endregion

        #region Configuration

        private sealed class Configuration
        {
            [JsonProperty(PropertyName = "Default Chat Avatar")]
            public ulong ChatAvatar { get; set; }

            [JsonProperty(PropertyName = "Enable UpdateNotice Plugin (required for update tracking)")]
            public bool EnableUpdateNotice { get; set; }
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                ChatAvatar = 0,
                EnableUpdateNotice = false,
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            SaveConfig();
        }

        protected override void LoadDefaultConfig()
        {
            _config = GetDefaultConfig();
        }

        protected override void SaveConfig()
        {
            Config.WriteObject(_config, true);
        }

        #endregion

        #region Oxide Hooks

        private void Init()
        {
            permission.RegisterPermission(Permission.Admin, this);
        }

        private void Loaded()
        {
            if (UpdateNotice == null)
            {
                PrintWarning(GetMessage(MessageKey.NoPermission));
                _config.EnableUpdateNotice = false;
            }
        }

        private void Unload()
        {
            _config = null;
        }

        #endregion

        #region Hooks



        #endregion

        #region Functions



        #endregion

        #region Helper
        private void SendChatMessage(BasePlayer player, string message)
        {
            Player.Message(player, message, _config.ChatAvatar);
        }

        private void SendGlobalChatMessage(string message)
        {
            Server.Broadcast(message, _config.ChatAvatar);
        }

        private bool HasPermission(BasePlayer player, string permissionName)
        {
            return permission.UserHasPermission(player.UserIDString, permissionName);
        }

        #endregion

        #region Commands

        private static class Command
        {
            public const string Prefix = "rs";
            public const string Help = "help";
        }

        [ChatCommand(Command.Prefix)]
        private void ChatCommand(BasePlayer player, string command, string[] args)
        {
            switch (args[0])
            {
                case "help":
                    SendChatMessage(player, GetMessage(MessageKey.Help, player.UserIDString));
                    break;
                default:
                    SendChatMessage(player, "Hello there!");
                    break;
            }
        }

        [ConsoleCommand(Command.Prefix)]
        private void ConsoleCommand(BasePlayer player, string command, string[] args)
        {
            if (player.isClient && !HasPermission(player, Permission.Admin))
            {
                SendChatMessage(player, GetMessage(MessageKey.NoPermission, player.UserIDString));
                return;
            }

            switch (args[0])
            {
                case "help":
                    Puts(GetMessage(MessageKey.Help, player.UserIDString));
                    break;
                default:
                    Puts("Hello there!");
                    break;
            }
        }

        #endregion

        #region Localization

        private static class MessageKey
        {
            public const string UpdateNoticeMissing = "UpdateNoticeMissing";
            public const string NoPermission = "NoPermission";
            public const string Help = "Help";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [MessageKey.UpdateNoticeMissing] = "The plugin \"UpdateMissing\" was not found. Check on UMod: https://umod.org/plugins/update-notice",
                [MessageKey.NoPermission] = "You are not allowed to run this command!",
                [MessageKey.Help] = "Some useful help!",
            }, this);
        }

        private string GetMessage(string messageKey, string playerId = null, params object[] data)
        {
            try
            {
                var template = lang.GetMessage(messageKey, this, playerId);
                return string.Format(template, data);
            }
            catch (Exception exception)
            {
                PrintError(exception.ToString());
                throw;
            }
        }

        #endregion

        #region Internal API
        public bool IsRebootSchedule() => false;

        public DateTime? GetNextRebootSchedule() => null;

        #endregion Internal API

        #region External API

        [PluginReference]
        Plugin UpdateNotice;

        #endregion External API
    }
}
