extends Button

const icon_offset = Vector2(1, 4)
const no_preview = preload("res://gfx/ui/none16.png")

enum ranges {rates, velocity, levels}
export (ranges) var intent = ranges.rates



func _ready():
	$P/Curve.set_intent(intent)
	$P/Curve.set_table_default(intent)


	connect("pressed", self, "_on_Pressed")



func _on_Pressed():
	var pos = get_global_mouse_position()
	pos.x = min(pos.x, get_viewport().get_visible_rect().size.x - $P/Curve.rect_size.x - 4)
	pos.y = min(pos.y+16, get_viewport().get_visible_rect().size.y - $P/Curve.rect_size.y)
	$P.popup(Rect2(pos, $P/Curve.rect_size))



func table(pos:int):
	return $P/Curve/VU.tbl[pos]


func _draw():
	draw_texture(no_preview, icon_offset)
	
	
#	for x in 16:
#		var offset = icon_offset
#		offset.x += x
#		offset.y += 16
#		var pos2 = Vector2(offset.x, offset.y - ease((16-x)/16.0, 2)*16 )
#		draw_line(offset, pos2, Color(1,1,1, 0.25), 1.0, false)
#		draw_line(pos2, Vector2(pos2.x-1, pos2.y), Color(1,1,1), 1, true)
