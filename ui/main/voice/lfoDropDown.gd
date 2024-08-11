extends MenuButton
enum {FROM_PRESET, MANUAL_FREQUENCY, KNEE, __S1=255} #Determines how to set the frequency and knee in the UI.
var previous_id = FROM_PRESET
var knee_is_manual = false

func _ready():
	var s = get_popup()

#	s.theme = preload("res://ui/responseCurve/PresetMenu.theme")
	s.add_font_override("font", owner.get_font("font"))
	s.add_font_override("font_separator", owner.get_font("font"))
	s.add_color_override("font_color_separator", Color("#5fe0e0e0") )
	s.add_constant_override("vseparation", 8)
	

	s.add_separator("LFO Speed Type", __S1)
	
	s.add_icon_radio_check_item(null, "From Preset", FROM_PRESET)
	s.add_icon_radio_check_item(null, "Set Frequency", MANUAL_FREQUENCY)
#	s.add_icon_radio_check_item(null, "Manual Knee")
	s.add_separator("Knee Length", __S1)
	s.add_icon_check_item(null, "Set Manually", KNEE)

	s.set_item_checked(1, true)
	s.connect("id_pressed", self, "_on_id_pressed")


func _on_id_pressed(index):
	var c = get_node(owner.chip_loc)

	match index:
		FROM_PRESET, MANUAL_FREQUENCY:
			get_parent().get_node("Speed").visible = index==FROM_PRESET
			get_parent().get_node("Frequency").visible = index!=FROM_PRESET

			var pop = get_popup()
			pop.set_item_checked(pop.get_item_index(previous_id), false)
			pop.set_item_checked(pop.get_item_index(index), true)
		
			previous_id = index

	match index:
		FROM_PRESET:
			c.SetLFO("Speed", get_parent().get_node("Speed").value)
		MANUAL_FREQUENCY:
			c.SetLFO("Frequency", get_parent().get_node("Frequency").value)
		
		KNEE:
			get_parent().get_node("Knee").disabled = knee_is_manual
			knee_is_manual = !knee_is_manual
			get_popup().set_item_checked(get_popup().get_item_index(KNEE), knee_is_manual)
			
		_: #Default
			pass

	#Set the speed type for the LFO so serialization knows what we want to specify.
	var speedType = MANUAL_FREQUENCY if get_parent().get_node("Frequency").visible else FROM_PRESET
	if knee_is_manual:  speedType += KNEE

	c.SetLFOSpeedType(speedType)
