extends Control
class_name VoicePanel, "res://gfx/ui/ops/icon_fm.svg"

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code
export(int,0,8) var operator = 0
const ENV_TITLE = "[ Op%s ] %s Envelope"
const FOLLOW_TITLE = "[ Op%s ] %s Key Mapping"
const VEL_TITLE = "[ Op%s ] %s Velocity Table"

enum {NONE=0, ENVELOPE=1, KEY_FOLLOW=2, VELOCITY_TABLE=4, ALL=0x7} #BindAbilities
const WINDOW_TITLE = {ENVELOPE: ENV_TITLE, KEY_FOLLOW: FOLLOW_TITLE, VELOCITY_TABLE: VEL_TITLE}
enum {LOC_TYPE_EG, LOC_TYPE_PG}



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


#Pulls bind values from Chip.  Need to specify the location, whether the EG/PG etc
func get_bind_values(loc, property) -> Dictionary:
	return get_node(chip_loc).GetBindValues(loc, operator, property)
func existing_binds(loc, property):
	return get_node(chip_loc).ExistingBinds(loc, operator, property)

	
func request_bind_editor(sender:EGSlider, data:Dictionary):
	#Get the requisite bind's data packet for the given slider.
#	var data = get_node(chip_loc).GetBindValues(0, operator, sender.associated_property)

	#determine the title based on the current panel's specifications.
	var title = ENV_TITLE % [operator+1, sender.associated_property.to_upper()]
	sender.request_bind_editor(title, data, global.Contexts.VOICE, operator)
	pass
	
func rebind(sender:EGSlider, property_loc, type=ALL):  #Rebinds a control to its envelope editor.
	if type == null or type == 0:
		printerr("BasePanel.gd:  Attempting to rebind to nothing.  Check call stack!!")
		
	if !existing_binds(property_loc, sender.associated_property):  return
	print("Found bind(s) for ", sender.associated_property, ".  Rebinding...")

#	var abilities=sender.bind_abilities

	#Look and see if there's an existing popup and rebind it to our control.
	for i in 3:
		if (type >>i) & 1 == 0:  continue  #Flag not enabled for this bind type.

		var title = WINDOW_TITLE[1<<i] % [operator+1, sender.associated_property.to_upper()]
		var existing_popup = global.get_modeless_popup(title, global.Contexts.VOICE)
		if existing_popup:
			sender.bind_editor = existing_popup.get_path()
			existing_popup.rebind_to(sender.get_path())
		else:
			print(name, ":  Couldn't find existing panel for ", title)


	sender.is_bound = true
	sender.update()  #Redraws the bound icon

#General purpose func to find a bind editor for a given EGSlider
func find_bind_editor(sender:EGSlider,property_loc, type=NONE) -> WindowDialog:
	#TODO:  SUPPORT MULTIPLE EDITORS
	if type == NONE:  return null
	var output
	output = get_node_or_null(sender.bind_editor)  #Check the sender's editor loc first.
	print("BasePanel:  Sender has invalid editor location.")
	if output !=null:  return output
	
	#If we reached here then the sender's editor location was invalidated.  Try the modeless window manager
	var title = ENV_TITLE % [operator+1, sender.associated_property.to_upper()]
	print ("Attempting to find ", title)
	output = global.get_modeless_popup(title, global.Contexts.VOICE)
	
	return output
	
	
