extends MenuButton
enum {LINEAR, IN, OUT, IN_OUT, OUT_IN, TWELFTH_ROOT_OF_2}
enum {BOTH_SIDES, LEFT_ONLY, RIGHT_ONLY}

const CURVEMAP = [1, 2, 0.5, -2, -0.5]

onready var defaults = {owner.ranges.velocity: "x"}

func _ready():
	
	
	#We need to move the ease submenu to the invisible child popup of self.
	var p = get_popup()
	var submenu = $EaseSubmenu
	p.theme = submenu.theme

#	p.connect("index_pressed",self, "_on_Menu_index_pressed")

	

	#FIXME:  THIS IS A HACKY WORKAROUND TO LACK OF PopupMenu.get_current_index()
	#		CONSIDER REPLACING IT IF IT IS AVAILABLE IN GODOT 3.2.2
	for i in 6:
		var q = $EaseSubmenu.duplicate()
		q.name = "Ease%s" % i
		p.add_child(q,true)
		q.owner = owner
		
		q.connect("index_pressed", self,"_on_EaseMenu_index_pressed", [i, i%2 !=0])


	$EaseSubmenu.queue_free()
	
	
#	p.add_icon_item(preload("res://gfx/ui/icon_reset.svg"), "Default")
	p.add_separator("All Sides")
#	p.add_submenu_item("Left / Right ", "EaseSubmenu")
#	p.add_submenu_item("Right / Left ", "EaseSubmenu")
	p.add_submenu_item("Ascending (L / R)", "Ease0")
	p.add_submenu_item("Descending (R / L) ", "Ease1")
	p.add_separator("Left")
#	p.add_submenu_item("Left / Right ", "EaseSubmenu")
#	p.add_submenu_item("Right / Left ", "EaseSubmenu")
	p.add_submenu_item("LEFT: Ascending", "Ease2")
	p.add_submenu_item("LEFT: Descending ", "Ease3")
	p.add_separator("Right")
#	p.add_submenu_item("Left / Right ", "EaseSubmenu")
#	p.add_submenu_item("Right / Left ", "EaseSubmenu")
	p.add_submenu_item("RIGHT: Ascending", "Ease4")
	p.add_submenu_item("RIGHT: Descending ", "Ease5")

	p.add_separator()
	p.add_item("Custom...", 0x40)
	p.set_item_accelerator(10, KEY_KP_ADD)  #Be sure to update this if Default moves

	p.add_separator()
	p.add_icon_item(preload("res://gfx/ui/icon_reset.svg"), "Default", 0xFF)
	p.set_item_accelerator(12, KEY_D | KEY_MASK_SHIFT)  #Be sure to update this if Default moves

	p.rect_size.x += 32

	p.connect("id_pressed", owner, "_on_btnPresets_id_pressed")


#Activated when one of the ease menus is selected.
func _on_EaseMenu_index_pressed(curveType, parent_index, descending):
	var startpos = 0
	var endpos = 128
	var reinterpolate:bool = true

	var startval = global.RTABLE_SIZE if descending else 0
	var endval = 0 if descending else global.RTABLE_SIZE
	
	prints("preset?", curveType, parent_index, descending)
	var tbl = owner.get_node("VU").tbl
	
	#Determine the area we need to apply the curve preset.
	match int(parent_index/2):
		LEFT_ONLY:
			endpos = owner.get_node("VU").split
			print ("Left only.  Startpos is ", startpos)
		RIGHT_ONLY:
			startpos = owner.get_node("VU").split
			print ("Right only.  Startpos is ", startpos)
		BOTH_SIDES:
			reinterpolate=false  #Mark this for first/last value 
#			endpos = 127

	#Determine whether reinterpolation is needed.
	if not (owner.get_node("VU").split < 128 and reinterpolate):  reinterpolate=false
	else:
		startval = tbl[startpos]
		endval = tbl[min(endpos,127)]
		match int(parent_index/2):  #Check if the direction makes sense for reinterpolation. Else use bounds
			LEFT_ONLY:
				if descending and startval <= endval:
					startval = global.RTABLE_SIZE
				elif not descending and startval >= endval:
					startval = 0
			RIGHT_ONLY:
				if descending and startval <= endval:
					endval = 0
				elif not descending and startval >= endval:
					endval = global.RTABLE_SIZE

	#Now, apply the curve to the table.
	var size = float(endpos-startpos)  #Number of elements.
	match curveType:
		LINEAR:
			for i in range(startpos, endpos):
#				var val = lerp(0, global.RTABLE_SIZE, i/(startpos+size))
				var val = lerp(startval, endval, (i-startpos)/size)
#				var pos = startpos+endpos-i-1 if descending else i
				var pos = i
				tbl[pos] = val

		IN, OUT, IN_OUT, OUT_IN:
			for i in range(startpos, endpos):
#				var val = ease(i/(startpos+size), CURVEMAP[curveType]) * global.RTABLE_SIZE
				var curve = 1.0/CURVEMAP[curveType] if descending else CURVEMAP[curveType]
				var val = ease((i-startpos)/size, curve) * (endval-startval) + startval
#				var pos = startpos+endpos-i-1 if descending else i
				var pos = i
				tbl[pos] = val

		TWELFTH_ROOT_OF_2:
#			if descending:
#				for i in range(startpos, endpos):
#					var pos = range_lerp((i-startpos), 0, endpos-startpos, 0, 128) 
#					tbl[i] = 1 / pow(2, (pos)/12.0) * global.RTABLE_SIZE
#			else:  #Ascending
#				for i in range(startpos, endpos):
#					var j = range_lerp((i-startpos), 0, size, 0, 128)
##					print (j)
#					tbl[i] = pow(2, (j-127)/12.0) * global.RTABLE_SIZE
			for i in range(startpos, endpos):
				if descending:
					var pos = range_lerp((i-startpos), 0, endpos-startpos, 0, 128) 
					tbl[i] = 1 / pow(2, (pos)/12.0) * global.RTABLE_SIZE
				else:
					var pos = range_lerp(i, startpos, endpos-1, 0, 10) 
					tbl[i] = pow(2, (pos))
		
				
#	if reinterpolate:
#		print("Reinterpolating positions %s to %s with values %s to %s...." % [startpos,endpos,startval,endval])
#		for i in range(startpos, endpos):
#			tbl[i] = range_lerp(tbl[i], 0, global.RT_MINUS_ONE, startval, endval)
		

	owner.get_node("VU").update()
	owner.get_node("VU").emit_signal("table_updated", -1, -1)

func ease12(val, percent):
	
	pass


	#  12th ROOT OF 2 CURVE is implemented as 1 / Pow(2, x/12) for descending,
	#  Pow(2, (x-global.RT_MINUS_ONE) / 12) for ascending
