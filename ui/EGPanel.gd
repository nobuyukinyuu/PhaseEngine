extends Tabs

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code
export(int,1,8) var operator = 1


func _ready():
	for o in $Rates.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setEG", [o.associated_property])
			
	pass


func setEG(value, property):
	get_node(chip_loc).SetEG(operator, property, value)
