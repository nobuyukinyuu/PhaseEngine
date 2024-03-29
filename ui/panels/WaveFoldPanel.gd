extends VoicePanel
onready var sliders = { $Tweak/Gain : 0,  $Bias : 32768}
onready var rTables = [$KSR, $Velocity, $KSL]

func _ready():

	for o in $Tweak.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setEG", [o.associated_property])
		o.connect("bind_requested", self, "bind_val", [o, o.associated_property, true])
		o.connect("unbind_requested", self, "bind_val", [o, o.associated_property, false])

	$Bias.connect("value_changed", self, "setEG", [$Bias.associated_property, 32768])
	$Bias.connect("bind_requested", self, "bind_val", [$Bias, $Bias.associated_property, true])
	$Bias.connect("unbind_requested", self, "bind_val", [$Bias, $Bias.associated_property, false])

	$Crush.connect("value_changed", self, "setEG", [$Crush.associated_property, 1])

	for o in $Rates.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setEG", [o.associated_property])
		o.connect("value_changed", self, "update_env", [o])
	
	for o in $Levels.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setEG", [o.associated_property])
		o.connect("value_changed", self, "update_env", [o])
		o.connect("bind_requested", self, "bind_val", [o, o.associated_property, true])
		o.connect("unbind_requested", self, "bind_val", [o, o.associated_property, false])

	$Limit.connect("toggled", self, "setLimit")

	#Connect the rTables.
	for o in rTables:
		o.connect("table_updated", self, "update_table")
		o.connect("minmax_changed", self, "update_table_minmax")
		
		

	if !chip_loc.is_empty():
		set_from_op(operator)



#Bus operator values from the C# Chip handler.  
func set_from_op(op:int):
	var eg = get_node(chip_loc)
	var d = eg.GetOpValues(0, op)  #EG dictionary

	#Get dictionary of rTable values and populate the tbl_placeholder in ResponseButtons
	for btn in rTables:
		var intent = btn.intent
		var data = eg.GetTable(op, intent)
		var err = validate_json(data)
		if err:
			print("EGPanel:  Error parsing RTable; ", err)
			continue
		btn.init_table(parse_json(data))
	
	var rates = d["rates"]
	$"Rates/Attack".value = rates[0]
	$"Rates/Decay".value = rates[1]
	$"Rates/Sustain".value = rates[2]
	$"Rates/Release".value = rates[3]
	
	
	$"Rates/+Delay".value = d["delay"]
	$"Rates/+Hold".value = d["hold"]

	var levels = d["levels"]
	$"Levels/Total Level".value = levels[4] 
	$"Levels/Attack Level".value = levels[0]
	$"Levels/Decay Level".value = levels[1]
	$"Levels/Sustain Level".value = levels[2]
	
	$Limit.pressed = d["osc_sync"]
	$Tweak/AMS.value = d["ams"]
	$Tweak/Gain.value = d["gain"]
	$Bias.value = d["duty"] - 32767
	$Crush.value = d["aux_func"]-1  #Compensate for crush starting at 2 after 0. -1 will automatically become 0

	
	$Mute.pressed = d["mute"]
	$Bypass.pressed = d["bypass"]

	refresh_envelope_preview()
	check_binds()

func check_binds():  #Goes through all bindable controls and rebinds them to editors if necessary.
	if chip_loc.is_empty():  return
	for o in $Rates.get_children():
		if !o is EGSlider:  continue
		if o.bind_abilities == NONE:  continue
		rebind(o, LOC_TYPE_EG, o.bind_abilities)
	for o in $Levels.get_children():
		if !o is Slider:  continue
		if o.bind_abilities == NONE:  continue
		rebind(o, LOC_TYPE_EG, o.bind_abilities)
	for o in $Tweak.get_children():
		if !o is Slider:  continue
		if o.bind_abilities == NONE:  continue
		rebind(o, LOC_TYPE_EG, o.bind_abilities)

	for o in [$Bias, $Crush]:
		if !o is Slider:  continue
		if o.bind_abilities == NONE:  continue
		rebind(o, LOC_TYPE_EG, o.bind_abilities)


func refresh_envelope_preview():
	var d = get_node(chip_loc).GetOpValues(0, operator)
	var rates = d["rates"]
	var levels = d["levels"]

	$EnvelopeDisplay.Attack = rates[0]
	$EnvelopeDisplay.Decay = rates[1]
	$EnvelopeDisplay.Sustain = rates[2]
	$EnvelopeDisplay.Release = rates[3]

	$EnvelopeDisplay.Delay = d["delay"]
	$EnvelopeDisplay.Hold = d["hold"]
	
	$EnvelopeDisplay.tl = levels[4] / global.L_MAX
	$EnvelopeDisplay.al = levels[0] / global.L_MAX
	$EnvelopeDisplay.dl = levels[1] / global.L_MAX
	$EnvelopeDisplay.sl = levels[2] / global.L_MAX

#Updates a single part of the envelope preview.
const env_map = {"ar":"Attack", "dr":"Decay", "sr":"Sustain", "rr":"Release"}
func update_env(value, sender:EGSlider):
	var prop = sender.associated_property

	if prop.ends_with("l"):
		$EnvelopeDisplay.call("update_" + prop, value/global.L_MAX)
	else:
		$EnvelopeDisplay.call("update_" + prop, value)



func setEG(value, property, adjustment=0):
	if property=="aux_func":  #Crush value is special. 
		# Send 0 if value is 0 regardless of adjustment. Adjustment starts the crush at 2 bits.
		if value == 0:  adjustment = 0
	get_node(chip_loc).SetEG(operator, property, value+adjustment)
	global.emit_signal("op_tab_value_changed")

func setLimit(value):  #For the OscSync limiter
	get_node(chip_loc).SetEG(operator, "osc_sync", value)
	global.emit_signal("op_tab_value_changed")


#Updates an rTable for this operator.
func update_table(column:int, value:int, intent):
	var c = get_node(chip_loc)
	var curve = rTables[intent]
	
	if column >= 0:
		c.UpdateTable(operator, column, value, intent)
	else:
		#Update the entire table.
		#FIXME:  Don't reference this if instance is placeholder.  Probably shouldn't ever happen, but?
		c.SetTable(operator, curve.get_node("P/Curve/VU").tbl, intent)


func update_table_minmax(value, isMax:bool, intent):
	get_node(chip_loc).SetTableMinMax(operator, value, isMax, intent)
