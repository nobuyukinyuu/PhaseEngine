extends TabContainer

var dragPreview = preload("res://ui/kanbanScroll/DragTabPreview.tscn")
onready var ownerColumn = $"../.."

var drop_preview:Rect2
var dropZone = Rect2(Vector2.ZERO, Vector2(rect_size.x, 16))


func _ready():
	set_tabs_rearrange_group(global.OPERATOR_TAB_GROUP)
	connect("tab_changed", self, "_on_tab_changed")	
	connect("mouse_exited", self, "set_drop_preview", [false])
	hint_tooltip = "-"


func _on_tab_changed(idx):
#	print("%s: tab changed? to %s" % [name, idx])
#	if get_tab_count() == 0:  print("uh oh, empty.  Time to go away...")
	if ownerColumn.dirty:  ownerColumn.cleanup()
	set_drop_preview(false)

func _gui_input(event):

	#This check overrides the drag preview, since we can't override TabContainers' get_drag_data().
	var vp = get_viewport()
	if event is InputEventMouseButton and event.button_index == BUTTON_LEFT:
		
		#This delays the function long enough to catch a drag even if we hovered off the control.
		yield(get_tree().create_timer(0.15), "timeout")

	#Wake up, capture the drag.
	if !vp.gui_is_dragging():  return

	#Viewport says we're in drag mode.  Check to see if the drag data matches us.
	#{from_path:/root/Control/ScrollContainer/V/TabGroup0, tabc_element:0, type:tabc_element}
	var data = vp.gui_get_drag_data()

	if data is Dictionary and data.has("type") and data["type"] == "tabc_element":
		if get_node(data["from_path"]) == self:
			#We can get data["tabc_element"] to determine the tab number here
			if not ownerColumn.dirty:  #No custom override has been set yet.  Set it to us.
				__set_drag_preview( get_child(data["tabc_element"]) )

		#Also set the drop preview
		set_drop_preview(true)

#Called by the tab group -and- child tabs at times when dragging.
func __set_drag_preview(var tab):
	var p = dragPreview.instance()
	p.get_node("Tab/Lbl").text = tab.name
	set_drag_preview(p)

	ownerColumn.dirty = true

func set_drop_preview(dragging:bool):
	var can_drop = dragging and dropZone.has_point(get_local_mouse_position()) 

	if can_drop:  
		drop_preview = Rect2(Vector2.ZERO, rect_size)
	else:
		drop_preview = Rect2(Vector2.ZERO, Vector2.ZERO)

	update()

func _draw():
#	draw_string(preload("res://gfx/fonts/spelunkid_font.tres"), Vector2(32, 32), str(rect_position.y), ColorN("red"))
	draw_rect(drop_preview, ColorN("cyan", 0.2))


func _make_custom_tooltip(for_text):
	#FIXME:  Support other panel types
	var p = preload("res://ui/EGTooltip.tscn").instance()
	p.rect_position = get_local_mouse_position()  #Adjust this position when too close to the window border, check later!

	#Probably need to emit a signal here telling a parent that an EGTooltip appeared and to set its values correctly.
	global.emit_signal("op_tooltip_needs_data", self, p)
	return p


#Base tab width is 32+20 (1 char and icon); active is 40.  
enum Widths {base=24+20, active=+8, chr=+8}
func get_tab_idx_at_point(pos=get_local_mouse_position()):
	#NOTE:  This method does not exist in GDScript prior to Godot 3.4, and this is a crappy stopgap implementation
	#		Which DOES NOT WORK if the tabs spill past the control width (there's no way to get the scroll position),
	#		therefore this method will always return -1 if tabs spill over.
	#		You should be able to safely comment this func out if you're running Godot 3.4 or later.

		var x = pos.x
		var running_x = 0
		var probable_idx = -1
		for i in get_child_count():
			var tab = get_child(i)
			var width = Widths.base + len(tab.name)*Widths.chr
			if tab == get_current_tab_control():  width += Widths.active
			
			if x >= running_x and x < running_x + width:  probable_idx=i
			running_x += width
			
		if running_x >= rect_size.x:  return -1  #No scroll support!!
		if x < 0 or x > rect_size.x:  return -1

		return probable_idx


