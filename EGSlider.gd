tool
extends HSlider
var font = preload("res://gfx/fonts/spelunkid_font.tres")
var font2 = preload("res://gfx/fonts/numerics_8x10.tres")
const charw = 8
onready var lblPos = Vector2(rect_size.x - len(name)*charw, -2)
onready var lblPos2 = Vector2(rect_size.x/2 - (len(str(value))+1)*charw/2.0, rect_size.y/2)

func _ready():
	pass # Replace with function body.


func _draw():
	var col = ColorN("yellow") if has_focus() else ColorN("white")
	draw_string(font, lblPos, name, col)
	draw_string(font2, lblPos2, str(value), col )


func recalc():
	lblPos = Vector2(rect_size.x - len(name)*charw, -2)
	lblPos2 = Vector2(rect_size.x/2 - (len(str(value))+1)*charw/2.0, rect_size.y/2)
	pass
