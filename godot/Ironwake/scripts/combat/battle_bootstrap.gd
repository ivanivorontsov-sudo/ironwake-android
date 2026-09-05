extends Node3D
## Boots local battle: environment, tanks, sim, HUD, cameras.

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
var _match_start_msec: int = 0
var _reported: bool = false


func _ready() -> void:
	var env := BattleEnvironment.new()
	env.name = "Environment"
	world_root.add_child(env)
	env.build()

	sim = LocalBattleSim.new()
	sim.name = "LocalBattleSim"
	add_child(sim)
	sim.state_updated.connect(_on_state)
	sim.match_ended.connect(_on_match_end)
	sim.game_event.connect(_on_event)

	var stick: VirtualStick = hud.get_node("Root/Stick")
	controller.setup(sim, stick)
	hud.fire_pressed.connect(func(): controller.fire_held = true)
	hud.fire_released.connect(func(): controller.fire_held = false)
	hud.camera_pressed.connect(_toggle_camera)
	hud.hangar_pressed.connect(_to_hangar)

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


func _unhandled_input(event: InputEvent) -> void:
	controller.handle_aim_input(event)


func _toggle_camera() -> void:
	controller.toggle_camera()
	_apply_camera()


func _apply_camera() -> void:
	var fps := controller.camera_mode == 1
	gunner_camera.current = fps
	chase_camera.current = not fps


func _to_hangar() -> void:
	get_tree().change_scene_to_file("res://scenes/hangar.tscn")


func _on_event(ev: Dictionary) -> void:
	var t := str(ev.get("type", ""))
	if t == "hit" and not bool(ev.get("bounce", false)):
		hud.set_status("Попадание · %s · %s" % [ev.get("module", "?"), "pen" if ev.get("pen") else "spall"])
	elif t == "kill":
		hud.set_status("Уничтожен · %s" % ev.get("id", ""))
	elif t == "cookoff":
		hud.set_status("COOK-OFF · %s" % ev.get("id", ""))
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
		vis.visible = bool(us.get("alive", true)) or bool(us.get("spectator", false))
		if id == local_id:
			hud.set_hp(float(us.get("hp", 0)), float(us.get("maxHp", 1)))
			hud.set_modules(us.get("modules", {}), bool(us.get("onFire", false)))
			_update_cameras(vis, float(us.get("yaw", 0)), float(us.get("turretYaw", 0)), float(us.get("gunPitch", 0)), pos)

	# projectiles
	var live_ids: Dictionary = {}
	for p in state.get("projectiles", []):
		var pid: String = str(p.get("id", ""))
		live_ids[pid] = true
		if not projectile_meshes.has(pid):
			var mi := MeshInstance3D.new()
			var sph := SphereMesh.new()
			sph.radius = 0.12
			sph.height = 0.24
			mi.mesh = sph
			mi.material_override = IWMaterials.tracer()
			projectiles_root.add_child(mi)
			projectile_meshes[pid] = mi
		(projectile_meshes[pid] as MeshInstance3D).global_position = Vector3(float(p.get("x", 0)), float(p.get("y", 0)), float(p.get("z", 0)))
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
	var back := Vector3(sin(yaw), 0, cos(yaw)) * -10.0 + Vector3.UP * 4.5
	chase_camera.global_position = pos + back
	chase_camera.look_at(pos + Vector3.UP * 1.5, Vector3.UP)

	var aim := LocalBattleSim._aim_direction(turret_yaw, gun_pitch)
	var gun_pos := pos + Vector3.UP * 1.55 + aim * 0.4
	gunner_camera.global_position = gun_pos
	gunner_camera.look_at(gun_pos + aim * 20.0, Vector3.UP)
	_apply_camera()
