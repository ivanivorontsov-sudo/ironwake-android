class_name CombatFx
extends Node3D
## Tracers, muzzle flash, hit sparks, optional audio stub.


func spawn_tracer(from: Vector3, velocity: Vector3) -> MeshInstance3D:
	var mi := MeshInstance3D.new()
	var cyl := CylinderMesh.new()
	cyl.top_radius = 0.06
	cyl.bottom_radius = 0.04
	cyl.height = 1.8
	cyl.radial_segments = 6
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
	var up := velocity.normalized()
	# Cylinder default axis is Y — align Y with velocity.
	var basis := Basis()
	var y := up
	var x := y.cross(Vector3.UP)
	if x.length_squared() < 0.001:
		x = y.cross(Vector3.RIGHT)
	x = x.normalized()
	var z := x.cross(y).normalized()
	basis = Basis(x, y, z)
	mi.global_transform = Transform3D(basis, pos)


func spawn_muzzle_flash(pos: Vector3, dir: Vector3) -> void:
	var mi := MeshInstance3D.new()
	var sph := SphereMesh.new()
	sph.radius = 0.45
	sph.height = 0.9
	mi.mesh = sph
	mi.material_override = IWMaterials.muzzle_flash()
	mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF
	add_child(mi)
	mi.global_position = pos + dir.normalized() * 0.2
	var light := OmniLight3D.new()
	light.light_color = Color(1.0, 0.75, 0.35)
	light.light_energy = 4.5
	light.omni_range = 8.0
	mi.add_child(light)
	var tw := get_tree().create_timer(0.07)
	tw.timeout.connect(func():
		if is_instance_valid(mi):
			mi.queue_free()
	)


func spawn_hit_sparks(pos: Vector3) -> void:
	var root := Node3D.new()
	add_child(root)
	root.global_position = pos
	for i in 8:
		var spark := MeshInstance3D.new()
		var box := BoxMesh.new()
		box.size = Vector3(0.08, 0.08, 0.35)
		spark.mesh = box
		spark.material_override = IWMaterials.spark()
		root.add_child(spark)
		var dir := Vector3(randf_range(-1, 1), randf_range(0.2, 1.0), randf_range(-1, 1)).normalized()
		spark.position = dir * 0.15
		spark.look_at(spark.global_position + dir, Vector3.UP)
	var light := OmniLight3D.new()
	light.light_color = Color(1.0, 0.55, 0.2)
	light.light_energy = 3.0
	light.omni_range = 6.0
	root.add_child(light)
	var tw := get_tree().create_timer(0.18)
	tw.timeout.connect(func():
		if is_instance_valid(root):
			root.queue_free()
	)


func play_shot_stub() -> void:
	# Optional audible stub — no asset required; keep silent-safe.
	pass


func play_hit_stub() -> void:
	pass
