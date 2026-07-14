using D1U.Game;
using UnityEngine;

namespace D1U.Presentation
{
    /// <summary>
    /// Host-side network game settings shown in the HOST setup dialog and
    /// persisted in PlayerPrefs (the port's stand-in for the pilot .ngp) so they
    /// survive relaunches. Wraps the wire/gameplay <see cref="NetGameRules"/>
    /// plus the two local-only bits (player name, host port).
    /// </summary>
    public sealed class NetGameConfig
    {
        public readonly NetGameRules Rules = new NetGameRules();
        public string PlayerName = "PILOT";
        public int Port = NetSession.DefaultPort;

        public NetGameConfig()
        {
            PlayerName = PlayerPrefs.GetString("d1u_net_name", "PILOT");
            Port = Mathf.Clamp(PlayerPrefs.GetInt("d1u_net_port", NetSession.DefaultPort), 1, 65535);
            var r = Rules;
            r.GameName = PlayerPrefs.GetString("d1u_net_game", "");
            r.Difficulty = B("d1u_net_diff", 1, 0, 4);
            r.KillGoal = B("d1u_net_killgoal", 0, 0, 10);
            r.MaxTime = B("d1u_net_maxtime", 0, 0, 10);
            r.ReactorLife = B("d1u_net_reactor", 0, 0, 10);
            r.MaxPlayers = B("d1u_net_maxplayers", 8, 2, 8);
            r.ClosedGame = PlayerPrefs.GetInt("d1u_net_closed", 0) != 0;
            r.AllowedItems = (ushort)(PlayerPrefs.GetInt("d1u_net_allowed", NetGameRules.AllItemsMask)
                                      & NetGameRules.AllItemsMask);
            r.PrimaryDup = B("d1u_net_primdup", 1, 1, 8);
            r.SecondaryDup = B("d1u_net_secdup", 1, 1, 8);
            r.SecondaryCap = B("d1u_net_seccap", 0, 0, 2);
            r.LowVulcan = PlayerPrefs.GetInt("d1u_net_lowvulcan", 0) != 0;
            r.VulcanStyle = B("d1u_net_vulcanstyle", 1, 0, 3);
            r.AckAckMode = PlayerPrefs.GetInt("d1u_net_ackack", 0) != 0;
            r.BombFlareTimer = B("d1u_net_bombflare", 2, 0, 4);
            r.HomingRate = B("d1u_net_homing", 25, 20, 30);
            r.SpawnStyle = B("d1u_net_spawnstyle", 0, 0, 3);
            r.NewSpawnAlgo = PlayerPrefs.GetInt("d1u_net_newspawn", 0) != 0;
            r.RespawnConcs = PlayerPrefs.GetInt("d1u_net_respawnconcs", 0) != 0;
            r.BrightShips = PlayerPrefs.GetInt("d1u_net_bright", 1) != 0;
            r.ShowEnemyNames = PlayerPrefs.GetInt("d1u_net_names", 0) != 0;
            r.ReducedFlash = PlayerPrefs.GetInt("d1u_net_reducedflash", 0) != 0;
        }

        static byte B(string key, int def, int lo, int hi)
            => (byte)Mathf.Clamp(PlayerPrefs.GetInt(key, def), lo, hi);

        public void Save()
        {
            var r = Rules;
            PlayerPrefs.SetString("d1u_net_name", string.IsNullOrWhiteSpace(PlayerName) ? "PILOT" : PlayerName);
            PlayerPrefs.SetInt("d1u_net_port", Mathf.Clamp(Port, 1, 65535));
            PlayerPrefs.SetString("d1u_net_game", r.GameName ?? "");
            PlayerPrefs.SetInt("d1u_net_diff", r.Difficulty);
            PlayerPrefs.SetInt("d1u_net_killgoal", r.KillGoal);
            PlayerPrefs.SetInt("d1u_net_maxtime", r.MaxTime);
            PlayerPrefs.SetInt("d1u_net_reactor", r.ReactorLife);
            PlayerPrefs.SetInt("d1u_net_maxplayers", r.MaxPlayers);
            PlayerPrefs.SetInt("d1u_net_closed", r.ClosedGame ? 1 : 0);
            PlayerPrefs.SetInt("d1u_net_allowed", r.AllowedItems);
            PlayerPrefs.SetInt("d1u_net_primdup", r.PrimaryDup);
            PlayerPrefs.SetInt("d1u_net_secdup", r.SecondaryDup);
            PlayerPrefs.SetInt("d1u_net_seccap", r.SecondaryCap);
            PlayerPrefs.SetInt("d1u_net_lowvulcan", r.LowVulcan ? 1 : 0);
            PlayerPrefs.SetInt("d1u_net_vulcanstyle", r.VulcanStyle);
            PlayerPrefs.SetInt("d1u_net_ackack", r.AckAckMode ? 1 : 0);
            PlayerPrefs.SetInt("d1u_net_bombflare", r.BombFlareTimer);
            PlayerPrefs.SetInt("d1u_net_homing", r.HomingRate);
            PlayerPrefs.SetInt("d1u_net_spawnstyle", r.SpawnStyle);
            PlayerPrefs.SetInt("d1u_net_newspawn", r.NewSpawnAlgo ? 1 : 0);
            PlayerPrefs.SetInt("d1u_net_respawnconcs", r.RespawnConcs ? 1 : 0);
            PlayerPrefs.SetInt("d1u_net_bright", r.BrightShips ? 1 : 0);
            PlayerPrefs.SetInt("d1u_net_names", r.ShowEnemyNames ? 1 : 0);
            PlayerPrefs.SetInt("d1u_net_reducedflash", r.ReducedFlash ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void ResetDefaults()
        {
            var d = new NetGameRules();
            Rules.Difficulty = d.Difficulty;
            Rules.KillGoal = d.KillGoal;
            Rules.MaxTime = d.MaxTime;
            Rules.ReactorLife = d.ReactorLife;
            Rules.MaxPlayers = d.MaxPlayers;
            Rules.ClosedGame = d.ClosedGame;
            Rules.AllowedItems = d.AllowedItems;
            Rules.PrimaryDup = d.PrimaryDup;
            Rules.SecondaryDup = d.SecondaryDup;
            Rules.SecondaryCap = d.SecondaryCap;
            Rules.LowVulcan = d.LowVulcan;
            Rules.VulcanStyle = d.VulcanStyle;
            Rules.AckAckMode = d.AckAckMode;
            Rules.BombFlareTimer = d.BombFlareTimer;
            Rules.HomingRate = d.HomingRate;
            Rules.SpawnStyle = d.SpawnStyle;
            Rules.NewSpawnAlgo = d.NewSpawnAlgo;
            Rules.RespawnConcs = d.RespawnConcs;
            Rules.BrightShips = d.BrightShips;
            Rules.ShowEnemyNames = d.ShowEnemyNames;
            Rules.ReducedFlash = d.ReducedFlash;
            Save();
        }
    }
}
