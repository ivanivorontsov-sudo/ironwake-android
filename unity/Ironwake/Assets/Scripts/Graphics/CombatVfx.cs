using UnityEngine;

namespace Ironwake.Graphics
{
    /// <summary>
    /// Muzzle flash, tracer streak, impact sparks, fire, cook-off explosion,
    /// dust on move. Mobile-friendly ParticleSystem based.
    /// </summary>
    public sealed class CombatVfx : MonoBehaviour
    {
        public static CombatVfx Instance { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static CombatVfx Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("CombatVfx");
            return go.AddComponent<CombatVfx>();
        }

        public void MuzzleFlash(Vector3 pos, Vector3 dir)
        {
            // Bright short-lived sphere + sparks
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "MuzzleFlash";
            flash.transform.position = pos;
            flash.transform.localScale = Vector3.one * 0.55f;
            Object.Destroy(flash.GetComponent<Collider>());
            var r = flash.GetComponent<Renderer>();
            if (r) r.sharedMaterial = IwMaterials.Unlit(new Color(1f, 0.85f, 0.35f));
            Object.Destroy(flash, 0.07f);

            var psGo = new GameObject("MuzzleSparks");
            psGo.transform.position = pos;
            psGo.transform.rotation = Quaternion.LookRotation(dir == Vector3.zero ? Vector3.forward : dir);
            var ps = psGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.15f;
            main.startSpeed = 12f;
            main.startSize = 0.08f;
            main.startColor = new Color(1f, 0.7f, 0.2f);
            main.maxParticles = 24;
            main.loop = false;
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            Object.Destroy(psGo, 0.5f);
        }

        public void Tracer(Vector3 from, Vector3 to, Color? color = null)
        {
            var go = new GameObject("Tracer");
            go.transform.position = from;
            var lr = go.AddComponent<LineRenderer>();
            lr.positionCount = 2;
            lr.SetPosition(0, from);
            lr.SetPosition(1, to);
            lr.startWidth = 0.08f;
            lr.endWidth = 0.02f;
            Color c = color ?? new Color(1f, 0.85f, 0.4f, 0.9f);
            lr.material = IwMaterials.Unlit(c);
            lr.startColor = c;
            lr.endColor = new Color(c.r, c.g, c.b, 0.1f);
            Object.Destroy(go, 0.12f);
        }

        public void ImpactSparks(Vector3 pos, Vector3 normal)
        {
            var go = new GameObject("ImpactSparks");
            go.transform.position = pos;
            if (normal.sqrMagnitude > 0.01f)
                go.transform.rotation = Quaternion.LookRotation(normal);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.startLifetime = 0.35f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 10f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.15f);
            main.startColor = new Color(1f, 0.6f, 0.2f);
            main.maxParticles = 40;
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 28) });
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Hemisphere;
            shape.radius = 0.15f;
            Object.Destroy(go, 0.8f);

            // Dust puff
            var dust = new GameObject("ImpactDust");
            dust.transform.position = pos;
            var dps = dust.AddComponent<ParticleSystem>();
            var dm = dps.main;
            dm.loop = false;
            dm.startLifetime = 0.9f;
            dm.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            dm.startColor = new Color(0.45f, 0.4f, 0.3f, 0.5f);
            dm.startSpeed = 1.2f;
            var de = dps.emission;
            de.SetBursts(new[] { new ParticleSystem.Burst(0f, 10) });
            Object.Destroy(dust, 1.2f);
        }

        public ParticleSystem AttachFire(Transform parent)
        {
            var go = new GameObject("FireFx");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 1.2f, -0.5f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.8f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.1f);
            main.startColor = new Color(1f, 0.45f, 0.1f, 0.85f);
            main.startSpeed = 1.5f;
            main.maxParticles = 60;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 28f;
            var color = ps.colorOverLifetime;
            color.enabled = true;
            var g = new Gradient();
            g.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.9f, 0.3f), 0f),
                    new GradientColorKey(new Color(1f, 0.3f, 0.05f), 0.5f),
                    new GradientColorKey(new Color(0.1f, 0.1f, 0.1f), 1f)
                },
                new[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0.5f, 0.5f), new GradientAlphaKey(0f, 1f) });
            color.color = g;
            return ps;
        }

        public void CookOffExplosion(Vector3 pos)
        {
            // Flash sphere
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "CookOffFlash";
            ball.transform.position = pos + Vector3.up;
            ball.transform.localScale = Vector3.one * 3.5f;
            Object.Destroy(ball.GetComponent<Collider>());
            var r = ball.GetComponent<Renderer>();
            if (r) r.sharedMaterial = IwMaterials.Unlit(new Color(1f, 0.55f, 0.15f));
            Object.Destroy(ball, 0.18f);

            var go = new GameObject("CookOffFx");
            go.transform.position = pos + Vector3.up;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.startLifetime = 1.4f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 18f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 1.5f);
            main.startColor = new Color(1f, 0.4f, 0.1f);
            main.maxParticles = 80;
            var em = ps.emission;
            em.SetBursts(new[] { new ParticleSystem.Burst(0f, 60) });
            Object.Destroy(go, 2f);

            ImpactSparks(pos + Vector3.up, Vector3.up);
        }

        public ParticleSystem AttachDust(Transform parent)
        {
            var go = new GameObject("MoveDust");
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, 0.05f, -1.5f);
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 0.7f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.3f, 0.8f);
            main.startColor = new Color(0.5f, 0.45f, 0.32f, 0.4f);
            main.startSpeed = 0.4f;
            main.maxParticles = 30;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission;
            em.rateOverTime = 0f; // driven by movement
            return ps;
        }

        public void SetDustRate(ParticleSystem dust, float speed)
        {
            if (dust == null) return;
            var em = dust.emission;
            em.rateOverTime = Mathf.Clamp(speed * 3.5f, 0f, 28f);
        }

        /// <summary>Optional simple track mark quad that fades.</summary>
        public void TrackMark(Vector3 pos, float yawDeg)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = "TrackMark";
            go.transform.position = pos + Vector3.up * 0.03f;
            go.transform.rotation = Quaternion.Euler(90f, yawDeg, 0f);
            go.transform.localScale = new Vector3(0.35f, 1.2f, 1f);
            Object.Destroy(go.GetComponent<Collider>());
            var r = go.GetComponent<Renderer>();
            if (r) r.sharedMaterial = IwMaterials.Unlit(new Color(0.2f, 0.18f, 0.14f, 0.55f));
            Object.Destroy(go, 8f);
        }
    }
}
