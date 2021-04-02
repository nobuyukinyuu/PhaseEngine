tool
extends HSlider
var font = preload("res://gfx/fonts/spelunkid_font.tres")
var font2 = preload("res://gfx/fonts/numerics_8x10.tres")
const charw = 8
onready var lblPos = Vector2(rect_size.x - len(name)*charw + text_offset, -2)
onready var lblPos2 = Vector2(rect_size.x/2 - (len(str(value))+1)*charw/2.0, rect_size.y/2)

export(int,-32,32) var text_offset = 0 setget set_offset
var needs_recalc=false

func set_offset(val):
	text_offset = val
	needs_recalc = true
	update()


func _ready():
	connect("resized", self, "_on_Resize")
	pass # Replace with function body.


func _draw():
	if needs_recalc:  recalc()
	var col = ColorN("yellow") if has_focus() else ColorN("white")
	draw_string(font, lblPos, name, col)
#	draw_string(font2, lblPos2, str(value), col )
	draw_string(font2, calc_pos2(), str(value), col )

func _on_Resize():
	needs_recalc = true;


#func _notification(what):
#	match what:
#		NOTIFICATION_RESIZED

func recalc():
	lblPos = Vector2(rect_size.x - len(name)*charw + text_offset, -2)
	lblPos2 = Vector2(rect_size.x/2 - (len(str(value))+1)*charw/2.0, rect_size.y/2)
	needs_recalc = false
	
func calc_pos2():
	return Vector2(rect_size.x*0.5 - (len(str(self.value))+1)*charw*0.5, lblPos2.y)
