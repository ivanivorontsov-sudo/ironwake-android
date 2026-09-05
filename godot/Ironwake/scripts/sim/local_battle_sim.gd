class_name LocalBattleSim
extends Node
## Device-authoritative battle @ 20 Hz. Server is NOT used for combat.

const TICK_HZ := 20.0
const TANK_RADIUS := 2.35

signal state_updated(state: Dictionary)
signal game_event(ev: Dictionary)
signal match_ended(winner: String)

var arena_half: float = 110.0
var match_time_limit: float = 360.0
var blue_bots: int = 3
var red_bots: int = 4

var units: Dictionary = {}  # id -> SimUnit
var shells: Array = []  # Dictionary shells
var obstacles: Array = []  # {center, half_extents, yaw}
var local_player_id: String = ""
var running: bool = false
var ended: bool = false
var winner: String = ""
var match_timer: float = 360.0
var tick: int = 0

var _accum: float = 0.0
var _bots: LocalBotAI
var _events: Array = []


func set_obstacles(list: Array) -> void:
	obstacles = list.duplicate(true)


func start_local_battle(player_id: String, callsign: String, vehicle_id: String, team: String = "blue") -> void:
	local_player_id = player_id
	units.clear()
	shells.clear()
	ended = false
	winner = ""
	tick = 0
	match_timer = match_time_limit
	_accum = 0.0
	running = true

	var def := GameState.get_vehicle(vehicle_id)
	var side := -1.0 if team == "red" else 1.0
	var spawn := Vector3(side * 8.0, _ground_y(def), side * 22.0)
	var yaw := 0.0 if team == "red" else PI
	var player := add_unit(player_id, team, callsign, vehicle_id, spawn, yaw, def, false)
	player.is_bot = false

	_bots = LocalBotAI.new(self)
	_bots.fill_room(blue_bots, red_bots)
	_emit({"type": "join", "id": player_id, "callsign": callsign, "team": team, "vehicleId": vehicle_id})
	_push_state()
	print("[LocalBattleSim] started local=%s vehicle=%s units=%d obstacles=%d" % [player_id, vehicle_id, units.size(), obstacles.size()])


func add_unit(id: String, team: String, callsign: String, vehicle_id: String, pos: Vector3, yaw: float, def: Dictionary, is_bot: bool) -> SimUnit:
	var u := SimUnit.new()
	u.id = id
	u.team = team
	u.callsign = callsign
	u.vehicle_id = vehicle_id if not vehicle_id.is_empty() else "k72-ural"
	u.def = def
	u.position = pos
	u.yaw = yaw
	u.turret_yaw = yaw
	u.is_bot = is_bot
	u.modules.reset(VehicleCatalog.hull_hp(def))
	units[id] = u
	return u


func get_unit(id: String) -> SimUnit:
	if id.is_empty() or not units.has(id):
		return null
	return units[id]


func find_nearest_enemy(self_id: String, team: String) -> String:
	var self_u: SimUnit = get_unit(self_id)
	if self_u == null:
		return ""
	var best := ""
	var best_d := INF
	for u_any in units.values():
		var u: SimUnit = u_any
		if not u.alive or u.spectator or u.team == team or u.id == self_id:
			continue
		var d: float = u.position.distance_squared_to(self_u.position)
		if d < best_d:
			best_d = d
			best = u.id
	return best


func set_local_input(frame: Dictionary) -> void:
	var u: SimUnit = get_unit(local_player_id)
	if u == null or not u.alive or u.spectator:
		return
	u.pending_input = frame


func _process(delta: float) -> void:
	if not running or ended:
		return
	_accum += delta
	var step := 1.0 / TICK_HZ
	var guard := 0
	while _accum >= step and guard < 4:
		_accum -= step
		guard += 1
		_tick(step)


func _tick(dt: float) -> void:
	tick += 1
	match_timer -= dt
	_events.clear()
	if _bots:
		_bots.tick(dt)
	for u_any in units.values():
		var u: SimUnit = u_any
		if not u.alive or u.spectator:
			continue
		_integrate_unit(u, dt)
	_resolve_all_collisions()
	_integrate_shells(dt)
	_check_match_end()
	_push_state()


func _integrate_unit(u: SimUnit, dt: float) -> void:
	var input: Dictionary = u.pending_input
	u.pending_input = {
		"throttle": float(input.get("throttle", 0.0)),
		"steer": float(input.get("steer", 0.0)),
		"brake": bool(input.get("brake", false)),
		"fire": false,
		"aim_yaw": float(input.get("aim_yaw", u.turret_yaw)),
		"aim_pitch": float(input.get("aim_pitch", u.gun_pitch)),
	}

	if u.modules.tick_fire(dt):
		_emit({"type": "cookoff", "id": u.id})
		_kill_unit(u, u.id)
		return

	var aim_mul := u.modules.aim_mul()
	# Smooth but responsive turret — exponential ease toward aim point.
	var yaw_speed := 70.0 * deg_to_rad(1.0) * aim_mul
	var target_turret := float(input.get("aim_yaw", u.turret_yaw))
	var target_pitch := float(input.get("aim_pitch", u.gun_pitch))
	u.turret_yaw = lerp_angle(u.turret_yaw, target_turret, 1.0 - exp(-yaw_speed * dt))
	var p_min := deg_to_rad(-12.0)
	var p_max := deg_to_rad(18.0)
	u.gun_pitch = clampf(lerpf(u.gun_pitch, target_pitch, 1.0 - exp(-yaw_speed * 0.9 * dt)), p_min, p_max)

	var mob := u.modules.mobility_mul()
	var speed := VehicleCatalog.max_speed(u.def) * mob
	var turn := deg_to_rad(48.0) * mob
	var thr := clampf(float(input.get("throttle", 0.0)), -0.45, 1.0)
	if bool(input.get("brake", false)):
		thr *= 0.3

	if not u.modules.immobilized and absf(float(input.get("steer", 0.0))) > 0.02:
		var steer_sign := 1.0 if thr >= 0.0 else -1.0
		u.yaw += float(input.get("steer", 0.0)) * turn * dt * steer_sign

	var forward := Vector3(sin(u.yaw), 0.0, cos(u.yaw))
	var wish := thr * speed
	var accel := 8.0 * 0.35
	u.velocity = u.velocity.lerp(forward * wish, 1.0 - exp(-accel * dt))
	if bool(input.get("brake", false)):
		u.velocity = u.velocity.lerp(Vector3.ZERO, 6.0 * dt)

	var pos := u.position + u.velocity * dt
	pos.x = clampf(pos.x, -arena_half, arena_half)
	pos.z = clampf(pos.z, -arena_half, arena_half)
	pos.y = _ground_y(u.def)
	u.position = pos
	u.moving = u.velocity.length_squared() > 0.4

	if u.reload_timer > 0.0:
		u.reload_timer -= dt

	if bool(input.get("fire", false)) and u.modules.can_fire and u.reload_timer <= 0.0:
		_fire_shell(u)


func _resolve_all_collisions() -> void:
	var ids: Array = units.keys()
	# Tank vs tank
	for i in range(ids.size()):
		var a: SimUnit = units[ids[i]]
		if not a.alive or a.spectator:
			continue
		for j in range(i + 1, ids.size()):
			var b: SimUnit = units[ids[j]]
			if not b.alive or b.spectator:
				continue
			var delta := a.position - b.position
			delta.y = 0.0
			var dist := delta.length()
			var min_d := TANK_RADIUS * 2.0
			if dist < 0.001:
				delta = Vector3(1, 0, 0)
				dist = 0.001
			if dist < min_d:
				var push := (min_d - dist) * 0.5
				var n := delta / dist
				a.position += n * push
				b.position -= n * push
				# Kill closing velocity along contact normal.
				var rel := a.velocity - b.velocity
				var sep := rel.dot(n)
				if sep < 0.0:
					a.velocity -= n * sep * 0.5
					b.velocity += n * sep * 0.5
	# Tank vs world obstacles
	for u_any in units.values():
		var u: SimUnit = u_any
		if not u.alive or u.spectator:
			continue
		_resolve_obstacles(u)
		u.position.x = clampf(u.position.x, -arena_half, arena_half)
		u.position.z = clampf(u.position.z, -arena_half, arena_half)
		u.position.y = _ground_y(u.def)


func _resolve_obstacles(u: SimUnit) -> void:
	for obs in obstacles:
		var center: Vector3 = obs.get("center", Vector3.ZERO)
		var half: Vector3 = obs.get("half_extents", Vector3.ONE)
		var yaw: float = float(obs.get("yaw", 0.0))
		# Transform unit into obstacle local XZ.
		var to := u.position - center
		to.y = 0.0
		var c := cos(-yaw)
		var s := sin(-yaw)
		var lx := to.x * c - to.z * s
		var lz := to.x * s + to.z * c
		var hx := half.x + TANK_RADIUS
		var hz := half.z + TANK_RADIUS
		if absf(lx) > hx or absf(lz) > hz:
			continue
		# Push out along least penetration axis.
		var pen_x := hx - absf(lx)
		var pen_z := hz - absf(lz)
		if pen_x < pen_z:
			var sx := signf(lx)
			if absf(sx) < 0.001:
				sx = 1.0
			lx += pen_x * sx
		else:
			var sz := signf(lz)
			if absf(sz) < 0.001:
				sz = 1.0
			lz += pen_z * sz
		var wx := lx * c + lz * s
		var wz := -lx * s + lz * c
		u.position.x = center.x + wx
		u.position.z = center.z + wz
		# Dampen velocity into obstacle.
		var n_world := Vector3(wx, 0, wz) - Vector3(to.x, 0, to.z)
		if n_world.length_squared() > 0.0001:
			n_world = n_world.normalized()
			var into := u.velocity.dot(-n_world)
			if into > 0.0:
				u.velocity += n_world * into


func _fire_shell(u: SimUnit) -> void:
	u.reload_timer = VehicleCatalog.fire_cooldown(u.def)
	var dir := _aim_direction(u.turret_yaw, u.gun_pitch)
	var origin := u.position + Vector3.UP * 1.5 + dir * 2.8
	var shell_speed := 80.0
	var dmg := VehicleCatalog.shell_damage(u.def)
	var pid := "p%d_%s_%d" % [tick, u.id, shells.size()]
	shells.append({
		"id": pid,
		"ownerId": u.id,
		"team": u.team,
		"position": origin,
		"velocity": dir * shell_speed,
		"damage": dmg,
		"life": 3.5,
	})
	_emit({"type": "shot", "id": u.id, "projectileId": pid, "origin": origin, "dir": dir})


static func _aim_direction(yaw: float, pitch: float) -> Vector3:
	var cp := cos(pitch)
	return Vector3(sin(yaw) * cp, sin(pitch), cos(yaw) * cp).normalized()


func _integrate_shells(dt: float) -> void:
	const GRAVITY := 9.81 * 0.35
	var i := shells.size() - 1
	while i >= 0:
		var s: Dictionary = shells[i]
		var vel: Vector3 = s.velocity
		vel += Vector3.DOWN * GRAVITY * dt
		var next: Vector3 = s.position + vel * dt
		s.life = float(s.life) - dt
		var hit := false
		var hit_pos := next
		# World obstacle block
		for obs in obstacles:
			if _segment_hits_obb(s.position, next, obs):
				hit = true
				hit_pos = next
				_emit({"type": "impact", "x": hit_pos.x, "y": hit_pos.y, "z": hit_pos.z, "kind": "world"})
				break
		if not hit:
			for u_any in units.values():
				var u: SimUnit = u_any
				if not u.alive or u.spectator or u.id == s.ownerId or u.team == s.team:
					continue
				var c: Vector3 = u.position + Vector3.UP * 1.1
				if _segment_hits_sphere(s.position, next, c, 2.4):
					var inbound: Vector3 = (next - s.position).normalized()
					var facing: String = u.modules.resolve_facing(-inbound, u.yaw)
					var hr: Dictionary = u.modules.apply_shot(float(s.damage), facing)
					hit_pos = c
					_emit({
						"type": "hit",
						"id": u.id,
						"by": s.ownerId,
						"module": hr.module,
						"projectileId": s.id,
						"facing": facing,
						"bounce": hr.bounce,
						"pen": hr.pen,
						"hp": u.modules.hull_hp,
						"x": c.x,
						"y": c.y,
						"z": c.z,
					})
					if hr.module_broken:
						_emit({"type": "module_break", "id": u.id, "module": hr.module, "by": s.ownerId})
					if u.modules.on_fire:
						_emit({"type": "fire_start", "id": u.id})
					if u.modules.hull_hp <= 0.0 or u.modules.cooked_off:
						var killer: SimUnit = get_unit(str(s.ownerId))
						if killer:
							killer.kills += 1
						_kill_unit(u, str(s.ownerId))
					hit = true
					break
		if not hit and next.y <= 0.05:
			hit = true
			hit_pos = Vector3(next.x, 0.05, next.z)
			_emit({"type": "impact", "x": hit_pos.x, "y": hit_pos.y, "z": hit_pos.z, "kind": "ground"})
		s.position = next
		s.velocity = vel
		if hit or float(s.life) <= 0.0 or absf(s.position.x) > arena_half + 40.0 or absf(s.position.z) > arena_half + 40.0:
			shells.remove_at(i)
		else:
			shells[i] = s
		i -= 1


static func _segment_hits_obb(a: Vector3, b: Vector3, obs: Dictionary) -> bool:
	var center: Vector3 = obs.get("center", Vector3.ZERO)
	var half: Vector3 = obs.get("half_extents", Vector3.ONE)
	var yaw: float = float(obs.get("yaw", 0.0))
	# Sample midpoint in local space (good enough for shell step).
	var mid := (a + b) * 0.5
	var to := mid - center
	var c := cos(-yaw)
	var s := sin(-yaw)
	var lx := to.x * c - to.z * s
	var ly := to.y
	var lz := to.x * s + to.z * c
	return absf(lx) <= half.x and absf(ly) <= maxf(half.y, 1.5) and absf(lz) <= half.z


static func _segment_hits_sphere(a: Vector3, b: Vector3, center: Vector3, radius: float) -> bool:
	var ab := b - a
	var ab2 := ab.length_squared()
	if ab2 < 1e-6:
		return a.distance_squared_to(center) <= radius * radius
	var t := clampf((center - a).dot(ab) / ab2, 0.0, 1.0)
	var p := a + ab * t
	return p.distance_squared_to(center) <= radius * radius


func _kill_unit(u: SimUnit, killer_id: String) -> void:
	if not u.alive:
		return
	u.alive = false
	u.spectator = true
	u.modules.hull_hp = 0.0
	_emit({"type": "kill", "id": u.id, "by": killer_id})
	_emit({"type": "spectator", "id": u.id, "by": killer_id})


func _check_match_end() -> void:
	if ended:
		return
	var blue := 0
	var red := 0
	for u_any in units.values():
		var u: SimUnit = u_any
		if not u.alive or u.spectator:
			continue
		if u.team == "blue":
			blue += 1
		else:
			red += 1
	if blue == 0 or red == 0 or match_timer <= 0.0:
		ended = true
		running = false
		if match_timer <= 0.0 and blue == red:
			winner = ""
		elif blue == 0:
			winner = "red"
		elif red == 0:
			winner = "blue"
		else:
			winner = "blue" if blue > red else "red"
		_emit({"type": "end", "winner": winner})
		match_ended.emit(winner)
		print("[LocalBattleSim] MATCH END winner=%s" % winner)


func _emit(ev: Dictionary) -> void:
	ev["t"] = Time.get_ticks_msec()
	_events.append(ev)
	game_event.emit(ev)


func _push_state() -> void:
	var unit_snaps: Array = []
	for u_any in units.values():
		var u: SimUnit = u_any
		unit_snaps.append(u.to_snapshot())
	var proj: Array = []
	for s in shells:
		proj.append({
			"id": s.id,
			"ownerId": s.ownerId,
			"x": s.position.x,
			"y": s.position.y,
			"z": s.position.z,
			"vx": s.velocity.x,
			"vy": s.velocity.y,
			"vz": s.velocity.z,
		})
	var state := {
		"tick": tick,
		"timer": match_timer,
		"ended": ended,
		"winner": winner,
		"units": unit_snaps,
		"projectiles": proj,
		"events": _events.duplicate(),
		"localPlayerId": local_player_id,
	}
	state_updated.emit(state)


func _ground_y(_def: Dictionary) -> float:
	return 0.0
