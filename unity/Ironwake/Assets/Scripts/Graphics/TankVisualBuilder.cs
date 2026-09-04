using UnityEngine;
using Ironwake.Vehicles;

namespace Ironwake.Graphics
{
    /// <summary>
    /// Assembles readable tank / APC / car / heli / plane from multiple primitives
    /// with dark military colors, decals, exhaust smoke.
    /// </summary>
    public static class TankVisualBuilder
    {
        public struct Rig
        {
            public Transform Root;
            public Transform Hull;
            public Transform Turret;
            public Transform Gun;
            public Transform Muzzle;
            public Transform[] Tracks;
            public ParticleSystem Exhaust;
        }

        public static Rig Build(VehicleDef def, Transform parent = null)
        {
            VehicleKind kind = def != null ? def.kind : VehicleKind.Tank;
            Color baseCol = MilitaryColor(kind, def);
            Color dark = baseCol * 0.72f;
            Color accent = new Color(0.15f, 0.16f, 0.12f);

            var root = new GameObject(def != null ? def.id + "_Visual" : "VehicleVisual");
            if (parent) root.transform.SetParent(parent, false);

            var rig = new Rig { Root = root.transform };

            switch (kind)
            {
                case VehicleKind.Apc: BuildApc(ref rig, baseCol, dark, accent); break;
                case VehicleKind.Car: BuildCar(ref rig, baseCol, dark, accent); break;
                case VehicleKind.Heli: BuildHeli(ref rig, baseCol, dark, accent); break;
                case VehicleKind.Plane: BuildPlane(ref rig, baseCol, dark, accent); break;
                default: BuildTank(ref rig, baseCol, dark, accent); break;
            }

            AddDecals(rig, kind, def);
            rig.Exhaust = AddExhaust(rig.Hull != null ? rig.Hull : rig.Root, kind);
            return rig;
        }

        static Color MilitaryColor(VehicleKind kind, VehicleDef def)
        {
            if (def != null && def.previewColor.maxColorComponent > 0.05f)
            {
                // Darken catalog colors toward military palette
                Color c = def.previewColor;
                return new Color(c.r * 0.55f + 0.12f, c.g * 0.55f + 0.14f, c.b * 0.4f + 0.1f);
            }
            switch (kind)
            {
                case VehicleKind.Apc: return new Color(0.28f, 0.32f, 0.22f);
                case VehicleKind.Car: return new Color(0.25f, 0.28f, 0.2f);
                case VehicleKind.Heli: return new Color(0.22f, 0.24f, 0.2f);
                case VehicleKind.Plane: return new Color(0.3f, 0.32f, 0.28f);
                default: return new Color(0.3f, 0.34f, 0.24f);
            }
        }

        static void BuildTank(ref Rig rig, Color col, Color dark, Color accent)
        {
            float clearance = 0.55f;
            rig.Hull = Part(rig.Root, "Hull", PrimitiveType.Cube, new Vector3(0f, clearance + 0.55f, 0f),
                new Vector3(2.6f, 1.1f, 5.0f), col);
            // Glacis wedge
            Part(rig.Hull, "Glacis", PrimitiveType.Cube, new Vector3(0f, 0.15f, 1.9f),
                new Vector3(0.92f, 0.35f, 0.55f), dark).localRotation = Quaternion.Euler(-28f, 0f, 0f);
            // Side skirts
            Part(rig.Hull, "SkirtL", PrimitiveType.Cube, new Vector3(-1.35f, -0.15f, 0f), new Vector3(0.12f, 0.55f, 4.6f), dark);
            Part(rig.Hull, "SkirtR", PrimitiveType.Cube, new Vector3(1.35f, -0.15f, 0f), new Vector3(0.12f, 0.55f, 4.6f), dark);

            var tracks = new Transform[2];
            tracks[0] = Part(rig.Hull, "TrackL", PrimitiveType.Cube, new Vector3(-1.2f, -0.55f, 0f), new Vector3(0.45f, 0.55f, 4.8f), accent);
            tracks[1] = Part(rig.Hull, "TrackR", PrimitiveType.Cube, new Vector3(1.2f, -0.55f, 0f), new Vector3(0.45f, 0.55f, 4.8f), accent);
            // Road wheels
            for (int i = 0; i < 5; i++)
            {
                float z = -1.8f + i * 0.9f;
                Part(tracks[0], "WheelL" + i, PrimitiveType.Cylinder, new Vector3(0f, -0.15f, z), new Vector3(0.5f, 0.18f, 0.5f), dark)
                    .localRotation = Quaternion.Euler(0f, 0f, 90f);
                Part(tracks[1], "WheelR" + i, PrimitiveType.Cylinder, new Vector3(0f, -0.15f, z), new Vector3(0.5f, 0.18f, 0.5f), dark)
                    .localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            rig.Tracks = tracks;

            rig.Turret = Part(rig.Root, "Turret", PrimitiveType.Cube, new Vector3(0f, clearance + 1.35f, 0.15f),
                new Vector3(1.9f, 0.75f, 2.1f), col * 0.95f);
            Part(rig.Turret, "Cupola", PrimitiveType.Cylinder, new Vector3(0.35f, 0.5f, -0.2f), new Vector3(0.55f, 0.2f, 0.55f), dark);

            rig.Gun = Part(rig.Turret, "Gun", PrimitiveType.Cube, new Vector3(0f, 0.05f, 1.9f),
                new Vector3(0.22f, 0.22f, 3.6f), accent);
            Part(rig.Gun, "Mantlet", PrimitiveType.Cube, new Vector3(0f, 0f, -0.35f), new Vector3(0.55f, 0.45f, 0.4f), dark);
            Part(rig.Gun, "MuzzleBrake", PrimitiveType.Cube, new Vector3(0f, 0f, 0.48f), new Vector3(0.32f, 0.28f, 0.25f), dark);

            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(rig.Gun, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            rig.Muzzle = muzzle.transform;
        }

        static void BuildApc(ref Rig rig, Color col, Color dark, Color accent)
        {
            float clearance = 0.5f;
            rig.Hull = Part(rig.Root, "Hull", PrimitiveType.Cube, new Vector3(0f, clearance + 0.55f, 0f),
                new Vector3(2.2f, 1.15f, 4.6f), col);
            Part(rig.Hull, "Cabin", PrimitiveType.Cube, new Vector3(0f, 0.55f, 1.2f), new Vector3(0.95f, 0.7f, 0.9f), dark);
            var tracks = new Transform[2];
            tracks[0] = Part(rig.Hull, "TrackL", PrimitiveType.Cube, new Vector3(-1.05f, -0.55f, 0f), new Vector3(0.4f, 0.5f, 4.4f), accent);
            tracks[1] = Part(rig.Hull, "TrackR", PrimitiveType.Cube, new Vector3(1.05f, -0.55f, 0f), new Vector3(0.4f, 0.5f, 4.4f), accent);
            rig.Tracks = tracks;

            rig.Turret = Part(rig.Root, "Turret", PrimitiveType.Cylinder, new Vector3(0f, clearance + 1.4f, 0.2f),
                new Vector3(1.2f, 0.35f, 1.2f), col * 0.92f);
            rig.Gun = Part(rig.Turret, "Gun", PrimitiveType.Cube, new Vector3(0f, 0.1f, 1.1f),
                new Vector3(0.14f, 0.14f, 2.2f), accent);
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(rig.Gun, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            rig.Muzzle = muzzle.transform;
        }

        static void BuildCar(ref Rig rig, Color col, Color dark, Color accent)
        {
            float clearance = 0.4f;
            rig.Hull = Part(rig.Root, "Hull", PrimitiveType.Cube, new Vector3(0f, clearance + 0.4f, 0f),
                new Vector3(1.7f, 0.75f, 3.4f), col);
            Part(rig.Hull, "Cabin", PrimitiveType.Cube, new Vector3(0f, 0.55f, 0.2f), new Vector3(0.85f, 0.7f, 1.1f), dark);
            // Wheels
            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0) ? -0.85f : 0.85f;
                float z = (i < 2) ? 1.1f : -1.1f;
                Part(rig.Hull, "Wheel" + i, PrimitiveType.Cylinder, new Vector3(x, -0.35f, z),
                    new Vector3(0.55f, 0.22f, 0.55f), accent).localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            rig.Turret = Part(rig.Root, "Turret", PrimitiveType.Cube, new Vector3(0f, clearance + 1.05f, -0.2f),
                new Vector3(0.8f, 0.35f, 0.8f), col * 0.9f);
            rig.Gun = Part(rig.Turret, "Gun", PrimitiveType.Cube, new Vector3(0f, 0.05f, 0.7f),
                new Vector3(0.1f, 0.1f, 1.3f), accent);
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(rig.Gun, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            rig.Muzzle = muzzle.transform;
        }

        static void BuildHeli(ref Rig rig, Color col, Color dark, Color accent)
        {
            rig.Hull = Part(rig.Root, "Hull", PrimitiveType.Cube, new Vector3(0f, 2.2f, 0f),
                new Vector3(1.5f, 1.0f, 5.4f), col);
            Part(rig.Hull, "Tail", PrimitiveType.Cube, new Vector3(0f, 0.2f, -3.2f), new Vector3(0.35f, 0.35f, 2.8f), dark);
            Part(rig.Hull, "TailFin", PrimitiveType.Cube, new Vector3(0f, 0.7f, -4.2f), new Vector3(0.12f, 1.2f, 0.6f), dark);
            var rotor = Part(rig.Hull, "Rotor", PrimitiveType.Cube, new Vector3(0f, 0.85f, 0.3f),
                new Vector3(6.5f, 0.08f, 0.35f), accent);
            rotor.gameObject.AddComponent<Spinner>().rpm = 220f;
            Part(rig.Hull, "Rotor2", PrimitiveType.Cube, new Vector3(0f, 0.85f, 0.3f),
                new Vector3(0.35f, 0.08f, 6.5f), accent).gameObject.AddComponent<Spinner>().rpm = 220f;
            Part(rig.Hull, "SkidL", PrimitiveType.Cube, new Vector3(-0.7f, -0.7f, 0f), new Vector3(0.12f, 0.12f, 3.2f), accent);
            Part(rig.Hull, "SkidR", PrimitiveType.Cube, new Vector3(0.7f, -0.7f, 0f), new Vector3(0.12f, 0.12f, 3.2f), accent);

            rig.Turret = Part(rig.Hull, "NoseTurret", PrimitiveType.Cube, new Vector3(0f, -0.2f, 2.2f),
                new Vector3(0.5f, 0.35f, 0.6f), dark);
            rig.Gun = Part(rig.Turret, "Gun", PrimitiveType.Cube, new Vector3(0f, 0f, 0.6f),
                new Vector3(0.1f, 0.1f, 1.4f), accent);
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(rig.Gun, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            rig.Muzzle = muzzle.transform;
        }

        static void BuildPlane(ref Rig rig, Color col, Color dark, Color accent)
        {
            rig.Hull = Part(rig.Root, "Hull", PrimitiveType.Cube, new Vector3(0f, 8f, 0f),
                new Vector3(1.2f, 0.7f, 6.8f), col);
            Part(rig.Hull, "Wing", PrimitiveType.Cube, new Vector3(0f, 0f, 0.2f), new Vector3(7.5f, 0.12f, 1.4f), dark);
            Part(rig.Hull, "TailWing", PrimitiveType.Cube, new Vector3(0f, 0.2f, -2.8f), new Vector3(3.2f, 0.1f, 0.7f), dark);
            Part(rig.Hull, "Fin", PrimitiveType.Cube, new Vector3(0f, 0.7f, -2.9f), new Vector3(0.12f, 1.3f, 0.8f), dark);
            Part(rig.Hull, "Intake", PrimitiveType.Cube, new Vector3(0f, -0.15f, 2.5f), new Vector3(0.7f, 0.4f, 0.8f), accent);

            rig.Turret = rig.Hull; // fixed gun
            rig.Gun = Part(rig.Hull, "Gun", PrimitiveType.Cube, new Vector3(0f, -0.1f, 3.2f),
                new Vector3(0.12f, 0.12f, 1.6f), accent);
            var muzzle = new GameObject("Muzzle");
            muzzle.transform.SetParent(rig.Gun, false);
            muzzle.transform.localPosition = new Vector3(0f, 0f, 0.55f);
            rig.Muzzle = muzzle.transform;
        }

        static void AddDecals(Rig rig, VehicleKind kind, VehicleDef def)
        {
            if (rig.Hull == null) return;
            // Number plate / chevron
            var plate = Part(rig.Hull, "DecalPlate", PrimitiveType.Quad, new Vector3(0f, 0.2f, -0.51f),
                new Vector3(0.8f, 0.35f, 1f), new Color(0.12f, 0.12f, 0.1f));
            plate.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Color stripe = kind == VehicleKind.Tank
                ? new Color(0.55f, 0.45f, 0.15f)
                : new Color(0.2f, 0.45f, 0.25f);
            var s1 = Part(rig.Hull, "Stripe", PrimitiveType.Quad, new Vector3(0f, 0.52f, 0f),
                new Vector3(0.15f, 0.02f, 0.9f), stripe);
            s1.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // Team-ish mark on turret
            if (rig.Turret != null && rig.Turret != rig.Hull)
            {
                var star = Part(rig.Turret, "Mark", PrimitiveType.Quad, new Vector3(0.51f, 0.1f, 0f),
                    new Vector3(0.35f, 0.35f, 1f), new Color(0.7f, 0.55f, 0.2f));
                star.localRotation = Quaternion.Euler(0f, 90f, 0f);
            }
        }

        static ParticleSystem AddExhaust(Transform parent, VehicleKind kind)
        {
            var go = new GameObject("ExhaustSmoke");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = kind == VehicleKind.Plane || kind == VehicleKind.Heli
                ? new Vector3(0f, 0f, -2.5f)
                : new Vector3(0.6f, 0.2f, -2.2f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.35f, 0.9f);
            main.startColor = new Color(0.25f, 0.25f, 0.22f, 0.45f);
            main.startSpeed = 0.6f;
            main.maxParticles = 40;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = kind == VehicleKind.Heli || kind == VehicleKind.Plane ? 18f : 10f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.08f;
            var colorOver = ps.colorOverLifetime;
            colorOver.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(new Color(0.3f, 0.3f, 0.28f), 0f), new GradientColorKey(new Color(0.4f, 0.4f, 0.38f), 1f) },
                new[] { new GradientAlphaKey(0.5f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOver.color = grad;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.material = IwMaterials.Unlit(new Color(0.3f, 0.3f, 0.28f, 0.4f));
            return ps;
        }

        static Transform Part(Transform parent, string name, PrimitiveType type, Vector3 localPos, Vector3 scale, Color color)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            Object.Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            if (r) r.sharedMaterial = IwMaterials.Metal(color);
            return go.transform;
        }

        public sealed class Spinner : MonoBehaviour
        {
            public float rpm = 200f;
            void Update() => transform.Rotate(Vector3.up, rpm * 6f * Time.deltaTime, Space.Self);
        }
    }
}
