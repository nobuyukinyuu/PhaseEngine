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

	for o in [$Frequency, $"Q [Resonance]"]:
		o.connect("value_changed", self, "setEG", [o.associated_property])

	for i in $G.get_child_count():
		$G.get_child(i).connect("pressed", self, "select", [i])

	yield(get_tree(),"idle_frame")
	$lblTitle.text = "Filter %s" % (self.operator+1)

#Selects a filter type.
func select(value):
	var eg = get_node(chip_loc)
	eg.SetFilterType(self.operator, value)
	global.emit_signal("op_tab_value_changed")


#Bus operator values from the C# Chip handler.  
func set_from_op(op:int):
	var eg = get_node(chip_loc)
	var d = eg.GetOpValues(0, op)  #EG dictionary

	var type = d["aux_func"]
	$Filter.value = type
	$G.get_child(type).pressed = true
	select(type)
	
	
#	$Filter.value = d[""]
	$Frequency.value = d["cutoff"]
	$"Q [Resonance]".value = d["resonance"]

	#TODO:  Consider using AMS to control wet/dry
#	$Tweak/AMS.value = d["ams"]

	#TODO:  Consider using duty as the wet/dry value.	
#	$Duty.value = d["duty"]

	
	$Mute.pressed = d["mute"]
	$Bypass.pressed = d["bypass"]


func setEG(value, property):
	get_node(chip_loc).SetEG(operator, property, value)
	get_node(chip_loc).RecalcFilter(operator)
	global.emit_signal("op_tab_value_changed")



func _on_Filter_value_changed(value):
	var eg = get_node(chip_loc)
	$Filter/Icon.texture = icons[value]
	eg.SetFilterType(self.operator, value)
	global.emit_signal("op_tab_value_changed")

