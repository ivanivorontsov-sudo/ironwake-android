extends CanvasLayer
## Non-overlapping mobile HUD: stick, fire, camera, hangar, iron-sight crosshair.

signal fire_pressed
signal fire_released
signal camera_pressed
signal hangar_pressed

@onready var stick: VirtualStick = $Root/Stick
@onready var btn_fire: Button = $Root/BtnFire
@onready var btn_camera: Button = $Root/BtnCamera
@onready var btn_hangar: Button = $Root/BtnHangar
@onready var hp_bar: ProgressBar = $Root/HpBar
@onready var status_label: Label = $Root/Status
@onready var modules_label: Label = $Root/Modules
@onready var crosshair: Control = $Root/Crosshair


func _ready() -> void:
	btn_fire.button_down.connect(func(): fire_pressed.emit())
	btn_fire.button_up.connect(func(): fire_released.emit())
	btn_camera.pressed.connect(func(): camera_pressed.emit())
	btn_hangar.pressed.connect(func(): hangar_pressed.emit())
	if crosshair:
		crosshair.queue_redraw()


func set_status(text: String) -> void:
	if status_label:
		status_label.text = text


func set_hp(cur: float, mx: float) -> void:
	if hp_bar:
		hp_bar.max_value = maxf(1.0, mx)
		hp_bar.value = cur


func set_modules(mods: Dictionary, on_fire: bool) -> void:
	if modules_label == null:
		return
	var parts: PackedStringArray = []
	if on_fire:
		parts.append("🔥 ОГОНЬ")
	for k in ["gun", "engine", "track_l", "track_r", "ammo", "optics"]:
		var v := float(mods.get(k, 1.0))
		if v < 0.5:
			parts.append("%s %.0f%%" % [k, v * 100.0])
	modules_label.text = " · ".join(parts)


func set_gunner_mode(on: bool) -> void:
	if crosshair and crosshair.has_method("set_gunner_mode"):
		crosshair.set_gunner_mode(on)
	elif crosshair:
		crosshair.queue_redraw()
