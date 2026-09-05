class_name IWMaterials
extends RefCounted
## Shared StandardMaterial3D factories — olive military paints, never magenta.


static func olive(roughness: float = 0.82, metallic: float = 0.18) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(0.28, 0.32, 0.22)
	m.roughness = roughness
	m.metallic = metallic
	return m


static func olive_dark() -> StandardMaterial3D:
	var m := olive(0.88, 0.12)
	m.albedo_color = Color(0.18, 0.22, 0.14)
	return m


static func olive_light() -> StandardMaterial3D:
	var m := olive(0.78, 0.2)
	m.albedo_color = Color(0.36, 0.40, 0.28)
	return m


static func track() -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(0.12, 0.12, 0.11)
	m.roughness = 0.95
	m.metallic = 0.55
	return m


static func rubber() -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(0.08, 0.08, 0.08)
	m.roughness = 0.92
	m.metallic = 0.05
	return m


static func rust_metal() -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(0.35, 0.22, 0.14)
	m.roughness = 0.9
	m.metallic = 0.4
	return m


static func sandbag() -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(0.55, 0.48, 0.32)
	m.roughness = 0.95
	m.metallic = 0.0
	return m


static func concrete() -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(0.45, 0.44, 0.40)
	m.roughness = 0.92
	m.metallic = 0.05
	return m


static func fire_emissive() -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(1.0, 0.35, 0.05)
	m.emission_enabled = true
	m.emission = Color(1.0, 0.4, 0.05)
	m.emission_energy_multiplier = 4.0
	m.roughness = 0.6
	return m


static func tracer() -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = Color(1.0, 0.85, 0.35)
	m.emission_enabled = true
	m.emission = Color(1.0, 0.7, 0.2)
	m.emission_energy_multiplier = 6.0
	m.roughness = 0.4
	return m


static func dirt_ground() -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	var diff: Texture2D = load("res://assets/textures/aerial_grass_rock_diff_1k.jpg")
	var nor: Texture2D = load("res://assets/textures/aerial_grass_rock_nor_gl_1k.jpg")
	var rough: Texture2D = load("res://assets/textures/aerial_grass_rock_rough_1k.jpg")
	if diff:
		m.albedo_texture = diff
		m.uv1_scale = Vector3(24, 24, 24)
	else:
		m.albedo_color = Color(0.42, 0.36, 0.24)
	if nor:
		m.normal_enabled = true
		m.normal_texture = nor
		m.normal_scale = 0.85
	if rough:
		m.roughness_texture = rough
		m.roughness = 1.0
	else:
		m.roughness = 0.95
	m.metallic = 0.0
	return m


static func team_tint(base: StandardMaterial3D, team: String) -> StandardMaterial3D:
	var m := base.duplicate() as StandardMaterial3D
	if team == "red":
		m.albedo_color = m.albedo_color.lerp(Color(0.45, 0.22, 0.16), 0.35)
	else:
		m.albedo_color = m.albedo_color.lerp(Color(0.22, 0.32, 0.42), 0.28)
	return m
