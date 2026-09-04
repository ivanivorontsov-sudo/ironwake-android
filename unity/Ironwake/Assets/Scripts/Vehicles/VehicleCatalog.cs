using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Ironwake.Net;

namespace Ironwake.Vehicles
{
    public enum VehicleKind
    {
        Tank,
        Apc,
        Car,
        Heli,
        Plane
    }

    [Serializable]
    public class VehicleDef
    {
        public string id;
        public string displayName;
        public string classLabel;
        public string description;
        public VehicleKind kind;
        public float maxSpeed = 12f;
        public float accel = 8f;
        public float turnRate = 48f;
        public float fireCooldown = 1.4f;
        public float turretYawSpeed = 55f;
        public float shellSpeed = 80f;
        public float shellDamage = 110f;
        public float classSpeedMul = 1f;
        public float cruiseAltitude = 0f;
        public float groundClearance = 2.2f;
        public int costSteel;
        public int costIntel;
        public bool starter;
        public float hp = 1000f;
        public Color previewColor = new Color(0.76f, 0.71f, 0.54f);
    }

    /// <summary>
    /// Garage catalog — prefers GET /catalog/vehicles from live server;
    /// falls back to embedded defaults matching server ids.
    /// Spawns primitive prefabs by class tank/apc/car/heli/plane via VehicleController.
    /// </summary>
    [CreateAssetMenu(menuName = "Ironwake/Vehicle Catalog", fileName = "VehicleCatalog")]
    public sealed class VehicleCatalog : ScriptableObject
    {
        public List<VehicleDef> vehicles = new List<VehicleDef>();

        public VehicleDef Get(string id)
        {
            if (vehicles == null) return null;
            for (int i = 0; i < vehicles.Count; i++)
                if (vehicles[i] != null && vehicles[i].id == id) return vehicles[i];
            return vehicles.Count > 0 ? vehicles[0] : null;
        }

        public static VehicleCatalog CreateDefaultRuntime()
        {
            var cat = CreateInstance<VehicleCatalog>();
            cat.vehicles = BuildFallbackList();
            return cat;
        }

        public IEnumerator FetchFromServer(string baseUrl, Action<bool> done = null)
        {
            string url = (baseUrl ?? "http://biker9td.beget.tech").TrimEnd('/') + "/catalog/vehicles";
            using var req = UnityWebRequest.Get(url);
            req.timeout = 10;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.LogWarning($"[VehicleCatalog] fetch fail: {req.error}");
                if (vehicles == null || vehicles.Count == 0)
                    vehicles = BuildFallbackList();
                done?.Invoke(false);
                yield break;
            }
            try
            {
                vehicles = ParseCatalogJson(req.downloadHandler.text);
                if (vehicles.Count == 0) vehicles = BuildFallbackList();
                done?.Invoke(true);
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[VehicleCatalog] parse: {ex.Message}");
                vehicles = BuildFallbackList();
                done?.Invoke(false);
            }
        }

        public static List<VehicleDef> ParseCatalogJson(string json)
        {
            var list = new List<VehicleDef>();
            string arr = IronwakeClient.ExtractArrayBlock(json, "vehicles") ?? json;
            foreach (var obj in IronwakeClient.SplitJsonObjects(arr))
            {
                var d = new VehicleDef();
                IronwakeClient.TryExtractString(obj, "id", out d.id);
                IronwakeClient.TryExtractString(obj, "name", out d.displayName);
                if (string.IsNullOrEmpty(d.displayName))
                    IronwakeClient.TryExtractString(obj, "displayName", out d.displayName);
                string cls = null;
                IronwakeClient.TryExtractString(obj, "class", out cls);
                d.kind = ParseKind(cls);
                d.classLabel = ClassLabelRu(d.kind);
                IronwakeClient.TryExtractBool(obj, "starter", out d.starter);
                if (IronwakeClient.TryExtractNumber(obj, "hp", out float hp)) d.hp = hp;
                if (IronwakeClient.TryExtractNumber(obj, "speed", out float sp)) d.maxSpeed = sp * 0.35f; // world scale
                // cost nested
                string cost = IronwakeClient.ExtractObjectBlock(obj, "cost");
                if (!string.IsNullOrEmpty(cost))
                {
                    if (IronwakeClient.TryExtractNumber(cost, "steel", out float st)) d.costSteel = Mathf.RoundToInt(st);
                    if (IronwakeClient.TryExtractNumber(cost, "intel", out float it)) d.costIntel = Mathf.RoundToInt(it);
                }
                string gun = IronwakeClient.ExtractObjectBlock(obj, "gun");
                if (!string.IsNullOrEmpty(gun))
                {
                    if (IronwakeClient.TryExtractNumber(gun, "reload", out float rel)) d.fireCooldown = rel;
                    if (IronwakeClient.TryExtractNumber(gun, "damage", out float dmg)) d.shellDamage = dmg;
                }
                ApplyClassDefaults(d);
                d.description = $"{d.classLabel}. HP {d.hp:0} · скорость {d.maxSpeed:0.#}"
                                + (d.starter ? " · стартовая" : $" · {d.costSteel} стали / {d.costIntel} разведки");
                d.previewColor = ColorForKind(d.kind);
                if (!string.IsNullOrEmpty(d.id)) list.Add(d);
            }
            return list;
        }

        public static VehicleKind ParseKind(string cls)
        {
            if (string.IsNullOrEmpty(cls)) return VehicleKind.Tank;
            switch (cls.Trim().ToLowerInvariant())
            {
                case "apc": return VehicleKind.Apc;
                case "car": case "jeep": return VehicleKind.Car;
                case "heli": case "helicopter": return VehicleKind.Heli;
                case "plane": case "aircraft": return VehicleKind.Plane;
                default: return VehicleKind.Tank;
            }
        }

        public static string ClassLabelRu(VehicleKind k)
        {
            switch (k)
            {
                case VehicleKind.Apc: return "БТР";
                case VehicleKind.Car: return "Авто";
                case VehicleKind.Heli: return "Вертолёт";
                case VehicleKind.Plane: return "Самолёт";
                default: return "Танк";
            }
        }

        static void ApplyClassDefaults(VehicleDef d)
        {
            switch (d.kind)
            {
                case VehicleKind.Apc:
                    d.accel = 12f; d.turnRate = 70f; d.turretYawSpeed = 70f;
                    d.classSpeedMul = 1.15f; d.groundClearance = 1.8f;
                    if (d.fireCooldown <= 0) d.fireCooldown = 0.35f;
                    break;
                case VehicleKind.Car:
                    d.accel = 16f; d.turnRate = 85f; d.turretYawSpeed = 90f;
                    d.classSpeedMul = 1.3f; d.groundClearance = 1.2f;
                    if (d.fireCooldown <= 0) d.fireCooldown = 0.12f;
                    break;
                case VehicleKind.Heli:
                    d.accel = 14f; d.turnRate = 90f; d.turretYawSpeed = 80f;
                    d.classSpeedMul = 1.3f; d.cruiseAltitude = 8f; d.groundClearance = 8f;
                    if (d.fireCooldown <= 0) d.fireCooldown = 0.2f;
                    break;
                case VehicleKind.Plane:
                    d.accel = 20f; d.turnRate = 55f; d.turretYawSpeed = 60f;
                    d.classSpeedMul = 1.6f; d.cruiseAltitude = 14f; d.groundClearance = 14f;
                    d.shellSpeed = 120f;
                    if (d.fireCooldown <= 0) d.fireCooldown = 0.1f;
                    break;
                default:
                    d.accel = 8f; d.turnRate = 42f; d.turretYawSpeed = 55f;
                    d.classSpeedMul = 1f; d.groundClearance = 2.2f;
                    if (d.fireCooldown <= 0) d.fireCooldown = 1.5f;
                    break;
            }
        }

        static Color ColorForKind(VehicleKind k)
        {
            switch (k)
            {
                case VehicleKind.Apc: return new Color(0.45f, 0.5f, 0.38f);
                case VehicleKind.Car: return new Color(0.35f, 0.38f, 0.3f);
                case VehicleKind.Heli: return new Color(0.35f, 0.42f, 0.32f);
                case VehicleKind.Plane: return new Color(0.4f, 0.38f, 0.45f);
                default: return new Color(0.76f, 0.71f, 0.54f);
            }
        }

        public static List<VehicleDef> BuildFallbackList()
        {
            // Mirrors live /catalog/vehicles ids so offline Hangar still works.
            return new List<VehicleDef>
            {
                Make("k72-ural", "K-72 Ural", VehicleKind.Tank, 1800, 14, 0, 0, true, 7.5f, 420),
                Make("m-raptor", "M-Raptor", VehicleKind.Tank, 1650, 16, 0, 0, true, 6.8f, 390),
                Make("t-84m-vanguard", "T-84M Vanguard", VehicleKind.Tank, 2100, 13, 45000, 220, false, 7.2f, 480),
                Make("leopard-x", "Leopard-X", VehicleKind.Tank, 1950, 17, 52000, 260, false, 6.2f, 460),
                Make("btr-iron", "BTR-Iron", VehicleKind.Apc, 950, 22, 18000, 80, false, 0.35f, 85),
                Make("wolf-jeep", "Wolf Jeep", VehicleKind.Car, 420, 28, 8000, 30, false, 0.12f, 40),
                Make("ka-scythe", "Ka-Scythe", VehicleKind.Heli, 1100, 55, 62000, 340, false, 0.2f, 120),
                Make("ah-spectre", "AH-Spectre", VehicleKind.Heli, 1050, 58, 68000, 380, false, 0.08f, 55),
                Make("su-talon", "Su-Talon", VehicleKind.Plane, 880, 140, 90000, 520, false, 0.1f, 140),
                Make("a10-hammer", "A-10 Hammer", VehicleKind.Plane, 1200, 110, 85000, 480, false, 0.05f, 95),
            };
        }

        static VehicleDef Make(string id, string name, VehicleKind kind, float hp, float speed,
            int steel, int intel, bool starter, float reload, float dmg)
        {
            var d = new VehicleDef
            {
                id = id,
                displayName = name,
                kind = kind,
                classLabel = ClassLabelRu(kind),
                hp = hp,
                maxSpeed = speed * 0.35f,
                costSteel = steel,
                costIntel = intel,
                starter = starter,
                fireCooldown = reload,
                shellDamage = dmg,
                previewColor = ColorForKind(kind)
            };
            ApplyClassDefaults(d);
            d.description = $"{d.classLabel}. HP {hp:0}"
                            + (starter ? " · стартовая" : $" · {steel} стали / {intel} разведки");
            return d;
        }
    }
}
