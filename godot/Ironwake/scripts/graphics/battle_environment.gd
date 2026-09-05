class_name BattleEnvironment
extends Node3D
## Terrain, sky, sun+fill shadows, collidable ruins/rocks — military dirt arena.

## Obstacles for sim collision: { "center": Vector3, "half_extents": Vector3, "yaw": float }
var obstacles: Array = []


func build() -> void:
	obstacles.clear()
	_setup_environment()
	_build_ground()
	_build_hills()
	_build_props()
	_build_bounds()


func _setup_environment() -> void:
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color(0.28, 0.48, 0.78)
	sky_mat.sky_horizon_color = Color(0.78, 0.72, 0.58)
	sky_mat.ground_bottom_color = Color(0.22, 0.18, 0.12)
	sky_mat.ground_horizon_color = Color(0.58, 0.50, 0.36)
	sky_mat.sun_angle_max = 28.0
	sky_mat.sun_curve = 0.08
	sky_mat.sky_energy_multiplier = 1.15
	var sky := Sky.new()
	sky.sky_material = sky_mat

	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_sky_contribution = 0.62
	env.ambient_light_energy = 0.78
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.tonemap_exposure = 1.08
	env.ssao_enabled = false
	env.glow_enabled = true
	env.glow_intensity = 0.42
	env.glow_bloom = 0.12
	env.glow_levels_1 = 0.0
	env.glow_levels_2 = 0.6
	env.glow_levels_3 = 0.9
	env.glow_levels_4 = 0.7
	env.fog_enabled = true
	env.fog_light_color = Color(0.72, 0.74, 0.78)
	env.fog_density = 0.00045
	env.fog_aerial_perspective = 0.65
	env.adjustment_enabled = true
	env.adjustment_saturation = 1.12
	env.adjustment_contrast = 1.06

	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)

	var sun := DirectionalLight3D.new()
	sun.name = "Sun"
	sun.rotation_degrees = Vector3(-52.0, 42.0, 0.0)
	sun.light_energy = 1.75
	sun.light_color = Color(1.0, 0.93, 0.82)
	sun.shadow_enabled = true
	sun.shadow_blur = 0.85
	sun.directional_shadow_max_distance = 260.0
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS
	sun.directional_shadow_split_1 = 0.08
	sun.directional_shadow_split_2 = 0.22
	sun.directional_shadow_split_3 = 0.5
	add_child(sun)

	var fill := DirectionalLight3D.new()
	fill.name = "Fill"
	fill.rotation_degrees = Vector3(-22.0, -125.0, 0.0)
	fill.light_energy = 0.32
	fill.light_color = Color(0.72, 0.80, 0.95)
	fill.shadow_enabled = false
	add_child(fill)


func _add_static_box(size: Vector3, pos: Vector3, yaw: float, mat: StandardMaterial3D, name_hint: String = "Prop") -> void:
	var root := Node3D.new()
	root.name = name_hint
	root.position = pos
	root.rotation.y = yaw
	add_child(root)

	var mi := MeshInstance3D.new()
	var box := BoxMesh.new()
	box.size = size
	mi.mesh = box
	mi.material_override = mat
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	root.add_child(mi)

	var body := StaticBody3D.new()
	body.collision_layer = 1
	body.collision_mask = 0
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = size
	col.shape = shape
	body.add_child(col)
	root.add_child(body)

	obstacles.append({
		"center": pos,
		"half_extents": size * 0.5,
		"yaw": yaw,
	})


func _add_static_sphere(radius: float, height: float, pos: Vector3, mat: StandardMaterial3D, scale_xz: float = 1.0) -> void:
	var mi := MeshInstance3D.new()
	var mesh := SphereMesh.new()
	mesh.radius = radius
	mesh.height = height
	mesh.radial_segments = 20
	mesh.rings = 10
	mi.mesh = mesh
	mi.material_override = mat
	mi.position = pos
	mi.scale = Vector3(scale_xz, 1.0, scale_xz)
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
	add_child(mi)

	var body := StaticBody3D.new()
	body.collision_layer = 1
	body.position = pos
	var col := CollisionShape3D.new()
	var shape := SphereShape3D.new()
	shape.radius = radius * scale_xz * 0.85
	col.shape = shape
	body.add_child(col)
	add_child(body)

	obstacles.append({
		"center": pos,
		"half_extents": Vector3(radius * scale_xz * 0.85, height * 0.5, radius * scale_xz * 0.85),
		"yaw": 0.0,
	})


func _build_ground() -> void:
	var ground := MeshInstance3D.new()
	ground.name = "Ground"
	var plane := PlaneMesh.new()
	plane.size = Vector2(280, 280)
	plane.subdivide_width = 48
	plane.subdivide_depth = 48
	ground.mesh = plane
	ground.material_override = IWMaterials.dirt_ground()
	ground.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(ground)

	# Detail dirt patches for scale
	var patch_mat := IWMaterials.dirt_patch()
	var rng := RandomNumberGenerator.new()
	rng.seed = 91
	for i in 22:
		var patch := MeshInstance3D.new()
		var pmesh := PlaneMesh.new()
		var sz := rng.randf_range(8.0, 22.0)
		pmesh.size = Vector2(sz, sz)
		pmesh.subdivide_width = 4
		pmesh.subdivide_depth = 4
		patch.mesh = pmesh
		patch.material_override = patch_mat
		patch.position = Vector3(rng.randf_range(-90, 90), 0.02, rng.randf_range(-90, 90))
		patch.rotation.y = rng.randf() * TAU
		add_child(patch)

	var body := StaticBody3D.new()
	body.collision_layer = 1
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = Vector3(280, 1.0, 280)
	col.shape = shape
	col.position.y = -0.5
	body.add_child(col)
	add_child(body)


func _build_hills() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 42
	var mat := IWMaterials.dirt_ground().duplicate() as StandardMaterial3D
	mat.uv1_scale = Vector3(5, 5, 5)
	for i in 16:
		var radius := rng.randf_range(7.0, 15.0)
		var height := radius * rng.randf_range(0.5, 0.85)
		var ang := rng.randf() * TAU
		var dist := rng.randf_range(48.0, 105.0)
		var pos := Vector3(cos(ang) * dist, -height * 0.52, sin(ang) * dist)
		_add_static_sphere(radius, height, pos, mat, 1.45)


func _build_ruin_cluster(origin: Vector3, yaw: float, rng: RandomNumberGenerator) -> void:
	var conc := IWMaterials.concrete()
	var brick := IWMaterials.brick()
	var wood := IWMaterials.wood_plank()
	var rust := IWMaterials.rust_metal()

	# Main wall
	_add_static_box(
		Vector3(rng.randf_range(5.0, 9.0), rng.randf_range(2.2, 3.8), 0.55),
		origin + Vector3(0, 1.4, 0),
		yaw,
		conc,
		"RuinWall"
	)
	# Perpendicular stub wall
	_add_static_box(
		Vector3(0.5, rng.randf_range(1.6, 2.8), rng.randf_range(3.0, 5.5)),
		origin + Vector3(cos(yaw) * 2.5, 1.1, -sin(yaw) * 2.5),
		yaw,
		brick,
		"RuinWing"
	)
	# Collapsed roof slab
	_add_static_box(
		Vector3(rng.randf_range(2.5, 4.5), 0.28, rng.randf_range(2.0, 3.5)),
		origin + Vector3(sin(yaw) * 1.2, 0.35, cos(yaw) * 1.2),
		yaw + 0.4,
		conc,
		"RuinSlab"
	)
	# Debris piles
	for j in 3:
		_add_static_box(
			Vector3(rng.randf_range(0.8, 1.8), rng.randf_range(0.4, 1.0), rng.randf_range(0.7, 1.5)),
			origin + Vector3(rng.randf_range(-3, 3), 0.35, rng.randf_range(-3, 3)),
			yaw + rng.randf() * 1.2,
			rust if j == 0 else wood,
			"Debris"
		)


func _build_props() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 77
	var bag_mat := IWMaterials.sandbag()
	var rock_mat := IWMaterials.rock()
	var rust := IWMaterials.rust_metal()

	# Sandbag berms with collision
	for i in 8:
		var a := i * TAU / 8.0 + 0.2
		var pos := Vector3(cos(a) * 38.0, 0.65, sin(a) * 38.0)
		_add_static_box(Vector3(8.0, 1.3, 2.2), pos, -a, bag_mat, "Berm")

	# Structured ruin clusters (not flat mush)
	var ruin_spots := [
		Vector3(22, 0, -18), Vector3(-30, 0, 12), Vector3(8, 0, 42),
		Vector3(-15, 0, -40), Vector3(48, 0, 8), Vector3(-45, 0, -22),
		Vector3(12, 0, -55),
	]
	for spot in ruin_spots:
		_build_ruin_cluster(spot, rng.randf() * TAU, rng)

	# Rock outcrops
	for i in 10:
		var ang := rng.randf() * TAU
		var dist := rng.randf_range(25.0, 85.0)
		var pos := Vector3(cos(ang) * dist, 0.7, sin(ang) * dist)
		_add_static_box(
			Vector3(rng.randf_range(2.0, 4.5), rng.randf_range(1.2, 2.8), rng.randf_range(1.8, 3.5)),
			pos,
			rng.randf() * TAU,
			rock_mat,
			"Rock"
		)

	# Scrap hulls
	for i in 5:
		var pos := Vector3(rng.randf_range(-70, 70), 0.55, rng.randf_range(-70, 70))
		_add_static_box(Vector3(3.2, 1.1, 5.0), pos, rng.randf() * TAU, rust, "ScrapHull")


func _build_bounds() -> void:
	var mat := IWMaterials.olive_dark()
	for i in 4:
		var size: Vector3
		var pos: Vector3
		if i % 2 == 0:
			size = Vector3(240, 3.0, 5)
			pos = Vector3(0, 0.6, 118 if i == 0 else -118)
		else:
			size = Vector3(5, 3.0, 240)
			pos = Vector3(118 if i == 1 else -118, 0.6, 0)
		_add_static_box(size, pos, 0.0, mat, "Bound")
