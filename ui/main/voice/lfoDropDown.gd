extends MenuButton
var previous_index = 1

func _ready():
	var s = get_popup()

#	s.theme = preload("res://ui/responseCurve/PresetMenu.theme")
	s.add_font_override("font", owner.get_font("font"))
	s.add_font_override("font_separator", owner.get_font("font"))
	s.add_color_override("font_color_separator", Color("#5fe0e0e0") )
#	s.add_constant_override("vseparation", 8)
	

	s.add_separator("LFO Speed Type")
	
	s.add_icon_radio_check_item(null, "Speed Preset")
	s.add_icon_radio_check_item(null, "Set Frequency")
	s.add_icon_radio_check_item(null, "Manual")

	s.set_item_checked(1, true)
	s.connect("id_pressed", self, "_on_id_pressed")
	
func _on_id_pressed(index):
	print("Menu item pressed: ", index)

	get_popup().set_item_checked(previous_index, false)
	get_popup().set_item_checked(index, true)

	match index:
		1:
			get_parent().get_node("Speed").visible = true
			get_parent().get_node("Knee").visible = false
			get_parent().get_node("Frequency").visible = false
		2, 3:
			get_parent().get_node("Knee").disabled = index==2
			
			get_parent().get_node("Speed").visible = false
			get_parent().get_node("Knee").visible = true
			get_parent().get_node("Frequency").visible = true
			
		_:
			pass

	previous_index = index
