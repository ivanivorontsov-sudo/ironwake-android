extends Node3D
## Boots local battle: environment, tanks, sim, HUD, cameras, combat FX.

@onready var world_root: Node3D = $World
@onready var units_root: Node3D = $Units
@onready var projectiles_root: Node3D = $Projectiles
@onready var chase_camera: Camera3D = $ChaseCamera
@onready var gunner_camera: Camera3D = $GunnerCamera
@onready var hud: CanvasLayer = $BattleHUD
@onready var controller: TankController = $TankController

var sim: LocalBattleSim
var visuals: Dictionary = {}  # id -> TankVisual
var projectile_meshes: Dictionary = {}
var fx: CombatFx
var _match_start_msec: int = 0
var _reported: bool = false
var _cam_smooth_pos: Vector3 = Vector3.ZERO
var _cam_ready: bool = false
var _bot_count: int = 2


func _ready() -> void:
	var env := BattleEnvironment.new()
	env.name = "Environment"
	world_root.add_child(env)
	env.build()

	fx = CombatFx.new()
	fx.name = "CombatFx"
	add_child(fx)

	sim = LocalBattleSim.new()
	sim.name = "LocalBattleSim"
	add_child(sim)
	sim.set_obstacles(env.obstacles)
	sim.state_updated.connect(_on_state)
	sim.match_ended.connect(_on_match_end)
	sim.game_event.connect(_on_event)

	var stick: VirtualStick = hud.get_node("Root/Stick")
	controller.setup(sim, stick)
	hud.fire_pressed.connect(func(): controller.fire_held = true)
	hud.fire_released.connect(func(): controller.fire_held = false)
	hud.camera_pressed.connect(_toggle_camera)
	hud.hangar_pressed.connect(_to_hangar)
	hud.bots_pressed.connect(_toggle_bots)
	hud.set_bot_count(_bot_count)

	_match_start_msec = Time.get_ticks_msec()
	sim.start_local_battle(
		GameState.user_id,
		GameState.callsign,
		GameState.vehicle_id,
		"blue"
	)
	chase_camera.current = true
	gunner_camera.current = false
	hud.set_status("Локальный бой · last stand · без респавна")
	hud.set_gunner_mode(false)


func _unhandled_input(event: InputEvent) -> void:
	controller.handle_aim_input(event)


func _toggle_camera() -> void:
	controller.toggle_camera()
	_apply_camera()


func _apply_camera() -> void:
	var fps := controller.camera_mode == 1
	gunner_camera.current = fps
	chase_camera.current = not fps
	hud.set_gunner_mode(fps)


func _toggle_bots() -> void:
	# Cycle 1..8; never allow zero because LocalBattleSim would end immediately.
	_bot_count = 1 if _bot_count >= 8 else _bot_count + 1
	sim.set_bot_count(_bot_count)
	# Clear transient input state so changing the room never leaves controls locked.
	controller.fire_held = false
	controller.brake_held = false
	controller.release_aim_input()
	hud.set_bot_count(_bot_count)
	hud.set_status("Противников: %d · управление готово" % _bot_count)

func _to_hangar() -> void:
	get_tree().change_scene_to_file("res://scenes/hangar.tscn")


func _on_event(ev: Dictionary) -> void:
	var t := str(ev.get("type", ""))
	if t == "shot":
		var id := str(ev.get("id", ""))
		if visuals.has(id):
			(visuals[id] as TankVisual).play_muzzle_flash()
		var origin: Vector3 = ev.get("origin", Vector3.ZERO)
		var dir: Vector3 = ev.get("dir", Vector3.FORWARD)
		if origin != Vector3.ZERO:
			fx.spawn_muzzle_flash(origin, dir)
		fx.play_shot_stub()
	elif t == "hit" and not bool(ev.get("bounce", false)):
		hud.set_status("Попадание · %s · %s" % [ev.get("module", "?"), "pen" if ev.get("pen") else "spall"])
		var hp := Vector3(float(ev.get("x", 0)), float(ev.get("y", 1)), float(ev.get("z", 0)))
		fx.spawn_hit_sparks(hp)
		fx.spawn_impact(hp, bool(ev.get("pen", false)))
		fx.play_hit_stub()
	elif t == "impact":
		fx.spawn_impact(Vector3(float(ev.get("x", 0)), float(ev.get("y", 0.2)), float(ev.get("z", 0))), false)
	elif t == "kill":
		var kp := Vector3(float(ev.get("x", 0)), float(ev.get("y", 1)), float(ev.get("z", 0)))
		fx.spawn_explosion(kp, true)
		hud.set_status("УНИЧТОЖЕН · %s" % ev.get("id", ""))
	elif t == "cookoff":
		hud.set_status("COOK-OFF · %s" % ev.get("id", ""))
	elif t == "fire_start":
		var fid := str(ev.get("id", ""))
		if visuals.has(fid):
			(visuals[fid] as TankVisual).set_on_fire(true)
	elif t == "end":
		hud.set_status("Бой окончен · победа: %s" % (ev.get("winner", "ничья") if str(ev.get("winner", "")) != "" else "ничья"))


func _on_match_end(winner: String) -> void:
	if _reported:
		return
	_reported = true
	var local_u: SimUnit = sim.get_unit(sim.local_player_id)
	var survived := local_u != null and local_u.alive
	var victory := winner == "blue"
	var duration := (Time.get_ticks_msec() - _match_start_msec) / 1000.0
	ApiClient.report_match({
		"userId": GameState.user_id,
		"vehicleId": GameState.vehicle_id,
		"team": "blue",
		"winner": winner,
		"victory": victory,
		"survived": survived,
		"duration": duration,
		"kills": local_u.kills if local_u else 0,
		"mode": "local_laststand",
	})


func _on_state(state: Dictionary) -> void:
	var local_id: String = str(state.get("localPlayerId", ""))
	for us in state.get("units", []):
		var id: String = str(us.get("id", ""))
		if not visuals.has(id):
			var tv := TankVisual.new()
			tv.name = "Tank_%s" % id
			units_root.add_child(tv)
			var def := GameState.get_vehicle(str(us.get("vehicleId", "k72-ural")))
			tv.build(def, str(us.get("team", "blue")))
			visuals[id] = tv
		var vis: TankVisual = visuals[id]
		var pos := Vector3(float(us.get("x", 0)), float(us.get("y", 0)), float(us.get("z", 0)))
		vis.set_pose(pos, float(us.get("yaw", 0)), float(us.get("turretYaw", 0)), float(us.get("gunPitch", 0)))
		vis.set_on_fire(bool(us.get("onFire", false)))
		var mods: Dictionary = us.get("modules", {})
		vis.set_module_smoke(float(mods.get("engine", 1.0)) < 0.45, float(mods.get("ammo", 1.0)) < 0.4)
		vis.visible = bool(us.get("alive", true)) or bool(us.get("spectator", false))
		if id == local_id:
			hud.set_hp(float(us.get("hp", 0)), float(us.get("maxHp", 1)))
			hud.set_modules(mods, bool(us.get("onFire", false)))
			_update_cameras(vis, float(us.get("yaw", 0)), float(us.get("turretYaw", 0)), float(us.get("gunPitch", 0)), pos)

	# projectiles as oriented tracers
	var live_ids: Dictionary = {}
	for p in state.get("projectiles", []):
		var pid: String = str(p.get("id", ""))
		live_ids[pid] = true
		var ppos := Vector3(float(p.get("x", 0)), float(p.get("y", 0)), float(p.get("z", 0)))
		var vel := Vector3(float(p.get("vx", 0)), float(p.get("vy", 0)), float(p.get("vz", 1)))
		if not projectile_meshes.has(pid):
			projectile_meshes[pid] = fx.spawn_tracer(ppos, vel)
		else:
			fx.update_tracer(projectile_meshes[pid] as MeshInstance3D, ppos, vel)
	var to_kill: Array = []
	for pid in projectile_meshes.keys():
		if not live_ids.has(pid):
			to_kill.append(pid)
	for pid in to_kill:
		(projectile_meshes[pid] as Node).queue_free()
		projectile_meshes.erase(pid)

	if bool(state.get("ended", false)):
		var w := str(state.get("winner", ""))
		hud.set_status("ИТОГ · %s · [АНГАР]" % ("победа синих" if w == "blue" else ("победа красных" if w == "red" else "ничья")))


func _update_cameras(vis: TankVisual, yaw: float, turret_yaw: float, gun_pitch: float, pos: Vector3) -> void:
	var back := Vector3(sin(yaw), 0, cos(yaw)) * -11.0 + Vector3.UP * 4.8
	var desired_chase := pos + back
	if not _cam_ready:
		_cam_smooth_pos = desired_chase
		_cam_ready = true
	else:
		_cam_smooth_pos = _cam_smooth_pos.lerp(desired_chase, 0.18)
	chase_camera.global_position = _cam_smooth_pos
	chase_camera.look_at(pos + Vector3.UP * 1.6, Vector3.UP)

	# Gunner / iron-sight: sit on gun mantle, look along aim with slight lead
	var aim := LocalBattleSim._aim_direction(turret_yaw, gun_pitch)
	var gun_pos := vis.get_muzzle_global() - aim * 0.85
	if gun_pos.distance_squared_to(pos) > 80.0:
		gun_pos = pos + Vector3.UP * 1.55 + aim * 0.35
	gunner_camera.global_position = gun_pos
	var look_target := gun_pos + aim * 40.0 + Vector3.UP * 0.05
	gunner_camera.look_at(look_target, Vector3.UP)
	gunner_camera.fov = 48.0 if controller.camera_mode == 1 else 55.0
	_apply_camera()
