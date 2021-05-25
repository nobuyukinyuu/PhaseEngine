extends TabContainer

var dragPreview = preload("res://ui/kanbanScroll/DragTabPreview.tscn")
onready var ownerColumn = $"../.."

func _ready():
	set_tabs_rearrange_group(global.OPERATOR_TAB_GROUP)
	connect("tab_changed", self, "_on_tab_changed")

func _on_tab_changed(idx):
#	print("%s: tab changed? to %s" % [name, idx])
#	if get_tab_count() == 0:  print("uh oh, empty.  Time to go away...")
	if ownerColumn.dirty:  ownerColumn.cleanup()

func _gui_input(event):

	#This check overrides the drag preview, since we can't override TabContainers' get_drag_data().
	var vp = get_viewport()
	if event is InputEventMouseButton and event.button_index == BUTTON_LEFT:
		#This delays the function long enough to catch a drag even if we hovered off the control.
#		if !vp.gui_is_dragging():
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
				set_preview( get_child(data["tabc_element"]) )


#Called by the tab group -and- child tabs at times when dragging.
func set_preview(var tab):
	var p = dragPreview.instance()
	p.get_node("Tab/Lbl").text = tab.name
	set_drag_preview(p)

	ownerColumn.dirty = true
	



