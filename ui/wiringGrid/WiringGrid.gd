extends Control

var fnt = preload("res://gfx/fonts/spelunkid_font.tres")
const MAX_OPS = 8

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code


func _ready():
	$SlotIndicator.connect("slot_moved", self, "_on_slot_moved")
	$Add.connect("pressed", self, "_on_slot_moved", [true])
	$Remove.connect("pressed", self, "_on_slot_moved", [true])
	pass

func _physics_process(delta):
	update()
	pass


func _on_slot_moved(delay=false):
	if chip_loc.is_empty():  
		printerr("_on_slot_moved():  Wiring grid isn't connected to a chip bus!")
		return
	
	var d = {}  #Bussing dict
	
	if delay:  yield(get_tree(), "idle_frame")
	d["opCount"] = $SlotIndicator.ops.size()
	d["grid"] = get_grid_description()
	
	d["processOrder"] = get_process_order()
	d["connections"] = $SlotIndicator.get_connection_descriptions()

	get_node(chip_loc).SetAlgorithm(d)


func _on_Add_pressed():
	if $SlotIndicator.total_ops < MAX_OPS:
		$SlotIndicator.total_ops +=1


func _on_Remove_pressed():
	if $SlotIndicator.total_ops > 2:
		$SlotIndicator.total_ops -=1
		

#Describes the wiring grid in terms of a 32-bit integer.  Only works when MAX_OPS <= 8,
#Otherwise a 64-bit integer will be necessary if bussing to C#.
func get_grid_description():
	var output:int = 0
	
	for o in $SlotIndicator.get_children():
		#Every ID number is described from index 1, with index 0 being "unused slot".
		#1 nibble per slot.
		output <<=4
		output |= (o.id+1) 
		
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
	draw_string(fnt, Vector2(0, 256), str(get_grid_description()))

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
