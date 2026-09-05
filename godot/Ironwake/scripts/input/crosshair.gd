extends Control
## Centered iron-sight / crosshair for chase + gunner modes.

var gunner_mode: bool = false


func set_gunner_mode(on: bool) -> void:
	gunner_mode = on
	queue_redraw()


func _draw() -> void:
	var c := size * 0.5
	var col := Color(0.95, 0.92, 0.55, 0.92 if gunner_mode else 0.7)
	var col_dim := Color(0.2, 0.25, 0.15, 0.55)
	var gap := 10.0 if gunner_mode else 14.0
	var arm := 28.0 if gunner_mode else 22.0
	var thick := 2.0
	draw_arc(c, 34.0 if gunner_mode else 26.0, 0, TAU, 48, col_dim, 1.5, true)
	if gunner_mode:
		draw_arc(c, 48.0, 0, TAU, 48, Color(0.85, 0.8, 0.4, 0.35), 1.0, true)
	draw_line(c + Vector2(-arm, 0), c + Vector2(-gap, 0), col, thick, true)
	draw_line(c + Vector2(gap, 0), c + Vector2(arm, 0), col, thick, true)
	draw_line(c + Vector2(0, -arm), c + Vector2(0, -gap), col, thick, true)
	draw_line(c + Vector2(0, gap), c + Vector2(0, arm), col, thick, true)
	draw_circle(c, 2.2, col)
	if gunner_mode:
		for ang in [0.0, PI * 0.5, PI, PI * 1.5]:
			var d := Vector2(cos(ang), sin(ang)) * 48.0
			draw_circle(c + d, 2.0, Color(0.9, 0.7, 0.3, 0.5))
