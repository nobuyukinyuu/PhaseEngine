extends Control
export (NodePath) var chip_loc  #Used by kanban columns to populate with new EGPanels.

	
func _ready():
	$WiringGrid/SlotIndicator.connect("op_size_changed", self, "_on_op_size_changed")

	#Convert the chip location to an absolute location!!
	#Otherwise, the value specified in the editor will only be valid for us and none of our children.
	chip_loc = get_node(chip_loc).get_path()  
	
	global.connect("op_tooltip_needs_data", self, "_on_op_tooltip_needs_data")
	global.connect("op_intent_changed", self, "_on_op_intent_changed")
	
	#Expand the kanban scroller to fill the window.
	get_tree().connect("screen_resized", self, "resized")
	resized()  #Populate kanban columns.
	
	#Populate EGPanels in column 0.
	_on_op_size_changed(get_node(chip_loc).GetOpCount(), 0)


func resized():
	$Kanban.populate_columns(rect_size.x - $Kanban.rect_position.x)


func _on_op_size_changed(opNum:int, oldSz):
#	if !is_inside_tree():  return

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
		
	#Update FM preview.
	$FMPreview.recalc()

func _on_op_intent_changed(opNum:int, intent, sender=null):  #Default sender:  WiringGrid.  Updated itself already.
	if sender!=$WiringGrid:  $WiringGrid/SlotIndicator.redraw_grid()

	#Update the kanbans to reflect the correct panel type.  First, find the location of the panel needing change.
	var opName = "Op" + str(opNum+1)
	var node_to_replace = $Kanban.find_node(opName, true, false)
	if !node_to_replace:  
		print("Can't find %s to change panels!" % opName)
		return
	
	var tab_group = node_to_replace.get_parent()
	var column =  tab_group.get_parent().get_parent()  #ScrollContainer <- VBox <- TabGroup
	var pos = node_to_replace.get_position_in_parent()
	
	tab_group.remove_child(node_to_replace)
	var p = column.make_tab(tab_group, opNum, intent)  #Make a new panel.
	tab_group.move_child(p, pos)
	
	


#Handler for tooltips that need data from a chip.
func _on_op_tooltip_needs_data(sender, tooltip):
	pass
#	if sender is TabContainer and tooltip is EGTooltip:
#		var idx = sender.get_tab_idx_at_point(sender.get_local_mouse_position())
#		if idx==-1:
##			print("Couldn't find tab at %s!" % sender.get_local_mouse_position())
#			yield(get_tree(), "idle_frame")
#			tooltip.visible = false
#			return
#
#		var c = get_node(chip_loc)
#		var tab = sender.get_tab_control(idx)
#		if c.GetOpIntent(tab.operator) == global.OpIntent.FILTER:  
#			print("replacement")
##			tooltip.replace_by(preload("res://ui/FilterTooltip.tscn").instance())
#			global.swap_scene(tooltip, preload("res://ui/FilterTooltip.tscn").instance())
#		tooltip.setup(chip_loc, tab.operator)
