extends Control

var fnt = preload("res://gfx/fonts/spelunkid_font.tres")
const MAX_OPS = 8

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code


func _ready():
	#Populate the operator types popup menu.
	var lbl=["Nothing", "FM (Default)", "Filter", "Bitwise Op", "Wave Folder"]
	$OpType.add_separator("Operator X")
#	$OpType.add_separator()
	for i in range(1, global.OpIntentIcons.size()):
		$OpType.add_icon_radio_check_item(global.OpIntentIcons[i], lbl[i], i)

	var check = AtlasTexture.new()
	var uncheck = AtlasTexture.new()
	
	check.atlas = preload("res://gfx/ui/radio_check.png")
	uncheck.atlas = preload("res://gfx/ui/radio_check.png")
	uncheck.region = Rect2(0,0,16,16)
	check.region = Rect2(16,0,16,16)
	
	$OpType.add_icon_override("radio_unchecked", uncheck)
	$OpType.add_icon_override("radio_checked", check)
	

	#Initialize the control grid
	$SlotIndicator.connect("slot_moved", self, "_on_slot_moved")
	
	yield(get_tree(),"idle_frame")
	yield(get_tree(),"idle_frame")
	yield(get_tree(),"idle_frame")


	#Set the operators to whatever the chip_loc (or parent's) says.
	var chip = get_node(chip_loc)
	if chip:
		$SlotIndicator.total_ops = chip.GetOpCount()
	else:
		chip_loc = owner.chip_loc
		chip = get_node(chip_loc)
		if chip:
			$SlotIndicator.total_ops = chip.GetOpCount()

	check_if_presets()

func _physics_process(delta):
	update()
	pass


#Executed every time a slot moves.  Sets the algorithm in the chip.
func _on_slot_moved(delay=0):
	if chip_loc.is_empty():  
		printerr("_on_slot_moved():  Wiring grid isn't connected to a chip bus!")
		return

	if delay:  yield(get_tree().create_timer(delay), "timeout")

	var d = {}  #Bussing dict
	d = get_algorithm_description()
	get_node(chip_loc).SetAlgorithm(d)
	
	check_if_presets()
	
	global.emit_signal("algorithm_changed")

func get_algorithm_description() -> Dictionary:
	var d= {}
	d["opCount"] = $SlotIndicator.total_ops
	d["grid"] = get_grid_description()
	
	d["processOrder"] = get_process_order()
	d["connections"] = $SlotIndicator.get_connection_descriptions()
	return d


func _on_Add_pressed():
	if $SlotIndicator.total_ops < MAX_OPS:
		$SlotIndicator.total_ops +=1
		_on_slot_moved()


func _on_Remove_pressed():
	if $SlotIndicator.total_ops > 1:
		$SlotIndicator.total_ops -=1
		_on_slot_moved()


func _on_Preset_pressed():
	$Popup.popup(Rect2(get_global_mouse_position(), $Popup.rect_size))


#Describes the wiring grid in terms of an array of bytes.  Only works when MAX_OPS <= 8.
#Each position in the output array describes an ID's location, with maxValue being 0b 0111_0111.
#The 4 MSBs describe Ypos, and 4LSBs describe Xpos.
func get_grid_description():
	var output = [] #:PoolByteArray
	for o in $SlotIndicator.ops:
#		output.append(o.gridPos.x)
#		output.append(o.gridPos.y)
		output.append( ( int(o.gridPos.y) << 4) | int(o.gridPos.x)  )

	return output
	
#Describes the wiring grid in terms of the height of each operator on the stack.
func get_process_order():
	var ops = $SlotIndicator.ops.duplicate()
	ops.sort_custom($SlotIndicator, "compare_height")
	
	var output = []
	for op in ops:
		output.append(op.id)
	return output

	

#func _draw():
#	draw_string(fnt, Vector2(0, 256), var2str(get_grid_description()))
#
#	draw_string(fnt, Vector2(0, 272), str( $SlotIndicator.get_connection_descriptions()))
#	draw_string(fnt, Vector2(0, 288), str(get_process_order()) )



#func _draw():
#	var grid = Vector2($SlotIndicator._grid.size(), 0)
#	if grid.x > 0: grid.y = $SlotIndicator._grid[0].size()
#	for y in grid.y:
#		for x in grid.x:
#			var pos = Vector2(x,y)
#			draw_string(fnt, pos*24 + Vector2(256, 64), str($SlotIndicator.getGridID(pos)))
#
#	for op in $SlotIndicator.ops:
#		var s = "%s:  %s" % [op.id, op.gridPos]
#		draw_string(fnt, Vector2(256, 24* op.id + 320), s)




func _on_Copy_pressed():
	OS.clipboard = to_json(get_algorithm_description())


func _on_Paste_pressed():
	var err = validate_json(OS.clipboard)
	if err:  
		print("WiringGrid:  Clipboard data failed to pass JSON validation... Error at ", err)
		return

	#For some reason, CSVs pass JSON validation without enclosing brackets. So, we need to make sure
	#that parsing only produces a Dictionary!  CSV's inexplicably return float
	var desc = parse_json(OS.clipboard)
	if not desc is Dictionary:  return  
	
	$SlotIndicator.load_from_description( desc )
	_on_slot_moved(0.05)


func _on_Preset_item_activated(index):
	if chip_loc.is_empty():  
		printerr("_on_Preset_item_activated():  Wiring grid isn't connected to a chip bus!")
		return
	
	var chip = get_node(chip_loc)
	var preset_description = chip.SetPreset(index, true if $Popup.intent==6 else false)
	#The grid description here is the default.  Remove it.  load_from_description() will make a new one.
	preset_description.erase("grid")  
	$SlotIndicator.load_from_description( preset_description )
	_on_slot_moved(0.05)

func check_if_presets():
	#Check to see if presets are available.
	match $SlotIndicator.total_ops:
		4,6:
			$Preset.disabled = false
			$Popup.intent = $SlotIndicator.total_ops
			pass
		_:
			$Preset.disabled = true


func popup_intent_menu(id):  #Spawns the popup to view/change the operator type.
	#Determine the operator's intent and select the default item.
	var intent = get_node(chip_loc).GetOpIntent(id)
	if intent <=0:
		printerr("WiringGrid.gd:  Invalid intent %s returned from operator %s..." % [intent, id])
		return

	for i in $OpType.get_item_count():
		$OpType.set_item_checked(i, false)

	$OpType.set_item_text(0, "Operator %s Type" % (id+1))
	$OpType.set_item_checked($OpType.get_item_index(intent), true)
	$OpType.popup(Rect2(get_global_mouse_position(), Vector2.ONE))
	$OpType.set_meta("id", id)


func _on_OpType_id_pressed(intent):
	var opTarget = $OpType.get_meta("id")
	print ("Setting Op%s to ID %s..." % [opTarget + 1, intent])
	get_node(chip_loc).SetOpIntent(opTarget, intent)
	
	global.emit_signal("op_intent_changed", opTarget, intent)
	$SlotIndicator.redraw_grid()
