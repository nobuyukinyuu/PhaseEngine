extends Control

var fnt = preload("res://gfx/fonts/spelunkid_font.tres")
const MAX_OPS = 8

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code


func _ready():
	#Convert the chip location to an absolute location!!
	#Otherwise, the value specified in the editor will only be valid for us and none of our children.
	chip_loc = get_node(chip_loc).get_path()  
	
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

#func _physics_process(_delta):
#	update()


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

#Used to send the algorithm description to the chip.
func get_algorithm_description() -> Dictionary:
	var d= {}
	d["opCount"] = $SlotIndicator.total_ops
	d["grid"] = get_grid_description()
	
	d["processOrder"] = get_process_order()
	d["connections"] = $SlotIndicator.get_connection_descriptions()
	return d


func _on_Add_pressed():
	if $SlotIndicator.total_ops < MAX_OPS:
		set_buttons_enabled(false)

		var target = $SlotIndicator.total_ops +1
		yield($SlotIndicator.set_ops(target), "completed")
		
		#Probably because the chip isn't yet ready, the new op doesn't have a type to set.
		#We can set it here to override that;  it's ugly, but it works......
		var slot = $SlotIndicator.get_child($SlotIndicator.get_child_count()-1)
		slot.set_slot_type(slot.opType.CARRIER)

		_on_slot_moved() 
		set_buttons_enabled(true)


func _on_Remove_pressed():

	if $SlotIndicator.total_ops > 1:
		set_buttons_enabled(false)
		var target = $SlotIndicator.total_ops -1
		#We don't want to reset default positions here, so instead we'll get an algorithm description from
		#the chip once we've set the operator size.
		var c = get_node(chip_loc)
		var new_alg = c.SetOpCount(target)
		
		yield($SlotIndicator.set_ops(target, new_alg), "completed")
		check_if_presets()
		global.emit_signal("algorithm_changed")

		
#		$SlotIndicator.total_ops -=1
#		_on_slot_moved()
		set_buttons_enabled(true)


func _on_Preset_pressed():
	popup_presets()

#Describes the wiring grid in terms of an array of bytes.  Only works when MAX_OPS <= 8.
#Each position in the output array describes an ID's location, with maxValue being 0b 0111_0111.
#The 4 MSBs describe Ypos, and 4LSBs describe Xpos.
func get_grid_description():
	var output = [] #:PoolByteArray
	for o in $SlotIndicator.ops:
#		output.append(o.gridPos.x)
#		output.append(o.gridPos.y)
		output.append( ( int(o.gridPos.y) << 4) | int(o.gridPos.x)  )

	return PoolByteArray(output)
	
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
#	OS.clipboard = to_json(get_algorithm_description())
	OS.clipboard = get_node(chip_loc).VoiceAsJSONString()
	pass


func _on_Paste_pressed():
	var desc:JSONParseResult = JSON.parse(OS.clipboard)
	if desc.error != OK:  
		print("WiringGrid:  Clipboard data failed to pass JSON validation...")
		print("%s (line %s)" % [desc.error_string, desc.error_line])
		return

	#For some reason, CSVs pass JSON validation without enclosing brackets. So, we need to make sure
	#that parsing only produces a Dictionary!  CSV's inexplicably return float
	
	if not desc.result is Dictionary:  return  
	if not desc.result.has("algorithm"):  return  #JSON data is not a Voice.

	var err = get_node(chip_loc).PasteJSONData(OS.clipboard)
	if err != OK:
		print("PasteJSONData returned error %s" % err)
		return
	
	#Reinit everything.
	owner.reinit_all()
#	$SlotIndicator.load_from_description( preset_description )
	check_if_presets()


#Activated when the user selects a preset algorithm.
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

#Checks whenever opNum changes as to whether or not presets are available for this number of ops.
func check_if_presets():
	#Check to see if presets are available.
	match $SlotIndicator.total_ops:
		4,6:
			$Preset.disabled = false
#			$Popup.intent = $SlotIndicator.total_ops
		_:
			$Preset.disabled = true


func popup_presets():
	if !has_node("Popup"):
		var p = load("res://ui/wiringGrid/PresetsPopup.tscn").instance()
		add_child(p, true)
		p.name = "Popup"
		p.owner = self
		p.get_node("VBox/Scroll/ItemList").connect("item_activated", self, "_on_Preset_item_activated")
		
	$Popup.intent = $SlotIndicator.total_ops
	$Popup.popup(Rect2(get_global_mouse_position(), $Popup.rect_size))


func set_buttons_enabled(enabled:bool):
	var aux_btns = [$Add, $Remove, $Copy, $Paste]
	for button in aux_btns:
		button.disabled = !enabled
