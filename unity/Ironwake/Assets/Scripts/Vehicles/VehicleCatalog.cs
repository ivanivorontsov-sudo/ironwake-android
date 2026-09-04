using System;
using System.Collections.Generic;
using UnityEngine;

namespace Ironwake.Vehicles
{
    public enum VehicleKind
    {
        Tank,
        Apc,
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
        public Color previewColor = new Color(0.76f, 0.71f, 0.54f);
    }

    /// <summary>
    /// Placeholder garage catalog — tank / APC / heli / plane.
    /// Replace meshes later with Asset Store packs listed in README.
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
            cat.vehicles = new List<VehicleDef>
            {
                new VehicleDef
                {
                    id = "k72-ural",
                    displayName = "К-72 Урал",
                    classLabel = "Танк",
                    description = "Тяжёлая машина. Инерция корпуса, лаг башни, камера из башни или сзади.",
                    kind = VehicleKind.Tank,
                    maxSpeed = 12f, accel = 8f, turnRate = 42f, fireCooldown = 1.5f,
                    classSpeedMul = 1f, groundClearance = 2.2f
                },
                new VehicleDef
                {
                    id = "btr-82",
                    displayName = "БТР-82",
                    classLabel = "БТР",
                    description = "Колёсный БТР. Быстрее танка, слабее броня (placeholder).",
                    kind = VehicleKind.Apc,
                    maxSpeed = 18f, accel = 12f, turnRate = 70f, fireCooldown = 0.6f,
                    shellDamage = 45f, classSpeedMul = 1.15f, groundClearance = 1.8f,
                    previewColor = new Color(0.45f, 0.5f, 0.38f)
                },
                new VehicleDef
                {
                    id = "ka-52",
                    displayName = "Ка-52",
                    classLabel = "Вертолёт",
                    description = "Ударный вертолёт. Крейсерская высота, высокая манёвренность.",
                    kind = VehicleKind.Heli,
                    maxSpeed = 28f, accel = 14f, turnRate = 90f, fireCooldown = 0.35f,
                    shellDamage = 55f, classSpeedMul = 1.3f, cruiseAltitude = 6.2f,
                    groundClearance = 6.2f, previewColor = new Color(0.35f, 0.42f, 0.32f)
                },
                new VehicleDef
                {
                    id = "su-25",
                    displayName = "Су-25",
                    classLabel = "Штурмовик",
                    description = "Штурмовка. Высокая скорость, широкая траектория.",
                    kind = VehicleKind.Plane,
                    maxSpeed = 48f, accel = 20f, turnRate = 55f, fireCooldown = 0.2f,
                    shellDamage = 70f, shellSpeed = 120f, classSpeedMul = 1.6f,
                    cruiseAltitude = 12f, groundClearance = 12f,
                    previewColor = new Color(0.4f, 0.38f, 0.45f)
                },
            };
            return cat;
        }
    }
}
