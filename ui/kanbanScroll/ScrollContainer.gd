extends ScrollContainer
class_name KanbanColumn

var drop_preview:Rect2 = Rect2(Vector2.ZERO, Vector2.ZERO)  #Rect box of the drag tab preview size to draw
var dirty = false



func _ready():
	cleanup()
	global.connect("tab_dropped", self, "check_if_dirty")
	connect("mouse_exited", self, "reset_drop_preview")



#Populate this column with NEW tabs representing the specified operators.
func populate(to_add, to_remove):
	var rem = []
	if to_remove is int and to_remove==-1:  #Remove all children.
		for tab in $V.get_children():
			if !tab is TabContainer:  continue
			for t in tab.get_children():
				rem.append(t)
	elif to_remove is PoolByteArray:  #Remove selected in to_remove
		for item in to_remove:
			rem.append($V.find_node("Op" + str(item)))  #FIXME:  Find another way to find tab items!
			
	#Remove the nodes we found.
	if !rem.empty():
		for node in rem:
			node.queue_free()
		#Wait for a bit to make sure the nodes are removed.
		yield(rem[rem.size()-1], "tree_exited")

	cleanup()

	if to_add==null:  return
	#Start adding new tabs to the first TabGroup.
	if owner.chip_loc.is_empty():  return
	var c = get_node(owner.chip_loc)
	var t = $V.get_child(0)  #Should be TabGroup0
	for op in to_add:
		make_tab(t, op, c.GetOpIntent(op))
		
#	t.set_tab_icon(0, preload("res://gfx/ui/icon_fm.svg"))
#	print(var2str(t.get_child(0).get_meta_list()), "\n", t.get_child(0).get_meta("_tab_icon"))

#Called by global.tab_dropped by any control that wants to trigger us
func check_if_dirty(source=null):
#	print("Dirty trigger check!")
#	if source == null:  
		if dirty:  cleanup()
#		return
#	elif source == self and dirty:  cleanup()


func cleanup(total_rearrange=false):
	#Clean up empty tab groups and rearrange existing ones.
#	print("Cleaning up!")

	reset_drop_preview()

	for o in $V.get_children():
		if !o is TabContainer:  continue
		o.name = "TempTab" + str(o.get_position_in_parent())
		
		if o.get_tab_count() == 0:  #Empty tab group.  Delete.
			o.remove_and_skip()
#			print ("Node %s removed" % o.name)
			o.queue_free()

	#Rename existing tab containers.  Easiest way to guarantee safe names is rename twice.
	#TODO:  Check if second loop needs to defer an idle frame
	for i in $V.get_child_count():
		var o = $V.get_child(i)
		if !o is TabContainer:  continue
		o.name = "TabGroup" + str(i)
			
	
	#Add prototype empty tab group.
	add_tab_group()
	
	dirty = false

	if total_rearrange:
		yield(get_tree(), "idle_frame")
		#Arrange button hit, meaning all tabs with binds will have locations invalidated.
		#Node names probably have changed, meaning the nodepaths from child panels have changed.
		#For each panel in a TabGroup, have it re-check its connections to bound envelope editors.
#		yield(get_tree(), "idle_frame")
		for o in $V.get_children():
			if !o is TabContainer:  continue
			for panel in o.get_children():
				if not is_instance_valid(panel):  continue
				if not panel is VoicePanel:  continue
				print("Checking binds on ", panel.name, ": ", panel.get_path())
				panel.check_binds()



func add_tab_group():
	#Add prototype empty tab group.
	var p = preload("res://ui/kanbanScroll/TabGroup.tscn").instance()
	$V.add_child(p)
	p.owner = owner
	p.name = "TabGroup" + str($V.get_child_count()-1)

	return p

#Creates a new tab in the given group with the specified intent.
func make_tab(group_loc:TabContainer, opNum:int, intent):
	var p
	match intent:
		global.OpIntent.FM_OP:
			p = preload("res://ui/panels/EGPanel.tscn").instance()
		global.OpIntent.FM_HQ:
			p = preload("res://ui/panels/EGPanel.tscn").instance()
		global.OpIntent.FILTER:
			p = preload("res://ui/panels/FilterPanel.tscn").instance()
		global.OpIntent.BITWISE:
			p = preload("res://ui/panels/BitwiseOpPanel.tscn").instance()
		global.OpIntent.WAVEFOLDER:
			p = preload("res://ui/panels/WaveFolderPanel.tscn").instance()
		_:
			printerr("ScrollContainer.make_tab():  Unknown intent %s!" % intent)

	p.name = "Op" + str(opNum+1)
	group_loc.add_child(p)
	p.chip_loc = owner.chip_loc
	p.operator = opNum
	p.owner = owner
	p.set_from_op(opNum)  #This should be done in the _ready() of the panel already??
	p.set_meta("_tab_icon", global.OpIntentIcons[intent])

	return p


func can_drop_data(position, data):
	if data is Dictionary and data.has("type") and data["type"] == "tabc_element":
		return update_preview_rect(position)
	return false
	
func update_preview_rect(position):
	var last_child = $V.get_child($V.get_child_count()-1)
	var lastPos = last_child.rect_position.y + $V.rect_position.y #- last_child.rect_size.y


#	print("%s > %s, %s ?" % [position.y + scroll_vertical, last_child.name, lastPos])
	if position.y + scroll_vertical > lastPos:
		drop_preview.size = last_child.rect_size #data_tab_sz
		drop_preview.position = Vector2(8, lastPos + last_child.rect_size.y * 2)
		
		#Drop preview is out of bounds?  Move it to a visible range.
		if drop_preview.position.y > rect_size.y:
			drop_preview.position.y -= last_child.rect_size.y * 2
		
		update()
		return true
	else:
		reset_drop_preview()
		update()
		return false
			

func drop_data(position, data):
	reset_drop_preview()
	
	if !data is Dictionary:  return
	if !data.has("type"):  return
	
	if data["type"] == "tabc_element":  #We have a tab to move!
		var src = get_node(data["from_path"])
		var tab = src.get_tab_control(data["tabc_element"])
		var target = $V.get_child($V.get_child_count()-1)  #Target TabContainer
				
				
		src.remove_child(tab)
		target.add_child(tab)
		
		
		target.current_tab = target.get_tab_count()-1
		src.ownerColumn.dirty=true  #Flag src column as dirty.
		dirty = true  #Flag self as dirty.
		target.emit_signal("tab_changed", target.current_tab)  #Forces cleanup check.  Is this done automatically?

		#Check the binds inside the Modeless popups to point to the new location if they're still open.
		if tab is VoicePanel:
#			print("ScrollContainer:  checking binds on %s." % tab.name)
			tab.check_binds()
	
	
func reset_drop_preview():
	drop_preview = Rect2(Vector2.ZERO, Vector2.ZERO)
	update()
	

func _draw():
	draw_rect(drop_preview, ColorN("cyan", 0.2))
