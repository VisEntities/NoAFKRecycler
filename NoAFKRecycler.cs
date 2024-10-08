/*
 * Copyright (C) 2024 Game4Freak.io
 * This mod is provided under the Game4Freak EULA.
 * Full legal terms can be found at https://game4freak.io/eula/
 */

using Newtonsoft.Json;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("No AFK Recycler", "VisEntities", "1.0.0")]
    [Description("Prevents players from staying afk while using recyclers.")]
    public class NoAFKRecycler : RustPlugin
    {
        #region Fields

        private static NoAFKRecycler _plugin;
        private static Configuration _config;
        private Dictionary<BasePlayer, Timer> _afkTimers = new Dictionary<BasePlayer, Timer>();

        #endregion Fields

        #region Configuration

        private class Configuration
        {
            [JsonProperty("Version")]
            public string Version { get; set; }

            [JsonProperty("Duration Until Kicking Player From Recycler For Inactivity")]
            public float DurationUntilKickingPlayerFromRecyclerForInactivity { get; set; }
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();
            _config = Config.ReadObject<Configuration>();

            if (string.Compare(_config.Version, Version.ToString()) < 0)
                UpdateConfig();

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

        private void UpdateConfig()
        {
            PrintWarning("Config changes detected! Updating...");

            Configuration defaultConfig = GetDefaultConfig();

            if (string.Compare(_config.Version, "1.0.0") < 0)
                _config = defaultConfig;

            PrintWarning("Config update complete! Updated from version " + _config.Version + " to " + Version.ToString());
            _config.Version = Version.ToString();
        }

        private Configuration GetDefaultConfig()
        {
            return new Configuration
            {
                Version = Version.ToString(),
                DurationUntilKickingPlayerFromRecyclerForInactivity = 300f
            };
        }

        #endregion Configuration

        #region Oxide Hooks

        private void Init()
        {
            _plugin = this;
            PermissionUtil.RegisterPermissions();
        }

        private void Unload()
        {
            foreach (var timer in _afkTimers.Values)
            {
                if (timer != null)
                    timer.Destroy();
            }

            _config = null;
            _plugin = null;
        }

        private object OnItemRecycle(Item item, Recycler recycler)
        {
            if (item == null || recycler == null)
                return null;

            BasePlayer player = GetRecyclingPlayer(recycler);
            if (player != null && !PermissionUtil.HasPermission(player, PermissionUtil.IGNORE))
                ResetAfkTimer(player);

            return null;
        }

        private void OnLootEntity(BasePlayer player, Recycler recycler)
        {
            if (player == null || recycler == null)
                return;

            if (!PermissionUtil.HasPermission(player, PermissionUtil.IGNORE))
                StartAfkTimer(player, recycler);
        }

        private void OnLootEntityEnd(BasePlayer player, Recycler recycler)
        {
            if (player == null || recycler == null)
                return;

            if (!PermissionUtil.HasPermission(player, PermissionUtil.IGNORE))
                StopAfkTimer(player);
        }

        #endregion Oxide Hooks

        #region Afk Timer Management

        private void StartAfkTimer(BasePlayer player, Recycler recycler)
        {
            if (_afkTimers.ContainsKey(player))
                _afkTimers[player].Destroy();

            _afkTimers[player] = timer.Once(_config.DurationUntilKickingPlayerFromRecyclerForInactivity, () =>
            {
                player.EndLooting();
                _afkTimers.Remove(player);
            });
        }

        private void ResetAfkTimer(BasePlayer player)
        {
            if (_afkTimers.ContainsKey(player))
            {
                _afkTimers[player].Reset(_config.DurationUntilKickingPlayerFromRecyclerForInactivity);
            }
        }

        private void StopAfkTimer(BasePlayer player)
        {
            if (_afkTimers.ContainsKey(player))
            {
                _afkTimers[player].Destroy();
                _afkTimers.Remove(player);
            }
        }

        #endregion Afk Timer Management

        #region Recycling Player Retrieval

        private BasePlayer GetRecyclingPlayer(Recycler recycler)
        {
            List<BasePlayer> nearbyPlayers = Facepunch.Pool.Get<List<BasePlayer>>();
            Vis.Entities(recycler.transform.position, 3f, nearbyPlayers, 131072, QueryTriggerInteraction.Collide);

            foreach (BasePlayer player in nearbyPlayers)
            {
                if (player.IsAlive() && !player.IsSleeping() && player.inventory.loot.entitySource == recycler)
                {
                    Facepunch.Pool.FreeUnmanaged(ref nearbyPlayers);
                    return player;
                }
            }

            Facepunch.Pool.FreeUnmanaged(ref nearbyPlayers);
            return null;
        }

        #endregion Recycling Player Retrieval

        #region Permissions

        private static class PermissionUtil
        {
            public const string IGNORE = "noafkrecycler.ignore";
            private static readonly List<string> _permissions = new List<string>
            {
                IGNORE,
            };

            public static void RegisterPermissions()
            {
                foreach (var permission in _permissions)
                {
                    _plugin.permission.RegisterPermission(permission, _plugin);
                }
            }

            public static bool HasPermission(BasePlayer player, string permissionName)
            {
                return _plugin.permission.UserHasPermission(player.UserIDString, permissionName);
            }
        }

        #endregion Permissions
    }
}