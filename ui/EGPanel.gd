extends Tabs

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code
export(int,0,8) var operator = 0

var tt = 0

func _ready():
	for o in $Tune.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setPG", [o.associated_property])
		

	for o in $Rates.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setEG", [o.associated_property])

	for o in $Tweak.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setEG", [o.associated_property])


	for o in $Levels.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setEG", [o.associated_property])

	$WavePanel/Wave.connect("value_changed", self, "setWaveform")
#	$Tweak/Feedback.connect("value_changed", self, "setFeedback")
#	$Tweak/Duty.connect("value_changed", self, "setDuty")
	pass


onready var limiter:SceneTreeTimer = get_tree().create_timer(0)
func _gui_input(_event):
	if limiter.time_left > 0:  return
	var vp = get_viewport()
	if !vp.gui_is_dragging():  return
	#Since we're detecting a drag, might as well update the owner column's preview rect...
#	$"..".ownerColumn.update_preview_rect($"..".ownerColumn.get_local_mouse_position())
	$"..".ownerColumn.reset_drop_preview()
	limiter = get_tree().create_timer(0.2)




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
	


