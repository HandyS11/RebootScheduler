using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;            // There is no other way I swear (check Fields region)
using System.Text;

using Newtonsoft.Json;

using Oxide.Core;
using Oxide.Core.Libraries;
using Oxide.Core.Libraries.Covalence;
using Oxide.Core.Plugins;
using Oxide.Ext.Discord.Entities.Messages.Embeds;
using Oxide.Ext.Discord.Entities.Permissions;
using Oxide.Ext.Discord.Entities.Webhooks;

using UnityEngine;

using Global = ConVar.Global;

namespace Oxide.Plugins
{
    [Info("RebootScheduler", "HandyS11", "1.0.0")]
    [Description("Restart your Rust server on schedule or when updates comes out")]
    internal sealed class RebootScheduler : RustPlugin
    {
        #region Fields

        private Configuration _config;
        private UnityRestart _restart;

        private static RebootScheduler Instance;

        private bool IsRestartingNative
            => ServerMgr.Instance.Restarting;

        private readonly FieldInfo _nativeRestartRoutine = typeof(ServerMgr).GetField(
            "restartCoroutine",
            BindingFlags.Instance | BindingFlags.NonPublic
        );

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
            [JsonProperty(PropertyName = "Default chat avatar")]
            public ulong ChatAvatarId { get; set; }

            [JsonProperty(PropertyName = "Enable UpdateNotice plugin (required for hooks)")]
            public bool EnableUpdateNotice { get; set; }

            [JsonProperty(PropertyName = "Hooks configuration (require UpdateNotice)")]
            public HooksConfig HooksConfig { get; set; }

            [JsonProperty(PropertyName = "Restart messages cooldown")]
            public int[] RestartMessageCooldown { get; set; }

            [JsonProperty(PropertyName = "Enable daily restart")]
            public bool EnableDailyRestart { get; set; }

            [JsonProperty(PropertyName = "Daily restart time (13:30:00 as example for 1:30 pm UTC)")]
            public string DailyRestartTime { get; set; }

            [JsonProperty(PropertyName = "Daily restart cooldown (for message visibility)")]
            public int DailyRestartCooldown { get; set; }

            [JsonProperty(PropertyName = "Enable discord notifications")]
            public bool EnableDiscordNotification { get; set; }

            [JsonProperty(PropertyName = "Discord webhook url")]
            public string DiscordWebhook { get; set; }

            [JsonProperty(PropertyName = "Discord role id to mention (0 = no mention)")]
            public ulong DiscordRole { get; set; }
        }

        private sealed class HooksConfig
        {
            [JsonProperty(PropertyName = "When the Server Restart (COOLDOWN | DAILY_TIME)")]
            public string Method { get; set; }

            [JsonProperty(PropertyName = "Cooldown time before restart (in seconds)")]
            public int CooldownTime { get; set; }

            [JsonProperty(PropertyName = "Enable restart OnCarbonUpdate")]
            public bool EnableOnCarbonUpdate { get; set; }

            [JsonProperty(PropertyName = "Enable restart OnOxideUpdate")]
            public bool EnableOnOxideUpdate { get; set; }

            [JsonProperty(PropertyName = "Enable restart OnServerUpdate")]
            public bool EnableOnServerUpdate { get; set; }
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                ChatAvatarId = 0,
                EnableUpdateNotice = true,
                HooksConfig = new HooksConfig
                {
                    Method = "COOLDOWN",
                    CooldownTime = 300,
                    EnableOnCarbonUpdate = false,
                    EnableOnOxideUpdate = true,
                    EnableOnServerUpdate = true,
                },
                RestartMessageCooldown =
                [
                    3600,
                    1800,
                    900,
                    300,
                    120,
                    60,
                    30,
                    10,
                    5,
                    4,
                    3,
                    2,
                    1
                ],
                EnableDailyRestart = false,
                DailyRestartTime = "04:00:00",
                DailyRestartCooldown = 300,
                EnableDiscordNotification = false,
                DiscordWebhook = "https://support.discordapp.com/hc/en-us/articles/228383668-Intro-to-Webhooks",
                DiscordRole = 0
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            Instance = this;
            try
            {
                _config = Config.ReadObject<Configuration>();
            }
            catch (Exception exception)
            {
                PrintError(exception.ToString());
                Puts("Configuration has been reset!");
                _config = GetDefaultConfig();
            }
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

        void Init()
        {
            permission.RegisterPermission(Permission.Admin, this);
        }

        void OnServerInitialized()
        {
            _restart = ServerMgr.Instance.gameObject.AddComponent<UnityRestart>();
            AddCovalenceCommand(Command.Prefix, nameof(SendWipeCommand));
        }

        private void Loaded()
        {
            if (UpdateNotice != null) return;
            PrintWarning(GetMessage(MessageKey.UpdateNoticeMissing));
            _config.EnableUpdateNotice = false;
        }

        void Unload()
        {
            if (IsRestartingNative) CancelNativeRestart();

            UnityEngine.Object.Destroy(_restart);
            Instance = null;
        }

        #endregion

        #region UpdateNotice Hooks

        void OnCarbonUpdate(string version)
        {
            Puts($"Carbon got updated! - {version}");

            if (!_config.HooksConfig.EnableOnCarbonUpdate) return;

            ScheduleUpdateRestart(RestartReason.CarbonUpdate);
        }

        void OnOxideUpdate(string version)
        {
            Puts($"Oxide got updated! - {version}");

            if (!_config.HooksConfig.EnableOnOxideUpdate) return;

            ScheduleUpdateRestart(RestartReason.OxideUpdate);
        }

        void OnServerUpdate(string version)
        {
            Puts($"Server got updated! - {version}");

            if (!_config.HooksConfig.EnableOnServerUpdate) return;

            ScheduleUpdateRestart(RestartReason.ServerUpdate);
        }

        #endregion

        #region Functions

        private void KickPlayers()
        {
            foreach (var player in BasePlayer.allPlayerList.ToList())
            {
                player.Kick(GetMessage(MessageKey.KickReason, player.UserIDString));
            }
        }

        private void CancelNativeRestart()
        {
            var routine = (IEnumerator)_nativeRestartRoutine.GetValue(ServerMgr.Instance);
            ServerMgr.Instance.StopCoroutine(routine);

            _nativeRestartRoutine.SetValue(ServerMgr.Instance, null);
        }

        private void ScheduleUpdateRestart(RestartReason reason)
        {
            switch (_config.HooksConfig.Method)
            {
                case "COOLDOWN":
                    _restart.DoRestart(DateTime.Now.AddSeconds(_config.HooksConfig.CooldownTime), reason);
                    break;
                case "DAILY_TIME":
                    {
                        if (_restart.IsRestarting) return;
                        if (_config.EnableDailyRestart) return;
                        var time = ParseTime(_config.DailyRestartTime);
                        if (time == null)
                        {
                            Puts(GetMessage(MessageKey.WrongTimeFormat));
                            return;
                        }
                        _restart.DoRestart(time.Value, reason);
                        break;
                    }
            }
        }

        private static class Status
        {
            public const string NO_PLANNED = "No planned restart";
            public const string RESTARTING_NATIVE = "Restarting (Native)";
            public const string RESTARTING = "Restarting";

        }

        private string GetStatus(out DateTime? restartTime)
        {
            restartTime = null;

            if (IsRestartingNative)
            {
                return Status.RESTARTING_NATIVE;
            }

            if (!_restart.IsRestarting) return Status.NO_PLANNED;

            restartTime = _restart.RestartTime;
            return Status.RESTARTING;
        }

        public enum RestartStatus
        {
            Canceled,
            Initialized,
            Now,
            Test,
        }

        private void SendRestartToDiscord(RestartStatus status, int? secondsLeft = null)
        {
            string desc;
            DiscordColor color;
            switch (status)
            {
                case RestartStatus.Canceled:
                    desc = "The server restart has been canceled!";
                    color = new DiscordColor(10181046);
                    break;
                case RestartStatus.Initialized:
                    desc = $"The server will restart in {FormatTime(secondsLeft)}!";
                    color = new DiscordColor(15105570);
                    break;
                case RestartStatus.Now:
                    desc = "The server is restarting!";
                    color = new DiscordColor(16711686);
                    break;
                case RestartStatus.Test:
                    desc = "This is a test message!";
                    color = new DiscordColor(1752220);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(status), status, null);
            }

            SendWebhookToDiscord(_config.DiscordWebhook,
                new WebhookCreateMessage()
                {
                    AvatarUrl = "https://i.imgur.com/O7s0Z1i.png",
                    Username = "RebootScheduler",
                    Content = (_config.DiscordRole != 0) ? $"<@&{_config.DiscordRole}>" : "",
                    Embeds =
                    [
                        new DiscordEmbed
                        {
                            Title = ConVar.Server.hostname,
                            Description = desc,
                            Color = color,
                            Thumbnail = new EmbedThumbnail()
                            {
                                Url = "https://i.imgur.com/O7s0Z1i.png"
                            },
                            Fields =
                            [
                                new EmbedField
                                {
                                    Name = "Restart time",
                                    Value = _restart.RestartTime?.ToString("dd/MM/yyyy - HH:mm:ss UTC"),
                                    Inline = true
                                },

                                new EmbedField
                                {
                                    Name = "Reason",
                                    Value = _restart.Reason.ToString(),
                                    Inline = true
                                }
                            ],
                            Footer = new EmbedFooter()
                            {
                                Text = "RebootScheduler",
                                IconUrl = "https://i.imgur.com/O7s0Z1i.png"
                            }
                        }
                    ]
                });
        }

        #endregion

        #region Restart

        public enum RestartReason
        {
            ApiCall,
            CarbonUpdate,
            DailyRestart,
            OxideUpdate,
            ServerAdmin,
            ServerUpdate,
        }

        private sealed class UnityRestart : MonoBehaviour
        {
            #region Fields

            private IEnumerator _restartRoutine = null;
            private bool _hasBeenSent = false;

            public DateTime? RestartTime { get; private set; }
            public RestartReason? Reason { get; private set; }
            public bool IsRestarting { get; private set; }

            #endregion

            #region Unity hooks

            void Start()
            {
                if (!Instance._config.EnableDailyRestart) return;

                var time = ParseTime(Instance._config.DailyRestartTime);
                if (time == null)
                {
                    Instance.Puts(Instance.GetMessage(MessageKey.WrongTimeFormat));
                    return;
                }

                RestartTime = time.Value;
                Reason = RestartReason.DailyRestart;

                DoRestart(time.Value, RestartReason.DailyRestart);
            }

            void OnDestroy()
            {
                if (IsRestarting)
                {
                    CancelRestart();
                }
            }

            #endregion

            #region Functions

            public void DoRestart(DateTime restartTime, RestartReason reason)
            {
                var secondsLeft = (int)(restartTime - DateTime.Now).TotalSeconds;

                RestartTime = restartTime;
                Reason = reason;
                IsRestarting = true;

                OnRestartInit(secondsLeft, reason);

                _restartRoutine = InitRestartRoutine(secondsLeft);
                StartCoroutine(_restartRoutine);
            }

            private IEnumerator InitRestartRoutine(int totalSecondsLeft)
            {
                while (totalSecondsLeft > 0)
                {
                    var nextCd = GetNextCountdownValue(Mathf.CeilToInt(totalSecondsLeft - 1f));
                    yield return new WaitForSecondsRealtime(totalSecondsLeft - nextCd);

                    OnRestartTick(nextCd);
                    totalSecondsLeft = nextCd;
                }
                RestartNow();
            }

            private void RestartNow()
            {
                OnRestartNow();

                Instance.KickPlayers();
                Global.quit(null);
            }

            public void CancelRestart()
            {
                OnRestartCancel();

                StopCoroutine(_restartRoutine);
                Cleanup();
            }

            private int GetNextCountdownValue(int secondsLeft)
            {
                var next = Instance._config.RestartMessageCooldown.FirstOrDefault(p => p <= secondsLeft);

                if (next < 0)
                    next = 0;

                return next;
            }

            private void Cleanup()
            {
                _restartRoutine = null;
                RestartTime = null;
                Reason = null;
                IsRestarting = false;
            }

            #endregion

            #region Hooks definition

            private void OnRestartInit(int secondsLeft, RestartReason reason)
            {
                Interface.CallHook("OnRestartInit", secondsLeft, reason);

                if (Instance._config.EnableDiscordNotification)
                    Instance.SendRestartToDiscord(RestartStatus.Initialized, secondsLeft);

                Instance.SendGlobalMessage(Instance.GetCustomMessage(MessageKey.RestartGlobalMessage, null, FormatTime(secondsLeft), Reason));
            }

            private void OnRestartTick(int secondsLeft)
            {
                Interface.CallHook("OnRestartTick", secondsLeft);

                Instance.SendGlobalMessage(Instance.GetCustomMessage(MessageKey.RestartGlobalMessageShort, null, FormatTime(secondsLeft)));

                if (!Instance._config.EnableDiscordNotification || secondsLeft >= 10 || _hasBeenSent) return;
                Instance.SendRestartToDiscord(RestartStatus.Now);
                _hasBeenSent = true;
            }

            private void OnRestartNow()
            {
                Interface.CallHook("OnRestartNow");
            }

            private void OnRestartCancel()
            {
                Interface.CallHook("OnRestartCancel");

                if (Instance._config.EnableDiscordNotification)
                    Instance.SendRestartToDiscord(RestartStatus.Canceled);

                Instance.SendGlobalMessage(Instance.GetMessage(MessageKey.RestartCancelMessage));
            }

            #endregion
        }

        #endregion

        #region Helper

        private void SendGlobalMessage(string message)
        {
            Puts(message);
            Server.Broadcast(message, _config.ChatAvatarId);
        }

        private string GetCustomMessage(string messageKey, string playerId = null, params object[] data)
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

        private bool HasPermission(IPlayer player, string perm)
            => permission.UserHasPermission(player.Id, perm);

        private static DateTime? ParseTime(string str)
        {
            if (int.TryParse(str, out var seconds))
                return DateTime.Now.AddSeconds(seconds);

            if (!TimeSpan.TryParseExact(str, @"hh\:mm\:ss", CultureInfo.InvariantCulture, TimeSpanStyles.None, out var time)) return null;
            return time < DateTime.Now.TimeOfDay ? DateTime.Now.Date.AddDays(1).Add(time) : DateTime.Now.Date.Add(time);
        }

        private static string FormatTime(int? seconds)
        {
            if (!seconds.HasValue) return "DNF";

            var hours = seconds / 3600;
            var minutes = (seconds % 3600) / 60;
            var secondsRemaining = seconds % 60;

            var result = new StringBuilder();

            if (hours > 0)
            {
                result.Append(hours).Append("h");
                if (minutes > 0 || secondsRemaining > 0)
                    result.Append(" ");
            }

            if (minutes > 0)
            {
                result.Append(minutes).Append("m");
                if (secondsRemaining > 0)
                    result.Append(" ");
            }

            if (secondsRemaining > 0 || (hours == 0 && minutes == 0))
                result.Append(secondsRemaining).Append("s");

            return result.ToString();
        }

        #region Discord

        private readonly Dictionary<string, string> _headers = new()
        {
            {"Content-Type", "application/json"}
        };

        private void SendWebhookToDiscord(string url, WebhookCreateMessage webHook)
        {
            var payload = JsonConvert.SerializeObject(webHook,
                Formatting.None,
                new JsonSerializerSettings
                {
                    NullValueHandling = NullValueHandling.Ignore
                });

            webrequest.Enqueue(url, payload, (code, response) =>
            {
                if (code is 200 or 204) return;
                if (response == null) PrintWarning($"Discord didn't respond. Error Code: {code}");
                else Puts($"Discord respond with: {response} Payload: {payload}");
            }, this, RequestMethod.POST, _headers);
        }

        #endregion

        #endregion

        #region Commands

        private static class Command
        {
            public const string Prefix = "rs";
            public const string Cancel = "cancel";
            public const string Discord = "discord";
            public const string Help = "help";
            public const string Restart = "restart";
            public const string Status = "status";
        }

        private bool SendWipeCommand(IPlayer player, string cmd, string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));
            if (!HasPermission(player, Permission.Admin))
            {
                player.Message(GetMessage(MessageKey.NoPermission));
                return true;
            }
            if (args.Length == 0)
            {
                player.Message(GetMessage(MessageKey.Help));
                return true;
            }
            switch (args[0])
            {
                case Command.Cancel:
                    if (IsRestartingNative)
                    {
                        CancelNativeRestart();
                        player.Message(GetMessage(MessageKey.NativeRestartCancel, player.Id));
                        break;
                    }

                    if (_restart.IsRestarting)
                    {
                        _restart.CancelRestart();
                        player.Message(GetMessage(MessageKey.RestartCancelMessage, player.Id));
                        break;
                    }

                    player.Message(GetMessage(MessageKey.NoRestartOnGoing, player.Id));
                    break;

                case Command.Discord:
                    SendRestartToDiscord(RestartStatus.Test);
                    player.Message("Test message sent to discord!");
                    break;

                case Command.Help:
                    player.Message(GetMessage(MessageKey.Help, player.Id));
                    break;

                case Command.Restart:
                    if (args.Length == 1)
                    {
                        _restart.DoRestart(DateTime.Now.AddSeconds(12), RestartReason.ServerAdmin);
                        player.Message(GetMessage(MessageKey.RestartInitialized, player.Id));
                        break;
                    }

                    if (args.Length == 2)
                    {
                        var time = ParseTime(args[1]);
                        if (time == null) player.Message(GetMessage(MessageKey.WrongTimeFormat, player.Id));
                        else _restart.DoRestart(time.Value.AddSeconds(2), RestartReason.ServerAdmin);
                        player.Message(GetMessage(MessageKey.RestartInitialized, player.Id));
                        break;
                    }

                    player.Message(GetMessage(MessageKey.WrongNumberOfArgument, player.Id));
                    break;

                case Command.Status:
                    var status = GetStatus(out var restartTime);
                    player.Message(restartTime.HasValue
                        ? GetCustomMessage(MessageKey.StatusWithTime, player.Id, status, restartTime)
                        : GetCustomMessage(MessageKey.Status, player.Id, status));
                    break;

                default:
                    player.Message(GetMessage(MessageKey.UnknownCommand, player.Id));
                    break;
            }
            return true;
        }

        #endregion

        #region Localization

        private static class MessageKey
        {
            public const string Help = "Help";
            public const string KickReason = "KickReason";
            public const string NativeRestartCancel = "NativeRestartCancel";
            public const string NoPermission = "NoPermission";
            public const string NoRestartOnGoing = "NoRestartOnGoing";
            public const string RestartCancelMessage = "RestartCancelMessage";
            public const string RestartGlobalMessage = "RestartGlobalMessage";
            public const string RestartGlobalMessageShort = "RestartGlobalMessageShort";
            public const string RestartInitialized = "RestartInitialized";
            public const string Status = "Status";
            public const string StatusWithTime = "StatusWithTime";
            public const string UnknownCommand = "UnknownCommand";
            public const string UpdateNoticeMissing = "UpdateNoticeMissing";
            public const string WrongNumberOfArgument = "WrongNumberOfElements";
            public const string WrongTimeFormat = "WrongTimeFormat";
        }

        protected override void LoadDefaultMessages()
        {
            lang.RegisterMessages(new Dictionary<string, string>
            {
                [MessageKey.Help] = "\nCommands:\t\t\t\tExplanations:\n\n- rs cancel\t\t\t\tCancel the ongoing restart\n- rs discord\t\t\t\tSend a test message to discord\n- rs help\t\t\t\tDisplay the help message\n- rs restart <time in seconds>\t\tInitiate a restart (10s if no time given)\n- rs status\t\t\t\tDisplay the current restart status",
                [MessageKey.KickReason] = "The server is restarting for update.",
                [MessageKey.NativeRestartCancel] = "Native restart was cancelled.",
                [MessageKey.NoPermission] = "You are not allowed to run this command!",
                [MessageKey.NoRestartOnGoing] = "There is no restart on going!",
                [MessageKey.RestartCancelMessage] = "The restart has been cancelled.",
                [MessageKey.RestartGlobalMessage] = "The server is restarting in {0} due to {1}!",
                [MessageKey.RestartGlobalMessageShort] = "The server is restarting in {0}!",
                [MessageKey.RestartInitialized] = "Restart has been initialize.",
                [MessageKey.Status] = "Status: {0}",
                [MessageKey.StatusWithTime] = "Status: {0} - {1}",
                [MessageKey.UnknownCommand] = "Unknown command!",
                [MessageKey.UpdateNoticeMissing] = "The plugin \"UpdateMissing\" was not found. Check on UMod: https://umod.org/plugins/update-notice",
                [MessageKey.WrongNumberOfArgument] = "Wrong number of elements! Please check the help command.",
                [MessageKey.WrongTimeFormat] = "Wrong time format! Please use \"hh:mm:ss\" for a planned time OR xxx (in seconds) for a cooldown",
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

        #region APIs

        #region Internal API

        public void AddNewRestart(DateTime date)
            => _restart.DoRestart(date, RestartReason.ApiCall);

        public void EnableDailyRestart(bool b)
        {
            _config.EnableDailyRestart = b;
            SaveConfig();
        }

        public DateTime? GetNextRestartSchedule()
            => _restart.RestartTime;

        public bool IsDailyRestartEnable()
            => _config.EnableDailyRestart;

        public bool IsRestarting()
            => _restart.IsRestarting;

        public void SetDailyRestartTime(string time)
        {
            _config.DailyRestartTime = time;
            SaveConfig();
        }

        #endregion Internal API

        #region External API

        [PluginReference]
        Plugin? UpdateNotice;

        #endregion External API

        #endregion
    }
}
