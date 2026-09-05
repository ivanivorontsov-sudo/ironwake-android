extends Node
## Persistent hangar selections across scene changes.

var user_id: String = ""
var callsign: String = "OPERATOR"
var vehicle_id: String = "k72-ural"
var battle_mode: String = "local"  # local | online
var steel: int = 25000
var intel: int = 150
var commendations: int = 0
var catalog: Array = []  # Array of Dictionary vehicle defs

const SAVE_PATH := "user://ironwake_state.cfg"


func _ready() -> void:
	if user_id.is_empty():
		user_id = "u" + str(Time.get_unix_time_from_system()).sha256_text().substr(0, 8)
	_load()
	if catalog.is_empty():
		catalog = VehicleCatalog.fallback_list()


func select_vehicle(id: String) -> void:
	vehicle_id = id
	_save()


func get_vehicle(id: String = "") -> Dictionary:
	var want := id if not id.is_empty() else vehicle_id
	for v in catalog:
		if str(v.get("id", "")) == want:
			return v
	return VehicleCatalog.fallback_list()[0]


func save() -> void:
	_save()


func _save() -> void:
	var cfg := ConfigFile.new()
	cfg.set_value("player", "user_id", user_id)
	cfg.set_value("player", "callsign", callsign)
	cfg.set_value("player", "vehicle_id", vehicle_id)
	cfg.set_value("player", "steel", steel)
	cfg.set_value("player", "intel", intel)
	cfg.set_value("player", "commendations", commendations)
	cfg.save(SAVE_PATH)


func _load() -> void:
	var cfg := ConfigFile.new()
	if cfg.load(SAVE_PATH) != OK:
		return
	user_id = str(cfg.get_value("player", "user_id", user_id))
	callsign = str(cfg.get_value("player", "callsign", callsign))
	vehicle_id = str(cfg.get_value("player", "vehicle_id", vehicle_id))
	steel = int(cfg.get_value("player", "steel", steel))
	intel = int(cfg.get_value("player", "intel", intel))
	commendations = int(cfg.get_value("player", "commendations", commendations))
