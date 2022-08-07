extends Control
class_name VoicePanel, "res://gfx/ui/ops/icon_fm.svg"

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code
export(int,0,8) var operator = 0
const ENV_TITLE = "[ Op%s ] %s Envelope"

func _ready():
	pass # Replace with function body.

func set_from_op(op:int):
	printerr("BasePanel.gd:  set_from_op() not defined for derived panel type! Op", op+1)

func check_binds():
	printerr("BasePanel.gd:  check_binds() not defined for derived panel type! Op", operator+1)

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


func get_bind_values(type, property) -> Dictionary:
	return get_node(chip_loc).GetBindValues(type, operator, property)
func bind_exists(type, property):
	return get_node(chip_loc).BindExists(type, operator, property)

	
func request_envelope_editor(sender:EGSlider, data:Dictionary):
	#Get the requisite bind's data packet for the given slider.
#	var data = get_node(chip_loc).GetBindValues(0, operator, sender.associated_property)

	#determine the title based on the current panel's specifications.
	var title = ENV_TITLE % [operator+1, sender.associated_property.to_upper()]
	sender.request_envelope_editor(title, data, global.Contexts.VOICE, operator)
	pass
func rebind(sender:EGSlider, type):  #Rebinds a control to its envelope editor.
	if !bind_exists(type, sender.associated_property):  return
	print("Found bind for ", sender.associated_property, ".  Rebinding...")

	#Look and see if there's an existing popup and rebind it to our control.
	var title = ENV_TITLE % [operator+1, sender.associated_property.to_upper()]
	var existing_popup = global.get_modeless_popup(title, global.Contexts.VOICE)
	if existing_popup:
		sender.envelope_editor = existing_popup.get_path()
		existing_popup.rebind_to(sender.get_path())
	else:
		print(name, ":  Couldn't find existing panel for ", title)

	sender.is_bound = true
	sender.update()

func update_initial_value_of(target:EGSlider):
	pass
	pass
