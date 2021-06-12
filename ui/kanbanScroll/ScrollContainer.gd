extends ScrollContainer

var drop_preview:Rect2 = Rect2(Vector2.ZERO, Vector2.ZERO)  #Rect box of the drag tab preview size to draw
var dirty = false


func _ready():
	cleanup()

	global.connect("tab_dropped", self, "check_if_dirty")

#Called by global.tab_dropped by any control that wants to trigger us
func check_if_dirty(source=null):
#	print("Dirty trigger check!")
	if source == null:  
		if dirty:  cleanup()
		return
	elif source == self and dirty:  cleanup()


func cleanup():
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

	#Add prototype empty tab.
	var p = preload("res://ui/kanbanScroll/TabGroup.tscn").instance()
	$V.add_child(p)
	p.owner = owner
	p.name = "TabGroup" + str($V.get_child_count()-1)
	
	dirty = false


func can_drop_data(position, data):
	if data is Dictionary and data.has("type") and data["type"] == "tabc_element":
		return update_preview_rect(position)
	return false
	
func update_preview_rect(position):
	var last_child = $V.get_child($V.get_child_count()-1)
	var lastPos = last_child.rect_position.y + $V.rect_position.y #- last_child.rect_size.y

#	var data_tab_sz = Vector2(288,320) #Debug.  Pack into the data!!

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
		var target = $V.get_child($V.get_child_count()-1)
				
				
		src.remove_child(tab)
		target.add_child(tab)
		
		target.current_tab = target.get_tab_count()-1
#		target.emit_signal("tab_changed", target.current_tab)  #Forces cleanup check.  Is this done automatically?
#		if dirty:  cleanup()
	
	
func reset_drop_preview():
	drop_preview = Rect2(Vector2.ZERO, Vector2.ZERO)
	update()
	

func _draw():
	draw_rect(drop_preview, ColorN("cyan", 0.2))