extends Control
var font = preload("res://gfx/fonts/numerics_5x8.tres")
var lbls = ["63", "56", "48", "40", "32", "24", "16", "8"]
var vals = [63, 56, 48, 40, 32, 24, 16, 8]
const TOTAL_HEIGHT = 256
var STEP_HEIGHT = 32
func _ready():
	pass


func _draw():
	var previous_h = 0
	if owner.log_scale:
		for i in range(1,vals.size()/2):
			var h = 256-round(clamp(global.xerp(0, 256, vals[i]/float(owner.hi)), 0, 256))
			var x = rect_size.x - len(lbls[lbls.size()-i-1])*5
			if h-previous_h < 12:   continue
			
			draw_string(font, Vector2(x, h), lbls[lbls.size()-i-1])
			previous_h = h

		draw_string(font, Vector2(rect_size.x - len(lbls[lbls.size()-1])*5, 0), lbls[lbls.size()-1])
		draw_string(font, Vector2(rect_size.x - len(lbls[0])*5, 256), lbls[0])
		
	else:
		var h = 0
		for i in lbls.size():
			draw_string(font, Vector2(0, h), lbls[i])
			h += STEP_HEIGHT
		pass
