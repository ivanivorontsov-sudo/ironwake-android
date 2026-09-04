using System.Collections.Generic;
using UnityEngine;
using Ironwake.Net;
using Ironwake.Vehicles;

namespace Ironwake.Sim
{
    /// <summary>Fills a local battle with bot units that chase / circle / fire.</summary>
    public sealed class LocalBotAI
    {
        readonly LocalBattleSim _sim;
        readonly Dictionary<string, BotBrain> _brains = new Dictionary<string, BotBrain>();

        public LocalBotAI(LocalBattleSim sim) => _sim = sim;

        public void FillRoom(VehicleCatalog catalog, int blueBots, int redBots, List<string> vehiclePool = null)
        {
            string[] pool = vehiclePool != null && vehiclePool.Count > 0
                ? vehiclePool.ToArray()
                : new[] { "k72-ural", "m-raptor", "btr-iron", "wolf-jeep", "ka-scythe" };

            for (int i = 0; i < blueBots; i++)
                SpawnBot(catalog, "blue", pool[i % pool.Length], i);
            for (int i = 0; i < redBots; i++)
                SpawnBot(catalog, "red", pool[(i + 2) % pool.Length], i + 10);
        }

        void SpawnBot(VehicleCatalog catalog, string team, string vehicleId, int slot)
        {
            var def = catalog != null ? catalog.Get(vehicleId) : null;
            float side = team == "blue" ? 1f : -1f;
            float x = (slot % 3 - 1) * 12f + Random.Range(-3f, 3f);
            float z = side * (28f + (slot % 4) * 8f) + Random.Range(-4f, 4f);
            float y = def != null
                ? (def.kind == VehicleKind.Heli || def.kind == VehicleKind.Plane
                    ? def.cruiseAltitude
                    : def.groundClearance * 0.15f)
                : 1f;
            float yaw = team == "blue" ? Mathf.PI : 0f;

            string id = $"bot_{team}_{slot}";
            var unit = _sim.AddUnit(id, team, $"BOT-{slot}", vehicleId, new Vector3(x, y, z), yaw, def);
            _brains[id] = new BotBrain
            {
                UnitId = id,
                Aggro = Random.Range(0.4f, 1f),
                CircleDir = Random.value > 0.5f ? 1f : -1f,
                RetargetTimer = Random.Range(0.5f, 2f)
            };
            unit.IsBot = true;
        }

        public void Tick(float dt)
        {
            foreach (var kv in _brains)
            {
                var brain = kv.Value;
                var self = _sim.GetUnit(brain.UnitId);
                if (self == null || !self.Alive || self.Spectator) continue;

                brain.RetargetTimer -= dt;
                if (brain.TargetId == null || brain.RetargetTimer <= 0f || !_sim.IsAliveEnemy(brain.TargetId, self.Team))
                {
                    brain.TargetId = _sim.FindNearestEnemy(self.Id, self.Team);
                    brain.RetargetTimer = Random.Range(1.2f, 3.5f);
                }

                var input = new InputFrame();
                var target = brain.TargetId != null ? _sim.GetUnit(brain.TargetId) : null;
                if (target == null || !target.Alive)
                {
                    input.Throttle = 0.2f;
                    input.Steer = Mathf.Sin(Time.time * 0.4f + brain.Aggro) * 0.4f;
                    self.PendingInput = input;
                    continue;
                }

                Vector3 to = target.Position - self.Position;
                to.y = 0f;
                float dist = to.magnitude;
                float desiredYaw = Mathf.Atan2(to.x, to.z);
                float yawErr = Mathf.DeltaAngle(self.Yaw * Mathf.Rad2Deg, desiredYaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;

                // Aim at target
                Vector3 aim = target.Position + Vector3.up * 1.2f - (self.Position + Vector3.up * 1.4f);
                float aimYaw = Mathf.Atan2(aim.x, aim.z);
                float aimPitch = -Mathf.Asin(Mathf.Clamp(aim.normalized.y, -0.9f, 0.9f));
                input.AimYaw = aimYaw;
                input.AimPitch = aimPitch;
                input.TurretYaw = aimYaw;
                input.GunPitch = aimPitch;

                float engage = 28f + brain.Aggro * 18f;
                if (dist > engage)
                {
                    input.Throttle = 0.85f;
                    input.Steer = Mathf.Clamp(yawErr * 1.8f, -1f, 1f);
                }
                else if (dist < 14f)
                {
                    input.Throttle = -0.25f;
                    input.Steer = brain.CircleDir * 0.7f;
                }
                else
                {
                    input.Throttle = 0.35f;
                    input.Steer = brain.CircleDir * 0.55f + Mathf.Clamp(yawErr * 0.5f, -0.4f, 0.4f);
                }

                float aimErr = Mathf.Abs(Mathf.DeltaAngle(self.TurretYaw * Mathf.Rad2Deg, aimYaw * Mathf.Rad2Deg));
                if (dist < engage + 10f && aimErr < 12f && self.Modules.CanFire && self.ReloadTimer <= 0f)
                    input.Fire = Random.value < 0.08f + brain.Aggro * 0.12f;

                self.PendingInput = input;
            }
        }

        class BotBrain
        {
            public string UnitId;
            public string TargetId;
            public float Aggro;
            public float CircleDir;
            public float RetargetTimer;
        }
    }
}
