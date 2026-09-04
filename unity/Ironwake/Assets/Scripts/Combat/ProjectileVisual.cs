using System.Collections.Generic;
using UnityEngine;
using Ironwake.Net;

namespace Ironwake.Combat
{
    /// <summary>
    /// Tracer presentation driven by authoritative state.projectiles and shot events.
    /// Local Launch() remains for muzzle FX only — hits are server-owned.
    /// </summary>
    public sealed class ProjectileVisual : MonoBehaviour
    {
        [SerializeField] float life = 3f;
        [SerializeField] float followLerp = 18f;

        string _id;
        Vector3 _target;
        bool _hasTarget;
        bool _dead;
        TrailRenderer _trail;

        public string ProjectileId => _id;

        public void BindServer(string id, Vector3 pos)
        {
            _id = id;
            _target = pos;
            _hasTarget = true;
            transform.position = pos;
            EnsureMesh();
            EnsureTrail();
            Destroy(gameObject, life);
        }

        public void UpdateServerPos(Vector3 pos)
        {
            _target = pos;
            _hasTarget = true;
        }

        /// <summary>Optional local muzzle tracer (cosmetic). Prefer server projectiles.</summary>
        public void Launch(Vector3 direction, float speed, float damage, GameObject owner)
        {
            EnsureMesh();
            EnsureTrail();
            var rb = gameObject.AddComponent<ProjectileLocalFlight>();
            rb.Init(direction.normalized * speed, life);
        }

        void EnsureMesh()
        {
            if (GetComponentInChildren<MeshRenderer>() != null) return;
            var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.transform.SetParent(transform, false);
            ball.transform.localScale = Vector3.one * 0.18f;
            Object.Destroy(ball.GetComponent<Collider>());
            var r = ball.GetComponent<Renderer>();
            if (r) r.material.color = new Color(1f, 0.85f, 0.25f);
        }

        void EnsureTrail()
        {
            _trail = GetComponent<TrailRenderer>();
            if (_trail != null) return;
            _trail = gameObject.AddComponent<TrailRenderer>();
            _trail.time = 0.25f;
            _trail.startWidth = 0.12f;
            _trail.endWidth = 0.02f;
            _trail.material = new Material(Shader.Find("Sprites/Default") ?? Shader.Find("Standard"));
            _trail.startColor = new Color(1f, 0.9f, 0.4f, 1f);
            _trail.endColor = new Color(1f, 0.4f, 0.1f, 0f);
        }

        void Update()
        {
            if (_dead || !_hasTarget) return;
            Vector3 prev = transform.position;
            transform.position = Vector3.Lerp(transform.position, _target, 1f - Mathf.Exp(-followLerp * Time.deltaTime));
            Vector3 d = transform.position - prev;
            if (d.sqrMagnitude > 1e-6f)
                transform.rotation = Quaternion.LookRotation(d.normalized);
        }

        public void Kill()
        {
            if (_dead) return;
            _dead = true;
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "ImpactFlash";
            flash.transform.position = transform.position;
            flash.transform.localScale = Vector3.one * 0.4f;
            Object.Destroy(flash.GetComponent<Collider>());
            var r = flash.GetComponent<Renderer>();
            if (r) r.material.color = Color.yellow;
            Object.Destroy(flash, 0.12f);
            Destroy(gameObject);
        }
    }

    /// <summary>Tiny helper for cosmetic local flight when Launch() is used.</summary>
    public sealed class ProjectileLocalFlight : MonoBehaviour
    {
        Vector3 _vel;
        float _life;

        public void Init(Vector3 vel, float life)
        {
            _vel = vel;
            _life = life;
            Destroy(gameObject, life);
        }

        void Update()
        {
            // Cosmetic gravity for local tracers
            _vel += Physics.gravity * 0.35f * Time.deltaTime;
            transform.position += _vel * Time.deltaTime;
            if (_vel.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(_vel);
        }
    }

    /// <summary>
    /// Spawns / updates tracers from room state projectiles and shot events.
    /// Attach beside BattleBootstrap.
    /// </summary>
    public sealed class ProjectilePresenter : MonoBehaviour
    {
        readonly Dictionary<string, ProjectileVisual> _live = new Dictionary<string, ProjectileVisual>();
        readonly HashSet<string> _seenThisFrame = new HashSet<string>();

        void OnEnable()
        {
            if (IronwakeClient.Instance != null)
            {
                IronwakeClient.Instance.OnState += OnState;
                IronwakeClient.Instance.OnGameEvent += OnEvent;
            }
        }

        void OnDisable()
        {
            if (IronwakeClient.Instance != null)
            {
                IronwakeClient.Instance.OnState -= OnState;
                IronwakeClient.Instance.OnGameEvent -= OnEvent;
            }
        }

        public void BindClient(IronwakeClient client)
        {
            if (client == null) return;
            client.OnState -= OnState;
            client.OnGameEvent -= OnEvent;
            client.OnState += OnState;
            client.OnGameEvent += OnEvent;
        }

        void OnState(RoomStatePayload state)
        {
            if (state?.Projectiles == null) return;
            _seenThisFrame.Clear();
            foreach (var p in state.Projectiles)
            {
                if (p == null || string.IsNullOrEmpty(p.Id)) continue;
                _seenThisFrame.Add(p.Id);
                Vector3 pos = new Vector3(p.X, p.Y, p.Z);
                if (!_live.TryGetValue(p.Id, out var vis) || vis == null)
                {
                    vis = Spawn(p.Id, pos);
                    _live[p.Id] = vis;
                }
                else vis.UpdateServerPos(pos);
            }

            var gone = new List<string>();
            foreach (var kv in _live)
            {
                if (!_seenThisFrame.Contains(kv.Key))
                    gone.Add(kv.Key);
            }
            foreach (var id in gone)
            {
                if (_live[id]) _live[id].Kill();
                _live.Remove(id);
            }
        }

        void OnEvent(GameEvent ev)
        {
            if (ev == null) return;
            if (ev.Type == "shot" && !string.IsNullOrEmpty(ev.ProjectileId))
            {
                if (!_live.ContainsKey(ev.ProjectileId))
                {
                    // Position unknown until next state — spawn at origin briefly
                    var vis = Spawn(ev.ProjectileId, Vector3.up * 2f);
                    _live[ev.ProjectileId] = vis;
                }
            }
            if (ev.Type == "hit" || ev.Type == "kill")
            {
                // Impact flash near victim if we can find them
                // Presenter stays lightweight — ModuleDamagePresenter reacts via state modules.
            }
        }

        static ProjectileVisual Spawn(string id, Vector3 pos)
        {
            var go = new GameObject("Tracer_" + id);
            go.transform.position = pos;
            var vis = go.AddComponent<ProjectileVisual>();
            vis.BindServer(id, pos);
            return vis;
        }
    }
}
