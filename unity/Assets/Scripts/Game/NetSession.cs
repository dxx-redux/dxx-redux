using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;

namespace D1U.Game
{
    public sealed class NetPlayer
    {
        public int Slot;
        public string Name = "";
        public int Frags;
        public Vector3 Pos;
        public Mat3 Orient = Mat3.Identity;
        public Vector3 Vel;
        public int Segnum = -1;      // filled in by the presentation layer
        public double LastHeard;
        public IPEndPoint EndPoint;  // host side only
    }

    /// <summary>
    /// UDP anarchy session (net_udp.c in spirit, much simplified): the host
    /// owns the player list and relays every message to the other clients;
    /// damage is victim-authoritative — each machine simulates incoming fire
    /// against its own ship and announces its own death. Single-threaded:
    /// call Update(now) every frame from the main loop.
    /// </summary>
    public sealed class NetSession : IDisposable
    {
        public const int DefaultPort = 28342;
        public const byte ProtocolVersion = 2;
        public const int MaxPlayers = 8;

        const byte MsgJoin = 1, MsgWelcome = 2, MsgJoined = 3, MsgLeft = 4,
                   MsgState = 5, MsgFire = 6, MsgDied = 7, MsgDoor = 8,
                   MsgPickup = 9, MsgPing = 10, MsgReject = 11, MsgEggs = 12;
        const double StateInterval = 1.0 / 15;
        const double TimeoutSeconds = 8.0;
        const int MaxEggsPerPacket = 32;

        readonly UdpClient udp;
        readonly IPEndPoint hostEndPoint; // client side
        readonly Dictionary<int, NetPlayer> players = new Dictionary<int, NetPlayer>();

        public bool IsHost { get; }
        public int LocalSlot { get; private set; } = -1;
        public bool Connected => LocalSlot >= 0;
        public bool Failed { get; private set; }
        public string MissionKey { get; private set; } = "";
        public int LevelNumber { get; private set; } = 1;
        public int LocalFrags;
        public string LocalName = "PILOT";
        /// <summary>Remote players by slot (never contains the local player).</summary>
        public IReadOnlyDictionary<int, NetPlayer> Players => players;

        public event Action<NetPlayer> PlayerJoined;
        public event Action<int> PlayerLeft;
        public event Action JoinAccepted;
        public event Action<string> JoinFailed;
        /// <summary>(slot, weaponId, pos, dir) — replicate the projectile locally.</summary>
        public event Action<int, byte, Vector3, Vector3> RemoteFire;
        /// <summary>(victimSlot, killerSlot) — scoreboard already updated.</summary>
        public event Action<int, int> RemoteDied;
        public event Action<int> RemoteDoor;
        /// <summary>(netObjectId) — a NET object id: a level-baked object index,
        /// or an egg netId from RemoteEggs; mapping it to a local object is the
        /// caller's job.</summary>
        public event Action<int> RemotePickup;
        /// <summary>(senderSlot, eggs) — spawn the sender's death drops locally
        /// under the shared (netId, subId) ids so pickups resolve everywhere.</summary>
        public event Action<int, (int netId, byte subId, Vector3 pos, Vector3 vel)[]> RemoteEggs;

        double lastStateSend, lastJoinSend, lastPingSend, joinStarted, hostLastHeard;
        Vector3 pendingPos, pendingVel;
        Mat3 pendingOrient = Mat3.Identity;
        bool statePending;
        int nextSlot = 1;

        NetSession(bool isHost, UdpClient udp, IPEndPoint hostEp)
        {
            IsHost = isHost;
            this.udp = udp;
            hostEndPoint = hostEp;
            // Windows: don't let ICMP port-unreachable kill the socket
            try
            {
                udp.Client.IOControl((IOControlCode)(-1744830452), new byte[] { 0 }, null);
            }
            catch
            {
            }
        }

        public static NetSession Host(string missionKey, int levelNumber, string name, int port = DefaultPort)
        {
            var udp = new UdpClient(port);
            return new NetSession(true, udp, null)
            {
                MissionKey = missionKey,
                LevelNumber = levelNumber,
                LocalName = name,
                LocalSlot = 0,
            };
        }

        public static NetSession Join(string address, string name, int port = DefaultPort)
        {
            var ep = new IPEndPoint(IPAddress.Parse(address), port);
            return new NetSession(false, new UdpClient(0), ep) { LocalName = name };
        }

        public void Dispose()
        {
            try
            {
                if (Connected)
                {
                    var p = NewPacket(MsgLeft);
                    p.bw.Write((byte)LocalSlot);
                    Broadcast(p);
                    Broadcast(p);
                }
            }
            catch
            {
            }
            udp.Close();
        }

        // ------------------------------------------------------------------
        // per-frame pump

        public void Update(double now)
        {
            if (Failed)
                return;

            if (!IsHost && !Connected)
            {
                if (joinStarted == 0)
                    joinStarted = now;
                if (now - lastJoinSend > 1.0)
                {
                    lastJoinSend = now;
                    var p = NewPacket(MsgJoin);
                    p.bw.Write(ProtocolVersion);
                    p.bw.Write(LocalName);
                    SendTo(p, hostEndPoint);
                }
                if (now - joinStarted > 6.0)
                {
                    Fail("no answer from host");
                    return;
                }
            }

            Receive(now);

            if (Connected && statePending && now - lastStateSend > StateInterval)
            {
                lastStateSend = now;
                statePending = false;
                var p = NewPacket(MsgState);
                p.bw.Write((byte)LocalSlot);
                SaveIo.Write(p.bw, pendingPos);
                SaveIo.Write(p.bw, pendingOrient);
                SaveIo.Write(p.bw, pendingVel);
                Broadcast(p);
            }
            if (Connected && now - lastPingSend > 2.0)
            {
                lastPingSend = now;
                var p = NewPacket(MsgPing);
                p.bw.Write((byte)LocalSlot);
                Broadcast(p);
            }

            // timeouts
            if (IsHost)
            {
                foreach (var slot in players.Keys.ToList())
                {
                    if (now - players[slot].LastHeard <= TimeoutSeconds)
                        continue;
                    players.Remove(slot);
                    var p = NewPacket(MsgLeft);
                    p.bw.Write((byte)slot);
                    Broadcast(p);
                    PlayerLeft?.Invoke(slot);
                }
            }
            else if (Connected && hostLastHeard > 0 && now - hostLastHeard > TimeoutSeconds)
            {
                Fail("connection to host lost");
            }
        }

        void Fail(string why)
        {
            Failed = true;
            JoinFailed?.Invoke(why);
        }

        // ------------------------------------------------------------------
        // outgoing game events

        /// <summary>Buffered; actually sent at 15 Hz from Update.</summary>
        public void SendState(Vector3 pos, in Mat3 orient, Vector3 vel)
        {
            pendingPos = pos;
            pendingOrient = orient;
            pendingVel = vel;
            statePending = true;
        }

        public void SendFire(byte weaponId, Vector3 pos, Vector3 dir)
        {
            if (!Connected)
                return;
            var p = NewPacket(MsgFire);
            p.bw.Write((byte)LocalSlot);
            p.bw.Write(weaponId);
            SaveIo.Write(p.bw, pos);
            SaveIo.Write(p.bw, dir);
            Broadcast(p);
        }

        public void SendDied(int killerSlot)
        {
            if (!Connected)
                return;
            ApplyDeathToScore(LocalSlot, killerSlot);
            var p = NewPacket(MsgDied);
            p.bw.Write((byte)LocalSlot);
            p.bw.Write((sbyte)killerSlot);
            Broadcast(p);
        }

        public void SendDoor(int wallIndex)
        {
            if (!Connected)
                return;
            var p = NewPacket(MsgDoor);
            p.bw.Write((byte)LocalSlot);
            p.bw.Write(wallIndex);
            Broadcast(p);
        }

        /// <summary>The id is a NET object id — a level-baked object index, or
        /// an egg netId announced via SendEggs; mapping is the caller's job.</summary>
        public void SendPickup(int objectId)
        {
            if (!Connected)
                return;
            var p = NewPacket(MsgPickup);
            p.bw.Write((byte)LocalSlot);
            p.bw.Write(objectId);
            Broadcast(p);
        }

        /// <summary>Announce death drops so they exist on every peer under the
        /// shared (netId, subId) ids; split into packets of 32 entries.</summary>
        public void SendEggs((int netId, byte subId, Vector3 pos, Vector3 vel)[] eggs)
        {
            if (!Connected || eggs == null || eggs.Length == 0)
                return;
            for (int start = 0; start < eggs.Length; start += MaxEggsPerPacket)
            {
                int count = Math.Min(MaxEggsPerPacket, eggs.Length - start);
                var p = NewPacket(MsgEggs);
                p.bw.Write((byte)LocalSlot);
                p.bw.Write((byte)count);
                for (int i = 0; i < count; i++)
                {
                    var egg = eggs[start + i];
                    p.bw.Write(egg.netId);
                    p.bw.Write(egg.subId);
                    SaveIo.Write(p.bw, egg.pos);
                    SaveIo.Write(p.bw, egg.vel);
                }
                Broadcast(p);
            }
        }

        // ------------------------------------------------------------------
        // receive path

        void Receive(double now)
        {
            while (true)
            {
                byte[] data;
                var from = new IPEndPoint(IPAddress.Any, 0);
                try
                {
                    if (udp.Available <= 0)
                        break;
                    data = udp.Receive(ref from);
                }
                catch (SocketException)
                {
                    break;
                }
                catch (ObjectDisposedException)
                {
                    break;
                }
                if (data.Length < 2 || data[0] != 0xD1)
                    continue;
                try
                {
                    var br = new BinaryReader(new MemoryStream(data));
                    br.ReadByte();
                    byte type = br.ReadByte();
                    if (IsHost)
                        HandleAsHost(type, br, data, from, now);
                    else
                        HandleAsClient(type, br, now);
                }
                catch (Exception e) when (e is EndOfStreamException || e is IOException
                                          || e is FormatException || e is ArgumentException)
                {
                    // truncated/garbage datagram: drop just this packet
                    // (FormatException = BinaryReader.ReadString on a bad
                    // 7-bit length) and keep pumping the rest of the queue
                }
            }
        }

        void HandleAsHost(byte type, BinaryReader br, byte[] raw, IPEndPoint from, double now)
        {
            if (type == MsgJoin)
            {
                byte version = br.ReadByte();
                string name = br.ReadString();
                var existing = players.Values.FirstOrDefault(pl => from.Equals(pl.EndPoint));
                if (existing != null)
                {
                    SendWelcome(existing); // duplicate knock: re-welcome
                    return;
                }
                if (version != ProtocolVersion || players.Count + 1 >= MaxPlayers)
                {
                    var reject = NewPacket(MsgReject);
                    reject.bw.Write(version != ProtocolVersion ? "version mismatch" : "game is full");
                    SendTo(reject, from);
                    return;
                }
                var joined = new NetPlayer
                {
                    Slot = nextSlot++,
                    Name = name,
                    EndPoint = from,
                    LastHeard = now,
                };
                players[joined.Slot] = joined;
                SendWelcome(joined);
                var announce = NewPacket(MsgJoined);
                announce.bw.Write((byte)joined.Slot);
                announce.bw.Write(joined.Name);
                BroadcastExcept(announce, from);
                PlayerJoined?.Invoke(joined);
                return;
            }

            // everything else must come from a known player
            var sender = players.Values.FirstOrDefault(pl => from.Equals(pl.EndPoint));
            if (sender == null)
                return;
            sender.LastHeard = now;

            if (type == MsgLeft)
            {
                // payload parsed (validated) before the relay goes out; the
                // host drops the leaver itself instead of waiting for the
                // 8s timeout to notice
                int slot = br.ReadByte();
                RelayRaw(raw, from);
                if (players.Remove(slot))
                    PlayerLeft?.Invoke(slot);
                return;
            }

            // parse before relaying: a truncated payload throws out of
            // ProcessCommon and dies here instead of poisoning every client,
            // and unknown message types are ignored, never forwarded
            if (ProcessCommon(type, br, now))
                RelayRaw(raw, from);
        }

        void HandleAsClient(byte type, BinaryReader br, double now)
        {
            hostLastHeard = now;
            switch (type)
            {
                case MsgWelcome:
                {
                    if (Connected)
                        return;
                    LocalSlot = br.ReadByte();
                    MissionKey = br.ReadString();
                    LevelNumber = br.ReadInt32();
                    ObjectSystem.Difficulty = Math.Min(4, Math.Max(0, (int)br.ReadByte()));
                    int count = br.ReadByte();
                    for (int i = 0; i < count; i++)
                    {
                        var player = new NetPlayer
                        {
                            Slot = br.ReadByte(),
                            Name = br.ReadString(),
                            Frags = br.ReadInt32(),
                        };
                        if (player.Slot != LocalSlot)
                        {
                            players[player.Slot] = player;
                            player.LastHeard = now;
                        }
                    }
                    JoinAccepted?.Invoke();
                    return;
                }
                case MsgReject:
                    Fail(br.ReadString());
                    return;
                case MsgJoined:
                {
                    var player = new NetPlayer { Slot = br.ReadByte(), Name = br.ReadString(), LastHeard = now };
                    if (player.Slot != LocalSlot)
                    {
                        players[player.Slot] = player;
                        PlayerJoined?.Invoke(player);
                    }
                    return;
                }
                case MsgLeft:
                {
                    int slot = br.ReadByte();
                    if (players.Remove(slot))
                        PlayerLeft?.Invoke(slot);
                    return;
                }
                default:
                    ProcessCommon(type, br, now);
                    return;
            }
        }

        /// <summary>Gameplay messages common to host and clients. Each case reads
        /// its whole payload before acting, so a malformed packet throws before it
        /// has side effects — and before the host relays it. Returns false for
        /// unknown types so the host never forwards what it cannot parse.</summary>
        bool ProcessCommon(byte type, BinaryReader br, double now)
        {
            switch (type)
            {
                case MsgState:
                {
                    int slot = br.ReadByte();
                    var pos = SaveIo.ReadVec(br);
                    var orient = SaveIo.ReadMat(br);
                    var vel = SaveIo.ReadVec(br);
                    if (slot != LocalSlot)
                    {
                        var player = GetOrTrack(slot, now);
                        player.Pos = pos;
                        player.Orient = orient;
                        player.Vel = vel;
                        player.LastHeard = now;
                    }
                    return true;
                }
                case MsgFire:
                {
                    int slot = br.ReadByte();
                    byte weapon = br.ReadByte();
                    var pos = SaveIo.ReadVec(br);
                    var dir = SaveIo.ReadVec(br);
                    if (slot != LocalSlot)
                        RemoteFire?.Invoke(slot, weapon, pos, dir);
                    return true;
                }
                case MsgDied:
                {
                    int victim = br.ReadByte();
                    int killer = br.ReadSByte();
                    if (victim != LocalSlot)
                    {
                        ApplyDeathToScore(victim, killer);
                        RemoteDied?.Invoke(victim, killer);
                    }
                    return true;
                }
                case MsgDoor:
                {
                    br.ReadByte(); // sender slot
                    int wallIndex = br.ReadInt32();
                    RemoteDoor?.Invoke(wallIndex);
                    return true;
                }
                case MsgPickup:
                {
                    br.ReadByte();
                    int netObjectId = br.ReadInt32();
                    RemotePickup?.Invoke(netObjectId);
                    return true;
                }
                case MsgEggs:
                {
                    int slot = br.ReadByte();
                    int count = br.ReadByte();
                    var eggs = new (int netId, byte subId, Vector3 pos, Vector3 vel)[count];
                    for (int i = 0; i < count; i++)
                    {
                        int netId = br.ReadInt32();
                        byte subId = br.ReadByte();
                        var pos = SaveIo.ReadVec(br);
                        var vel = SaveIo.ReadVec(br);
                        eggs[i] = (netId, subId, pos, vel);
                    }
                    if (slot != LocalSlot)
                        RemoteEggs?.Invoke(slot, eggs);
                    return true;
                }
                case MsgPing:
                {
                    int slot = br.ReadByte();
                    if (slot != LocalSlot)
                        GetOrTrack(slot, now).LastHeard = now;
                    return true;
                }
                default:
                    return false;
            }
        }

        NetPlayer GetOrTrack(int slot, double now)
        {
            if (!players.TryGetValue(slot, out var player))
            {
                players[slot] = player = new NetPlayer { Slot = slot, Name = $"PLAYER {slot + 1}", LastHeard = now };
                PlayerJoined?.Invoke(player);
            }
            return player;
        }

        /// <summary>Anarchy scoring: kill +1 to the killer, suicide -1 to the victim.</summary>
        void ApplyDeathToScore(int victim, int killer)
        {
            if (killer >= 0 && killer != victim)
            {
                if (killer == LocalSlot)
                    LocalFrags++;
                else if (players.TryGetValue(killer, out var k))
                    k.Frags++;
            }
            else
            {
                if (victim == LocalSlot)
                    LocalFrags--;
                else if (players.TryGetValue(victim, out var v))
                    v.Frags--;
            }
        }

        // ------------------------------------------------------------------
        // plumbing

        void SendWelcome(NetPlayer to)
        {
            var p = NewPacket(MsgWelcome);
            p.bw.Write((byte)to.Slot);
            p.bw.Write(MissionKey);
            p.bw.Write(LevelNumber);
            p.bw.Write((byte)Math.Min(4, Math.Max(0, ObjectSystem.Difficulty)));
            var everyone = players.Values.Where(pl => pl.Slot != to.Slot).ToList();
            p.bw.Write((byte)(everyone.Count + 1));
            p.bw.Write((byte)LocalSlot); // the host itself
            p.bw.Write(LocalName);
            p.bw.Write(LocalFrags);
            foreach (var pl in everyone)
            {
                p.bw.Write((byte)pl.Slot);
                p.bw.Write(pl.Name);
                p.bw.Write(pl.Frags);
            }
            SendTo(p, to.EndPoint);
        }

        (MemoryStream ms, BinaryWriter bw) NewPacket(byte type)
        {
            var ms = new MemoryStream(96);
            var bw = new BinaryWriter(ms);
            bw.Write((byte)0xD1);
            bw.Write(type);
            return (ms, bw);
        }

        void SendTo((MemoryStream ms, BinaryWriter bw) p, IPEndPoint ep)
        {
            var buf = p.ms.ToArray();
            try
            {
                udp.Send(buf, buf.Length, ep);
            }
            catch (SocketException)
            {
            }
        }

        /// <summary>Host: to every client. Client: to the host (which relays).</summary>
        void Broadcast((MemoryStream ms, BinaryWriter bw) p) => BroadcastExcept(p, null);

        void BroadcastExcept((MemoryStream ms, BinaryWriter bw) p, IPEndPoint except)
        {
            if (IsHost)
            {
                foreach (var pl in players.Values)
                    if (pl.EndPoint != null && !pl.EndPoint.Equals(except))
                        SendTo(p, pl.EndPoint);
            }
            else
            {
                SendTo(p, hostEndPoint);
            }
        }

        void RelayRaw(byte[] raw, IPEndPoint from)
        {
            foreach (var pl in players.Values)
            {
                if (pl.EndPoint == null || pl.EndPoint.Equals(from))
                    continue;
                try
                {
                    udp.Send(raw, raw.Length, pl.EndPoint);
                }
                catch (SocketException)
                {
                }
            }
        }
    }
}
