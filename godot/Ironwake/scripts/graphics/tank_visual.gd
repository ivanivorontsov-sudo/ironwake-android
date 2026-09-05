class_name TankVisual
extends Node3D
## Procedural tank / APC / car from MeshInstance3D + olive StandardMaterial3D.

var hull: Node3D
var turret: Node3D
var gun: Node3D
var fire_fx: MeshInstance3D
var team: String = "blue"
var vehicle_class: String = "tank"


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

	match vehicle_class:
		"apc":
			_build_apc()
		"car":
			_build_car()
		"heli":
			_build_heli()
		_:
			_build_tank()

	fire_fx = MeshInstance3D.new()
	fire_fx.name = "FireFx"
	var sm := SphereMesh.new()
	sm.radius = 0.55
	sm.height = 1.1
	fire_fx.mesh = sm
	fire_fx.material_override = IWMaterials.fire_emissive()
	fire_fx.position = Vector3(0, 1.2, -1.2)
	fire_fx.visible = false
	add_child(fire_fx)


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


func _mat() -> StandardMaterial3D:
	return IWMaterials.team_tint(IWMaterials.olive(), team)


func _mat_dark() -> StandardMaterial3D:
	return IWMaterials.team_tint(IWMaterials.olive_dark(), team)


func _add_box(parent: Node3D, size: Vector3, pos: Vector3, mat: StandardMaterial3D, rot_y: float = 0.0) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = size
	mi.mesh = box
	mi.material_override = mat
	mi.position = pos
	mi.rotation.y = rot_y
	parent.add_child(mi)
	return mi


func _add_cyl(parent: Node3D, radius: float, height: float, pos: Vector3, mat: StandardMaterial3D, rot_x: float = 0.0) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var cyl := CylinderMesh.new()
	cyl.top_radius = radius
	cyl.bottom_radius = radius
	cyl.height = height
	cyl.radial_segments = 12
	mi.mesh = cyl
	mi.material_override = mat
	mi.position = pos
	mi.rotation.x = rot_x
	parent.add_child(mi)
	return mi


func _build_tank() -> void:
	var m := _mat()
	var md := _mat_dark()
	var track := IWMaterials.track()
	_add_box(hull, Vector3(2.6, 0.85, 4.6), Vector3(0, 0.75, 0), m)
	_add_box(hull, Vector3(2.4, 0.35, 1.6), Vector3(0, 1.25, 0.9), md)  # glacis
	_add_box(hull, Vector3(0.45, 0.55, 4.2), Vector3(-1.35, 0.55, 0), track)
	_add_box(hull, Vector3(0.45, 0.55, 4.2), Vector3(1.35, 0.55, 0), track)
	# road wheels
	for z in [-1.6, -0.8, 0.0, 0.8, 1.6]:
		_add_cyl(hull, 0.28, 0.2, Vector3(-1.35, 0.28, z), IWMaterials.rubber(), deg_to_rad(90))
		_add_cyl(hull, 0.28, 0.2, Vector3(1.35, 0.28, z), IWMaterials.rubber(), deg_to_rad(90))
	# turret
	_add_box(turret, Vector3(2.0, 0.7, 2.2), Vector3(0, 1.55, -0.15), m)
	_add_box(turret, Vector3(1.4, 0.25, 1.0), Vector3(0, 1.95, -0.1), md)
	# gun
	_add_cyl(gun, 0.12, 3.2, Vector3(0, 1.55, 2.0), IWMaterials.olive_dark(), deg_to_rad(90))
	_add_cyl(gun, 0.18, 0.35, Vector3(0, 1.55, 0.55), md, deg_to_rad(90))  # mantlet


func _build_apc() -> void:
	var m := _mat()
	var track := IWMaterials.track()
	_add_box(hull, Vector3(2.4, 1.3, 5.0), Vector3(0, 0.95, 0), m)
	_add_box(hull, Vector3(2.2, 0.4, 1.2), Vector3(0, 1.55, 1.6), _mat_dark())
	_add_box(hull, Vector3(0.4, 0.5, 4.6), Vector3(-1.25, 0.5, 0), track)
	_add_box(hull, Vector3(0.4, 0.5, 4.6), Vector3(1.25, 0.5, 0), track)
	_add_box(turret, Vector3(1.1, 0.45, 1.1), Vector3(0, 1.85, 0.2), m)
	_add_cyl(gun, 0.07, 1.6, Vector3(0, 1.85, 1.2), IWMaterials.olive_dark(), deg_to_rad(90))


func _build_car() -> void:
	var m := _mat()
	_add_box(hull, Vector3(1.9, 0.7, 3.6), Vector3(0, 0.7, 0), m)
	_add_box(hull, Vector3(1.7, 0.55, 1.4), Vector3(0, 1.25, -0.2), _mat_dark())
	for xz in [[-0.85, 1.1], [0.85, 1.1], [-0.85, -1.1], [0.85, -1.1]]:
		_add_cyl(hull, 0.35, 0.25, Vector3(xz[0], 0.35, xz[1]), IWMaterials.rubber(), deg_to_rad(90))
	_add_box(turret, Vector3(0.6, 0.35, 0.6), Vector3(0, 1.55, 0.4), m)
	_add_cyl(gun, 0.05, 1.0, Vector3(0, 1.55, 1.0), IWMaterials.olive_dark(), deg_to_rad(90))


func _build_heli() -> void:
	var m := _mat()
	_add_box(hull, Vector3(1.6, 1.0, 4.5), Vector3(0, 2.5, 0), m)
	_add_box(hull, Vector3(0.4, 0.4, 3.0), Vector3(0, 2.3, -3.2), _mat_dark())
	_add_cyl(hull, 0.08, 5.5, Vector3(0, 3.2, 0), IWMaterials.olive_light())
	_add_box(turret, Vector3(0.8, 0.4, 0.8), Vector3(0, 2.0, 1.2), m)
	_add_cyl(gun, 0.06, 1.4, Vector3(0, 2.0, 2.0), IWMaterials.olive_dark(), deg_to_rad(90))
