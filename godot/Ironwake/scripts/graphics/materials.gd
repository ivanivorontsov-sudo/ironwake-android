class_name IWMaterials
extends RefCounted
## Shared StandardMaterial3D factories — olive military PBR paints, never magenta.


static func _base(albedo: Color, roughness: float, metallic: float) -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	m.albedo_color = albedo
	m.roughness = roughness
	m.metallic = metallic
	m.specular_mode = BaseMaterial3D.SPECULAR_SCHLICK_GGX
	return m


static func olive(roughness: float = 0.72, metallic: float = 0.28) -> StandardMaterial3D:
	return _base(Color(0.30, 0.34, 0.24), roughness, metallic)


static func olive_dark() -> StandardMaterial3D:
	var m := olive(0.78, 0.22)
	m.albedo_color = Color(0.16, 0.19, 0.13)
	return m


static func olive_light() -> StandardMaterial3D:
	var m := olive(0.68, 0.3)
	m.albedo_color = Color(0.38, 0.42, 0.30)
	return m


static func painted_armor(team: String = "blue") -> StandardMaterial3D:
	var m := olive(0.62, 0.35)
	m.albedo_color = Color(0.27, 0.31, 0.21)
	m.clearcoat_enabled = true
	m.clearcoat = 0.18
	m.clearcoat_roughness = 0.35
	return team_tint(m, team)


static func track() -> StandardMaterial3D:
	var m := _base(Color(0.10, 0.10, 0.09), 0.92, 0.62)
	return m


static func rubber() -> StandardMaterial3D:
	return _base(Color(0.07, 0.07, 0.07), 0.94, 0.04)


static func rust_metal() -> StandardMaterial3D:
	var m := _base(Color(0.38, 0.22, 0.12), 0.88, 0.48)
	return m


static func sandbag() -> StandardMaterial3D:
	return _base(Color(0.58, 0.50, 0.34), 0.96, 0.0)


static func concrete() -> StandardMaterial3D:
	var m := _base(Color(0.48, 0.46, 0.42), 0.9, 0.04)
	m.normal_enabled = false
	return m


static func brick() -> StandardMaterial3D:
	return _base(Color(0.42, 0.28, 0.22), 0.88, 0.02)


static func wood_plank() -> StandardMaterial3D:
	return _base(Color(0.32, 0.24, 0.14), 0.9, 0.0)


static func rock() -> StandardMaterial3D:
	return _base(Color(0.36, 0.34, 0.30), 0.93, 0.05)


static func fire_emissive() -> StandardMaterial3D:
	var m := _base(Color(1.0, 0.35, 0.05), 0.55, 0.0)
	m.emission_enabled = true
	m.emission = Color(1.0, 0.42, 0.06)
	m.emission_energy_multiplier = 5.5
	m.shading_mode = BaseMaterial3D.SHADING_MODE_PER_PIXEL
	return m


static func smoke_mat() -> StandardMaterial3D:
	var m := _base(Color(0.22, 0.22, 0.22, 0.55), 1.0, 0.0)
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	m.emission_enabled = true
	m.emission = Color(0.15, 0.15, 0.15)
	m.emission_energy_multiplier = 0.2
	return m


static func tracer() -> StandardMaterial3D:
	var m := _base(Color(1.0, 0.88, 0.4), 0.35, 0.0)
	m.emission_enabled = true
	m.emission = Color(1.0, 0.75, 0.25)
	m.emission_energy_multiplier = 8.0
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	return m


static func muzzle_flash() -> StandardMaterial3D:
	var m := _base(Color(1.0, 0.85, 0.45), 0.4, 0.0)
	m.emission_enabled = true
	m.emission = Color(1.0, 0.7, 0.2)
	m.emission_energy_multiplier = 12.0
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.albedo_color.a = 0.9
	return m


static func spark() -> StandardMaterial3D:
	var m := _base(Color(1.0, 0.7, 0.25), 0.4, 0.0)
	m.emission_enabled = true
	m.emission = Color(1.0, 0.55, 0.1)
	m.emission_energy_multiplier = 10.0
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	return m


static func dirt_ground() -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	var diff: Texture2D = load("res://assets/textures/aerial_grass_rock_diff_1k.jpg")
	var nor: Texture2D = load("res://assets/textures/aerial_grass_rock_nor_gl_1k.jpg")
	var rough: Texture2D = load("res://assets/textures/aerial_grass_rock_rough_1k.jpg")
	m.albedo_color = Color(0.55, 0.50, 0.38)
	if diff:
		m.albedo_texture = diff
		m.uv1_scale = Vector3(18, 18, 18)
	else:
		m.albedo_color = Color(0.42, 0.36, 0.24)
	if nor:
		m.normal_enabled = true
		m.normal_texture = nor
		m.normal_scale = 1.15
	if rough:
		m.roughness_texture = rough
		m.roughness = 1.0
	else:
		m.roughness = 0.95
	m.metallic = 0.0
	m.specular_mode = BaseMaterial3D.SPECULAR_SCHLICK_GGX
	return m


static func dirt_patch() -> StandardMaterial3D:
	var m := dirt_ground().duplicate() as StandardMaterial3D
	m.uv1_scale = Vector3(6, 6, 6)
	m.albedo_color = Color(0.62, 0.52, 0.36)
	return m


static func team_tint(base: StandardMaterial3D, team: String) -> StandardMaterial3D:
	var m := base.duplicate() as StandardMaterial3D
	if team == "red":
		m.albedo_color = m.albedo_color.lerp(Color(0.48, 0.20, 0.14), 0.38)
	else:
		m.albedo_color = m.albedo_color.lerp(Color(0.20, 0.30, 0.40), 0.30)
	return m
