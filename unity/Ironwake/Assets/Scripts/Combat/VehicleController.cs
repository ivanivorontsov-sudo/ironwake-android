using UnityEngine;
using Ironwake.Net;
using Ironwake.Vehicles;

namespace Ironwake.Combat
{
    /// <summary>
    /// Tank-first locomotion: hull inertia, lagged turret, dual cameras
    /// (FPS gunner inside turret + chase). Works with touch/keyboard stubs.
    /// Swap VehicleDef for APC / heli / plane class modifiers.
    /// </summary>
    public sealed class VehicleController : MonoBehaviour
    {
        public enum CamMode { GunnerFps, Chase }

        [Header("Refs")]
        [SerializeField] Transform hull;
        [SerializeField] Transform turret;
        [SerializeField] Transform gun;
        [SerializeField] Transform muzzle;
        [SerializeField] Camera gunnerCam;
        [SerializeField] Camera chaseCam;
        [SerializeField] ProjectileVisual projectilePrefab;

        [Header("Tuning")]
        [SerializeField] VehicleDef def;
        [SerializeField] float hullYawLag = 2.2f;
        [SerializeField] float turretYawSpeed = 55f;
        [SerializeField] float gunPitchSpeed = 40f;
        [SerializeField] float gunPitchMin = -12f;
        [SerializeField] float gunPitchMax = 18f;
        [SerializeField] float accel = 8f;
        [SerializeField] float drag = 3.5f;
        [SerializeField] float maxSpeed = 12f;
        [SerializeField] float turnRate = 48f;
        [SerializeField] float fireCooldown = 1.4f;
        [SerializeField] CamMode camMode = CamMode.GunnerFps;

        [Header("Input (debug / editor)")]
        [SerializeField] bool useKeyboard = true;

        public string VehicleId => def != null ? def.id : "k72-ural";
        public CamMode CurrentCam => camMode;
        public bool TracksBroken { get; set; }
        public bool EngineDead { get; set; }

        Vector3 _vel;
        float _hullYaw;
        float _turretYaw;
        float _gunPitch;
        float _moveX, _moveZ;
        float _lookX, _lookY;
        float _nextFire;
        ModuleDamagePresenter _modules;
        string _callsign = "OPERATOR";

        void Awake()
        {
            if (hull == null) hull = transform;
            _hullYaw = transform.eulerAngles.y;
            _turretYaw = _hullYaw;
            _modules = GetComponent<ModuleDamagePresenter>();
            ApplyCam();
            ApplyDef();
        }

        void ApplyDef()
        {
            if (def == null) return;
            maxSpeed = def.maxSpeed;
            accel = def.accel;
            turnRate = def.turnRate;
            fireCooldown = def.fireCooldown;
            turretYawSpeed = def.turretYawSpeed;
        }

        public void SetDef(VehicleDef d)
        {
            def = d;
            ApplyDef();
        }

        public void SetCallsign(string c) => _callsign = string.IsNullOrEmpty(c) ? "OPERATOR" : c;

        public void SetMove(float x, float z) { _moveX = Mathf.Clamp(x, -1f, 1f); _moveZ = Mathf.Clamp(z, -1f, 1f); }
        public void SetLook(float x, float y) { _lookX = x; _lookY = y; }

        public void ToggleCam()
        {
            camMode = camMode == CamMode.GunnerFps ? CamMode.Chase : CamMode.GunnerFps;
            ApplyCam();
        }

        void ApplyCam()
        {
            if (gunnerCam) gunnerCam.enabled = camMode == CamMode.GunnerFps;
            if (chaseCam) chaseCam.enabled = camMode == CamMode.Chase;
            // Hide own hull mesh bits in FPS — presenters can refine later.
            if (hull != null)
            {
                foreach (var r in hull.GetComponentsInChildren<Renderer>())
                    r.enabled = camMode == CamMode.Chase;
                if (turret != null)
                {
                    foreach (var r in turret.GetComponentsInChildren<Renderer>())
                        r.enabled = true; // keep gun silhouette optional
                }
            }
        }

        void Update()
        {
            if (useKeyboard && Application.isEditor)
                ReadKeyboard();

            float dt = Time.deltaTime;
            float speedMul = EngineDead ? 0.15f : (TracksBroken ? 0.35f : 1f);
            float classMul = def != null ? def.classSpeedMul : 1f;

            // Look → turret / gun (lag behind stick)
            _turretYaw += _lookX * turretYawSpeed * dt;
            _gunPitch = Mathf.Clamp(_gunPitch - _lookY * gunPitchSpeed * dt, gunPitchMin, gunPitchMax);

            // Hull turns toward move intent with inertia
            if (!TracksBroken && Mathf.Abs(_moveX) > 0.05f)
                _hullYaw += _moveX * turnRate * speedMul * dt;

            // Forward thrust along hull with acceleration / drag
            Vector3 forward = Quaternion.Euler(0f, _hullYaw, 0f) * Vector3.forward;
            Vector3 right = Quaternion.Euler(0f, _hullYaw, 0f) * Vector3.right;
            Vector3 wish = (forward * -_moveZ + right * _moveX).normalized;
            float wishMag = new Vector2(_moveX, _moveZ).magnitude;
            if (wishMag > 0.01f && !EngineDead)
                _vel += wish * accel * classMul * speedMul * dt;
            _vel = Vector3.Lerp(_vel, Vector3.zero, drag * dt);
            if (_vel.magnitude > maxSpeed * classMul * speedMul)
                _vel = _vel.normalized * maxSpeed * classMul * speedMul;

            // Vertical by class (heli / plane placeholders)
            float y = transform.position.y;
            if (def != null)
            {
                if (def.kind == VehicleKind.Heli) y = Mathf.Lerp(y, def.cruiseAltitude, dt * 2f);
                else if (def.kind == VehicleKind.Plane) y = Mathf.Lerp(y, def.cruiseAltitude, dt * 1.2f);
                else y = def.groundClearance;
            }

            Vector3 pos = transform.position + _vel * dt;
            pos.y = y;
            transform.position = pos;

            // Apply rotations: hull lags toward turret for "heavy" feel
            float hullTarget = Mathf.LerpAngle(_hullYaw, _turretYaw, 0f); // hull independent
            _hullYaw = Mathf.LerpAngle(_hullYaw, hullTarget, 1f);
            if (hull) hull.rotation = Quaternion.Euler(0f, _hullYaw, 0f);
            else transform.rotation = Quaternion.Euler(0f, _hullYaw, 0f);

            if (turret)
            {
                float ty = Mathf.LerpAngle(turret.eulerAngles.y, _turretYaw, 1f - Mathf.Exp(-hullYawLag * dt));
                turret.rotation = Quaternion.Euler(0f, _turretYaw, 0f);
            }
            if (gun) gun.localRotation = Quaternion.Euler(_gunPitch, 0f, 0f);

            UpdateChaseRig();
            PushNetInput(false);
        }

        void UpdateChaseRig()
        {
            if (chaseCam == null || camMode != CamMode.Chase) return;
            Vector3 back = Quaternion.Euler(0f, _hullYaw, 0f) * new Vector3(0f, 4.2f, -9f);
            chaseCam.transform.position = transform.position + back;
            chaseCam.transform.LookAt(transform.position + Quaternion.Euler(0f, _hullYaw, 0f) * Vector3.forward * 6f + Vector3.up * 0.6f);
        }

        void ReadKeyboard()
        {
            _moveX = Input.GetAxisRaw("Horizontal");
            _moveZ = -Input.GetAxisRaw("Vertical");
            _lookX = Input.GetAxis("Mouse X");
            _lookY = Input.GetAxis("Mouse Y");
            if (Input.GetKeyDown(KeyCode.C)) ToggleCam();
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0)) Fire();
        }

        public void Fire()
        {
            if (Time.time < _nextFire) return;
            if (_modules != null && _modules.IsCookedOff) return;
            _nextFire = Time.time + fireCooldown;

            Vector3 origin = muzzle != null ? muzzle.position : (gun != null ? gun.position : transform.position + Vector3.up);
            Vector3 dir = gun != null ? gun.forward : Quaternion.Euler(_gunPitch, _turretYaw, 0f) * Vector3.forward;

            if (projectilePrefab != null)
            {
                var p = Instantiate(projectilePrefab, origin, Quaternion.LookRotation(dir));
                p.Launch(dir, def != null ? def.shellSpeed : 80f, def != null ? def.shellDamage : 110f, gameObject);
            }

            // Client-side aim assist stub → server hit report (authoritative later)
            string hitTarget = null;
            string hitModule = "hull_front";
            if (Physics.Raycast(origin, dir, out var hit, 180f))
            {
                var zone = hit.collider.GetComponent<ModuleHitZone>();
                if (zone != null)
                {
                    hitTarget = zone.OwnerUnitId;
                    hitModule = zone.ModuleId;
                }
                else
                {
                    var other = hit.collider.GetComponentInParent<VehicleController>();
                    if (other != null && other != this)
                    {
                        hitTarget = other.GetComponent<NetUnitId>()?.Id;
                        hitModule = "hull_front";
                    }
                }
            }

            PushNetInput(true, hitTarget, hitModule, def != null ? def.shellDamage : 110f);
        }

        void PushNetInput(bool withHit, string target = null, string module = null, float dmg = 0f)
        {
            var client = IronwakeClient.Instance;
            if (client == null || !client.Joined) return;
            var frame = new InputFrame
            {
                X = transform.position.x,
                Y = transform.position.y,
                Z = transform.position.z,
                Yaw = _hullYaw * Mathf.Deg2Rad,
                TurretYaw = _turretYaw * Mathf.Deg2Rad,
                GunPitch = _gunPitch * Mathf.Deg2Rad,
                HasHit = withHit && !string.IsNullOrEmpty(target),
                HitTarget = target,
                HitModule = module ?? "hull_front",
                HitDamage = dmg
            };
            client.SendInput(frame);
        }

        /// <summary>Build a primitive tank/heli/plane from cubes if no mesh assigned.</summary>
        public static VehicleController SpawnPrimitive(VehicleDef def, Vector3 pos, Transform parent = null)
        {
            var root = new GameObject(def != null ? def.id : "vehicle");
            if (parent) root.transform.SetParent(parent);
            root.transform.position = pos;
            var vc = root.AddComponent<VehicleController>();
            vc.def = def;

            var hullGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            hullGo.name = "Hull";
            hullGo.transform.SetParent(root.transform, false);
            hullGo.transform.localScale = def != null && def.kind == VehicleKind.Tank
                ? new Vector3(2.4f, 1.1f, 4.8f) : new Vector3(1.2f, 0.7f, 4.2f);
            hullGo.transform.localPosition = Vector3.up * (def != null ? def.groundClearance * 0.4f : 0.9f);
            Object.Destroy(hullGo.GetComponent<Collider>());
            var hullCol = root.AddComponent<BoxCollider>();
            hullCol.size = hullGo.transform.localScale;
            hullCol.center = hullGo.transform.localPosition;

            var turretGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            turretGo.name = "Turret";
            turretGo.transform.SetParent(root.transform, false);
            turretGo.transform.localScale = new Vector3(1.7f, 0.72f, 1.9f);
            turretGo.transform.localPosition = Vector3.up * 1.72f;
            Object.Destroy(turretGo.GetComponent<Collider>());

            var gunGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            gunGo.name = "Gun";
            gunGo.transform.SetParent(turretGo.transform, false);
            gunGo.transform.localScale = new Vector3(0.18f, 0.18f, 3.5f);
            gunGo.transform.localPosition = new Vector3(0f, 0.05f, 1.6f);
            Object.Destroy(gunGo.GetComponent<Collider>());

            var muzzleGo = new GameObject("Muzzle");
            muzzleGo.transform.SetParent(gunGo.transform, false);
            muzzleGo.transform.localPosition = new Vector3(0f, 0f, 0.55f);

            var gCamGo = new GameObject("GunnerCam");
            gCamGo.transform.SetParent(turretGo.transform, false);
            gCamGo.transform.localPosition = new Vector3(0f, 0.35f, 0.2f);
            var gCam = gCamGo.AddComponent<Camera>();
            gCam.fieldOfView = 62f;
            gCam.nearClipPlane = 0.05f;

            var cCamGo = new GameObject("ChaseCam");
            cCamGo.transform.SetParent(root.transform, false);
            var cCam = cCamGo.AddComponent<Camera>();
            cCam.fieldOfView = 60f;
            cCam.enabled = false;

            vc.hull = hullGo.transform;
            vc.turret = turretGo.transform;
            vc.gun = gunGo.transform;
            vc.muzzle = muzzleGo.transform;
            vc.gunnerCam = gCam;
            vc.chaseCam = cCam;
            vc.ApplyDef();
            vc.ApplyCam();
            root.AddComponent<ModuleDamagePresenter>();
            root.AddComponent<NetUnitId>();
            return vc;
        }
    }

    public sealed class NetUnitId : MonoBehaviour
    {
        public string Id;
    }

    public sealed class ModuleHitZone : MonoBehaviour
    {
        public string ModuleId = "hull_front";
        public string OwnerUnitId;
    }
}
