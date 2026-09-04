using System;
using System.Collections.Generic;
using UnityEngine;
using Ironwake.Net;
using Ironwake.Vehicles;

namespace Ironwake.Sim
{
    /// <summary>
    /// Device-authoritative battle at 20 Hz: movement, ballistics, modules, bots,
    /// fire DoT, cook-off, no respawn, spectator, match end.
    /// Server is NOT used for combat — only meta (POST /match) after results.
    /// </summary>
    public sealed class LocalBattleSim : MonoBehaviour
    {
        public const float TickHz = 20f;
        public static LocalBattleSim Instance { get; private set; }

        [SerializeField] float arenaHalf = 110f;
        [SerializeField] float matchTimeLimit = 360f;
        [SerializeField] int blueBots = 3;
        [SerializeField] int redBots = 4;

        readonly Dictionary<string, SimUnit> _units = new Dictionary<string, SimUnit>();
        readonly List<SimShell> _shells = new List<SimShell>();
        readonly List<GameEvent> _events = new List<GameEvent>();
        readonly List<ProjectileSnapshot> _projSnap = new List<ProjectileSnapshot>();

        LocalBotAI _bots;
        VehicleCatalog _catalog;
        float _accum;
        float _matchTimer;
        long _tick;
        bool _ended;
        string _winner;
        string _localPlayerId;
        bool _running;

        public bool Running => _running;
        public bool Ended => _ended;
        public string Winner => _winner;
        public string LocalPlayerId => _localPlayerId;
        public event Action<RoomStatePayload> OnState;
        public event Action<GameEvent> OnGameEvent;
        public event Action<string> OnMatchEnd;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void StartLocalBattle(VehicleCatalog catalog, string playerId, string callsign, string vehicleId, string team = "blue")
        {
            _catalog = catalog ?? VehicleCatalog.CreateDefaultRuntime();
            _localPlayerId = playerId;
            _units.Clear();
            _shells.Clear();
            _events.Clear();
            _ended = false;
            _winner = null;
            _tick = 0;
            _matchTimer = matchTimeLimit;
            _accum = 0f;
            _running = true;

            var def = _catalog.Get(vehicleId);
            float y = GroundY(def);
            float side = team == "red" ? -1f : 1f;
            Vector3 spawn = new Vector3(side * 8f, y, side * 22f);
            float yaw = team == "red" ? 0f : Mathf.PI;
            var player = AddUnit(playerId, team, callsign, vehicleId, spawn, yaw, def);
            player.IsBot = false;

            _bots = new LocalBotAI(this);
            // Fill both teams with bots (player already occupies one slot on their team)
            _bots.FillRoom(_catalog, blueBots, redBots);

            Emit(new GameEvent { Type = "join", Id = playerId, Callsign = callsign, Team = team, VehicleId = vehicleId, T = NowMs() });
            PushState();
            Debug.Log($"[LocalBattleSim] started local={playerId} vehicle={vehicleId} bots={_units.Count - 1}");
        }

        public SimUnit AddUnit(string id, string team, string callsign, string vehicleId, Vector3 pos, float yaw, VehicleDef def)
        {
            var u = new SimUnit
            {
                Id = id,
                Team = team,
                Callsign = callsign,
                VehicleId = vehicleId ?? "k72-ural",
                Position = pos,
                Yaw = yaw,
                TurretYaw = yaw,
                GunPitch = 0f,
                Def = def,
                Alive = true
            };
            float hp = def != null ? def.hp : 1000f;
            u.Modules.Reset(hp);
            _units[id] = u;
            return u;
        }

        public SimUnit GetUnit(string id) =>
            string.IsNullOrEmpty(id) ? null : (_units.TryGetValue(id, out var u) ? u : null);

        public bool IsAliveEnemy(string id, string myTeam)
        {
            var u = GetUnit(id);
            return u != null && u.Alive && !u.Spectator && u.Team != myTeam;
        }

        public string FindNearestEnemy(string selfId, string team)
        {
            var self = GetUnit(selfId);
            if (self == null) return null;
            string best = null;
            float bestD = float.MaxValue;
            foreach (var u in _units.Values)
            {
                if (!u.Alive || u.Spectator || u.Team == team || u.Id == selfId) continue;
                float d = (u.Position - self.Position).sqrMagnitude;
                if (d < bestD) { bestD = d; best = u.Id; }
            }
            return best;
        }

        public void SetLocalInput(InputFrame frame)
        {
            var u = GetUnit(_localPlayerId);
            if (u == null || !u.Alive || u.Spectator) return;
            u.PendingInput = frame;
        }

        void Update()
        {
            if (!_running || _ended) return;
            _accum += Time.deltaTime;
            float step = 1f / TickHz;
            int guard = 0;
            while (_accum >= step && guard++ < 4)
            {
                _accum -= step;
                Tick(step);
            }
        }

        void Tick(float dt)
        {
            _tick++;
            _matchTimer -= dt;
            _events.Clear();
            _bots?.Tick(dt);

            foreach (var u in _units.Values)
            {
                if (!u.Alive || u.Spectator) continue;
                IntegrateUnit(u, dt);
            }

            IntegrateShells(dt);
            CheckMatchEnd();
            PushState();
        }

        void IntegrateUnit(SimUnit u, float dt)
        {
            var input = u.PendingInput;
            // Clear fire pulse after consuming
            var cleared = input;
            cleared.Fire = false;
            u.PendingInput = cleared;

            u.Modules.TickFire(dt, out bool cook);
            if (cook)
            {
                Emit(new GameEvent { Type = "cookoff", Id = u.Id, T = NowMs() });
                KillUnit(u, u.Id);
                return;
            }

            float aimMul = u.Modules.AimMul();
            float yawSpeed = (u.Def != null ? u.Def.turretYawSpeed : 55f) * Mathf.Deg2Rad * aimMul;
            // VehicleController / bots always push absolute aim each frame while active.
            float targetTurret = input.AimYaw;
            float targetPitch = input.AimPitch;
            if (Mathf.Abs(input.TurretYaw) > 0f) targetTurret = input.TurretYaw;
            if (Mathf.Abs(input.GunPitch) > 0f) targetPitch = input.GunPitch;
            u.TurretYaw = Mathf.LerpAngle(u.TurretYaw * Mathf.Rad2Deg, targetTurret * Mathf.Rad2Deg, 1f - Mathf.Exp(-yawSpeed * dt)) * Mathf.Deg2Rad;
            float pMin = -12f * Mathf.Deg2Rad;
            float pMax = (u.Def != null && (u.Def.kind == VehicleKind.Plane || u.Def.kind == VehicleKind.Heli) ? 35f : 18f) * Mathf.Deg2Rad;
            u.GunPitch = Mathf.Clamp(Mathf.Lerp(u.GunPitch, targetPitch, 1f - Mathf.Exp(-yawSpeed * 0.85f * dt)), pMin, pMax);

            float mob = u.Modules.MobilityMul();
            float speed = (u.Def != null ? u.Def.maxSpeed : 12f) * mob;
            float turn = (u.Def != null ? u.Def.turnRate : 48f) * Mathf.Deg2Rad * mob;
            float thr = Mathf.Clamp(input.Throttle, -0.45f, 1f);
            if (input.Brake) thr *= 0.3f;

            if (!u.Modules.Immobilized && Mathf.Abs(input.Steer) > 0.02f)
                u.Yaw += input.Steer * turn * dt * (thr >= 0f ? 1f : -1f);

            Vector3 forward = new Vector3(Mathf.Sin(u.Yaw), 0f, Mathf.Cos(u.Yaw));
            float wish = thr * speed;
            u.Velocity = Vector3.Lerp(u.Velocity, forward * wish, 1f - Mathf.Exp(-(u.Def != null ? u.Def.accel : 8f) * 0.35f * dt));
            if (input.Brake) u.Velocity = Vector3.Lerp(u.Velocity, Vector3.zero, 6f * dt);

            Vector3 pos = u.Position + u.Velocity * dt;
            pos.x = Mathf.Clamp(pos.x, -arenaHalf, arenaHalf);
            pos.z = Mathf.Clamp(pos.z, -arenaHalf, arenaHalf);

            if (u.Def != null && (u.Def.kind == VehicleKind.Heli || u.Def.kind == VehicleKind.Plane))
                pos.y = Mathf.Lerp(pos.y, u.Def.cruiseAltitude, dt * 1.4f);
            else
                pos.y = GroundY(u.Def);
            u.Position = pos;
            u.Moving = u.Velocity.sqrMagnitude > 0.4f;

            if (u.ReloadTimer > 0f) u.ReloadTimer -= dt;

            if (input.Fire && u.Modules.CanFire && u.ReloadTimer <= 0f)
                FireShell(u);
        }

        void FireShell(SimUnit u)
        {
            float cd = u.Def != null ? u.Def.fireCooldown : 1.4f;
            u.ReloadTimer = cd;
            float muzzle = 2.8f;
            Vector3 dir = AimDirection(u.TurretYaw, u.GunPitch);
            Vector3 origin = u.Position + Vector3.up * 1.5f + dir * muzzle;
            float shellSpeed = u.Def != null ? u.Def.shellSpeed : 80f;
            float dmg = u.Def != null ? u.Def.shellDamage : 110f;
            string pid = $"p{_tick}_{u.Id}_{_shells.Count}";
            _shells.Add(new SimShell
            {
                Id = pid,
                OwnerId = u.Id,
                Team = u.Team,
                Position = origin,
                Velocity = dir * shellSpeed,
                Damage = dmg,
                Life = 3.5f
            });
            Emit(new GameEvent
            {
                Type = "shot",
                Id = u.Id,
                ProjectileId = pid,
                T = NowMs()
            });
        }

        static Vector3 AimDirection(float yaw, float pitch)
        {
            float cp = Mathf.Cos(pitch);
            return new Vector3(Mathf.Sin(yaw) * cp, Mathf.Sin(pitch), Mathf.Cos(yaw) * cp).normalized;
        }

        void IntegrateShells(float dt)
        {
            const float gravity = 9.81f * 0.35f;
            for (int i = _shells.Count - 1; i >= 0; i--)
            {
                var s = _shells[i];
                s.Velocity += Vector3.down * gravity * dt;
                Vector3 next = s.Position + s.Velocity * dt;
                s.Life -= dt;

                bool hit = false;
                foreach (var u in _units.Values)
                {
                    if (!u.Alive || u.Spectator || u.Id == s.OwnerId || u.Team == s.Team) continue;
                    Vector3 c = u.Position + Vector3.up * 1.1f;
                    float rad = 2.4f;
                    if (SegmentHitsSphere(s.Position, next, c, rad))
                    {
                        Vector3 inbound = (next - s.Position).normalized;
                        // Facing from attacker's approach relative to target hull
                        string facing = u.Modules.ResolveFacing(-inbound, u.Yaw);
                        var hr = u.Modules.ApplyShot(s.Damage, facing);
                        Emit(new GameEvent
                        {
                            Type = "hit",
                            Id = u.Id,
                            By = s.OwnerId,
                            Module = hr.Module,
                            ProjectileId = s.Id,
                            Facing = facing,
                            Bounce = hr.Bounce,
                            Pen = hr.Pen,
                            Hp = u.Modules.HullHp,
                            T = NowMs()
                        });
                        if (hr.ModuleBroken)
                        {
                            Emit(new GameEvent
                            {
                                Type = "module_break",
                                Id = u.Id,
                                Module = hr.Module,
                                By = s.OwnerId,
                                T = NowMs()
                            });
                        }
                        if (u.Modules.OnFire)
                            Emit(new GameEvent { Type = "fire_start", Id = u.Id, T = NowMs() });

                        if (u.Modules.HullHp <= 0f || u.Modules.CookedOff)
                            KillUnit(u, s.OwnerId);

                        hit = true;
                        break;
                    }
                }

                if (!hit && next.y <= 0.05f)
                {
                    // ground impact
                    hit = true;
                }

                s.Position = next;
                if (hit || s.Life <= 0f || Mathf.Abs(s.Position.x) > arenaHalf + 40f || Mathf.Abs(s.Position.z) > arenaHalf + 40f)
                    _shells.RemoveAt(i);
                else
                    _shells[i] = s;
            }
        }

        static bool SegmentHitsSphere(Vector3 a, Vector3 b, Vector3 center, float radius)
        {
            Vector3 ab = b - a;
            float ab2 = ab.sqrMagnitude;
            if (ab2 < 1e-6f) return (a - center).sqrMagnitude <= radius * radius;
            float t = Mathf.Clamp01(Vector3.Dot(center - a, ab) / ab2);
            Vector3 p = a + ab * t;
            return (p - center).sqrMagnitude <= radius * radius;
        }

        void KillUnit(SimUnit u, string killerId)
        {
            if (!u.Alive) return;
            u.Alive = false;
            u.Spectator = true;
            u.Modules.HullHp = 0f;
            Emit(new GameEvent { Type = "kill", Id = u.Id, By = killerId, T = NowMs() });
            Emit(new GameEvent { Type = "spectator", Id = u.Id, By = killerId, T = NowMs() });
        }

        void CheckMatchEnd()
        {
            if (_ended) return;
            int blue = 0, red = 0;
            foreach (var u in _units.Values)
            {
                if (!u.Alive || u.Spectator) continue;
                if (u.Team == "blue") blue++;
                else red++;
            }

            if (blue == 0 || red == 0 || _matchTimer <= 0f)
            {
                _ended = true;
                _running = false;
                if (_matchTimer <= 0f && blue == red) _winner = null;
                else if (blue == 0) _winner = "red";
                else if (red == 0) _winner = "blue";
                else _winner = blue > red ? "blue" : "red";

                Emit(new GameEvent { Type = "end", Winner = _winner, T = NowMs() });
                OnMatchEnd?.Invoke(_winner);
                Debug.Log($"[LocalBattleSim] MATCH END winner={_winner}");
            }
        }

        void PushState()
        {
            var units = new UnitSnapshot[_units.Count];
            int i = 0;
            foreach (var u in _units.Values)
                units[i++] = u.ToSnapshot();

            _projSnap.Clear();
            foreach (var s in _shells)
            {
                _projSnap.Add(new ProjectileSnapshot
                {
                    Id = s.Id,
                    OwnerId = s.OwnerId,
                    X = s.Position.x,
                    Y = s.Position.y,
                    Z = s.Position.z
                });
            }

            var payload = new RoomStatePayload
            {
                T = NowMs(),
                Units = units,
                Projectiles = _projSnap.ToArray(),
                Events = _events.ToArray(),
                Ended = _ended,
                Winner = _winner
            };

            OnState?.Invoke(payload);
            foreach (var ev in _events)
                OnGameEvent?.Invoke(ev);
        }

        void Emit(GameEvent ev) => _events.Add(ev);

        long NowMs() => _tick * (long)(1000f / TickHz);

        static float GroundY(VehicleDef def) =>
            def != null ? Mathf.Max(0.4f, def.groundClearance * 0.15f) : 1f;

        public MatchResult BuildResult()
        {
            var local = GetUnit(_localPlayerId);
            bool won = _winner != null && local != null && local.Team == _winner;
            return new MatchResult
            {
                UserId = _localPlayerId,
                VehicleId = local?.VehicleId,
                Team = local?.Team,
                Winner = _winner,
                Victory = won,
                Survived = local != null && local.Alive,
                DurationSec = matchTimeLimit - Mathf.Max(0f, _matchTimer),
                Mode = "local_laststand"
            };
        }
    }

    public sealed class SimUnit
    {
        public string Id, Team, Callsign, VehicleId;
        public Vector3 Position;
        public Vector3 Velocity;
        public float Yaw, TurretYaw, GunPitch;
        public VehicleDef Def;
        public ModuleSystem Modules = new ModuleSystem();
        public InputFrame PendingInput;
        public float ReloadTimer;
        public bool Alive = true;
        public bool Spectator;
        public bool IsBot;
        public bool Moving;

        public UnitSnapshot ToSnapshot()
        {
            return new UnitSnapshot
            {
                Id = Id,
                Team = Team,
                Callsign = Callsign,
                VehicleId = VehicleId,
                X = Position.x,
                Y = Position.y,
                Z = Position.z,
                Yaw = Yaw,
                TurretYaw = TurretYaw,
                GunPitch = GunPitch,
                Hp = Modules.HullHp,
                MaxHp = Modules.MaxHullHp,
                Alive = Alive,
                Spectator = Spectator,
                OnFire = Modules.OnFire,
                Immobilized = Modules.Immobilized,
                CanFire = Modules.CanFire,
                OpticsBroken = Modules.OpticsBroken,
                Fuel = Modules.Modules.Fuel,
                Ammo = Modules.Modules.Ammo,
                Modules = Modules.Modules
            };
        }
    }

    public struct SimShell
    {
        public string Id, OwnerId, Team;
        public Vector3 Position, Velocity;
        public float Damage, Life;
    }

    public sealed class MatchResult
    {
        public string UserId, VehicleId, Team, Winner, Mode;
        public bool Victory, Survived;
        public float DurationSec;
        public int Kills;
    }
}
