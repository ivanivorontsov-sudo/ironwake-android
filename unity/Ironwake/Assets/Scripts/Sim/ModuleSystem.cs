using System.Collections.Generic;
using UnityEngine;
using Ironwake.Net;

namespace Ironwake.Sim
{
    /// <summary>
    /// Local module HP + simplified penetration / facing.
    /// Keys match PROTOCOL: hull_f/s/r, turret, gun, engine, ammo, track_l/r, fuel, optics.
    /// </summary>
    public sealed class ModuleSystem
    {
        public readonly ModuleMap Modules = new ModuleMap();
        public bool OnFire;
        public float FireTimer;
        public bool CookedOff;
        public bool Immobilized;
        public bool CanFire = true;
        public bool OpticsBroken;
        public float HullHp = 1000f;
        public float MaxHullHp = 1000f;

        static readonly string[] HitOrderFront = { "gun", "optics", "turret", "hull_f", "ammo", "engine", "fuel", "track_l", "track_r" };
        static readonly string[] HitOrderSide = { "track_l", "track_r", "hull_s", "ammo", "fuel", "turret", "engine", "gun", "optics" };
        static readonly string[] HitOrderRear = { "engine", "fuel", "hull_r", "ammo", "turret", "track_l", "track_r", "gun", "optics" };

        public void Reset(float maxHp)
        {
            MaxHullHp = Mathf.Max(100f, maxHp);
            HullHp = MaxHullHp;
            foreach (var k in ModuleMap.Keys) Modules.Set(k, 1f);
            OnFire = false;
            FireTimer = 0f;
            CookedOff = false;
            Immobilized = false;
            CanFire = true;
            OpticsBroken = false;
        }

        public string ResolveFacing(Vector3 attackerDir, float targetYawRad)
        {
            Vector3 forward = new Vector3(Mathf.Sin(targetYawRad), 0f, Mathf.Cos(targetYawRad));
            Vector3 right = new Vector3(forward.z, 0f, -forward.x);
            Vector3 flat = attackerDir; flat.y = 0f;
            if (flat.sqrMagnitude < 0.0001f) return "front";
            flat.Normalize();
            float f = Vector3.Dot(flat, forward);
            float r = Vector3.Dot(flat, right);
            if (f > 0.45f) return "front";
            if (f < -0.45f) return "rear";
            return r >= 0f ? "side" : "side";
        }

        public HitResult ApplyShot(float baseDamage, string facing, float penChance = 0.75f)
        {
            var result = new HitResult { Facing = facing };
            float armorMul = facing == "front" ? 0.55f : (facing == "rear" ? 1.35f : 0.9f);
            bool pen = Random.value < penChance * (facing == "rear" ? 1.2f : (facing == "front" ? 0.7f : 1f));
            result.Pen = pen;
            result.Bounce = !pen && facing == "front" && Random.value < 0.35f;
            if (result.Bounce)
            {
                result.Damage = 0f;
                return result;
            }

            float dmg = baseDamage * armorMul * (pen ? 1f : 0.35f);
            result.Damage = dmg;
            HullHp = Mathf.Max(0f, HullHp - dmg);

            string module = PickModule(facing);
            result.Module = module;
            float before = Modules.Get(module);
            float moduleLoss = pen ? Random.Range(0.25f, 0.55f) : Random.Range(0.08f, 0.2f);
            Modules.Set(module, before - moduleLoss);
            result.ModuleBroken = before > 0.05f && Modules.Get(module) <= 0.05f;

            ApplyModuleEffects();
            if (module == "ammo" && Modules.Ammo < 0.15f && Random.value < 0.4f)
                StartFire(2.5f);
            if (module == "fuel" && Modules.Fuel < 0.2f && Random.value < 0.5f)
                StartFire(3.5f);
            if (module == "engine" && Modules.Engine < 0.1f && Random.value < 0.35f)
                StartFire(2f);

            return result;
        }

        string PickModule(string facing)
        {
            string[] order = facing == "rear" ? HitOrderRear : (facing == "side" ? HitOrderSide : HitOrderFront);
            // Weighted toward early entries
            float r = Random.value;
            int idx = r < 0.45f ? 0 : (r < 0.7f ? 1 : (r < 0.85f ? 2 : Random.Range(0, order.Length)));
            idx = Mathf.Clamp(idx, 0, order.Length - 1);
            return order[idx];
        }

        public void StartFire(float duration)
        {
            OnFire = true;
            FireTimer = Mathf.Max(FireTimer, duration);
        }

        public void TickFire(float dt, out bool cookOffNow)
        {
            cookOffNow = false;
            if (!OnFire) return;
            FireTimer -= dt;
            HullHp = Mathf.Max(0f, HullHp - 18f * dt);
            Modules.Set("ammo", Modules.Ammo - 0.04f * dt);
            Modules.Set("fuel", Modules.Fuel - 0.03f * dt);
            if (Modules.Ammo < 0.08f && !CookedOff && Random.value < 0.012f * dt * 60f)
            {
                CookedOff = true;
                cookOffNow = true;
                HullHp = 0f;
                OnFire = false;
            }
            if (FireTimer <= 0f) OnFire = false;
            ApplyModuleEffects();
        }

        public void ApplyModuleEffects()
        {
            Immobilized = Modules.TrackL < 0.15f || Modules.TrackR < 0.15f || Modules.Engine < 0.05f;
            CanFire = Modules.Gun > 0.08f && Modules.Ammo > 0.05f && !CookedOff;
            OpticsBroken = Modules.Optics < 0.12f;
        }

        public float MobilityMul()
        {
            float m = 1f;
            if (Modules.Engine < 0.05f) m *= 0.12f;
            else if (Modules.Engine < 0.4f) m *= 0.55f;
            if (Modules.TrackL < 0.15f || Modules.TrackR < 0.15f) m *= 0.3f;
            else if (Modules.TrackL < 0.5f || Modules.TrackR < 0.5f) m *= 0.7f;
            return m;
        }

        public float AimMul() => OpticsBroken ? 0.35f : 1f;

        public Dictionary<string, float> ToDict() => Modules.ToDictionary();
    }

    public struct HitResult
    {
        public string Facing;
        public string Module;
        public float Damage;
        public bool Pen;
        public bool Bounce;
        public bool ModuleBroken;
    }
}
