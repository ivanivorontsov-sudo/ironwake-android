class_name TankVisual
extends Node3D
## Procedural tank / APC / car with PBR olive materials + collision body.

var hull: Node3D
var turret: Node3D
var gun: Node3D
var muzzle: Marker3D
var fire_fx: MeshInstance3D
var smoke_fx: MeshInstance3D
var body: AnimatableBody3D
var team: String = "blue"
var vehicle_class: String = "tank"
var _muzzle_flash: MeshInstance3D
var _flash_timer: float = 0.0
var _smoke_pulse: float = 0.0
var _rotor_time: float = 0.0
var _main_rotor: Node3D
var _tail_rotor: Node3D


func build(def: Dictionary, p_team: String = "blue") -> void:
	team = p_team
	vehicle_class = str(def.get("class", "tank"))
	for c in get_children():
		remove_child(c)
		c.free()

	hull = Node3D.new()
	hull.name = "Hull"
	add_child(hull)
	turret = Node3D.new()
	turret.name = "Turret"
	add_child(turret)
	gun = Node3D.new()
	gun.name = "Gun"
	turret.add_child(gun)
	muzzle = Marker3D.new()
	muzzle.name = "Muzzle"
	gun.add_child(muzzle)

	match vehicle_class:
		"apc":
			_build_apc()
		"car":
			_build_car()
		"heli":
			_build_heli()
		"plane", "aircraft", "airplane":
			_build_plane()
		_:
			_build_tank()

	_setup_collision()
	_setup_damage_fx()


func _setup_collision() -> void:
	body = AnimatableBody3D.new()
	body.name = "CollisionBody"
	body.collision_layer = 2
	body.collision_mask = 1 | 2
	body.sync_to_physics = false
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	match vehicle_class:
		"car":
			shape.size = Vector3(2.0, 1.4, 3.8)
		"apc":
			shape.size = Vector3(2.6, 1.8, 5.2)
		"heli":
			shape.size = Vector3(1.8, 2.2, 5.0)
		_:
			shape.size = Vector3(2.8, 1.6, 4.8)
	col.shape = shape
	col.position = Vector3(0, shape.size.y * 0.5, 0)
	body.add_child(col)
	add_child(body)


func _setup_damage_fx() -> void:
	fire_fx = MeshInstance3D.new()
	fire_fx.name = "FireFx"
	var sm := SphereMesh.new()
	sm.radius = 0.55
	sm.height = 1.1
	fire_fx.mesh = sm
	fire_fx.material_override = IWMaterials.fire_emissive()
	fire_fx.position = Vector3(0, 1.35, -1.15)
	fire_fx.visible = false
	add_child(fire_fx)

	smoke_fx = MeshInstance3D.new()
	smoke_fx.name = "SmokeFx"
	var smoke_mesh := SphereMesh.new()
	smoke_mesh.radius = 0.7
	smoke_mesh.height = 1.4
	smoke_fx.mesh = smoke_mesh
	smoke_fx.material_override = IWMaterials.smoke_mat()
	smoke_fx.position = Vector3(0, 1.9, -1.0)
	smoke_fx.visible = false
	add_child(smoke_fx)

	_muzzle_flash = MeshInstance3D.new()
	_muzzle_flash.name = "MuzzleFlash"
	var flash := SphereMesh.new()
	flash.radius = 0.35
	flash.height = 0.7
	_muzzle_flash.mesh = flash
	_muzzle_flash.material_override = IWMaterials.muzzle_flash()
	_muzzle_flash.visible = false
	muzzle.add_child(_muzzle_flash)


func set_pose(pos: Vector3, yaw: float, turret_yaw: float, gun_pitch: float) -> void:
	global_position = pos
	rotation.y = yaw
	if turret:
		turret.rotation.y = turret_yaw - yaw
	if gun:
		gun.rotation.x = -gun_pitch


func set_on_fire(on: bool) -> void:
	if fire_fx:
		fire_fx.visible = on
	if smoke_fx and on:
		smoke_fx.visible = true


func set_module_smoke(engine_damaged: bool, ammo_damaged: bool) -> void:
	if smoke_fx == null:
		return
	if engine_damaged or ammo_damaged:
		smoke_fx.visible = true
		smoke_fx.position = Vector3(0, 1.7, -1.3 if engine_damaged else 0.2)
	elif fire_fx == null or not fire_fx.visible:
		smoke_fx.visible = false


func play_muzzle_flash() -> void:
	if _muzzle_flash == null:
		return
	_muzzle_flash.visible = true
	_muzzle_flash.scale = Vector3.ONE * randf_range(0.9, 1.4)
	_flash_timer = 0.08


func get_muzzle_global() -> Vector3:
	if muzzle:
		return muzzle.global_position
	return global_position + Vector3.UP * 1.55


func _process(delta: float) -> void:
	if _flash_timer > 0.0:
		_flash_timer -= delta
		if _flash_timer <= 0.0 and _muzzle_flash:
			_muzzle_flash.visible = false
	if smoke_fx and smoke_fx.visible:
		_smoke_pulse += delta * 2.5
		smoke_fx.scale = Vector3.ONE * (1.0 + 0.15 * sin(_smoke_pulse))
		smoke_fx.position.y = 1.7 + 0.2 * sin(_smoke_pulse * 0.7)
	if fire_fx and fire_fx.visible:
		fire_fx.scale = Vector3.ONE * (0.9 + 0.25 * sin(Time.get_ticks_msec() * 0.012))
	if _main_rotor:
		_rotor_time += delta
		_main_rotor.rotation.y = _rotor_time * 18.0
	if _tail_rotor:
		_tail_rotor.rotation.x = _rotor_time * 24.0


func _mat() -> StandardMaterial3D:
	return IWMaterials.painted_armor(team)


func _mat_dark() -> StandardMaterial3D:
	return IWMaterials.olive_dark()


func _add_box(parent: Node3D, size: Vector3, pos: Vector3, mat: StandardMaterial3D, rot_y: float = 0.0) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = size
	mi.mesh = box
	mi.material_override = mat
	mi.position = pos
	mi.rotation.y = rot_y
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	parent.add_child(mi)
	return mi


func _add_cyl(parent: Node3D, radius: float, height: float, pos: Vector3, mat: StandardMaterial3D, rot_x: float = 0.0) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var cyl := CylinderMesh.new()
	cyl.top_radius = radius
	cyl.bottom_radius = radius
	cyl.height = height
	cyl.radial_segments = 14
	mi.mesh = cyl
	mi.material_override = mat
	mi.position = pos
	mi.rotation.x = rot_x
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	parent.add_child(mi)
	return mi


func _build_tank() -> void:
	var m := _mat()
	var md := _mat_dark()
	var camo1 := IWMaterials.camouflage(m, 1)
	var camo2 := IWMaterials.camouflage(m, 2)
	var track := IWMaterials.track()
	# Faceted armor silhouette instead of primitive boxes: layered glacis, belly and side skirts.
	_add_box(hull, Vector3(2.72, 0.62, 4.35), Vector3(0, 0.72, 0.05), m)
	_add_box(hull, Vector3(2.45, 0.46, 1.75), Vector3(0, 1.18, 1.02), camo1, 0.0)
	_add_box(hull, Vector3(2.10, 0.28, 1.35), Vector3(0, 1.36, -1.40), camo2)
	_add_box(hull, Vector3(0.22, 0.50, 4.05), Vector3(-1.43, 0.67, 0.05), md)
	_add_box(hull, Vector3(0.22, 0.50, 4.05), Vector3(1.43, 0.67, 0.05), md)
	_add_box(hull, Vector3(0.44, 0.22, 4.15), Vector3(-1.38, 0.34, 0.05), track)
	_add_box(hull, Vector3(0.44, 0.22, 4.15), Vector3(1.38, 0.34, 0.05), track)
	for z in [-1.65, -0.82, 0.0, 0.82, 1.65]:
		_add_cyl(hull, 0.32, 0.24, Vector3(-1.43, 0.35, z), IWMaterials.rubber(), deg_to_rad(90))
		_add_cyl(hull, 0.32, 0.24, Vector3(1.43, 0.35, z), IWMaterials.rubber(), deg_to_rad(90))
	# Low-poly turret with commander's cupola, optics and mantlet.
	_add_box(turret, Vector3(2.12, 0.64, 2.12), Vector3(0, 1.56, -0.05), m)
	_add_box(turret, Vector3(1.62, 0.28, 1.18), Vector3(0, 1.94, -0.12), camo1)
	_add_cyl(turret, 0.34, 0.28, Vector3(0.42, 2.18, -0.55), md)
	_add_cyl(turret, 0.11, 0.18, Vector3(0.42, 2.40, -0.55), IWMaterials.olive_light())
	_add_box(turret, Vector3(0.58, 0.42, 0.48), Vector3(0, 1.62, 0.88), md)
	_add_cyl(gun, 0.14, 3.55, Vector3(0, 1.63, 2.05), IWMaterials.olive_dark(), deg_to_rad(90))
	_add_cyl(gun, 0.22, 0.44, Vector3(0, 1.63, 0.48), md, deg_to_rad(90))
	muzzle.position = Vector3(0, 1.63, 3.90)


func _build_apc() -> void:
	var m := _mat()
	var track := IWMaterials.track()
	_add_box(hull, Vector3(2.4, 1.35, 5.0), Vector3(0, 0.98, 0), m)
	_add_box(hull, Vector3(2.2, 0.42, 1.25), Vector3(0, 1.58, 1.65), _mat_dark())
	_add_box(hull, Vector3(0.42, 0.52, 4.7), Vector3(-1.28, 0.5, 0), track)
	_add_box(hull, Vector3(0.42, 0.52, 4.7), Vector3(1.28, 0.5, 0), track)
	_add_box(turret, Vector3(1.15, 0.48, 1.15), Vector3(0, 1.88, 0.2), m)
	_add_cyl(gun, 0.07, 1.7, Vector3(0, 1.88, 1.25), IWMaterials.olive_dark(), deg_to_rad(90))
	muzzle.position = Vector3(0, 1.88, 2.15)


func _build_car() -> void:
	var m := _mat()
	_add_box(hull, Vector3(1.95, 0.72, 3.7), Vector3(0, 0.72, 0), m)
	_add_box(hull, Vector3(1.75, 0.58, 1.45), Vector3(0, 1.28, -0.15), _mat_dark())
	for xz in [[-0.88, 1.15], [0.88, 1.15], [-0.88, -1.15], [0.88, -1.15]]:
		_add_cyl(hull, 0.36, 0.26, Vector3(xz[0], 0.36, xz[1]), IWMaterials.rubber(), deg_to_rad(90))
	_add_box(turret, Vector3(0.65, 0.38, 0.65), Vector3(0, 1.58, 0.4), m)
	_add_cyl(gun, 0.05, 1.05, Vector3(0, 1.58, 1.05), IWMaterials.olive_dark(), deg_to_rad(90))
	muzzle.position = Vector3(0, 1.58, 1.6)


func _build_heli() -> void:
	# Detailed attack-helicopter silhouette: rounded cabin, engine housings,
	# swept tail boom, twin skids, main/tail rotors and weapon pylons.
	var m := _mat()
	var md := _mat_dark()
	var glass := IWMaterials.canopy()
	var mi := MeshInstance3D.new()
	var body_mesh := SphereMesh.new()
	body_mesh.radius = 1.0
	body_mesh.height = 2.4
	body_mesh.radial_segments = 20
	body_mesh.rings = 10
	mi.mesh = body_mesh
	mi.scale = Vector3(0.92, 0.72, 2.05)
	mi.position = Vector3(0, 2.55, 0.25)
	mi.material_override = m
	hull.add_child(mi)
	_add_box(hull, Vector3(0.92, 0.55, 1.65), Vector3(0, 2.52, 1.52), glass)
	_add_box(hull, Vector3(1.9, 0.34, 0.62), Vector3(0, 2.0, 0.15), md)
	_add_box(hull, Vector3(0.38, 0.38, 3.4), Vector3(0, 2.45, -3.0), m, 0.0)
	_add_box(hull, Vector3(0.22, 0.30, 2.8), Vector3(0, 2.9, -3.15), md)
	# Engines / intakes
	for x in [-0.62, 0.62]:
		_add_box(hull, Vector3(0.42, 0.34, 0.82), Vector3(x, 3.18, -0.35), md)
		_add_cyl(hull, 0.14, 0.24, Vector3(x, 3.20, -0.82), IWMaterials.rubber(), deg_to_rad(90))
	# Landing skids and struts
	for x in [-0.78, 0.78]:
		_add_cyl(hull, 0.055, 3.65, Vector3(x, 1.55, 0.15), md, deg_to_rad(90))
		_add_cyl(hull, 0.045, 1.2, Vector3(x * 0.72, 1.72, 0.65), md, deg_to_rad(90))
		_add_cyl(hull, 0.045, 1.2, Vector3(x * 0.72, 1.72, -0.85), md, deg_to_rad(90))
	# Rotor mast and animated rotor hub.
	_add_cyl(hull, 0.075, 1.15, Vector3(0, 3.55, 0.0), md)
	_main_rotor = Node3D.new()
	_main_rotor.name = "MainRotor"
	_main_rotor.position = Vector3(0, 4.08, 0.0)
	hull.add_child(_main_rotor)
	_add_box(_main_rotor, Vector3(0.18, 0.055, 6.4), Vector3.ZERO, md)
	_add_box(_main_rotor, Vector3(6.4, 0.055, 0.18), Vector3.ZERO, md)
	_add_cyl(_main_rotor, 0.16, 0.16, Vector3.ZERO, IWMaterials.olive_light())
	# Tail rotor.
	_tail_rotor = Node3D.new()
	_tail_rotor.name = "TailRotor"
	_tail_rotor.position = Vector3(0, 2.72, -4.35)
	hull.add_child(_tail_rotor)
	_add_box(_tail_rotor, Vector3(1.45, 0.05, 0.10), Vector3.ZERO, md)
	_add_box(_tail_rotor, Vector3(0.10, 0.05, 1.45), Vector3.ZERO, md)
	# Forward gun and underwing rocket pods.
	_add_box(turret, Vector3(0.58, 0.42, 0.72), Vector3(0, 2.10, 1.55), md)
	_add_cyl(gun, 0.075, 1.65, Vector3(0, 2.05, 2.28), IWMaterials.olive_dark(), deg_to_rad(90))
	for x in [-1.05, 1.05]:
		_add_box(turret, Vector3(0.28, 0.18, 1.55), Vector3(x, 1.98, 0.55), md)
		for z in [0.05, 0.45, 0.85]:
			_add_cyl(turret, 0.075, 0.28, Vector3(x, 1.88, z + 0.05), IWMaterials.rubber(), deg_to_rad(90))
	muzzle.position = Vector3(0, 2.05, 3.12)


func _build_plane() -> void:
	# Compact fighter/attack-aircraft silhouette for server catalog entries
	# whose class is plane/aircraft; never fall back to a tank mesh.
	var m := _mat()
	var md := _mat_dark()
	var glass := IWMaterials.canopy()
	_add_box(hull, Vector3(1.0, 0.72, 4.8), Vector3(0, 3.6, 0), m)
	_add_box(hull, Vector3(0.72, 0.52, 1.55), Vector3(0, 3.98, 0.65), glass)
	_add_box(hull, Vector3(6.2, 0.18, 1.15), Vector3(0, 3.45, -0.15), m)
	_add_box(hull, Vector3(0.32, 0.75, 1.65), Vector3(0, 4.0, -1.7), md)
	_add_box(hull, Vector3(0.22, 1.15, 1.05), Vector3(0, 4.15, -2.0), md)
	_add_box(hull, Vector3(0.55, 0.20, 1.65), Vector3(-1.9, 3.35, 0.65), md)
	_add_box(hull, Vector3(0.55, 0.20, 1.65), Vector3(1.9, 3.35, 0.65), md)
	for x in [-1.65, 1.65]:
		_add_cyl(hull, 0.16, 0.9, Vector3(x, 3.05, 0.3), md, deg_to_rad(90))
	_add_cyl(gun, 0.045, 1.2, Vector3(0, 3.58, 2.7), md, deg_to_rad(90))
	muzzle.position = Vector3(0, 3.58, 3.35)
