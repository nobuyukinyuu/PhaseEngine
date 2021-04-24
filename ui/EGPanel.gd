extends Tabs

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code
export(int,1,8) var operator = 1

var tt = 0

func _ready():
	for o in $Tune.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setPG", [o.associated_property])
		

	for o in $Rates.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setEG", [o.associated_property])

	for o in $Levels.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setEG", [o.associated_property])

	$WavePanel/Wave.connect("value_changed", self, "setWaveform")
	$Tweak/Feedback.connect("value_changed", self, "setFeedback")
	$Tweak/Duty.connect("value_changed", self, "setDuty")
	pass




func setEG(value, property):
	get_node(chip_loc).SetEG(operator, property, value)
func setPG(value, property):
	get_node(chip_loc).SetPG(operator, property, value)

func setWaveform(value):
	get_node(chip_loc).SetWaveform(operator, value)

func setFeedback(value):
	get_node(chip_loc).SetFeedback(operator, value)

func setDuty(value):
	get_node(chip_loc).SetDuty(operator, value)
	


