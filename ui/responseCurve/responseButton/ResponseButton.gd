extends Button

const icon_offset = Vector2(1, 4)
const no_preview = preload("res://gfx/ui/none16.png")

enum ranges {rates, velocity, levels}
export (ranges) var intent = ranges.rates

signal table_updated
signal minmax_changed

func _ready():
	$P/Curve.set_intent(intent)
	$P/Curve.set_table_default(intent)


	connect("pressed", self, "_on_Pressed")
	$P/Curve/VU.connect("table_updated", self, "_on_Curve_updated")



func _on_Pressed():
	var pos = get_global_mouse_position()
	pos.x = min(pos.x, get_viewport().get_visible_rect().size.x - $P/Curve.rect_size.x - 4)
	pos.y = min(pos.y+16, get_viewport().get_visible_rect().size.y - $P/Curve.rect_size.y)
	$P.popup(Rect2(pos, $P/Curve.rect_size))



func table(pos:int):
	return $P/Curve/VU.tbl[pos]


func _on_P_popup_hide():
	#Update the entire table.
	emit_signal("table_updated", -1, -1, intent)  #-1 tells EGPanel to blast the update all message to the chip.


func _on_Curve_updated(column, value):
	#Update some column(s) of the table we represent for the EGPanel to catch.
	emit_signal("table_updated", column, value, intent)
	update()



func _draw():
	
	var previous_ln=Vector2.ZERO
	var enabled=false

	#Check to see if we should draw the graph preview.
	if $P/Curve/MinMax/sldMax.value != 0:  enabled = true

	if enabled:  #Check for empty table.  We should still show OFF if empty.
		enabled = false
		for i in $P/Curve/VU.tbl.size():
			if $P/Curve/VU.tbl[i] == 0:  continue
			#Whoops, we reached a nonzero value.  Curve's enabled.
			enabled = true
			break;
	
	if !enabled:
		draw_texture(no_preview, icon_offset)
		return
	else:
		for i in range(16):
			var offset = Vector2(icon_offset.x + i, 0)
			
			var val = $P/Curve/VU.tbl[min(127, i * 8)]
			offset.y = (val / 8.0)
			if val > 0:  
				enabled=true
	#			print (name, ": ", "index ", i, ";  ", offset.y, ".  Tbl: ", val)
			
			var pos2 = Vector2(offset.x, 16)
			offset.y = 16 - offset.y
			offset.y += icon_offset.y
			pos2.y += icon_offset.y

			
			if i ==0:  
				previous_ln = offset
				continue
			draw_line(offset, pos2, Color(1,1,1, 0.25), 1.0, false)
			draw_line(offset, previous_ln, Color(1,1,1), 1.0, false)

			previous_ln = offset
	
#	for x in 16:
#		var offset = icon_offset
#		offset.x += x
#		offset.y += 16
#		var pos2 = Vector2(offset.x, offset.y - ease((16-x)/16.0, 2)*16 )
#		draw_line(offset, pos2, Color(1,1,1, 0.25), 1.0, false)
#		draw_line(pos2, Vector2(pos2.x-1, pos2.y), Color(1,1,1), 1, true)


func _on_Curve_minmax_changed(value, isMax:bool=false):
	emit_signal("minmax_changed", value, isMax, intent)
