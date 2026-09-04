using UnityEngine;
using Ironwake.Net;
using Ironwake.Vehicles;
using Ironwake.Graphics;

namespace Ironwake.Combat
{
    /// <summary>
    /// Local prediction (optional) + soft correct to authoritative server pose.
    /// FPS gunner cam inside turret + chase cam (V key / ToggleCam).
    /// On death: spectator — no respawn; free-look or follow killer.
    /// Sends ONLY throttle/steer/brake/fire/aim* — never hp/hit/damage.
    /// </summary>
    public sealed class VehicleController : MonoBehaviour
    {
        public enum CamMode { GunnerFps, Chase, SpectatorFree, SpectatorFollow }

        [Header("Refs")]
        [SerializeField] Transform hull;
        [SerializeField] Transform turret;
        [SerializeField] Transform gun;
        [SerializeField] Transform muzzle;
        [SerializeField] Camera gunnerCam;
        [SerializeField] Camera chaseCam;

        [Header("Tuning")]
        [SerializeField] VehicleDef def;
        [SerializeField] float turretLag = 6f;
        [SerializeField] float gunPitchMin = -12f;
        [SerializeField] float gunPitchMax = 18f;
        [SerializeField] float softCorrectRate = 8f;
        [SerializeField] float softCorrectSnap = 6f;
        [SerializeField] bool localPrediction = true;
        [SerializeField] bool localSimDriven = false;
        [SerializeField] CamMode camMode = CamMode.GunnerFps;

        [Header("Input")]
        [SerializeField] bool useKeyboard = true;

        public string VehicleId => def != null ? def.id : "k72-ural";
        public CamMode CurrentCam => camMode;
        public bool TracksBroken { get; set; }
        public bool EngineDead { get; set; }
        public bool IsLocalPlayer { get; set; } = true;
        public bool IsSpectator { get; private set; }
        public bool IsAlive { get; private set; } = true;
        public string KillerId { get; private set; }
        public Transform Hull => hull;
        public Transform Turret => turret;
        public Transform Gun => gun;
        public Transform Muzzle => muzzle;
        public VehicleDef Def => def;

        float _hullYawDeg;
        float _aimYawDeg;
        float _aimPitchDeg;
        float _displayTurretYaw;
        float _displayGunPitch;
        float _throttle, _steer;
        bool _brake, _firePulse;
        float _lookX, _lookY;
        Vector3 _predVel;
        ModuleDamagePresenter _modules;
        string _callsign = "OPERATOR";
        UnitSnapshot _server;
        bool _hasServer;
        Transform _followTarget;
        float _specYaw, _specPitch = 12f;

        void Awake()
        {
            if (hull == null) hull = transform;
            _hullYawDeg = transform.eulerAngles.y;
            _aimYawDeg = _hullYawDeg;
            _displayTurretYaw = _aimYawDeg;
            _modules = GetComponent<ModuleDamagePresenter>();
            ApplyDef();
            ApplyCam();
        }

        void ApplyDef()
        {
            if (def == null) return;
            gunPitchMin = -12f;
            gunPitchMax = def.kind == VehicleKind.Plane || def.kind == VehicleKind.Heli ? 35f : 18f;
        }

        public void SetDef(VehicleDef d)
        {
            def = d;
            ApplyDef();
        }

        public void SetCallsign(string c) => _callsign = string.IsNullOrEmpty(c) ? "OPERATOR" : c;

        public void SetLocalSimDriven(bool v)
        {
            localSimDriven = v;
            if (v) localPrediction = false;
        }

        public float EstimatedSpeed => _predVel.magnitude;
        public float AimYawRad => _aimYawDeg * Mathf.Deg2Rad;
        public float AimPitchRad => _aimPitchDeg * Mathf.Deg2Rad;
        public float ThrottleInput => _throttle;
        public float SteerInput => _steer;
        public bool BrakeInput => _brake;
        public bool ConsumeFirePulse()
        {
            bool f = _firePulse;
            _firePulse = false;
            return f;
        }

        public void SetMove(float steer, float throttle)
        {
            _steer = Mathf.Clamp(steer, -1f, 1f);
            _throttle = Mathf.Clamp(throttle, -1f, 1f);
        }

        public void SetBrake(bool v) => _brake = v;
        public void SetLook(float x, float y) { _lookX = x; _lookY = y; }

        public void ToggleCam()
        {
            if (IsSpectator)
            {
                camMode = camMode == CamMode.SpectatorFollow ? CamMode.SpectatorFree : CamMode.SpectatorFollow;
            }
            else
            {
                camMode = camMode == CamMode.GunnerFps ? CamMode.Chase : CamMode.GunnerFps;
            }
            ApplyCam();
        }

        void ApplyCam()
        {
            bool gunner = camMode == CamMode.GunnerFps && !IsSpectator;
            bool chase = camMode == CamMode.Chase && !IsSpectator;
            bool spec = IsSpectator;
            if (gunnerCam)
            {
                gunnerCam.enabled = gunner || (spec && camMode == CamMode.SpectatorFree);
                if (spec && camMode == CamMode.SpectatorFree)
                    gunnerCam.transform.SetParent(null, true);
            }
            if (chaseCam)
            {
                chaseCam.enabled = chase || (spec && camMode == CamMode.SpectatorFollow);
            }

            if (hull != null && !IsSpectator)
            {
                foreach (var r in hull.GetComponentsInChildren<Renderer>())
                    r.enabled = camMode == CamMode.Chase;
                if (turret != null)
                {
                    foreach (var r in turret.GetComponentsInChildren<Renderer>())
                        r.enabled = true;
                }
            }
        }

        public void ApplyServerSnapshot(UnitSnapshot u, bool softCorrect = true)
        {
            if (u == null) return;
            _server = u;
            _hasServer = true;
            IsAlive = u.Alive && !u.Spectator;
            if (u.Modules != null && _modules != null)
                _modules.ApplyServerModules(u.Modules.ToDictionary());
            if (_modules != null)
            {
                if (u.OnFire) _modules.ForceFireVisual(true);
                TracksBroken = u.Immobilized || u.Modules.TrackL < 0.2f || u.Modules.TrackR < 0.2f;
                EngineDead = u.Modules.Engine < 0.05f;
            }

            if (!IsAlive && !IsSpectator)
                EnterSpectator(null);

            if (!IsLocalPlayer || !localPrediction || !softCorrect)
            {
                SnapToServer(u);
                return;
            }

            // Soft-correct local prediction toward server
            Vector3 serverPos = new Vector3(u.X, u.Y, u.Z);
            float err = Vector3.Distance(transform.position, serverPos);
            float t = Time.deltaTime * softCorrectRate;
            if (err > softCorrectSnap)
                transform.position = serverPos;
            else
                transform.position = Vector3.Lerp(transform.position, serverPos, t);

            float serverYaw = u.Yaw * Mathf.Rad2Deg;
            _hullYawDeg = Mathf.LerpAngle(_hullYawDeg, serverYaw, t);
            _displayTurretYaw = Mathf.LerpAngle(_displayTurretYaw, u.TurretYaw * Mathf.Rad2Deg, t * 1.2f);
            _displayGunPitch = Mathf.Lerp(_displayGunPitch, u.GunPitch * Mathf.Rad2Deg, t * 1.2f);
        }

        /// <summary>Apply authoritative pose from LocalBattleSim (no soft-correct fight).</summary>
        public void ApplySimSnapshot(UnitSnapshot u)
        {
            if (u == null) return;
            _server = u;
            _hasServer = true;
            IsAlive = u.Alive && !u.Spectator;
            Vector3 pos = new Vector3(u.X, u.Y > 0.01f ? u.Y : transform.position.y, u.Z);
            _predVel = (pos - transform.position) / Mathf.Max(Time.deltaTime, 0.001f);
            transform.position = pos;
            _hullYawDeg = u.Yaw * Mathf.Rad2Deg;
            _displayTurretYaw = u.TurretYaw * Mathf.Rad2Deg;
            _displayGunPitch = u.GunPitch * Mathf.Rad2Deg;
            // Keep aim intent if player is looking — otherwise follow sim
            if (!IsLocalPlayer)
            {
                _aimYawDeg = _displayTurretYaw;
                _aimPitchDeg = _displayGunPitch;
            }
            if (u.Modules != null && _modules != null)
                _modules.ApplyServerModules(u.Modules.ToDictionary());
            if (_modules != null)
            {
                if (u.OnFire) _modules.ForceFireVisual(true);
                TracksBroken = u.Immobilized || u.Modules.TrackL < 0.2f || u.Modules.TrackR < 0.2f;
                EngineDead = u.Modules.Engine < 0.05f;
            }
            if (!IsAlive && !IsSpectator)
                EnterSpectator(null);
            ApplyPoseVisuals();
            UpdateChaseRig();
        }

        void SnapToServer(UnitSnapshot u)
        {
            transform.position = new Vector3(u.X, u.Y > 0.01f ? u.Y : transform.position.y, u.Z);
            _hullYawDeg = u.Yaw * Mathf.Rad2Deg;
            _displayTurretYaw = u.TurretYaw * Mathf.Rad2Deg;
            _displayGunPitch = u.GunPitch * Mathf.Rad2Deg;
            _aimYawDeg = _displayTurretYaw;
            _aimPitchDeg = _displayGunPitch;
            ApplyPoseVisuals();
        }

        public void EnterSpectator(string killerId)
        {
            IsSpectator = true;
            IsAlive = false;
            KillerId = killerId;
            camMode = string.IsNullOrEmpty(killerId) ? CamMode.SpectatorFree : CamMode.SpectatorFollow;
            _specYaw = _aimYawDeg;
            _specPitch = 18f;
            ApplyCam();
            Debug.Log($"[Vehicle] spectator mode killer={killerId}");
        }

        public void SetFollowTarget(Transform t) => _followTarget = t;

        void Update()
        {
            if (!IsLocalPlayer)
            {
                ApplyPoseVisuals();
                return;
            }

            if (useKeyboard)
                ReadKeyboard();

            float dt = Time.deltaTime;

            if (IsSpectator)
            {
                UpdateSpectator(dt);
                PushNetInput(); // server ignores dead input; harmless
                return;
            }

            // Aim from look stick / mouse
            float yawSpeed = def != null ? def.turretYawSpeed : 55f;
            float pitchSpeed = def != null ? def.turretYawSpeed * 0.7f : 40f;
            _aimYawDeg += _lookX * yawSpeed * dt;
            _aimPitchDeg = Mathf.Clamp(_aimPitchDeg - _lookY * pitchSpeed * dt, gunPitchMin, gunPitchMax);

            if (localSimDriven)
            {
                // Pose comes from LocalBattleSim snapshots; still blend turret toward aim intent
                _displayTurretYaw = Mathf.LerpAngle(_displayTurretYaw, _aimYawDeg, 1f - Mathf.Exp(-turretLag * dt));
                _displayGunPitch = Mathf.Lerp(_displayGunPitch, _aimPitchDeg, 1f - Mathf.Exp(-turretLag * 0.85f * dt));
                if (turret) turret.rotation = Quaternion.Euler(0f, _displayTurretYaw, 0f);
                if (gun) gun.localRotation = Quaternion.Euler(_displayGunPitch, 0f, 0f);
                UpdateChaseRig();
            }
            else
            {
                _displayTurretYaw = Mathf.LerpAngle(_displayTurretYaw, _aimYawDeg, 1f - Mathf.Exp(-turretLag * dt));
                _displayGunPitch = Mathf.Lerp(_displayGunPitch, _aimPitchDeg, 1f - Mathf.Exp(-turretLag * 0.85f * dt));
                if (localPrediction)
                    PredictMotion(dt);
                ApplyPoseVisuals();
                UpdateChaseRig();
                PushNetInput();
                _firePulse = false;
            }
            _lookX = 0f;
            _lookY = 0f;
        }

        void LateUpdate()
        {
            if (!IsLocalPlayer || !localSimDriven || IsSpectator) return;
            PushLocalSimInput();
            _firePulse = false;
        }

        void PredictMotion(float dt)
        {
            float speed = def != null ? def.maxSpeed : 12f;
            float accel = def != null ? def.accel : 8f;
            float turn = def != null ? def.turnRate : 48f;
            float mul = EngineDead ? 0.15f : (TracksBroken ? 0.35f : 1f);
            if (_brake) mul *= 0.35f;

            if (!TracksBroken && Mathf.Abs(_steer) > 0.02f)
                _hullYawDeg += _steer * turn * mul * dt * (_throttle >= 0f ? 1f : -1f);

            Vector3 forward = Quaternion.Euler(0f, _hullYawDeg, 0f) * Vector3.forward;
            float wish = Mathf.Clamp(_throttle, -0.4f, 1f) * speed * mul;
            _predVel = Vector3.Lerp(_predVel, forward * wish, 1f - Mathf.Exp(-accel * 0.35f * dt));
            if (_brake) _predVel = Vector3.Lerp(_predVel, Vector3.zero, 6f * dt);

            Vector3 pos = transform.position + _predVel * dt;
            if (def != null)
            {
                if (def.kind == VehicleKind.Heli || def.kind == VehicleKind.Plane)
                    pos.y = Mathf.Lerp(pos.y, def.cruiseAltitude, dt * 1.5f);
                else
                    pos.y = def.groundClearance * 0.15f;
            }
            transform.position = pos;

            // Blend toward server if available
            if (_hasServer && _server != null)
            {
                Vector3 sp = new Vector3(_server.X, transform.position.y, _server.Z);
                float err = Vector3.Distance(new Vector3(transform.position.x, 0, transform.position.z),
                    new Vector3(sp.x, 0, sp.z));
                if (err > softCorrectSnap)
                    transform.position = new Vector3(sp.x, transform.position.y, sp.z);
                else if (err > 0.35f)
                    transform.position = Vector3.Lerp(transform.position, sp, softCorrectRate * 0.35f * dt);
            }
        }

        void ApplyPoseVisuals()
        {
            if (hull) hull.rotation = Quaternion.Euler(0f, _hullYawDeg, 0f);
            else transform.rotation = Quaternion.Euler(0f, _hullYawDeg, 0f);
            if (turret) turret.rotation = Quaternion.Euler(0f, _displayTurretYaw, 0f);
            if (gun) gun.localRotation = Quaternion.Euler(_displayGunPitch, 0f, 0f);
        }

        void UpdateChaseRig()
        {
            if (chaseCam == null) return;
            if (camMode == CamMode.Chase)
            {
                Vector3 back = Quaternion.Euler(0f, _hullYawDeg, 0f) * new Vector3(0f, 4.2f, -9f);
                chaseCam.transform.position = transform.position + back;
                chaseCam.transform.LookAt(transform.position + Quaternion.Euler(0f, _hullYawDeg, 0f) * Vector3.forward * 6f + Vector3.up * 0.6f);
            }
            else if (camMode == CamMode.SpectatorFollow && chaseCam.enabled)
            {
                Transform target = _followTarget != null ? _followTarget : transform;
                Vector3 back = target.forward * -10f + Vector3.up * 5f;
                chaseCam.transform.position = target.position + back;
                chaseCam.transform.LookAt(target.position + Vector3.up * 1.2f);
            }
        }

        void UpdateSpectator(float dt)
        {
            _specYaw += _lookX * 90f * dt;
            _specPitch = Mathf.Clamp(_specPitch - _lookY * 60f * dt, -30f, 70f);
            if (camMode == CamMode.SpectatorFree && gunnerCam)
            {
                Vector3 pivot = transform.position + Vector3.up * 3f;
                Quaternion rot = Quaternion.Euler(_specPitch, _specYaw, 0f);
                gunnerCam.transform.position = pivot + rot * new Vector3(0f, 0f, -12f);
                gunnerCam.transform.LookAt(pivot);
            }
            UpdateChaseRig();
            _lookX = 0f;
            _lookY = 0f;
        }

        void ReadKeyboard()
        {
            // WASD: W throttle+, S throttle-, A/D steer
            float thr = 0f, st = 0f;
            if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow)) thr += 1f;
            if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) thr -= 1f;
            if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) st -= 1f;
            if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) st += 1f;
            if (Mathf.Abs(thr) > 0.01f || Mathf.Abs(st) > 0.01f)
                SetMove(st, thr);

            if (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.B))
                _brake = true;

            _lookX += Input.GetAxis("Mouse X");
            _lookY += Input.GetAxis("Mouse Y");

            if (Input.GetKeyDown(KeyCode.V) || Input.GetKeyDown(KeyCode.C))
                ToggleCam();
            if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(0))
                Fire();
        }

        public void Fire()
        {
            if (IsSpectator || !IsAlive) return;
            if (_modules != null && _modules.IsCookedOff) return;
            _firePulse = true;
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up;
            Vector3 dir = muzzle != null ? muzzle.forward : transform.forward;
            var vfx = CombatVfx.Instance ?? CombatVfx.Ensure();
            vfx.MuzzleFlash(origin, dir);
        }

        void PushLocalSimInput()
        {
            var sim = Ironwake.Sim.LocalBattleSim.Instance;
            if (sim == null || !sim.Running || IsSpectator) return;
            bool fire = _firePulse;
            sim.SetLocalInput(new InputFrame
            {
                Throttle = _throttle,
                Steer = _steer,
                Brake = _brake,
                Fire = fire,
                AimYaw = AimYawRad,
                AimPitch = AimPitchRad,
                TurretYaw = AimYawRad,
                GunPitch = AimPitchRad
            });
            _brake = false;
        }

        void PushNetInput()
        {
            var client = IronwakeClient.Instance;
            if (client == null || !client.Joined) return;
            if (IsSpectator) return;

            float aimYawRad = _aimYawDeg * Mathf.Deg2Rad;
            float aimPitchRad = _aimPitchDeg * Mathf.Deg2Rad;
            var frame = new InputFrame
            {
                Throttle = _throttle,
                Steer = _steer,
                Brake = _brake,
                Fire = _firePulse,
                AimYaw = aimYawRad,
                AimPitch = aimPitchRad,
                TurretYaw = aimYawRad,
                GunPitch = aimPitchRad
            };
            client.SendInput(frame);
            _brake = false;
        }

        /// <summary>Build a primitive vehicle by class (tank/apc/car/heli/plane).</summary>
        public static VehicleController SpawnPrimitive(VehicleDef def, Vector3 pos, Transform parent = null)
        {
            var root = new GameObject(def != null ? def.id : "vehicle");
            if (parent) root.transform.SetParent(parent);
            root.transform.position = pos;
            var vc = root.AddComponent<VehicleController>();
            vc.def = def;

            var rig = TankVisualBuilder.Build(def, root.transform);
            // Flatten: move visual parts under root, destroy wrapper if needed
            if (rig.Hull) rig.Hull.SetParent(root.transform, true);
            if (rig.Turret && rig.Turret != rig.Hull) rig.Turret.SetParent(root.transform, true);

            var hullCol = root.AddComponent<BoxCollider>();
            hullCol.size = new Vector3(2.6f, 1.4f, 5f);
            hullCol.center = new Vector3(0f, 1f, 0f);

            Transform turretParent = rig.Turret != null ? rig.Turret : root.transform;
            var gCamGo = new GameObject("GunnerCam");
            gCamGo.transform.SetParent(turretParent, false);
            gCamGo.transform.localPosition = new Vector3(0f, 0.45f, 0.25f);
            var gCam = gCamGo.AddComponent<Camera>();
            gCam.fieldOfView = 62f;
            gCam.nearClipPlane = 0.05f;
            gCam.farClipPlane = 450f;
            gCam.allowHDR = true;
            gCam.tag = "MainCamera";
            gCam.backgroundColor = new Color(0.5f, 0.6f, 0.7f);
            gCam.clearFlags = CameraClearFlags.SolidColor;

            var cCamGo = new GameObject("ChaseCam");
            cCamGo.transform.SetParent(root.transform, false);
            var cCam = cCamGo.AddComponent<Camera>();
            cCam.fieldOfView = 60f;
            cCam.farClipPlane = 450f;
            cCam.allowHDR = true;
            cCam.enabled = false;
            cCam.backgroundColor = new Color(0.5f, 0.6f, 0.7f);
            cCam.clearFlags = CameraClearFlags.SolidColor;

            vc.hull = rig.Hull != null ? rig.Hull : root.transform;
            vc.turret = rig.Turret != null ? rig.Turret : vc.hull;
            vc.gun = rig.Gun != null ? rig.Gun : vc.turret;
            vc.muzzle = rig.Muzzle != null ? rig.Muzzle : vc.gun;
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
        public string ModuleId = "hull_f";
        public string OwnerUnitId;
    }
}
