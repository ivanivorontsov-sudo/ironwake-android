class_name TankController
extends Node
## Reads keyboard + virtual stick + aim drag; pushes input into LocalBattleSim.

var sim: LocalBattleSim
var stick: VirtualStick
var fire_held: bool = false
var brake_held: bool = false
var aim_yaw: float = PI
var aim_pitch: float = 0.0
var _aim_dragging: bool = false
var _last_aim_pos: Vector2 = Vector2.ZERO
var camera_mode: int = 0  # 0 chase, 1 fps/gunner
## Stick +X was turning the wrong way vs driver expectation — flip sign.
const STICK_STEER_SIGN := -1.0


func setup(p_sim: LocalBattleSim, p_stick: VirtualStick) -> void:
	sim = p_sim
	stick = p_stick
	var u: SimUnit = sim.get_unit(sim.local_player_id) if sim else null
	if u:
		aim_yaw = u.turret_yaw
		aim_pitch = u.gun_pitch


func toggle_camera() -> void:
	camera_mode = 1 - camera_mode


func _process(_delta: float) -> void:
	if sim == null or not sim.running:
		return
	var throttle := 0.0
	var steer := 0.0
	if stick and stick.value.length() > 0.05:
		throttle = -stick.value.y
		steer = stick.value.x * STICK_STEER_SIGN
	if Input.is_action_pressed("move_forward"):
		throttle = maxf(throttle, 1.0)
	if Input.is_action_pressed("move_back"):
		throttle = minf(throttle, -0.4)
	# Godot +yaw is CCW from above = turn LEFT when facing +Z.
	# Stick: STICK_STEER_SIGN flips +X (finger right) -> -steer -> CW = turn right.
	# Keys: Left = +yaw (CCW/left), Right = -yaw (CW/right).
	if Input.is_action_pressed("steer_left"):
		steer = 1.0
	if Input.is_action_pressed("steer_right"):
		steer = -1.0

	var fire := fire_held or Input.is_action_just_pressed("fire")
	var brake := brake_held or Input.is_action_pressed("brake")

	if Input.is_action_just_pressed("camera_toggle"):
		toggle_camera()

	if Input.get_mouse_mode() == Input.MOUSE_MODE_CAPTURED:
		var motion := Input.get_last_mouse_velocity() * 0.00002
		aim_yaw -= motion.x
		aim_pitch -= motion.y
		aim_pitch = clampf(aim_pitch, deg_to_rad(-12), deg_to_rad(18))

	sim.set_local_input({
		"throttle": throttle,
		"steer": steer,
		"brake": brake,
		"fire": fire,
		"aim_yaw": aim_yaw,
		"aim_pitch": aim_pitch,
	})


func release_aim_input() -> void:
	_aim_dragging = false
	_last_aim_pos = Vector2.ZERO

func handle_aim_input(event: InputEvent) -> void:
	if event is InputEventScreenTouch:
		var st := event as InputEventScreenTouch
		var vp := get_viewport().get_visible_rect().size
		if st.pressed and st.position.x > vp.x * 0.45:
			_aim_dragging = true
			_last_aim_pos = st.position
		elif not st.pressed:
			_aim_dragging = false
	elif event is InputEventScreenDrag and _aim_dragging:
		var sd := event as InputEventScreenDrag
		var delta := sd.position - _last_aim_pos
		_last_aim_pos = sd.position
		aim_yaw -= delta.x * 0.004
		aim_pitch -= delta.y * 0.003
		aim_pitch = clampf(aim_pitch, deg_to_rad(-12), deg_to_rad(18))
	elif event is InputEventMouseButton:
		var mb := event as InputEventMouseButton
		if mb.button_index == MOUSE_BUTTON_RIGHT:
			if mb.pressed:
				Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)
			else:
				Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
