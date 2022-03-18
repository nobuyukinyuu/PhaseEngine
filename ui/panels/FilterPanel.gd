extends VoicePanel
const icons = [
	preload("res://gfx/ui/filter/0.svg"),
	preload("res://gfx/ui/filter/1.svg"),
	preload("res://gfx/ui/filter/2.svg"),
	preload("res://gfx/ui/filter/3.svg"),
	preload("res://gfx/ui/filter/4.svg"),
	preload("res://gfx/ui/filter/5.svg"),
	preload("res://gfx/ui/filter/6.svg"),
	preload("res://gfx/ui/filter/7.svg"),
	preload("res://gfx/ui/filter/8.svg"),
	preload("res://gfx/ui/filter/9.svg"),
]


func _ready():
	$Filter.display_strings = global.FilterNames

	for o in [$Frequency, $"Q [Resonance]", $"Dry Mix", $Gain]:
		o.connect("value_changed", self, "setEG", [o.associated_property])

	for i in $G.get_child_count():
		$G.get_child(i).connect("pressed", self, "select", [i])


	if !chip_loc.is_empty():
		set_from_op(operator)
		
	yield(get_tree(),"idle_frame")
	$lblTitle.text = "Filter %s" % (self.operator+1)



#Selects a filter type.
func select(value):
	var eg = get_node(chip_loc)
	eg.SetFilterType(self.operator, value)
	
	set_editable($Gain, value > 6)
	
	global.emit_signal("op_tab_value_changed")


#Bus operator values from the C# Chip handler.  
func set_from_op(op:int):
	var eg = get_node(chip_loc)
	var d = eg.GetOpValues(0, op)  #EG dictionary

	var type = d["aux_func"]
	if type >= global.FilterType.size():  type = global.FilterType.NONE #Type was invalid.  Reset.
	$Filter.value = type 
	$G.get_child(type).pressed = true
	select(type)
	
	
#	$Filter.value = d[""]
	$Frequency.value = d["cutoff"]
	$"Q [Resonance]".value = d["resonance"]
	$"Dry Mix".value = d["duty"]
	$Gain.value = d["gain"]

	#TODO:  Consider using AMS to control wet/dry
#	$Tweak/AMS.value = d["ams"]

	
	$Mute.pressed = d["mute"]
	$Bypass.pressed = d["bypass"]


func setEG(value, property, recalc=true):
	get_node(chip_loc).SetEG(operator, property, value)
	if recalc:  
		get_node(chip_loc).RecalcFilter(operator)
	global.emit_signal("op_tab_value_changed")


func set_editable(which, is_enabled=true):  
	if is_enabled:
		which.modulate = Color(1,1,1)
	else:
		which.modulate = Color("505056")
	which.editable = is_enabled

func _on_Filter_value_changed(value):
	var eg = get_node(chip_loc)
	$Filter/Icon.texture = icons[value]
	eg.SetFilterType(self.operator, value)
	global.emit_signal("op_tab_value_changed")

