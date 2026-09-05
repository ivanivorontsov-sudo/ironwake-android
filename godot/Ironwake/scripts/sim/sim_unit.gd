class_name SimUnit
extends RefCounted

var id: String = ""
var team: String = "blue"
var callsign: String = ""
var vehicle_id: String = "k72-ural"
var def: Dictionary = {}
var position: Vector3 = Vector3.ZERO
var velocity: Vector3 = Vector3.ZERO
var yaw: float = 0.0
var turret_yaw: float = 0.0
var gun_pitch: float = 0.0
var alive: bool = true
var spectator: bool = false
var is_bot: bool = false
var modules: ModuleSystem = ModuleSystem.new()
var pending_input: Dictionary = {}
var reload_timer: float = 0.0
var target_id: String = ""
var moving: bool = false
var kills: int = 0


func to_snapshot() -> Dictionary:
	return {
		"id": id,
		"team": team,
		"callsign": callsign,
		"vehicleId": vehicle_id,
		"x": position.x,
		"y": position.y,
		"z": position.z,
		"yaw": yaw,
		"turretYaw": turret_yaw,
		"gunPitch": gun_pitch,
		"alive": alive,
		"spectator": spectator,
		"hp": modules.hull_hp,
		"maxHp": modules.max_hull_hp,
		"onFire": modules.on_fire,
		"immobilized": modules.immobilized,
		"canFire": modules.can_fire,
		"modules": modules.modules.duplicate(),
		"isBot": is_bot,
	}
