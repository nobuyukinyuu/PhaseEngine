#tool
extends GridContainer
export (int, 2, 8) var total_ops:int = 4 setget set_ops

const sProto = preload("res://ui/wiringGrid/SlotProto.tscn")
const spk = preload("res://gfx/ui/icon_speaker.svg")

var last_slot_focused=-1 setget reset_focus

var _grid = []  #References to opNodes in a particular grid position
var ops = []  #array of each operator being used and its connections
var isReady=false

var manual_src_pos = Vector2.ONE * -1

signal slot_moved
signal op_size_changed

func _ready():
#	reinit_grid(total_ops)
	isReady = true
	yield(set_ops(total_ops), "completed")
#	yield (get_tree(), "idle_frame")
#	yield (get_tree(), "idle_frame")
	reset_default_op_positions(total_ops)
	
	update()



func reset_focus(val):
	if val != last_slot_focused:
		if last_slot_focused >=0:
			prints("unfocusing", last_slot_focused, "and focusing", val)
			get_node(str(last_slot_focused)).unfocus()
		else:
			prints("refocusing", val)
#		if val == last_slot_focused:  val = -1
		last_slot_focused = val
	else:
		var n = get_node(str(val))
		if n.has_focus():
			prints("unfocusing", val)
			n.unfocus()
			last_slot_focused = -1


func set_ops(val):  #Set the number of operators in the grid.  Property setter.
#	print ("SetOps: ", val, "total_ops: ", total_ops)
	
	var oldsz = total_ops
	total_ops = val
	if not isReady:  return
	resize_op_array(val)
	if val >= oldsz:  yield(reinit_grid(val), "completed")
	
	visible = false
	if val < oldsz:  
#		yield(reinit_connections(), "completed")  #Grid's smaller.  Can't guarantee connection tree is valid.

	  #Gotta move the remaining ops left back to carrier positions.
		yield(reset_default_op_positions(val), "completed")
		yield(reinit_grid(val), "completed")


	elif val > oldsz:
		for i in oldsz:
			ops[i].gridPos.y += val-oldsz  #usually 1
		clearGridIDs()
		restore_grid()

	yield(get_tree(), "idle_frame")
	redraw_grid()
	visible = true
	emit_signal("op_size_changed")


func resize_op_array(newsz):  #Deals with re-initializing new opNodes in a larger array.
	var oldsz = ops.size()	
	ops.resize(newsz)
	
	if newsz > oldsz:
		#Fill with new opNodes.
		for i in range(oldsz, newsz):
			ops[i] = opNode.new()
			ops[i].id = i
			ops[i].gridPos = Vector2.ONE * (newsz-1)

func reinit_connections():  #Clears all opNode connections by making new opNodes for all ops.
	ops.clear()
	for i in total_ops:
		var p = opNode.new()
		p.id = i
		ops.append(p)

func reinit_grid(gridSize):  #Completely nuke the controls and rebuild the slot indicator grid.
	last_slot_focused = -1
	for o in get_children():
		if o.is_connected("dropped", self, "request_move"):
			o.disconnect("dropped", self, "request_move")
		if o.is_connected("right_clicked", self, "_onSlotRightClicked"):
			o.disconnect("right_clicked", self, "_onSlotRightClicked")
		o.queue_free()
	
	columns = gridSize
	
	yield (get_tree(), "idle_frame")
	yield (get_tree(), "idle_frame")

	resize_grid(gridSize)
	restore_grid()
	
	for i in range(gridSize*gridSize):  #Repopulate controls.
		var p = sProto.instance()
		p.name = str(i)
		p.editor_description = "Slot %s" % i
		p.gridPos = Vector2( i % gridSize, i/gridSize )
		p.connect("dropped", self, "request_move")
		p.connect("right_clicked", self, "_onSlotRightClicked")
		p.set_slot(-1, 0)  #0=None
		add_child(p)
		p.owner = owner

	update()

#Flips the opNodes positions to default and changes slot indicators to match.
func reset_default_op_positions(sz:int):  
	if sz == -1:  sz = total_ops
	var start = sz*sz - sz
	for i in sz:
		var p = get_child(start+i)
		p.set_slot(i, 1)  #1=carrier

		setGridID(p.gridPos, i)

		#Move the op nodes to the new positions.
#		ops[i].gridPos = p.gridPos
		ops[i].gridPos.x = i
		ops[i].gridPos.y = sz-1
		ops[i].connections = []
		ops[i].manual_connections = []

	yield(get_tree(), "idle_frame")

#	yield(get_tree().create_timer(0), "timeout")

func clearGridIDs():  #Fills grid with nulls.
	resize_grid(total_ops)
func resize_grid(newsz):
	_grid.clear()
	for i in newsz:
		var arr = []
		arr.resize(newsz)
		_grid.append(arr)

func restore_grid():  #Puts ops back in grid positions.
	for op in ops:
		setGridID(op.gridPos, op.id)

#Grid operations
func getGridID(pos):		return _grid[pos.x][pos.y]
func setGridID(pos,val):	_grid[pos.x][pos.y] = val
func resetGridID(pos):  	_grid[pos.x][pos.y] = null
func gridPosIsEmpty(pos):	return _grid[pos.x][pos.y] == null
func slotNodeAt(pos):
	return get_node(str(pos.y * total_ops + pos.x))


func redraw_grid():
	for slot in get_children():
		slot.reset()
	
	for op in ops:
		if op.pos_valid():  #Get the slot and set it
			var slot = slotNodeAt(op.gridPos)
			var opType = 1 if op.gridPos.y == total_ops-1 else 2
			slot.set_slot(op.id, opType)
		
	update()

#Tree logic:  When requesting connection from one op to another, make sure the destination operator
#		 isn't in the list of the source operator's connections.  If it is, swap the connections
#		between the 2 operators.  Consider updating the drag tooltip to state a swap? 
#		Any operators that weren't part of the swap should update references to the old op with the new one.
# 		If a swap isn't the intended operation, Remove the entire
#		reference tree starting at any operators that connect to the source op.  Reconnect to dest op.


enum targets {nothing, output, operator, swap}
func check_target_type(source, dest):
	if getGridID(source) == getGridID(dest):  return [targets.nothing, 0]
	var source_op = ops[getGridID(source)]
	var dest_op = getGridID(dest)
	if dest_op != null:  dest_op = ops[dest_op]  #Only fetch the opNode if it's actually there.
												#If not, we'll find a proper spot to connect to later.

	if dest_op != null:  #Determine whether the destination is in the connections of the source op.
		if source_op.has_connection_to(dest_op):
			return [targets.swap, dest_op.id]
		else:  #Bingo, modulator
			return [targets.operator, dest_op.id]
	else:
		if dest.y+1 == total_ops:  return [targets.output, 0]
		#TODO:  Find free slot drop-down here and indicate the target in the diagram.
		return [targets.nothing, 0]

func request_move(source, dest):
	if getGridID(source) == getGridID(dest):  return
	
	#If anything goes wrong with the operation, restore the grid to the original state.
	# var backup_grid = _grid.duplicate(true)
	# var backup_ops = ops.duplicate(true)
	
	print ("Dropped from ", source, " into ", dest)
	#Get source operator.
	var source_op = ops[getGridID(source)]
	var dest_op = getGridID(dest)
	if dest_op != null:  dest_op = ops[dest_op]  #Only fetch the opNode if it's actually there.
												#If not, we'll find a proper spot to connect to later.

	if dest_op != null:  
		#Determine whether the destination is in the connections of the source op.
		#If so, swap their connections and grid positions and call it a day.
		if source_op.has_connection_to(dest_op):
			source_op.swap_connections_with(dest_op)  

			for o in ops:  #The connections were swapped, but not the references inside them.  Do it now.
				global.arr_replace(o.connections, source_op, 0xFFFF)
				global.arr_replace(o.connections, dest_op, source_op)
				global.arr_replace(o.connections, 0xFFFF, dest_op)

				global.arr_replace(o.manual_connections, source_op, 0xFFFF)
				global.arr_replace(o.manual_connections, dest_op, source_op)
				global.arr_replace(o.manual_connections, 0xFFFF, dest_op)
			
			#Now swap their grid positions.
			setGridID(dest, source_op.id)
			setGridID(source, dest_op.id)
			source_op.gridPos = dest
			dest_op.gridPos = source
			redraw_grid()
			
			emit_signal("slot_moved")
			return
			
		else:
			#We bingo'd a destination that's free to connect to, so add the source op to the dest op connections.
			#First, remove the tree from the grid and break source ops' connections to it.
			remove_tree_at(source)
			move_tree(source_op, dest)
			emit_signal("slot_moved")

	else:  #User tried to drop on an empty slot.  Try to find a connection point beneath the drop point.
		#We shouldn't try to find a connection point with a grid that contains our tree, so let's remove
		#The tree from the grid before finding a connection point.
		remove_tree_at(source)
		break_all_connections_to(source_op)
		print ("Empty slot at ", dest)

		var connection_point = find_connection_point(dest)
		if connection_point.y == -1:  #Nothing to connect to.  Make the tree into a new stack.
			connection_point.y = total_ops-1
			move_tree(source_op, connection_point, false)
		else:  #We found an operator to connect to!
			move_tree(source_op, connection_point)

		emit_signal("slot_moved")
		return


#Moves an operator located at source and places at dest.  Careful not to replace dest directly unless empty.
func move_tree(source_op, dest, append_to_dest=true):
	var dest_op = getGridID(dest)
	if dest_op != null:  
		dest_op = ops[dest_op]  #Only fetch the opNode if it's actually there.
	elif dest_op==null and !append_to_dest:
		print ("move_tree(): Nothing to attach to.")

	#Remove the tree from the grid and break source ops' connections to it.
	break_all_connections_to(source_op)

	#Now connect the source to the destination and find slots for the tree.
	if append_to_dest:  dest_op.connections.append(source_op)
	var dest_offset = 1 if append_to_dest else 0

	

	#The ops for the tree have invalid grid positions, so we need to find free positions for all of them.
	#Using the source_op's Y position, we find an open space in the level it inhabits on the op stack.
	#The first call to recursive func should be level of dest-1. This calls func for all connections
	#until each one is found a home.
	find_free_slots(source_op, dest.y-dest_offset, dest.x)

	#Next:  redraw grid and exit out
	break_bad_connections()
	redraw_grid()
	return


func find_free_slots(op, level:int, start_from=0):
	print("Looking for free slot for OP%s on level %s starting at %s..." % [op.id, level, start_from])
	#Finds free slots for all items on a tree.
	op.gridPos = free_slot(level, start_from)
	setGridID(op.gridPos, op.id)

	for connection in op.connections:
		find_free_slots(connection, level-1, start_from)

func free_slot(level, start_from=0):  #returns the position of the first free slot on the level (ypos) specified.
	for x in range(start_from, total_ops):
		var pos = Vector2(x, level)
		if getGridID(pos) !=null:  continue
		return pos
			
	#Uh-oh.  Couldn't find a spot to the right of the operator.  Let's look to the left.
	var rev_range = range(0, start_from)
	rev_range.invert()
	for x in rev_range:
		var pos = Vector2(x, level)
		if getGridID(pos) !=null:  continue
		return pos

	print("free_slot():  No free slot found at level %s!!" %level)
	assert(false)

func break_all_connections_to(source_op):
	#Remove all references to the source operator in other operators' connections.
	for op in ops:
		var idx = op.connections.find(source_op)
		if idx >= 0: op.connections.remove(idx)

#func break_manual_connections_in(source_op, recursive=true):
#	while not source_op.manual_connections.empty():
#		var lower_op = source_op.manual_connections.pop_back()
##		print ("breaking connection from %s to %s..." % [source_op.id+1, lower_op.id+1])
#		global.arr_remove_all(lower_op.connections, source_op)
#
#	if !recursive:  return
#	for connection in source_op.connections:
#		break_manual_connections_in(connection)
#
#func break_bad_connections(source_op):
#	var bad_connections = []
#	for dest_op in source_op.manual_connections:
#		if source_op.gridPos.y >= dest_op.gridPos.y:
#			#Uh oh.  Bad connection.  Mark it.
#			bad_connections.append(dest_op)
#
#	for o in bad_connections:
#		print ("Breaking bad connection from %s to %s" % [source_op.id+1, o.id+1])
#		global.arr_remove_all(source_op.manual_connections, o)
#		global.arr_remove_all(o.connections, source_op)
#
#	#Recursively check
#	for connection in source_op.connections:
#		break_bad_connections(connection)

#Checks manual connections for anything that breaks the rules.
func break_bad_connections():
	for op in ops:
		var flagged_indices = []
		for i in op.manual_connections.size():
			var connection = op.manual_connections[i]
			if not op.gridPos.y < connection.gridPos.y:
#				op.manual_connections.remove(i)
				flagged_indices.append(connection)
			if connection.connections.has(op):
#				op.manual_connections.remove(i)
				flagged_indices.append(connection)
		
		for j in flagged_indices:
#			op.manual_connections.remove(j)
			global.arr_remove_all(op.manual_connections, j)
	update()			

#Removes an entire operator tree off the grid.
func remove_tree_at(gridPos):
	if gridPosIsEmpty(gridPos):
		print ("remove_tree_at(%s): Nothing here..." % gridPos)
		return

	var sourceID = getGridID(gridPos)

	#Assemble a list of IDs to remove from grid.
	var ids = {sourceID: sourceID}
	ops[sourceID].get_tree_ids(ids)
	
	print ("Removing tree for OPs ", ids)
	
	for id in ids.keys():
		resetGridID(ops[id].gridPos)
#		break_manual_connections_in(ops[id])




func find_connection_point(dest):
	#Find a free connection point vertically on this stack. It's the first operator below drop dest.
	#If there's no operators found, then the operator we're dragging becomes a new carrier.
	#Function that calls this will be called separately

	#Get the first free slot below where the user dropped his tree.
	var free_x = total_ops
	for i in range(dest.y, total_ops):
		var pos = Vector2(dest.x, i)
		if getGridID(pos) != null:  
			print ("find_connection_point(): Connection point found at ", pos)
			return pos

	print ("find_connection_point(): Converting tree to carrier....", Vector2(dest.x, -1))
	return Vector2(dest.x, -1)



#======================= DRAW ROUTINES ================================
func _draw():
	#Draw the "connection to output" diagram
	var tile_size = rect_size / total_ops
	var y = rect_size.y - tile_size.y/4

	for i in total_ops:
		var a = Vector2(i * tile_size.x + tile_size.x / 2, y)
		var b = Vector2(a.x, rect_size.y + 8)
		draw_line(a,b, ColorN("white"),1.0, true)

	y = rect_size.y + 8
	var half = tile_size.x / 2
	draw_line(Vector2(half, y), Vector2(rect_size.x, y), ColorN("white"), 1.0, true)
	draw_texture(spk,Vector2(rect_size.x, y) - Vector2(8,8))
	
	#Draw connections.
	for op in ops:
		for connection in op.connections:
			draw_connection(op.gridPos, connection.gridPos)

	for op in ops:
		for dest in op.manual_connections:
			var dist = (half*0.002) * (dest.gridPos.y - op.gridPos.y)
			var dir = sign(0.01+ (dest.gridPos.x - op.gridPos.x))
#			var nudge = Vector2(0.01*dir + dist * dir, -dist+0.1)
			var nudge = Vector2(0.01*dir + dist * dir, 0)
			if sign((dest.gridPos.x - op.gridPos.x)) == 0:  
				nudge.x *=2.5
				nudge.x -= 0.1
#			draw_connection(dest.gridPos+nudge, op.gridPos+nudge, Color(1,0,0.5, 1))
			draw_connection(dest.gridPos+nudge, op.gridPos+nudge, ColorN("yellow", 0.75))
			
	if manual_src_pos.x > -1:
		draw_line(manual_src_pos * tile_size + Vector2(half,half)+Vector2.ONE, 
					get_local_mouse_position()+Vector2.ONE, ColorN("black", 0.85), 1.0, true)
		draw_line(manual_src_pos * tile_size + Vector2(half,half), get_local_mouse_position(),
					ColorN("yellow", 0.5), 1.0, true)

func draw_connection(source, dest, color=Color(1,1,1,1)):
	#Translate the connection point to the control's location on the grid.
	var tile_size = rect_size / total_ops
	source *= tile_size
	dest *= tile_size

	source += tile_size / 2
	dest += tile_size / 2

	var nudge = tile_size / 4
	var x_bias = sign(dest.x-source.x) * nudge.x
	source.x += x_bias
	dest.x -= x_bias
	source.y -= nudge.y
	dest.y += nudge.y
	
	draw_arrow(source, dest, color) 
	
func draw_arrow(a, b, color=Color(1,1,1,1), width=1.0):
	var arrow_spread= PI/6
	var arrow_length = 4
	var pts:PoolVector2Array
	pts.resize(3)
	pts[1] = a

	var angle = atan2(a.y-b.y, a.x-b.x) + PI
	
	pts[0] = Vector2(a.x + arrow_length*cos(angle+arrow_spread), a.y + arrow_length*sin(angle+arrow_spread))
	pts[2] = Vector2(a.x + arrow_length*cos(angle-arrow_spread), a.y + arrow_length*sin(angle-arrow_spread))

	draw_line(a,b,color,width, true)
	draw_line(a,pts[0],color,width, true)
	draw_line(a,pts[2],color,width, true)


#======================= GUI ROUTINES ================================
func _onSlotRightClicked(pos, pressed):
	if pressed:
		#Start a manual connection request.
		print ("Starting manual connection at ", pos)
		manual_src_pos = pos
		update()

func _gui_input(event):
	if event is InputEventMouseButton:
		if event.button_index == BUTTON_RIGHT:
			if !event.pressed:
				var tile_size = rect_size / total_ops
				var dest = (get_local_mouse_position() / tile_size).floor()
				print ("Requesting manual connection at ", dest)
				request_connection(manual_src_pos, dest)
				manual_src_pos = Vector2.ONE * -1
				update()
		return
	if event is InputEventMouseMotion and manual_src_pos.x > -1:
		#We're busy drawing a manual connection.  Update the control.
		update()
	
#Requests a manual connection between two operators.
func request_connection(source, dest):
	if source.x >= total_ops or dest.x >= total_ops or source.x < 0 or dest.x < 0:  return
	if source.y >= total_ops or dest.y >= total_ops or source.y < 0 or dest.y < 0:  return
	if dest.y <= source.y:  
		print("request_connection():  Destination operator must be at a lower level to connect")
		return
	
	
	var source_op = getGridID(source)
	var dest_op = getGridID(dest)
	if source_op==null or dest_op==null:
		print("request_connection():  Both source %s and destination %s must be valid operators."
				 % [source_op, dest_op])
		return
	
	#If we have reached this point, the above variables contain the operator indices we need.
	var src_idx = source_op
	var dest_idx = dest_op
	print("request_connection():  Connecting %s and %s..." % [src_idx+1, dest_idx+1])
	source_op = ops[source_op]
	dest_op = ops[dest_op]
	
	if dest_op.connections.find(source_op) != -1:
		print("request_connection():  %s and %s are already connected!" % [src_idx+1, dest_idx+1])
		return
	if source_op.manual_connections.find(dest_op) != -1:
		print("request_connection():  %s and %s are already connected!" % [src_idx+1, dest_idx+1])
		return
	
#	dest_op.connections.append(source_op)
	source_op.manual_connections.append(dest_op)
	emit_signal("slot_moved")  #FIXME:  Maybe make a different signal for this?
	update()
	
	

#Returns an array of 8-bit ints with each bit flagged with whether a slot is connected to that position's operator.
func get_connection_descriptions():
	var output = []
	for i in ops.size():  output.append(0)

	for op in ops:
		if op.id == -1:  continue
		
		#op.connections propagate upwards through the tree.
		for source in op.connections:
			output[source.id] |= 1 << (op.id)
			
		#however, op.manual_connections propagate _downwards_ from the source id.
		for dest in op.manual_connections:
			output[op.id] |= 1 << (dest.id)
	
	return output


func compare_height(a:opNode,b:opNode):
	return a.gridPos.y < b.gridPos.y


#===================== CLASS =============================
class opNode:
	var id = 0  
	var connections = []
	var manual_connections = []  #Used to break connections in a tree above as well as make draw offsets
	var gridPos = Vector2.ONE * -1

	func pos_valid():  return gridPos != Vector2(-1,-1)

	func reset_pos():
		gridPos = Vector2.ONE * -1

	func has_connection_to(op):  #Recursively checks if another operator is in the tree.
		for o in connections:
			if o == op:  return true
			if o.has_connection_to(op):  return true
		return false
		
	func swap_connections_with(op):  #Note:  You need to also update the grid slots to reflect this
		prints("Swapping connections between", id+1, "and", op.id+1)
		var c = connections
		global.arr_replace(c, id, op.id)
		global.arr_replace(op.connections, op.id, id)
		connections = op.connections
		op.connections = c

		c = manual_connections
		global.arr_replace(c, id, op.id)
		global.arr_replace(op.manual_connections, op.id, id)
		manual_connections = op.manual_connections
		op.manual_connections = c


	func get_tree_ids(output_dict={}):  #Returns the IDs of every operator connected to this tree.
		for o in connections:
			output_dict[o.id] = o.id
			o.get_tree_ids(output_dict)

	func get_connection_string():
		var output = "OP" + str(id+1) + ": ["
		for op in connections:
			output += str(op.id+1) + ", "
		return output + "]"
