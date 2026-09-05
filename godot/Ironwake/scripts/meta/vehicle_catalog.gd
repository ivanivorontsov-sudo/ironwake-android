class_name VehicleCatalog
extends RefCounted
## Parses GET /catalog/vehicles and provides offline fallbacks.


static func fallback_list() -> Array:
	return [
		{
			"id": "k72-ural",
			"name": "K-72 Ural",
			"class": "tank",
			"starter": true,
			"hp": 1800.0,
			"speed": 14.0,
			"armor": {"front": 220, "side": 110, "rear": 55},
			"gun": {"caliber": 125, "reload": 7.5, "pen": 290, "damage": 420},
			"cost": {"steel": 0, "intel": 0},
		},
		{
			"id": "m-raptor",
			"name": "M-Raptor",
			"class": "tank",
			"starter": true,
			"hp": 1650.0,
			"speed": 16.0,
			"armor": {"front": 200, "side": 100, "rear": 50},
			"gun": {"caliber": 120, "reload": 6.8, "pen": 275, "damage": 390},
			"cost": {"steel": 0, "intel": 0},
		},
		{
			"id": "btr-iron",
			"name": "BTR-Iron",
			"class": "apc",
			"starter": false,
			"hp": 950.0,
			"speed": 22.0,
			"armor": {"front": 80, "side": 45, "rear": 30},
			"gun": {"caliber": 30, "reload": 0.35, "pen": 95, "damage": 85},
			"cost": {"steel": 18000, "intel": 80},
		},
		{
			"id": "wolf-jeep",
			"name": "Wolf Jeep",
			"class": "car",
			"starter": false,
			"hp": 420.0,
			"speed": 28.0,
			"armor": {"front": 25, "side": 15, "rear": 10},
			"gun": {"caliber": 12.7, "reload": 0.12, "pen": 35, "damage": 40},
			"cost": {"steel": 8000, "intel": 30},
		},
	]


static func parse_catalog_json(text: String) -> Array:
	var data = JSON.parse_string(text)
	if typeof(data) != TYPE_DICTIONARY:
		return fallback_list()
	var arr = data.get("vehicles", [])
	if typeof(arr) != TYPE_ARRAY or arr.is_empty():
		return fallback_list()
	var out: Array = []
	for item in arr:
		if typeof(item) != TYPE_DICTIONARY:
			continue
		out.append(_normalize(item))
	return out if not out.is_empty() else fallback_list()


static func _normalize(v: Dictionary) -> Dictionary:
	var gun: Dictionary = v.get("gun", {}) if typeof(v.get("gun", {})) == TYPE_DICTIONARY else {}
	var armor: Dictionary = v.get("armor", {}) if typeof(v.get("armor", {})) == TYPE_DICTIONARY else {}
	var cost: Dictionary = v.get("cost", {}) if typeof(v.get("cost", {})) == TYPE_DICTIONARY else {}
	return {
		"id": str(v.get("id", "k72-ural")),
		"name": str(v.get("name", "Tank")),
		"class": str(v.get("class", "tank")),
		"starter": bool(v.get("starter", false)),
		"hp": float(v.get("hp", 1000.0)),
		"speed": float(v.get("speed", 14.0)),
		"armor": {
			"front": float(armor.get("front", 100)),
			"side": float(armor.get("side", 60)),
			"rear": float(armor.get("rear", 40)),
		},
		"gun": {
			"caliber": float(gun.get("caliber", 100)),
			"reload": float(gun.get("reload", 5.0)),
			"pen": float(gun.get("pen", 200)),
			"damage": float(gun.get("damage", 200)),
		},
		"cost": {
			"steel": int(cost.get("steel", 0)),
			"intel": int(cost.get("intel", 0)),
		},
	}


static func class_label(cls: String) -> String:
	match cls:
		"tank":
			return "Танк"
		"apc":
			return "БТР"
		"car":
			return "Авто"
		"heli":
			return "Вертолёт"
		"plane":
			return "Самолёт"
		_:
			return cls


static func max_speed(def: Dictionary) -> float:
	return float(def.get("speed", 14.0))


static func shell_damage(def: Dictionary) -> float:
	var gun: Dictionary = def.get("gun", {})
	return float(gun.get("damage", 200.0))


static func fire_cooldown(def: Dictionary) -> float:
	var gun: Dictionary = def.get("gun", {})
	return maxf(0.2, float(gun.get("reload", 5.0)))


static func hull_hp(def: Dictionary) -> float:
	return float(def.get("hp", 1000.0))
