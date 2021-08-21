extends Control
export (NodePath) var chip_loc  #Used by kanban columns to populate with new EGPanels.

	
func _ready():
	$WiringGrid/SlotIndicator.connect("op_size_changed", self, "_on_op_size_changed")

	#Convert the chip location to an absolute location!!
	#Otherwise, the value specified in the editor will only be valid for us and none of our children.
	chip_loc = get_node(chip_loc).get_path()  


func _on_op_size_changed(opNum:int, oldSz):
	if !is_inside_tree():  return

	#Repopulate kanbans.
	var col = $Kanban/Column0

	#First, find all the tabs which need to go away.
	#We probably can't rely on Column's to_remove because the tab could be in any column.
	if opNum < oldSz:
		for i in range(opNum+1, oldSz+1):
			var opName = "Op" + str(i)
			var node_to_free = $Kanban.find_node(opName, true, false)
			if node_to_free:  
				node_to_free.queue_free()
				yield(node_to_free, "tree_exited") #Make the function wait until the nodes are free before cleanup


		for column in $Kanban.get_children():
			if !column is ScrollContainer:  continue
			column.cleanup()
#
	elif opNum > oldSz:  #Next, find any tabs that need to be added.
		var to_add:PoolByteArray
		for i in range(oldSz, opNum):
			to_add.append(i)
		
		col.populate(to_add, null)
	
