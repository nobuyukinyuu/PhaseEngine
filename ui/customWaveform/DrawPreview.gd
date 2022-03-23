extends TextureRect

var tbl = []
var smooth_tbl = []
var display_tbl = []
var revert_tbl = []

const full16 = preload("res://gfx/ui/vu/full16_temp.png")
const indicator = preload("res://gfx/ui/vu/indicator16.png")

const light_color = Color("5b99c4")
const lighter_color = Color("80ccff")
const dark_color = Color("0072c4")

var elementWidth = 5  #How wide is a column?

export(int,1,255) var maxValue = 100

var cursor = preload("res://gfx/ui/vu/cursor.png").get_data()
var numbers = preload("res://gfx/fonts/small_numerics_thin.png").get_data()
var cursor_img = Image.new()
var cursor_texture=ImageTexture.new()
var custom_texture=ImageTexture.new()

#Held values to allow keyboard tweaking.
var last_column = 0
var last_value = 0

enum {NO, PROCESS_LEFT, PROCESS_MID, PROCESS_RIGHT, PROCESS_MIDDRAG, PROCESS_MIDUP}  #For input events

var changing=-1
const font = preload("res://gfx/fonts/numerics_7seg.tres")


var last_x=0   #Position to lock X to when holding shift.

var lineA=Rect2()  #Line positions for line-drawing (midclick)
var lineB=Rect2()  #Position is real x/y, size is table array pos + value

var fidelity_step:float = 1/65536.0
var quantize_step:float = 1

#Sets up table for use.  Imports a table from Chip and converts it to 0-100 float values we use.
func set_table(input):
	var output=[]
	revert_tbl=[]
	for i in input.size():
		var v=range_lerp(input[i], -32768, 32767, 0, 100)
		output.append(v)
		revert_tbl.append(v)
	tbl=output
	smooth_tbl=output
	display_tbl=output
	
	update()

func revert_to_original():
	tbl = Array(revert_tbl)
	update()

func _ready():
#	elementWidth = texture.get_width()
	if elementWidth == null:  elementWidth = 5

	for i in owner.wavetable_size:
		tbl.append(50)
		smooth_tbl.append(50)
		display_tbl.append(50)


	owner.connect("gui_input", self, "owner_input")

	connect("mouse_exited", self, "reset_relative_delta")

var relative_delta = Vector2.ZERO
func _gui_input(event):
	
	var process = NO
	if event is InputEventMouseMotion:
		if Input.is_mouse_button_pressed(BUTTON_LEFT): process = PROCESS_LEFT
		if Input.is_mouse_button_pressed(BUTTON_MIDDLE): process = PROCESS_MIDDRAG
		relative_delta = event.relative
	if event is InputEventMouseButton: 
		if event.button_index == BUTTON_LEFT and event.pressed: 
			process = PROCESS_LEFT
			last_x = get_local_mouse_position().x
		elif event.button_index == BUTTON_LEFT and !event.pressed:
			changing = -1
		if event.button_index == BUTTON_MIDDLE:
			if event.pressed:  
				process = PROCESS_MID  
			else:
				process = PROCESS_MIDUP
		if event.button_index == BUTTON_RIGHT and !event.pressed:
			#Popup copipe menu
			process = PROCESS_RIGHT
			var pop:PopupMenu = owner.get_node("CPMenu")
			pop.popup(Rect2(get_global_mouse_position(), pop.rect_size))

	if get_local_mouse_position().x < 0 or get_local_mouse_position().x > rect_size.x:  
		changing = -1
		update()
		return
	
	if process == PROCESS_LEFT:
		var locking = Input.is_key_pressed(KEY_SHIFT)
		var xy = get_table_pos(locking)
		var arpos = xy.x
		var val = xy.y

		if !locking:  
			xy = get_table_pos(locking, get_local_mouse_position() + -relative_delta)

		changing = val

#		tbl[arpos] = val  #Array position set to value.
		set_table_pos(arpos, val)  #Array position set to value.

		#Interpolate from last mouse position to fill gaps
		if abs(relative_delta.x) > 1:
			var arpos2 = clamp(xy.x, 0, tbl.size()-1)
			var stride = float(max(arpos, arpos2) - min(arpos, arpos2))
			for i in stride:
				var weight = i/stride
				var val2 = lerp(xy.y, val, weight if arpos>arpos2 else 1.0-weight)
				val2 = stepify(val2/100.0, fidelity_step) * 100
#				tbl[min(arpos, arpos2)+i] = lerp(val2, val, weight if arpos>arpos2 else 1.0-weight)
				set_table_pos( min(arpos, arpos2)+i, val2)


		update()
		return
	else:
#		Input.set_custom_mouse_cursor(null)
		changing=-1


	match process:
		PROCESS_MID:  #Start drawing a line.
			var xy = get_table_pos()
			lineA.position = get_local_mouse_position()
			lineA.size = xy
		PROCESS_MIDDRAG:
			lineB.position = get_local_mouse_position()
			update()
			pass
		PROCESS_MIDUP:
			process_line()


func owner_input(event):
	if event is InputEventMouseButton: 
		if event.button_index == BUTTON_MIDDLE and !event.pressed: 
			process_line()
	

func reset_relative_delta():  relative_delta = Vector2.ZERO

func process_line():
	lineB.position.x = clamp(lineB.position.x, 0, rect_size.x)
	lineB.position.y = clamp(lineB.position.y, 0, rect_size.y)
	
	lineB.size = get_table_pos(false, lineB.position)
	

	#Get start and end values and flip them if the end position was less than the start.	
	var startval = lineA.size.y
	var endval = lineB.size.y
	
	if lineB.size.x < lineA.size.x:
		var temp = startval
		startval = endval
		endval = temp
	
	var startpos = min(lineA.size.x, lineB.size.x)
	var endpos = max(lineA.size.x, lineB.size.x)
#	var startval = min(lineA.size.y, lineB.size.y)
#	var endval = max(lineA.size.y, lineB.size.y)

	for i in range(startpos, endpos):
		var percent = (i-startpos) / float(endpos-startpos)
		percent = stepify(percent, fidelity_step)
		var val = lerp(startval, endval, percent )
#		print(percent)
#		tbl[i] = val
		set_table_pos(i, val)
#		owner.emit_signal("value_changed", i, val)
	
	#finally,
	lineA = Rect2(0,0,0,0)
	lineB = Rect2(0,0,0,0)
	update()

#Returns a vector of the table position and value.
func get_table_pos(lock_x=false, mouse=get_local_mouse_position()) -> Vector2:
#	var mouse = get_local_mouse_position()
	if lock_x:  
		mouse.x = last_x
		
	var arpos = clamp(int(lerp(0, tbl.size(), mouse.x / float(rect_size.x))) , 0, tbl.size()-1)
	var val = clamp(lerp(1,0, mouse.y / float(rect_size.y)) , 0, 1)
	val = stepify(val, fidelity_step)
	val *= 100
	
	arpos = floor(arpos/quantize_step) * quantize_step
	return Vector2(arpos, val)

func set_table_pos(arpos, val):
	#Fill all values with the quantized step values
	for i in quantize_step:
		if arpos+i>= tbl.size():  break
		tbl[arpos+i] = val

		#Update single position if necessary
		if !owner.get_node("H2/Smooth").pressed:  owner.update_table(arpos+i, val)

	needs_recalc = true

func _draw():
	#Draw centerline
	draw_line(Vector2(0,rect_size.y/2), Vector2(rect_size.x, rect_size.y/2), ColorN('teal', 0.5))

	#Draw the line values.
	var lastPos
	var lastPos2
	for i in range(0, rect_size.x):
		var val =  tbl[ lerp(0, tbl.size(), i / float(rect_size.x)) ]
		var val2 =  display_tbl[ lerp(0, tbl.size(), i / float(rect_size.x)) ]
		var pos = Vector2(i, rect_size.y - val/100.0 * rect_size.y )
		var pos2 = Vector2(i, rect_size.y - val2/100.0 * rect_size.y )
		var center = Vector2(i, rect_size.y/2)
		
		var c=light_color
		var c2=dark_color
		c2.a = 0.2
		c.a = 0.5
		draw_line(center, pos, c2)
		if i>0:  
			draw_line(lastPos, pos, c, 1.0, true)
			draw_line(lastPos2, pos2, lighter_color, 1.0, true)
		lastPos=pos
		lastPos2=pos2

	changing = -1


	#Grid lines
	draw_line(Vector2(0,rect_size.y/4), Vector2(rect_size.x, rect_size.y/4), ColorN('teal', 0.1))
	draw_line(Vector2(0,rect_size.y/4 + rect_size.y/2), 
				Vector2(rect_size.x, rect_size.y/4 + rect_size.y/2), ColorN('teal', 0.1))

	draw_line(Vector2(rect_size.x/2, 0), Vector2(rect_size.x/2, rect_size.y), ColorN('teal', 0.5))

	#Draw the special process line.
	draw_line(lineA.position, lineB.position, ColorN("yellow"),1.0,true)

#	if changing>=0:
##		var scaleVal = round((rMax[owner.intent] / float(global.RT_MINUS_ONE)) * changing)
#		var scaleVal = round((changing - 50)/50.0 * (1.0/fidelity_step))
#		if scaleVal == 32768: scaleVal -=1
#		draw_string(font, get_local_mouse_position() + Vector2(16, 18), str(scaleVal), ColorN("black"))
#		draw_string(font, get_local_mouse_position() + Vector2(14, 16), str(scaleVal))	
	if changing != $Overlay/Txt.changing:
		$Overlay/Txt.changing = changing
		$Overlay/Txt.update()



func _make_custom_tooltip(_for_text):
	var p = $ToolTipProto.duplicate()
#	p.text = for_text
	var pos = get_table_pos()
	var hint2 = ""
	

	var yValue = (tbl[int(pos.x)]-50) / 50.0
	yValue = str(yValue).pad_decimals(2) 

	var currentY = String((get_table_pos().y - 50)/50.0).pad_decimals(2)
	p.text = "%sx: %s\ny: %s\n0n:%s" % [hint2, pos.x, yValue, currentY]
	return p


#Executed approx 24fps when recalc of the entire table is needed.
var needs_recalc = false
func _on_RecalcSmooth_timeout():
	if needs_recalc:
		smooth()
		needs_recalc = false
		update()
#		yield(get_tree(), "idle_frame")
		if owner.get_node("H2/Smooth").pressed:  owner.update_table(-1)


func smooth():
	if !owner.get_node("H2/Smooth").pressed or owner.get_node("Amount").value==0:
		display_tbl = tbl
		return
	else:
		smooth_tbl = global.arr_smooth(tbl, owner.get_node("Amount").value, owner.get_node("H2/Wrap").pressed)
	
		var val = owner.get_node("Preserve Center").value
		var tmp = []
		var dist:float = 64-val

		if val > 0:  #Preserve edges
			tmp.append_array(tbl)
			for i in dist:  #Limit dist to half of tbl size if increasing range of the val slider
				var j = tbl.size()-1-i
				var percent = i/dist
				tmp[i] = lerp(smooth_tbl[i], tbl[i], percent)
				tmp[j] = lerp(smooth_tbl[j], tbl[j], percent)
			for i in tbl.size():
				var percent = val/64.0
				tmp[i] = lerp(smooth_tbl[i], tmp[i], percent)
			display_tbl = tmp
		else:
			display_tbl = smooth_tbl


func to_short(val):
	return int(range_lerp(val, 0, 100, -32768, 32767))

func _on_Amount_value_changed(_val):
	needs_recalc = true

func _on_Preserve_Center_value_changed(_val):
	needs_recalc = true

func _on_Amt_value_changed(value):
	owner.get_node("Amount").value = value
#	needs_recalc = true

func _on_Ctr_value_changed(value):
	owner.get_node("Preserve Center").value = value
#	needs_recalc = true


