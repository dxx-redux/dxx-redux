using System;
using System.IO;

namespace D1U.Game
{
    /// <summary>
    /// Host-configured network game options (Descent's netgame_info, the
    /// anarchy-applicable subset). Set by the host at launch and by clients from
    /// the Welcome handshake; gameplay reads the shared <see cref="Active"/>
    /// instance, always gated behind multiplayer so single-player is untouched.
    /// Serialized field-by-field into the Welcome packet — keep Serialize and
    /// Deserialize in lockstep, and bump NetSession.ProtocolVersion on any change.
    /// </summary>
    public sealed class NetGameRules
    {
        // ---- AllowedItems bit layout (multi.h NETFLAG_DO*), 13 powerup classes.
        public const int BitLaser = 0, BitQuad = 1, BitVulcan = 2, BitVulcanAmmo = 3,
                         BitSpread = 4, BitPlasma = 5, BitFusion = 6, BitHoming = 7,
                         BitProx = 8, BitSmart = 9, BitMega = 10, BitCloak = 11, BitInvul = 12;
        public const int AllowedItemBits = 13;
        public const ushort AllItemsMask = 0x1FFF; // all 13 on (NETFLAG_DOPOWERUP)

        // ---- A: core match rules
        public string GameName = "";
        public byte Difficulty = 1;      // 0..4 (Trainee..Insane)
        public byte ReactorLife;         // 0..10 -> ×5 min invuln; 0 = indestructible (port)
        public byte MaxTime;             // 0..10 -> ×5 min play clock; 0 = no limit
        public byte KillGoal;            // 0..10 -> ×10 frags to win; 0 = none

        // ---- B: weapons & items
        public ushort AllowedItems = AllItemsMask;
        public byte PrimaryDup = 1;      // 1..8 (extra primaries factor)
        public byte SecondaryDup = 1;    // 1..8
        public byte SecondaryCap;        // 0 uncapped, 1 max six, 2 max two
        public bool LowVulcan;
        public byte VulcanStyle = 1;     // GAUSS_STYLE_DEPLETING
        public bool AckAckMode;
        public byte BombFlareTimer = 2;  // 0 never,1 3s,2 5s,3 10s,4 always
        public byte HomingRate = 25;     // 20..30

        // ---- C: spawn / respawn
        public byte SpawnStyle;          // 0 none, 1 half-sec, 2 two-sec, 3 preview
        public bool NewSpawnAlgo;
        public bool RespawnConcs;

        // ---- E: access / cosmetic / network
        public byte MaxPlayers = 8;      // 2..8
        public bool ClosedGame;          // no joining after start
        public bool BrightShips = true;
        public bool ShowEnemyNames;
        public bool ReducedFlash;

        /// <summary>Shared instance gameplay reads (MP only). Replaced wholesale
        /// by the host at launch and by clients on Welcome.</summary>
        public static NetGameRules Active = new NetGameRules();

        // ---- derived helpers
        public int KillGoalCount => KillGoal * 10;
        public int MaxTimeSeconds => MaxTime * 5 * 60;
        public float ReactorInvulnSeconds => ReactorLife * 5 * 60;
        public bool ReactorDestructible => ReactorLife > 0; // 0 = classic-port indestructible
        public bool ItemAllowed(int bit) => (AllowedItems & (1 << bit)) != 0;

        /// <summary>Reborn invulnerability seconds for the spawn style
        /// (game.c FakingInvul ~0.5 / 2.0). Preview (3) grants no invuln here —
        /// the dead-cam preview itself is not ported yet.</summary>
        public float SpawnInvulnSeconds => SpawnStyle switch { 1 => 0.5f, 2 => 2f, _ => 0f };

        /// <summary>Bomb-flare-detonates-mega window in seconds; 0 = never,
        /// large = always (collide.c IMMORTAL_TIME).</summary>
        public float BombFlareSeconds => BombFlareTimer switch
        { 0 => 0f, 1 => 3f, 2 => 5f, 3 => 10f, _ => 1e9f };

        public NetGameRules Clone() => (NetGameRules)MemberwiseClone();

        public void Serialize(BinaryWriter bw)
        {
            bw.Write(GameName ?? "");
            bw.Write(Difficulty);
            bw.Write(ReactorLife);
            bw.Write(MaxTime);
            bw.Write(KillGoal);
            bw.Write(AllowedItems);
            bw.Write(PrimaryDup);
            bw.Write(SecondaryDup);
            bw.Write(SecondaryCap);
            bw.Write(LowVulcan);
            bw.Write(VulcanStyle);
            bw.Write(AckAckMode);
            bw.Write(BombFlareTimer);
            bw.Write(HomingRate);
            bw.Write(SpawnStyle);
            bw.Write(NewSpawnAlgo);
            bw.Write(RespawnConcs);
            bw.Write(MaxPlayers);
            bw.Write(ClosedGame);
            bw.Write(BrightShips);
            bw.Write(ShowEnemyNames);
            bw.Write(ReducedFlash);
        }

        public static NetGameRules Deserialize(BinaryReader br)
        {
            var r = new NetGameRules
            {
                GameName = br.ReadString(),
                Difficulty = Clamp(br.ReadByte(), 0, 4),
                ReactorLife = Clamp(br.ReadByte(), 0, 10),
                MaxTime = Clamp(br.ReadByte(), 0, 10),
                KillGoal = Clamp(br.ReadByte(), 0, 10),
                AllowedItems = (ushort)(br.ReadUInt16() & AllItemsMask),
                PrimaryDup = Clamp(br.ReadByte(), 1, 8),
                SecondaryDup = Clamp(br.ReadByte(), 1, 8),
                SecondaryCap = Clamp(br.ReadByte(), 0, 2),
                LowVulcan = br.ReadBoolean(),
                VulcanStyle = Clamp(br.ReadByte(), 0, 3),
                AckAckMode = br.ReadBoolean(),
                BombFlareTimer = Clamp(br.ReadByte(), 0, 4),
                HomingRate = Clamp(br.ReadByte(), 20, 30),
                SpawnStyle = Clamp(br.ReadByte(), 0, 3),
                NewSpawnAlgo = br.ReadBoolean(),
                RespawnConcs = br.ReadBoolean(),
                MaxPlayers = Clamp(br.ReadByte(), 2, 8),
                ClosedGame = br.ReadBoolean(),
                BrightShips = br.ReadBoolean(),
                ShowEnemyNames = br.ReadBoolean(),
                ReducedFlash = br.ReadBoolean(),
            };
            return r;
        }

        static byte Clamp(int v, int lo, int hi) => (byte)Math.Min(hi, Math.Max(lo, v));

        /// <summary>Powerup id (POW_*) -> AllowedItems bit, or -1 if the item is
        /// never gated (energy, shield, keys). Mirrors multi_allow_powerup_mask.</summary>
        public static int PowerupBit(int powId) => powId switch
        {
            0 or 25 => BitInvul,   // extra life / invulnerability
            3 => BitLaser,
            12 => BitQuad,
            13 => BitVulcan,
            22 => BitVulcanAmmo,
            14 => BitSpread,
            15 => BitPlasma,
            16 => BitFusion,
            18 or 19 => BitHoming, // homing ammo 1 / 4
            17 => BitProx,
            20 => BitSmart,
            21 => BitMega,
            23 => BitCloak,
            _ => -1,
        };
    }
}
