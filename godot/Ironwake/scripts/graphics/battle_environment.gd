class_name BattleEnvironment
extends Node3D
## Terrain, sky, sun+fill shadows — car-simulator quality, military dirt arena.


func build() -> void:
	_setup_environment()
	_build_ground()
	_build_hills()
	_build_props()
	_build_bounds()


func _setup_environment() -> void:
	var sky_mat := ProceduralSkyMaterial.new()
	sky_mat.sky_top_color = Color(0.35, 0.55, 0.78)
	sky_mat.sky_horizon_color = Color(0.72, 0.78, 0.82)
	sky_mat.ground_bottom_color = Color(0.28, 0.24, 0.18)
	sky_mat.ground_horizon_color = Color(0.55, 0.50, 0.38)
	sky_mat.sun_angle_max = 30.0
	sky_mat.sun_curve = 0.12
	var sky := Sky.new()
	sky.sky_material = sky_mat

	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_sky_contribution = 0.55
	env.ambient_light_energy = 0.72
	env.tonemap_mode = Environment.TONE_MAPPER_ACES
	env.tonemap_exposure = 1.05
	env.ssao_enabled = false  # mobile-friendly
	env.glow_enabled = true
	env.glow_intensity = 0.35
	env.glow_bloom = 0.08
	env.fog_enabled = true
	env.fog_light_color = Color(0.65, 0.72, 0.78)
	env.fog_density = 0.00055
	env.fog_aerial_perspective = 0.55
	env.adjustment_enabled = true
	env.adjustment_saturation = 1.08
	env.adjustment_contrast = 1.04

	var we := WorldEnvironment.new()
	we.environment = env
	add_child(we)

	var sun := DirectionalLight3D.new()
	sun.name = "Sun"
	sun.rotation_degrees = Vector3(-48.0, 38.0, 0.0)
	sun.light_energy = 1.55
	sun.light_color = Color(1.0, 0.94, 0.84)
	sun.shadow_enabled = true
	sun.shadow_blur = 1.0
	sun.directional_shadow_max_distance = 220.0
	sun.directional_shadow_mode = DirectionalLight3D.SHADOW_PARALLEL_2_SPLITS
	add_child(sun)

	var fill := DirectionalLight3D.new()
	fill.name = "Fill"
	fill.rotation_degrees = Vector3(-25.0, -120.0, 0.0)
	fill.light_energy = 0.28
	fill.light_color = Color(0.75, 0.82, 0.95)
	fill.shadow_enabled = false
	add_child(fill)


func _build_ground() -> void:
	var ground := MeshInstance3D.new()
	ground.name = "Ground"
	var plane := PlaneMesh.new()
	plane.size = Vector2(260, 260)
	plane.subdivide_width = 32
	plane.subdivide_depth = 32
	ground.mesh = plane
	ground.material_override = IWMaterials.dirt_ground()
	add_child(ground)

	var body := StaticBody3D.new()
	var col := CollisionShape3D.new()
	var shape := BoxShape3D.new()
	shape.size = Vector3(260, 1.0, 260)
	col.shape = shape
	col.position.y = -0.5
	body.add_child(col)
	add_child(body)


func _build_hills() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 42
	var mat := IWMaterials.dirt_ground().duplicate() as StandardMaterial3D
	mat.uv1_scale = Vector3(4, 4, 4)
	for i in 14:
		var hill := MeshInstance3D.new()
		var mesh := SphereMesh.new()
		mesh.radius = rng.randf_range(6.0, 14.0)
		mesh.height = mesh.radius * rng.randf_range(0.55, 0.9)
		mesh.radial_segments = 16
		mesh.rings = 8
		hill.mesh = mesh
		hill.material_override = mat
		var ang := rng.randf() * TAU
		var dist := rng.randf_range(45.0, 100.0)
		hill.position = Vector3(cos(ang) * dist, -mesh.height * 0.55, sin(ang) * dist)
		hill.scale = Vector3(1.4, 0.55, 1.4)
		add_child(hill)


func _build_props() -> void:
	var bag_mat := IWMaterials.sandbag()
	var conc := IWMaterials.concrete()
	var rust := IWMaterials.rust_metal()
	# berms / sandbag lines
	for i in 8:
		var berm := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(8.0, 1.2, 2.2)
		berm.mesh = box
		berm.material_override = bag_mat
		var a := i * TAU / 8.0 + 0.2
		berm.position = Vector3(cos(a) * 38.0, 0.6, sin(a) * 38.0)
		berm.rotation.y = -a
		add_child(berm)
	# ruined walls
	for i in 5:
		var wall := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(randf_range(3.0, 7.0), randf_range(1.5, 3.2), 0.45)
		wall.mesh = box
		wall.material_override = conc
		wall.position = Vector3(randf_range(-50, 50), box.size.y * 0.5, randf_range(-50, 50))
		wall.rotation.y = randf() * TAU
		add_child(wall)
	# scrap metal
	for i in 6:
		var scrap := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(randf_range(1.2, 2.5), 0.25, randf_range(0.8, 1.6))
		scrap.mesh = box
		scrap.material_override = rust
		scrap.position = Vector3(randf_range(-60, 60), 0.15, randf_range(-60, 60))
		scrap.rotation.y = randf() * TAU
		add_child(scrap)


func _build_bounds() -> void:
	# subtle edge berm so arena reads
	var mat := IWMaterials.olive_dark()
	for i in 4:
		var edge := MeshInstance3D.new()
		var box := BoxMesh.new()
		if i % 2 == 0:
			box.size = Vector3(230, 2.5, 4)
			edge.position = Vector3(0, 0.4, 112 if i == 0 else -112)
		else:
			box.size = Vector3(4, 2.5, 230)
			edge.position = Vector3(112 if i == 1 else -112, 0.4, 0)
		edge.mesh = box
		edge.material_override = mat
		add_child(edge)
