class_name IWMaterials
extends RefCounted
## Shared mobile-friendly PBR materials. Textures are reused; no runtime network assets.

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
	return _base(Color(0.13, 0.16, 0.11), 0.82, 0.20)

static func olive_light() -> StandardMaterial3D:
	return _base(Color(0.39, 0.43, 0.29), 0.66, 0.30)

static func painted_armor(team: String = "blue") -> StandardMaterial3D:
	var m := _base(Color(0.25, 0.29, 0.19), 0.67, 0.30)
	m.clearcoat_enabled = true
	m.clearcoat = 0.12
	m.clearcoat_roughness = 0.42
	m.albedo_color = m.albedo_color.lerp(Color(0.16, 0.27, 0.18), 0.28)
	if team == "red":
		m.albedo_color = m.albedo_color.lerp(Color(0.43, 0.14, 0.10), 0.28)
	else:
		m.albedo_color = m.albedo_color.lerp(Color(0.12, 0.23, 0.31), 0.20)
	return m

static func camouflage(base: StandardMaterial3D, variant: int = 0) -> StandardMaterial3D:
	var m := base.duplicate() as StandardMaterial3D
	var a := m.albedo_color
	var dark := a.darkened(0.32)
	var light := a.lightened(0.16)
	m.albedo_color = [a, dark, light][variant % 3]
	m.roughness = clampf(m.roughness + 0.05, 0.0, 1.0)
	return m

static func track() -> StandardMaterial3D:
	return _base(Color(0.075, 0.08, 0.07), 0.94, 0.62)

static func rubber() -> StandardMaterial3D:
	return _base(Color(0.035, 0.04, 0.035), 0.97, 0.02)

static func rust_metal() -> StandardMaterial3D:
	return _base(Color(0.32, 0.17, 0.09), 0.90, 0.50)

static func sandbag() -> StandardMaterial3D:
	return _base(Color(0.55, 0.46, 0.30), 0.97, 0.0)

static func concrete() -> StandardMaterial3D:
	return _base(Color(0.43, 0.42, 0.39), 0.92, 0.04)

static func brick() -> StandardMaterial3D:
	return _base(Color(0.36, 0.22, 0.17), 0.90, 0.02)

static func wood_plank() -> StandardMaterial3D:
	return _base(Color(0.27, 0.19, 0.10), 0.92, 0.0)

static func rock() -> StandardMaterial3D:
	return _base(Color(0.30, 0.29, 0.26), 0.96, 0.03)

static func fire_emissive() -> StandardMaterial3D:
	var m := _base(Color(1.0, 0.18, 0.025), 0.42, 0.0)
	m.emission_enabled = true
	m.emission = Color(1.0, 0.20, 0.025)
	m.emission_energy_multiplier = 8.0
	m.shading_mode = BaseMaterial3D.SHADING_MODE_PER_PIXEL
	return m

static func smoke_mat() -> StandardMaterial3D:
	var m := _base(Color(0.16, 0.17, 0.16, 0.48), 1.0, 0.0)
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.cull_mode = BaseMaterial3D.CULL_DISABLED
	return m

static func tracer() -> StandardMaterial3D:
	var m := _base(Color(1.0, 0.72, 0.18), 0.30, 0.0)
	m.emission_enabled = true
	m.emission = Color(1.0, 0.46, 0.06)
	m.emission_energy_multiplier = 10.0
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	return m

static func muzzle_flash() -> StandardMaterial3D:
	var m := _base(Color(1.0, 0.62, 0.12, 0.92), 0.25, 0.0)
	m.emission_enabled = true
	m.emission = Color(1.0, 0.34, 0.025)
	m.emission_energy_multiplier = 18.0
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	m.billboard_mode = BaseMaterial3D.BILLBOARD_ENABLED
	return m

static func spark() -> StandardMaterial3D:
	var m := _base(Color(1.0, 0.62, 0.12), 0.30, 0.0)
	m.emission_enabled = true
	m.emission = Color(1.0, 0.28, 0.03)
	m.emission_energy_multiplier = 12.0
	m.shading_mode = BaseMaterial3D.SHADING_MODE_UNSHADED
	return m

static func dirt_ground() -> StandardMaterial3D:
	var m := StandardMaterial3D.new()
	var diff: Texture2D = load("res://assets/textures/aerial_grass_rock_diff_1k.jpg")
	var nor: Texture2D = load("res://assets/textures/aerial_grass_rock_nor_gl_1k.jpg")
	var rough: Texture2D = load("res://assets/textures/aerial_grass_rock_rough_1k.jpg")
	m.albedo_color = Color(0.50, 0.45, 0.34)
	if diff:
		m.albedo_texture = diff
		m.uv1_scale = Vector3(20, 20, 20)
	if nor:
		m.normal_enabled = true
		m.normal_texture = nor
		m.normal_scale = 1.05
	if rough:
		m.roughness_texture = rough
	m.roughness = 0.92
	m.metallic = 0.0
	return m

static func dirt_patch() -> StandardMaterial3D:
	var m := dirt_ground().duplicate() as StandardMaterial3D
	m.uv1_scale = Vector3(7, 7, 7)
	m.albedo_color = Color(0.60, 0.50, 0.34)
	return m
