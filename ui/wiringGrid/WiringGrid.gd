extends Control

var fnt = preload("res://gfx/fonts/spelunkid_font.tres")
const MAX_OPS = 8

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code


func _ready():
	$SlotIndicator.connect("slot_moved", self, "_on_slot_moved")
	
	yield(get_tree(),"idle_frame")
	yield(get_tree(),"idle_frame")
	yield(get_tree(),"idle_frame")
	if !chip_loc.is_empty():
		$SlotIndicator.total_ops = get_node(chip_loc).GetOpCount()


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
	
	#Check to see if presets are available.
	match $SlotIndicator.total_ops:
		4,6:
			$Preset.disabled = false
			$Popup.intent = $SlotIndicator.total_ops
			pass
		_:
			$Preset.disabled = true

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
	if $SlotIndicator.total_ops > 2:
		$SlotIndicator.total_ops -=1
		_on_slot_moved()


func _on_Preset_pressed():
	$Popup.popup(Rect2(get_global_mouse_position(), $Popup.rect_size))



#Describes the wiring grid in terms of an array of bytes.  Only works when MAX_OPS <= 8.
#Each position in the output array describes an ID's location, with maxValue being 0b 0111_0111.
#The 4 MSBs describe Ypos, and 4LSBs describe Xpos.
func get_grid_description():
	var output:PoolByteArray
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

	

func _draw():
	draw_string(fnt, Vector2(0, 256), var2str(get_grid_description()))

	draw_string(fnt, Vector2(0, 272), str( $SlotIndicator.get_connection_descriptions()))
	draw_string(fnt, Vector2(0, 288), str(get_process_order()) )



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
	OS.clipboard = var2str(get_algorithm_description())


func _on_Paste_pressed():
	$SlotIndicator.load_from_description( str2var(OS.clipboard) )
	_on_slot_moved(0.05)


