extends Control
## Hangar UI: currencies stub, vehicle select, Local Battle, Online→timeout→local.

@onready var status_label: Label = %StatusLabel
@onready var wallet_label: Label = %WalletLabel
@onready var vehicle_label: Label = %VehicleLabel
@onready var vehicle_desc: Label = %VehicleDesc
@onready var btn_prev: Button = %BtnPrev
@onready var btn_next: Button = %BtnNext
@onready var btn_local: Button = %BtnLocal
@onready var btn_online: Button = %BtnOnline
@onready var preview_root: Node3D = %PreviewRoot
@onready var preview_camera: Camera3D = %PreviewCamera

var _index: int = 0
var _online_busy: bool = false
var _preview_tank: TankVisual


func _ready() -> void:
	btn_prev.pressed.connect(func(): _cycle(-1))
	btn_next.pressed.connect(func(): _cycle(1))
	btn_local.pressed.connect(_on_local)
	btn_online.pressed.connect(_on_online)
	_setup_preview_env()
	_refresh_wallet()
	_sync_index_from_state()
	_refresh_vehicle()
	_boot()


func _setup_preview_env() -> void:
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color(0.3, 0.45, 0.65)
	sky_mat.sky_horizon_color = Color(0.6, 0.65, 0.7)
	sky_mat.ground_bottom_color = Color(0.2, 0.18, 0.14)
	sky_mat.ground_horizon_color = Color(0.4, 0.38, 0.3)
	var sky := Sky.new()
	sky.sky_material = sky_mat
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 0.8
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	var we := WorldEnvironment.new()
	we.environment = env
	preview_root.add_child(we)
	var sun := DirectionalLight3D.new()
	sun.rotation_degrees = Vector3(-40, 50, 0)
	sun.light_energy = 1.4
	sun.shadow_enabled = true
	preview_root.add_child(sun)
	var floor_mi := MeshInstance3D.new()
	var plane := PlaneMesh.new()
	plane.size = Vector2(20, 20)
	floor_mi.mesh = plane
	floor_mi.material_override = IWMaterials.dirt_ground()
	preview_root.add_child(floor_mi)


func _boot() -> void:
	_set_status("Ангар · подключение…")
	await ApiClient.health_check()
	await ApiClient.fetch_catalog()
	_sync_index_from_state()
	_refresh_vehicle()
	_refresh_wallet()
	if ApiClient.last_health_ok:
		_set_status("Ангар · мета с сервера · бой LocalSim на устройстве")
	else:
		_set_status("Ангар · офлайн · Local Battle доступен")


func _sync_index_from_state() -> void:
	var cat: Array = GameState.catalog
	_index = 0
	for i in cat.size():
		if str(cat[i].get("id", "")) == GameState.vehicle_id:
			_index = i
			break


func _cycle(delta: int) -> void:
	var cat: Array = GameState.catalog
	if cat.is_empty():
		return
	_index = (_index + delta + cat.size()) % cat.size()
	_refresh_vehicle()


func _refresh_vehicle() -> void:
	var cat: Array = GameState.catalog
	if cat.is_empty():
		return
	var v: Dictionary = cat[_index]
	GameState.select_vehicle(str(v.get("id", "k72-ural")))
	var cls := VehicleCatalog.class_label(str(v.get("class", "tank")))
	vehicle_label.text = "%s: %s" % [cls, str(v.get("name", "?"))]
	var gun: Dictionary = v.get("gun", {})
	vehicle_desc.text = "HP %d · скорость %s · орудие %s мм · урон %s · перезарядка %ss" % [
		int(v.get("hp", 0)),
		str(v.get("speed", 0)),
		str(gun.get("caliber", "?")),
		str(gun.get("damage", "?")),
		str(gun.get("reload", "?")),
	]
	_rebuild_preview(v)


func _rebuild_preview(def: Dictionary) -> void:
	if _preview_tank and is_instance_valid(_preview_tank):
		_preview_tank.queue_free()
	_preview_tank = TankVisual.new()
	preview_root.add_child(_preview_tank)
	_preview_tank.build(def, "blue")
	_preview_tank.position = Vector3(0, 0, 0)
	preview_camera.position = Vector3(5.5, 3.2, 6.5)
	preview_camera.look_at(Vector3(0, 1.2, 0), Vector3.UP)


func _process(delta: float) -> void:
	if _preview_tank and is_instance_valid(_preview_tank):
		_preview_tank.rotate_y(delta * 0.35)


func _refresh_wallet() -> void:
	wallet_label.text = "Сталь %d   ·   Разведка %d   ·   Награды %d" % [
		GameState.steel, GameState.intel, GameState.commendations
	]


func _set_status(s: String) -> void:
	status_label.text = s


func _on_local() -> void:
	if _online_busy:
		return
	GameState.battle_mode = "local"
	GameState.save()
	_set_status("Локальный бой · запуск…")
	get_tree().change_scene_to_file("res://scenes/battle.tscn")


func _on_online() -> void:
	if _online_busy:
		return
	_online_busy = true
	btn_online.disabled = true
	btn_local.disabled = true
	GameState.battle_mode = "online"
	_set_status("Онлайн · вход в комнату… (таймаут 10с)")
	# Race join against 10s wall-clock timeout; combat stays local either way.
	var ok := await _join_with_timeout(10.0)
	if not ok:
		_set_status("Онлайн таймаут/ошибка — запускаю локальный бой")
	else:
		_set_status("Онлайн ок — бой всё равно LocalSim на устройстве")
	GameState.battle_mode = "local"
	GameState.save()
	_online_busy = false
	get_tree().change_scene_to_file("res://scenes/battle.tscn")


func _join_with_timeout(seconds: float) -> bool:
	var finished := {"done": false, "ok": false}
	var runner := func():
		await ApiClient.join_room(GameState.callsign, GameState.vehicle_id, "laststand")
		finished.ok = ApiClient.last_status.begins_with("join ok")
		finished.done = true
	runner.call()
	var left := seconds
	while not finished.done and left > 0.0:
		await get_tree().process_frame
		left -= get_process_delta_time()
	return bool(finished.done) and bool(finished.ok)
