extends Control
var font = preload("res://gfx/fonts/numerics_5x8.tres")
var lbls = ["63", "56", "48", "40", "32", "24", "16", "8"]
const vals = [40, 74, 104, 128, 149, 166, 180, 192, 202, 211, 218, 224, 240]  #Value positions at tick for log
const TOTAL_HEIGHT = 256
var STEP_HEIGHT = 32
var CHAR_WIDTH = 5
func _ready():
	pass


func thin_font(yes=true):
	if yes: 
		font = preload("res://gfx/fonts/numerics_4x8.tres")
		CHAR_WIDTH = 4
	else:
		font = preload("res://gfx/fonts/numerics_5x8.tres")
		CHAR_WIDTH = 5

func _draw():
	var previous_h = 0
	if owner.log_scale:
		var last_val = 0
		for i in range(0,vals.size()):
			if vals[i] - last_val < 12:  continue
			var x = rect_size.x - len(lbls[i+1]) * CHAR_WIDTH
			draw_string(font, Vector2(x, vals[i]), lbls[i+1])
			last_val = vals[i]

		draw_string(font, Vector2(rect_size.x - len(lbls[lbls.size()-1])*CHAR_WIDTH, 0), lbls[lbls.size()-1])
		draw_string(font, Vector2(rect_size.x - len(lbls[0])*CHAR_WIDTH, TOTAL_HEIGHT), lbls[0])
		
	else:
		var h = 0
		for i in lbls.size():
			var x = rect_size.x - len(lbls[i]) * CHAR_WIDTH
			draw_string(font, Vector2(x, h), lbls[i])
			h += STEP_HEIGHT
		pass
