extends Button

const icon_offset = Vector2(1, 4)
const no_preview = preload("res://gfx/ui/none16.png")
const font = preload("res://gfx/fonts/small_numerics_thin.tres")

enum ranges {rates, velocity, levels}
export (ranges) var intent = ranges.rates

var placeholder_tbl  #Table used when the ResponseCurve node is still a placeholder.  Populated by OP setter...

signal table_updated
signal minmax_changed

var lastmin:int=0
var lastmax:int=0

func _ready():
	connect("pressed", self, "_on_Pressed")


func readyup_instance():
	$P/Curve.replace_by_instance()
	$P/Curve.set_intent(intent)
	$P/Curve/VU.connect("table_updated", self, "_on_Curve_updated")
	$P/Curve.connect("minmax_changed", self, "_on_Curve_minmax_changed")
	
	if placeholder_tbl != null:
		$P/Curve.set_from_placeholder(placeholder_tbl)
	else:
		$P/Curve.set_table_default(intent)

func _on_Pressed():
	if $P/Curve is InstancePlaceholder:  readyup_instance()

	var pos = get_global_mouse_position()
	pos.x = min(pos.x, get_viewport().get_visible_rect().size.x - $P/Curve.rect_size.x - 4)
	pos.y = min(pos.y+16, get_viewport().get_visible_rect().size.y - $P/Curve.rect_size.y)
	$P.popup(Rect2(pos, $P/Curve.rect_size))


#Sets the table to the specified one.  Placeholder will be used to populate response curve if necessary,
#Since this placeholder should be passed from an EGPanel getting the data from a specified operator.
func init_table(tbl):  
	placeholder_tbl = tbl 
	#Convert the base64 table data to proper data.
	if placeholder_tbl["intent"] == "RATES":
		var raw = Marshalls.base64_to_raw(placeholder_tbl["tbl"])
		var t = []
		for i in raw.size(): t.append(raw[i] << 4)
		placeholder_tbl["tbl"] = t
	else:	
		placeholder_tbl["tbl"] = global.base64_to_table(placeholder_tbl["tbl"])  #2-byte conversion


func table(pos:int):  #Returns the response curve table if it exists (and no placeholder exists)
	if not $P/Curve is InstancePlaceholder or placeholder_tbl==null:
		return $P/Curve/VU.tbl[pos] 
	else: return placeholder_tbl["tbl"][pos]


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

	if $P/Curve is InstancePlaceholder:  
		if placeholder_tbl==null:  enabled=false
	else:
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
			
			var val = table( min(global.RT_MINUS_ONE, i * 8) )
			offset.y = (val / 64.0)
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
		#Draw font
		var s=str(lastmax)
		if lastmax > 0:  
			draw_string(font, icon_offset + Vector2(16-str_w(s)/2,-4),s,Color(2,2,2, 0.8))
			s = str(lastmin)
			if lastmin > 0 and lastmin < lastmax:  
				draw_string(font, icon_offset + Vector2(1-str_w(s)/2,12),s,Color(1.4,1.4,1.4, 0.7))


func str_w(s:String):
	return len(s) * 4

func _on_Curve_minmax_changed(value, isMax:bool=false):
	if isMax: lastmax = value 
	else: lastmin = value
	emit_signal("minmax_changed", value, isMax, intent)
	update()
