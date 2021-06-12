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

	set_from_op(operator)


onready var limiter:SceneTreeTimer = get_tree().create_timer(0)
func _gui_input(_event):
	if limiter.time_left > 0:  return
	var vp = get_viewport()
	if !vp.gui_is_dragging():  return
	#Since we're detecting a drag, might as well update the owner column's preview rect...
#	$"..".ownerColumn.update_preview_rect($"..".ownerColumn.get_local_mouse_position())
	$"..".ownerColumn.reset_drop_preview()
	limiter = get_tree().create_timer(0.2)


func set_from_op(op:int):
	var eg = get_node(chip_loc)
	var d = eg.GetOpValues(0, op)
	var d2 = eg.GetOpValues(1, op)
	var type = clamp(eg.GetOpType(op), 0, $WavePanel/Wave.max_value)
	$WavePanel/Wave.value = type
	
	$Tune/Mult.value = d2["mult"]
	$Tune/Coarse.value = d2["coarse"]
	$Tune/Fine.value = d2["fine"]
	$Tune/Detune.value = d2["detune"]

	#TODO:  Set FIXED TUNING HZ ETC
	
	var rates = d["rates"]
	$"Rates/Attack".value = rates[0]
	$"Rates/Decay".value = rates[1]
	$"Rates/Sustain".value = rates[2]
	$"Rates/Release".value = rates[2]
	
	$"Rates/+Delay".value = d["delay"]
	$"Rates/+Hold".value = d["hold"]

	var levels = d["levels"]
	$"Levels/Total Level".value = levels[4]
	$"Levels/Attack Level".value = levels[0]
	$"Levels/Decay Level".value = levels[1]
	$"Levels/Sustain Level".value = levels[2]
	
	$Tweak/Feedback.value = d["feedback"]
	$Tweak/Duty.value = d["duty"]

func refresh_envelope_preview():
	var d = get_node(chip_loc).GetOpValues(0, operator)
	var rates = d["rates"]
	var levels = d["levels"]
	
	#TODO
	
#	$EnvelopeDisplay.Attack = 
	


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
	


