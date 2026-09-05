class_name ModuleSystem
extends RefCounted
## Module HP + simplified penetration / facing. Keys match PROTOCOL.

const KEYS := ["hull_f", "hull_s", "hull_r", "turret", "gun", "engine", "ammo", "track_l", "track_r", "fuel", "optics"]
const HIT_FRONT := ["gun", "optics", "turret", "hull_f", "ammo", "engine", "fuel", "track_l", "track_r"]
const HIT_SIDE := ["track_l", "track_r", "hull_s", "ammo", "fuel", "turret", "engine", "gun", "optics"]
const HIT_REAR := ["engine", "fuel", "hull_r", "ammo", "turret", "track_l", "track_r", "gun", "optics"]

var modules: Dictionary = {}
var on_fire: bool = false
var fire_timer: float = 0.0
var cooked_off: bool = false
var immobilized: bool = false
var can_fire: bool = true
var optics_broken: bool = false
var hull_hp: float = 1000.0
var max_hull_hp: float = 1000.0


func reset(max_hp: float) -> void:
	max_hull_hp = maxf(100.0, max_hp)
	hull_hp = max_hull_hp
	modules.clear()
	for k in KEYS:
		modules[k] = 1.0
	on_fire = false
	fire_timer = 0.0
	cooked_off = false
	immobilized = false
	can_fire = true
	optics_broken = false


func get_mod(key: String) -> float:
	return float(modules.get(key, 1.0))


func set_mod(key: String, v: float) -> void:
	modules[key] = clampf(v, 0.0, 1.0)


func resolve_facing(attacker_dir: Vector3, target_yaw: float) -> String:
	var forward := Vector3(sin(target_yaw), 0.0, cos(target_yaw))
	var flat := Vector3(attacker_dir.x, 0.0, attacker_dir.z)
	if flat.length_squared() < 0.0001:
		return "front"
	flat = flat.normalized()
	var f := flat.dot(forward)
	if f > 0.45:
		return "front"
	if f < -0.45:
		return "rear"
	return "side"


func apply_shot(base_damage: float, facing: String, pen_chance: float = 0.75) -> Dictionary:
	var result := {"facing": facing, "module": "", "damage": 0.0, "pen": false, "bounce": false, "module_broken": false}
	var armor_mul := 0.55 if facing == "front" else (1.35 if facing == "rear" else 0.9)
	var pen_mul := 0.7 if facing == "front" else (1.2 if facing == "rear" else 1.0)
	var pen := randf() < pen_chance * pen_mul
	result.pen = pen
	result.bounce = (not pen) and facing == "front" and randf() < 0.35
	if result.bounce:
		return result
	var dmg := base_damage * armor_mul * (1.0 if pen else 0.35)
	result.damage = dmg
	hull_hp = maxf(0.0, hull_hp - dmg)
	var module := _pick_module(facing)
	result.module = module
	var before := get_mod(module)
	var module_loss := randf_range(0.25, 0.55) if pen else randf_range(0.08, 0.2)
	set_mod(module, before - module_loss)
	result.module_broken = before > 0.05 and get_mod(module) <= 0.05
	apply_module_effects()
	if module == "ammo" and get_mod("ammo") < 0.15 and randf() < 0.4:
		start_fire(2.5)
	if module == "fuel" and get_mod("fuel") < 0.2 and randf() < 0.5:
		start_fire(3.5)
	if module == "engine" and get_mod("engine") < 0.1 and randf() < 0.35:
		start_fire(2.0)
	return result


func _pick_module(facing: String) -> String:
	var order: Array = HIT_REAR if facing == "rear" else (HIT_SIDE if facing == "side" else HIT_FRONT)
	var r := randf()
	var idx := 0
	if r < 0.45:
		idx = 0
	elif r < 0.7:
		idx = 1
	elif r < 0.85:
		idx = 2
	else:
		idx = randi() % order.size()
	idx = clampi(idx, 0, order.size() - 1)
	return str(order[idx])


func start_fire(duration: float) -> void:
	on_fire = true
	fire_timer = maxf(fire_timer, duration)


func tick_fire(dt: float) -> bool:
	## Returns true if cook-off happened this tick.
	if not on_fire:
		return false
	fire_timer -= dt
	hull_hp = maxf(0.0, hull_hp - 18.0 * dt)
	set_mod("ammo", get_mod("ammo") - 0.04 * dt)
	set_mod("fuel", get_mod("fuel") - 0.03 * dt)
	var cook := false
	if get_mod("ammo") < 0.08 and not cooked_off and randf() < 0.012 * dt * 60.0:
		cooked_off = true
		cook = true
		hull_hp = 0.0
		on_fire = false
	if fire_timer <= 0.0:
		on_fire = false
	apply_module_effects()
	return cook


func apply_module_effects() -> void:
	immobilized = get_mod("track_l") < 0.15 or get_mod("track_r") < 0.15 or get_mod("engine") < 0.05
	can_fire = get_mod("gun") > 0.08 and get_mod("ammo") > 0.05 and not cooked_off
	optics_broken = get_mod("optics") < 0.12


func mobility_mul() -> float:
	var m := 1.0
	if get_mod("engine") < 0.05:
		m *= 0.12
	elif get_mod("engine") < 0.4:
		m *= 0.55
	if get_mod("track_l") < 0.15 or get_mod("track_r") < 0.15:
		m *= 0.3
	elif get_mod("track_l") < 0.5 or get_mod("track_r") < 0.5:
		m *= 0.7
	return m


func aim_mul() -> float:
	return 0.35 if optics_broken else 1.0
