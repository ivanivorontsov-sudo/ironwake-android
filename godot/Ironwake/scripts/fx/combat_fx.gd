class_name CombatFx
extends Node3D
## Pooled-ish, allocation-light combat VFX: flash, tracer, impact, dust, smoke and explosion.

var _rng := RandomNumberGenerator.new()

func _ready() -> void:
	_rng.seed = 1207

func spawn_tracer(from: Vector3, velocity: Vector3) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var cyl := CylinderMesh.new()
	cyl.top_radius = 0.045
	cyl.bottom_radius = 0.025
	cyl.height = 2.4
	cyl.radial_segments = 5
	mi.mesh = cyl
	mi.material_override = IWMaterials.tracer()
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mi)
	_orient_tracer(mi, from, velocity)
	return mi

func update_tracer(mi: MeshInstance3D, pos: Vector3, velocity: Vector3) -> void:
	_orient_tracer(mi, pos, velocity)

func _orient_tracer(mi: MeshInstance3D, pos: Vector3, velocity: Vector3) -> void:
	mi.global_position = pos
	if velocity.length_squared() < 0.01:
		return
	var y := velocity.normalized()
	var x := y.cross(Vector3.UP)
	if x.length_squared() < 0.001:
		x = y.cross(Vector3.RIGHT)
	x = x.normalized()
	var z := x.cross(y).normalized()
	mi.global_transform = Transform3D(Basis(x, y, z), pos)

func spawn_muzzle_flash(pos: Vector3, dir: Vector3) -> void:
	var root := Node3D.new()
	add_child(root)
	root.global_position = pos + dir.normalized() * 0.25
	var core := MeshInstance3D.new()
	var cone := PrismMesh.new()
	cone.size = Vector3(0.72, 0.72, 1.35)
	core.mesh = cone
	core.material_override = IWMaterials.muzzle_flash()
	core.scale = Vector3(1.0, 1.0, 1.4)
	root.add_child(core)
	var light := OmniLight3D.new()
	light.light_color = Color(1.0, 0.55, 0.18)
	light.light_energy = 7.0
	light.omni_range = 9.0
	root.add_child(light)
	var tw := create_tween()
	tw.set_parallel(true)
	tw.tween_property(root, "scale", Vector3(0.2,0.2,0.2), 0.075)
	tw.tween_property(light, "light_energy", 0.0, 0.075)
	tw.set_parallel(false)
	tw.tween_callback(root.queue_free)

func spawn_hit_sparks(pos: Vector3) -> void:
	var root := Node3D.new()
	add_child(root)
	root.global_position = pos
	for i in 10:
		var spark := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(0.055, 0.055, _rng.randf_range(0.22, 0.48))
		spark.mesh = box
		spark.material_override = IWMaterials.spark()
		root.add_child(spark)
		var dir := Vector3(_rng.randf_range(-1,1), _rng.randf_range(0.15,1.0), _rng.randf_range(-1,1)).normalized()
		spark.position = dir * 0.08
		spark.look_at(spark.global_position + dir, Vector3.UP)
		var tw := create_tween()
		tw.tween_property(spark, "position", dir * _rng.randf_range(0.7,1.8), 0.22)
		tw.tween_callback(spark.queue_free)
	var light := OmniLight3D.new()
	light.light_color = Color(1.0,0.42,0.12)
	light.light_energy = 4.0
	light.omni_range = 5.5
	root.add_child(light)
	var lt := create_tween()
	lt.tween_property(light, "light_energy", 0.0, 0.20)
	lt.tween_callback(root.queue_free)

func spawn_impact(pos: Vector3, heavy: bool = false) -> void:
	spawn_hit_sparks(pos)
	var root := Node3D.new()
	add_child(root)
	root.global_position = pos
	var dust := MeshInstance3D.new()
	var sphere := SphereMesh.new()
	sphere.radius = 0.35 if heavy else 0.22
	sphere.height = sphere.radius * 2.0
	dust.mesh = sphere
	dust.material_override = IWMaterials.smoke_mat()
	root.add_child(dust)
	var tw := create_tween()
	tw.set_parallel(true)
	tw.tween_property(dust, "scale", Vector3(3.0,1.4,3.0) if heavy else Vector3(1.8,1.0,1.8), 0.45)
	tw.tween_property(dust, "modulate:a", 0.0, 0.45)
	tw.set_parallel(false)
	tw.tween_callback(root.queue_free)

func spawn_explosion(pos: Vector3, heavy: bool = true) -> void:
	var root := Node3D.new()
	add_child(root)
	root.global_position = pos
	var fire := MeshInstance3D.new()
	var sph := SphereMesh.new()
	sph.radius = 0.55 if heavy else 0.38
	sph.height = sph.radius * 2.0
	fire.mesh = sph
	fire.material_override = IWMaterials.fire_emissive()
	root.add_child(fire)
	var light := OmniLight3D.new()
	light.light_color = Color(1.0,0.34,0.06)
	light.light_energy = 10.0 if heavy else 6.0
	light.omni_range = 12.0 if heavy else 8.0
	root.add_child(light)
	for i in (12 if heavy else 7):
		var ember := MeshInstance3D.new()
		var e := SphereMesh.new()
		e.radius = 0.06
		e.height = 0.12
		ember.mesh = e
		ember.material_override = IWMaterials.spark()
		root.add_child(ember)
		var dir := Vector3(_rng.randf_range(-1,1), _rng.randf_range(0.1,1), _rng.randf_range(-1,1)).normalized()
		var tw := create_tween()
		tw.tween_property(ember, "position", dir * _rng.randf_range(1.0,3.0), 0.45)
		tw.tween_callback(ember.queue_free)
	var tw := create_tween()
	tw.set_parallel(true)
	tw.tween_property(fire, "scale", Vector3(2.8,2.8,2.8), 0.20)
	tw.tween_property(fire, "modulate:a", 0.0, 0.38)
	tw.tween_property(light, "light_energy", 0.0, 0.30)
	tw.set_parallel(false)
	tw.tween_callback(root.queue_free)

func spawn_dust_cloud(pos: Vector3, scale_factor: float = 1.0) -> void:
	var root := Node3D.new()
	add_child(root)
	root.global_position = pos
	for i in 4:
		var puff := MeshInstance3D.new()
		var s := SphereMesh.new()
		s.radius = _rng.randf_range(0.22,0.42) * scale_factor
		s.height = s.radius * 2.0
		puff.mesh = s
		puff.material_override = IWMaterials.smoke_mat()
		puff.position = Vector3(_rng.randf_range(-0.5,0.5), _rng.randf_range(0.0,0.3), _rng.randf_range(-0.5,0.5)) * scale_factor
		root.add_child(puff)
		var tw := create_tween()
		tw.tween_property(puff, "position", puff.position + Vector3(0, _rng.randf_range(0.5,1.4), 0), 0.55)
		tw.parallel().tween_property(puff, "scale", Vector3.ONE * 2.2, 0.55)
		tw.parallel().tween_property(puff, "modulate:a", 0.0, 0.55)
	var end := create_tween()
	end.tween_interval(0.58)
	end.tween_callback(root.queue_free)

func play_shot_stub() -> void:
	pass

func play_hit_stub() -> void:
	pass
