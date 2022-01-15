extends HBoxContainer
var column = preload("res://ui/kanbanScroll/Column.tscn")
var C_WIDTH = 360  #Initial value

func _ready():
	C_WIDTH = max(C_WIDTH, get_child(0).rect_size.x + get_constant("separation"))  #Buff size if necessary


func set_c_width(child=-1, use_max=false):
	var last_width = C_WIDTH
	if child==-1:  child = get_child_count() - 1
	C_WIDTH = get_child(child).rect_size.x + get_constant("separation")
	
	if use_max:  C_WIDTH = max(C_WIDTH, last_width)

func populate_columns(target_width:float=0):
#	prints(rect_size.x, target_width)
	if target_width == 0 or target_width==null:  target_width = rect_size.x  #Auto width.

	#Grab the last current column for spillover later.
	var srcColumnV = get_child(get_child_count()-1).get_node("V")

	if target_width > rect_size.x:  #Increase size
		var new_columns_added = _fill(target_width)
		if not new_columns_added:  return
		set_c_width(-1, true)
		
		#Spillover any tab groups which are off-screen into the new empty columns.
		var destV = min(srcColumnV.get_parent().get_position_in_parent()+1, get_child_count()-1) #Next column, probably
		destV = get_child(destV).get_node("V")  #Transform into a beautiful butterfly :p
		
		for group in srcColumnV.get_children():
			if not group is TabContainer or group.get_child_count() == 0:  continue
			if not group.rect_position.y + group.rect_size.y > rect_size.y:  continue
			resettle_tab_group(group, destV, srcColumnV)
			destV.move_child(group, max(0,destV.get_child_count()-2) ) #Move above the prototype.

		global.emit_signal("tab_dropped")
		
		
	elif target_width < rect_size.x and target_width > C_WIDTH:  #Decrease size
		#Find the column(s) to clear tabs from.
		var to_resettle = []
		var total_size = rect_size.x
		while total_size > target_width:
			total_size -= C_WIDTH
			to_resettle.append(get_child( get_child_count()- (to_resettle.size()+1) )) 
			
		var groups = []
		for c in to_resettle:
			var group_list = c.get_node("V")
			for group in group_list.get_children():
				#Don't add any fake shit or prototype groups.
				if not group is TabContainer or group.get_child_count() == 0:  continue
				groups.append(group)
				group_list.remove_child(group)  #Make orphan so we can nuke this column safely
			c.queue_free()
			
		#Wait for the columns to be deleted, then resettle the TabGroups.
		yield(get_tree(), "idle_frame")
		
		var lastColumnV = get_child(get_child_count()-1).get_node("V")
		if lastColumnV.get_child(lastColumnV.get_child_count()-1).get_child_count() == 0:
			#Check for empty TabGroup prototype and nuke it.
			var p = lastColumnV.get_child(lastColumnV.get_child_count()-1)
			p.queue_free()
			yield(p, "tree_exited")  #Wait for the empty tab to go away before doing anything else.
		
		#Rename any tabGroup in the groups to resettle to conform to the names of the ones in the lastColumn
		for group in groups:
			resettle_tab_group(group, lastColumnV)
			
		#Re-Add the TabGroup drag prototype.
		lastColumnV.get_parent().add_tab_group()


#Resettles a group TabContainer to dest, and removes it from src if necessary.
func resettle_tab_group(group, dest_column_vbox, src_column_vbox=null):
	if src_column_vbox:  
		src_column_vbox.remove_child(group)
		src_column_vbox.get_parent().dirty = true
		
	group.name = "TabGroup0"  #Should force every object to get renamed when added
	dest_column_vbox.add_child(group, true)  #Make sure it gets a unique name.
	group.ownerColumn = dest_column_vbox.get_parent()
	if !group.owner:  group.owner = owner  #Give the orphans a valid parent.

#Fills the kanban with scroll columns.
func _fill(target_width:float):
	var new_columns_added = false
	var total_size = rect_size.x
	while total_size <= target_width - C_WIDTH:
		new_columns_added = true
		var p = column.instance()
		add_child(p, true)
		p.owner=owner
		p.get_node("V/TabGroup0").owner = owner
		total_size += C_WIDTH

	return new_columns_added
