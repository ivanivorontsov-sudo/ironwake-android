using UnityEngine;

namespace Ironwake.Combat
{
    /// <summary>
    /// Client-side shell tracer. Authoritative hit resolution lives on the server;
    /// this only paints the shot and optionally notifies ModuleDamagePresenter on local hits.
    /// </summary>
    public sealed class ProjectileVisual : MonoBehaviour
    {
        [SerializeField] float life = 2.5f;
        [SerializeField] float radius = 0.08f;
        [SerializeField] TrailRenderer trail;

        Vector3 _vel;
        float _damage;
        GameObject _owner;
        bool _dead;

        public void Launch(Vector3 direction, float speed, float damage, GameObject owner)
        {
            _vel = direction.normalized * speed;
            _damage = damage;
            _owner = owner;
            if (trail == null) trail = GetComponent<TrailRenderer>();
            // Ensure visible primitive if launched from code without mesh.
            if (GetComponent<Renderer>() == null && GetComponent<MeshFilter>() == null)
            {
                var ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                ball.transform.SetParent(transform, false);
                ball.transform.localScale = Vector3.one * 0.15f;
                Object.Destroy(ball.GetComponent<Collider>());
                var r = ball.GetComponent<Renderer>();
                if (r) r.material.color = new Color(1f, 0.85f, 0.3f);
            }
            Destroy(gameObject, life);
        }

        void Update()
        {
            if (_dead) return;
            float dt = Time.deltaTime;
            Vector3 delta = _vel * dt;
            if (Physics.SphereCast(transform.position, radius, delta.normalized, out var hit, delta.magnitude + 0.01f))
            {
                transform.position = hit.point;
                OnImpact(hit);
                return;
            }
            transform.position += delta;
            if (_vel.sqrMagnitude > 0.01f)
                transform.rotation = Quaternion.LookRotation(_vel);
        }

        void OnImpact(RaycastHit hit)
        {
            _dead = true;
            var zone = hit.collider.GetComponent<ModuleHitZone>();
            if (zone != null)
            {
                var presenter = hit.collider.GetComponentInParent<ModuleDamagePresenter>();
                // Visual only — normalize damage roughly (110 dmg → ~0.1 on 1.0 scale for demo)
                presenter?.ApplyHit(zone.ModuleId, Mathf.Clamp01(_damage / 1000f));
            }
            // Tiny impact flash
            var flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "ImpactFlash";
            flash.transform.position = hit.point;
            flash.transform.localScale = Vector3.one * 0.35f;
            Object.Destroy(flash.GetComponent<Collider>());
            var r = flash.GetComponent<Renderer>();
            if (r) r.material.color = Color.yellow;
            Object.Destroy(flash, 0.12f);
            Destroy(gameObject);
        }
    }
}
