class_name BattleEnvironment
extends Node3D
## Mobile-first battlefield: strong silhouette, layered atmosphere, rocks/foliage and PBR terrain.

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
	sky_mat.sky_top_color = Color(0.10, 0.22, 0.38)
	sky_mat.sky_horizon_color = Color(0.72, 0.63, 0.48)
	sky_mat.ground_bottom_color = Color(0.09, 0.10, 0.08)
	sky_mat.ground_horizon_color = Color(0.42, 0.37, 0.28)
	sky_mat.sun_angle_max = 18.0
	sky_mat.sun_curve = 0.035
	sky_mat.sky_energy_multiplier = 0.95
	var sky := Sky.new()
	sky.sky_material = sky_mat
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_sky_contribution = 0.72
	env.ambient_light_energy = 0.70
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.tonemap_exposure = 1.0
	env.glow_enabled = true
	env.glow_intensity = 0.32
	env.glow_bloom = 0.10
	env.glow_levels_1 = 0.0
	env.glow_levels_2 = 0.42
	env.glow_levels_3 = 0.72
	env.glow_levels_4 = 0.35
	env.fog_enabled = true
	env.fog_light_color = Color(0.48, 0.51, 0.50)
	env.fog_density = 0.00070
	env.fog_aerial_perspective = 0.78
	env.adjustment_enabled = true
	env.adjustment_saturation = 1.06
	env.adjustment_contrast = 1.08
	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)
	var sun := DirectionalLight3D.new()
	sun.name = "Sun"
	sun.rotation_degrees = Vector3(-48.0, 34.0, 0.0)
	sun.light_energy = 1.55
	sun.light_color = Color(1.0, 0.90, 0.74)
	sun.shadow_enabled = true
	sun.shadow_blur = 0.65
	sun.directional_shadow_max_distance = 190.0
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_4_SPLITS
	sun.directional_shadow_split_1 = 0.07
	sun.directional_shadow_split_2 = 0.20
	sun.directional_shadow_split_3 = 0.48
	add_child(sun)
	var fill := DirectionalLight3D.new()
	fill.rotation_degrees = Vector3(-18.0, -125.0, 0.0)
	fill.light_energy = 0.24
	fill.light_color = Color(0.48, 0.62, 0.78)
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
	obstacles.append({"center": pos, "half_extents": size * 0.5, "yaw": yaw})

func _add_static_sphere(radius: float, height: float, pos: Vector3, mat: StandardMaterial3D, scale_xz: float = 1.0) -> void:
	var mi := MeshInstance3D.new()
	var mesh := SphereMesh.new()
	mesh.radius = radius
	mesh.height = height
	mesh.radial_segments = 18
	mesh.rings = 9
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
	obstacles.append({"center": pos, "half_extents": Vector3(radius * scale_xz * 0.85, height * 0.5, radius * scale_xz * 0.85), "yaw": 0.0})

func _build_ground() -> void:
	var ground := MeshInstance3D.new()
	ground.name = "Ground"
	var plane := PlaneMesh.new()
	plane.size = Vector2(280, 280)
	plane.subdivide_width = 48
	plane.subdivide_depth = 48
	ground.mesh = plane
	ground.material_override = IWMaterials.dirt_ground()
	add_child(ground)
	var patch_mat := IWMaterials.dirt_patch()
	var rng := RandomNumberGenerator.new()
	rng.seed = 91
	for i in 22:
		var patch := MeshInstance3D.new()
		var pmesh := PlaneMesh.new()
		var sz := rng.randf_range(8.0, 22.0)
		pmesh.size = Vector2(sz, sz)
		patch.mesh = pmesh
		patch.material_override = patch_mat
		patch.position = Vector3(rng.randf_range(-90, 90), 0.018, rng.randf_range(-90, 90))
		patch.rotation.y = rng.randf() * TAU
		add_child(patch)

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
		_add_static_sphere(radius, height, Vector3(cos(ang) * dist, -height * 0.52, sin(ang) * dist), mat, 1.45)

func _build_props() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 77
	var rock_mat := IWMaterials.rock()
	var rust := IWMaterials.rust_metal()
	var grass := IWMaterials.olive_dark()
	for i in 12:
		var a := rng.randf() * TAU
		var d := rng.randf_range(28.0, 88.0)
		_add_static_sphere(rng.randf_range(1.1, 2.8), rng.randf_range(1.4, 3.6), Vector3(cos(a)*d, 0.8, sin(a)*d), rock_mat, rng.randf_range(0.9, 1.7))
	# Low-poly foliage clumps: three crossed cones, no alpha textures and very cheap on mobile.
	for i in 34:
		var a := rng.randf() * TAU
		var d := rng.randf_range(18.0, 105.0)
		var p := Vector3(cos(a)*d, 0.0, sin(a)*d)
		for j in 2:
			var mi := MeshInstance3D.new()
			var cone := PrismMesh.new()
			cone.left_to_right = 0.62
			cone.size = Vector3(rng.randf_range(0.7, 1.6), rng.randf_range(1.2, 2.5), rng.randf_range(0.7, 1.6))
			mi.mesh = cone
			mi.material_override = grass
			mi.position = p + Vector3(0, cone.size.y * 0.5, 0)
			mi.rotation.y = j * PI * 0.5 + rng.randf()
			mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_ON
			add_child(mi)
	var ruin_spots := [Vector3(22,0,-18), Vector3(-30,0,12), Vector3(8,0,42), Vector3(-15,0,-40), Vector3(48,0,8), Vector3(-45,0,-22)]
	for spot in ruin_spots:
		_add_static_box(Vector3(rng.randf_range(5.0,9.0), rng.randf_range(2.2,3.8), 0.55), spot + Vector3(0,1.4,0), rng.randf()*TAU, IWMaterials.concrete(), "RuinWall")
		_add_static_box(Vector3(0.5, rng.randf_range(1.6,2.8), rng.randf_range(3.0,5.5)), spot + Vector3(2.5,1.1,0), rng.randf()*TAU, IWMaterials.brick(), "RuinWing")
	for i in 5:
		var pos := Vector3(rng.randf_range(-70,70), 0.55, rng.randf_range(-70,70))
		_add_static_box(Vector3(3.2,1.1,5.0), pos, rng.randf()*TAU, rust, "ScrapHull")

func _build_bounds() -> void:
	var mat := IWMaterials.olive_dark()
	for i in 4:
		var size := Vector3(240,3.0,5) if i % 2 == 0 else Vector3(5,3.0,240)
		var pos := Vector3(0,0.6,118 if i == 0 else (-118 if i == 2 else 0)) if i % 2 == 0 else Vector3(118 if i == 1 else -118,0.6,0)
		_add_static_box(size, pos, 0.0, mat, "Bound")
