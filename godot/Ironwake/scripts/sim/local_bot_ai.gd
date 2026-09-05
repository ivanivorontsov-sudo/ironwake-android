class_name LocalBotAI
extends RefCounted
## Chase / engage bots: aim at player, hold lead, fire when on target.

var sim: LocalBattleSim
var _timers: Dictionary = {}  # id -> retarget timer
var _engage_side: Dictionary = {}  # id -> circle side (+1/-1)


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
			u.target_id = _pick_target(u)
			_timers[u.id] = randf_range(0.45, 0.95)
			if not _engage_side.has(u.id):
				_engage_side[u.id] = 1.0 if (hash(u.id) & 1) == 0 else -1.0
		_drive(u, dt)


func _pick_target(u: SimUnit) -> String:
	# Prefer local player when enemy team, else nearest.
	var local_id := sim.local_player_id
	var local_u: SimUnit = sim.get_unit(local_id)
	if local_u and local_u.alive and not local_u.spectator and local_u.team != u.team:
		var d := local_u.position.distance_squared_to(u.position)
		if d < 120.0 * 120.0:
			return local_id
	return sim.find_nearest_enemy(u.id, u.team)


func _drive(u: SimUnit, dt: float) -> void:
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
		# Slow search — do NOT spin aimlessly.
		inp.throttle = 0.35
		inp.steer = 0.15 * signf(sin(Time.get_ticks_msec() * 0.0004 + hash(u.id)))
		inp.aim_yaw = u.yaw
		inp.aim_pitch = 0.0
		u.pending_input = inp
		return

	var to := enemy.position - u.position
	to.y = 0.0
	var dist := to.length()
	var desired_yaw := atan2(to.x, to.z)

	# Lead aim slightly for moving targets.
	var lead := enemy.velocity * clampf(dist / 80.0, 0.0, 0.55)
	var aim_to := (enemy.position + Vector3.UP * 1.2 + lead) - (u.position + Vector3.UP * 1.5)
	var aim_flat := Vector3(aim_to.x, 0.0, aim_to.z)
	var aim_yaw := atan2(aim_flat.x, aim_flat.z) if aim_flat.length_squared() > 0.01 else desired_yaw
	var aim_dist := maxf(aim_to.length(), 1.0)
	var elev := clampf(aim_to.y / aim_dist, -0.2, 0.28)

	inp.aim_yaw = aim_yaw
	inp.aim_pitch = elev

	var yaw_err := _angle_diff(desired_yaw, u.yaw)
	var side: float = float(_engage_side.get(u.id, 1.0))

	if dist > 70.0:
		inp.throttle = 1.0
		inp.steer = clampf(yaw_err * 2.2, -1.0, 1.0)
	elif dist > 32.0:
		inp.throttle = 0.65
		# Flank / circle while keeping nose roughly toward foe.
		inp.steer = clampf(yaw_err * 1.2 + 0.65 * side, -1.0, 1.0)
	elif dist > 16.0:
		inp.throttle = 0.25 if absf(yaw_err) < 0.55 else 0.1
		inp.steer = clampf(yaw_err * 2.0, -1.0, 1.0)
		inp.brake = absf(yaw_err) > 1.0
	else:
		# Too close — reverse a bit and re-orient.
		inp.throttle = -0.25
		inp.steer = clampf(-yaw_err * 1.5 + 0.4 * side, -1.0, 1.0)
		inp.brake = false

	var aim_err := absf(_angle_diff(aim_yaw, u.turret_yaw))
	var pitch_err := absf(elev - u.gun_pitch)
	if aim_err < 0.10 and pitch_err < 0.12 and dist < 95.0 and u.reload_timer <= 0.0:
		inp.fire = true
	elif aim_err < 0.18 and dist < 55.0 and u.reload_timer <= 0.0 and randf() < 0.35:
		inp.fire = true

	u.pending_input = inp


static func _angle_diff(a: float, b: float) -> float:
	var d := fmod(a - b + PI, TAU) - PI
	return d
