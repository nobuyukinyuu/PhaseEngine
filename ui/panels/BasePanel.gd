extends Control
class_name VoicePanel, "res://gfx/ui/icon_fm.svg"

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code
export(int,0,8) var operator = 0


func _ready():
	pass # Replace with function body.

func set_from_op(op:int):
	printerr("BasePanel.gd:  set_from_op() not defined for derived panel type! Op", op+1)

#Used as a helper for the KanbanScroll control element.
onready var limiter:SceneTreeTimer = get_tree().create_timer(0)
func _gui_input(_event):
	if limiter.time_left > 0:  return
	var vp = get_viewport()
	if !vp.gui_is_dragging():  return
	#Since we're detecting a drag, might as well update the owner column's preview rect...
#	$"..".ownerColumn.update_preview_rect($"..".ownerColumn.get_local_mouse_position())
	$"..".ownerColumn.reset_drop_preview()
	$"..".set_drop_preview(false)
	limiter = get_tree().create_timer(0.2)


func _on_Mute_toggled(button_pressed, bypass:bool):
	if bypass:
		get_node(chip_loc).SetBypass(operator, button_pressed)
	else:
		get_node(chip_loc).SetMute(operator, button_pressed)
	global.emit_signal("op_tab_value_changed")
