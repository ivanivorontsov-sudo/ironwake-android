class_name LocalBotAI
extends RefCounted
## Simple chase / circle / fire bots for local last-stand.

var sim: LocalBattleSim
var _timers: Dictionary = {}  # id -> retarget timer


func _init(p_sim: LocalBattleSim) -> void:
	sim = p_sim


func fill_room(blue_bots: int, red_bots: int) -> void:
	var starters: Array[String] = ["k72-ural", "m-raptor", "btr-iron", "wolf-jeep"]
	var bi := 0
	for i in blue_bots:
		var id := "bot_b_%d" % i
		var vid: String = starters[bi % starters.size()]
		bi += 1
		var def: Dictionary = GameState.get_vehicle(vid)
		var spawn := Vector3(18.0 + i * 6.0, 0.0, 28.0 + i * 4.0)
		sim.add_unit(id, "blue", "BLUE-%d" % (i + 1), vid, spawn, PI, def, true)
	for i in red_bots:
		var id := "bot_r_%d" % i
		var vid: String = starters[(bi + i) % starters.size()]
		var def: Dictionary = GameState.get_vehicle(vid)
		var spawn := Vector3(-18.0 - i * 6.0, 0.0, -28.0 - i * 4.0)
		sim.add_unit(id, "red", "RED-%d" % (i + 1), vid, spawn, 0.0, def, true)


func tick(dt: float) -> void:
	for u_any in sim.units.values():
		var u: SimUnit = u_any
		if not u.is_bot or not u.alive or u.spectator:
			continue
		_timers[u.id] = float(_timers.get(u.id, 0.0)) - dt
		if float(_timers.get(u.id, 0.0)) <= 0.0:
			u.target_id = sim.find_nearest_enemy(u.id, u.team)
			_timers[u.id] = randf_range(0.8, 1.6)
		_drive(u, dt)


func _drive(u: SimUnit, _dt: float) -> void:
	var inp := {
		"throttle": 0.0,
		"steer": 0.0,
		"brake": false,
		"fire": false,
		"aim_yaw": u.turret_yaw,
		"aim_pitch": u.gun_pitch,
	}
	var enemy: SimUnit = sim.get_unit(u.target_id) if u.target_id != "" else null
	if enemy == null or not enemy.alive:
		inp.throttle = 0.15
		inp.steer = sin(Time.get_ticks_msec() * 0.001 + hash(u.id) * 0.01) * 0.4
		u.pending_input = inp
		return
	var to := enemy.position - u.position
	to.y = 0.0
	var dist := to.length()
	var desired_yaw := atan2(to.x, to.z)
	var yaw_err := _angle_diff(desired_yaw, u.yaw)
	inp.steer = clampf(yaw_err * 1.8, -1.0, 1.0)
	if dist > 55.0:
		inp.throttle = 0.95
	elif dist > 28.0:
		inp.throttle = 0.55
		# circle
		inp.steer = clampf(inp.steer + 0.55 * signf(sin(hash(u.id))), -1.0, 1.0)
	else:
		inp.throttle = 0.2 if absf(yaw_err) < 0.4 else 0.05
		inp.brake = absf(yaw_err) > 0.9
	inp.aim_yaw = desired_yaw
	var elev := clampf((enemy.position.y + 1.2 - (u.position.y + 1.5)) / maxf(dist, 1.0), -0.2, 0.25)
	inp.aim_pitch = elev
	var aim_err := absf(_angle_diff(desired_yaw, u.turret_yaw))
	if aim_err < 0.12 and dist < 90.0 and u.reload_timer <= 0.0:
		inp.fire = true
	u.pending_input = inp


static func _angle_diff(a: float, b: float) -> float:
	var d := fmod(a - b + PI, TAU) - PI
	return d
