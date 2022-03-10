extends Control
export (NodePath) var chip_loc  #Used by kanban columns to populate with new EGPanels.

enum ArrangeType {GROUP, TILE, STACK_GROUPS, STACK_COLUMNS, BY_INTENT}
	
func _ready():
	$WiringGrid/SlotIndicator.connect("op_size_changed", self, "_on_op_size_changed")

	#Convert the chip location to an absolute location!!
	#Otherwise, the value specified in the editor will only be valid for us and none of our children.
	chip_loc = get_node(chip_loc).get_path()  
	
#	global.connect("op_tooltip_needs_data", self, "_on_op_tooltip_needs_data")
	global.connect("op_intent_changed", self, "_on_op_intent_changed")
	
	#Expand the kanban scroller to fill the window.
	get_tree().connect("screen_resized", self, "resized")
	resized()  #Populate kanban columns.
	
	#Populate EGPanels in column 0.
	_on_op_size_changed(get_node(chip_loc).GetOpCount(), 0)
	

func resized():
	$Kanban.populate_columns(rect_size.x - $Kanban.rect_position.x)


func _on_op_size_changed(opNum:int, oldSz):
	#This delay is necessary if the wiring grid is waiting to set the algorithm in order to
	#send the correct grid positions, since the grid needs to be re-init'd when opSize changes.
	#If we didn't wait to add tabs, the chip may not be ready to offer data for new operators.
	yield(get_tree(), "idle_frame")
	
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
			if !column is KanbanColumn:  continue
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
	
	node_to_replace.queue_free()


#Called when Chip changes a lot (for example when parsing in JSON data)
func reinit_all():
	var chip = get_node(chip_loc)
	var oldSz = chip.GetOpCount()
	$WiringGrid/SlotIndicator.load_from_description( chip.GetAlgorithm() )
	
	#Update the number of operators.
	var newSz = chip.GetOpCount()
	yield(_on_op_size_changed(newSz, oldSz), "completed")

	#Now, rebuild all op intents....
	for i in newSz:
		#Fake call from the wiring grid to avoid trying to redraw it, since we're going to do that later,
		#if not done already....
		_on_op_intent_changed(i, chip.GetOpIntent(i), $WiringGrid) 
	
	$LTab.reinit()
	global.emit_signal("algorithm_changed")


func _on_Arrange_pressed(type=ArrangeType.TILE):
	var c=get_node(chip_loc)
	if !c:  return

	depopulate_kanban()
	match type:
		ArrangeType.GROUP:
			_on_op_size_changed(get_node(chip_loc).GetOpCount(), 0)
			
		ArrangeType.TILE:
			#Get list of columns available.
			var columns = []
			for column in $Kanban.get_children():
				if column is KanbanColumn:  columns.append(column)

			#Cycle through columns, round-robin, adding tab groups as necessary.
			for i in c.GetOpCount():
				var col = columns[i % columns.size()]
				var group = col.add_tab_group()
				col.make_tab(group, i, c.GetOpIntent(i))


		ArrangeType.STACK_GROUPS:
			var carriers = c.GetCarriers()

			var columns = []
			for column in $Kanban.get_children():
				if column is KanbanColumn:  columns.append(column)
			
			var placed = {}  #Tabs which have already been placed.
			for i in carriers.size():
				#Each carrier becomes the basis of a TabGroup.
				var col = columns[i % columns.size()]
				var group = col.add_tab_group()
				var carrier = carriers[i]
				var modulators = c.GetModulators(carrier)
				
				col.make_tab(group, carrier, c.GetOpIntent(carrier))
				placed[carrier] = true
				
				for m in modulators:  #Place this carrier's modulators in the group.
					if placed.has(m):  continue
					col.make_tab(group, m, c.GetOpIntent(m))
					placed[m] = true

		ArrangeType.STACK_COLUMNS:
			#TODO:  When number of columns is exhausted, group instead of modulo....
			var carriers = c.GetCarriers()

			var columns = []
			for column in $Kanban.get_children():
				if column is KanbanColumn:  columns.append(column)
			
			var placed = {}  #Tabs which have already been placed.
			for i in carriers.size():
#				var col = columns[i % columns.size()]
				var col = columns[min(i, columns.size()-1)]
				var carrier = carriers[i]
				var modulators = c.GetModulators(carrier)
				
				var group = col.add_tab_group()
				col.make_tab(group, carrier, c.GetOpIntent(carrier))
				placed[carrier] = true
				
				for m in modulators:  #Place this carrier's modulators in the column.
					if placed.has(m):  continue
					if carriers.size() <= columns.size() or i < columns.size(): 
						col.make_tab(col.add_tab_group(), m, c.GetOpIntent(m))
					else:
						col.make_tab(group, m, c.GetOpIntent(m))
					placed[m] = true

		ArrangeType.BY_INTENT:
			var columns = []
			for column in $Kanban.get_children():
				if column is KanbanColumn:  columns.append(column)
			
			#Get the number of intents available, subtracting "NONE" and "LFO:
			var numIntents = {}
			for op in c.GetOpCount():
				var intent = c.GetOpIntent(op)
				if numIntents.has(intent):
					numIntents[intent].append(op)
				else:  numIntents[intent] = [op]

			#Count the number of intent types we found in the algorithm.
			#If the number of intent types exceed the number of columns, then stick all of the
			#extra intents into the last column as groups.
			var column_number=0
			var must_group=false
			for intent_group in numIntents.keys():
				var group = columns[column_number].add_tab_group()

				var ops = numIntents[intent_group]
				for p in ops:
					if not must_group: 
						 #Make unique TabGroup for our operator.
						group = columns[column_number].add_tab_group()
					columns[column_number].make_tab(group, p, intent_group)
	
				column_number= column_number+1
				if column_number >= columns.size()-1:
					if numIntents.keys().size() > columns.size():
						must_group=true
					column_number = columns.size()-1


	for column in $Kanban.get_children():
		if column is KanbanColumn:  column.cleanup()


func depopulate_kanban():
	
	var last_queued_to_free
	for column in $Kanban.get_children():
		if !(column is KanbanColumn):  continue
		for tabgroup in column.get_node("V").get_children():
			if !(tabgroup is TabContainer):  continue
			last_queued_to_free = tabgroup.queue_free()
			
		if is_instance_valid(last_queued_to_free):  yield(last_queued_to_free, "tree_exited")
		column.cleanup()


