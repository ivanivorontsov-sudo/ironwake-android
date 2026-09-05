class_name VirtualStick
extends Control
## Left-side virtual joystick for mobile.

signal stick_changed(vec: Vector2)

@export var knob_radius: float = 36.0
@export var base_radius: float = 72.0

var _touch_index: int = -1
var _center: Vector2 = Vector2.ZERO
var _knob: Vector2 = Vector2.ZERO
var value: Vector2 = Vector2.ZERO


func _ready() -> void:
	mouse_filter = Control.MOUSE_FILTER_STOP
	_center = size * 0.5
	_knob = _center
	resized.connect(_on_resized)


func _on_resized() -> void:
	_center = size * 0.5
	if _touch_index < 0:
		_knob = _center


func _gui_input(event: InputEvent) -> void:
	if event is InputEventScreenTouch:
		var st := event as InputEventScreenTouch
		if st.pressed and _touch_index < 0 and _in_base(st.position):
			_touch_index = st.index
			_update_knob(st.position)
			accept_event()
		elif not st.pressed and st.index == _touch_index:
			_touch_index = -1
			_knob = _center
			value = Vector2.ZERO
			stick_changed.emit(value)
			queue_redraw()
			accept_event()
	elif event is InputEventScreenDrag:
		var sd := event as InputEventScreenDrag
		if sd.index == _touch_index:
			_update_knob(sd.position)
			accept_event()
	elif event is InputEventMouseButton and DisplayServer.is_touchscreen_available() == false:
		var mb := event as InputEventMouseButton
		if mb.button_index == MOUSE_BUTTON_LEFT:
			if mb.pressed and _in_base(mb.position):
				_touch_index = 0
				_update_knob(mb.position)
				accept_event()
			elif not mb.pressed and _touch_index == 0:
				_touch_index = -1
				_knob = _center
				value = Vector2.ZERO
				stick_changed.emit(value)
				queue_redraw()
				accept_event()
	elif event is InputEventMouseMotion and _touch_index == 0:
		_update_knob((event as InputEventMouseMotion).position)
		accept_event()


func _in_base(pos: Vector2) -> bool:
	return pos.distance_to(_center) <= base_radius * 1.35


func _update_knob(pos: Vector2) -> void:
	var delta := pos - _center
	if delta.length() > base_radius:
		delta = delta.normalized() * base_radius
	_knob = _center + delta
	value = delta / base_radius
	stick_changed.emit(value)
	queue_redraw()


func _draw() -> void:
	draw_circle(_center, base_radius, Color(0.08, 0.1, 0.08, 0.45))
	draw_arc(_center, base_radius, 0, TAU, 48, Color(0.7, 0.75, 0.55, 0.55), 2.0, true)
	draw_circle(_knob, knob_radius, Color(0.35, 0.42, 0.28, 0.85))
	draw_arc(_knob, knob_radius, 0, TAU, 32, Color(0.85, 0.9, 0.7, 0.9), 2.0, true)
