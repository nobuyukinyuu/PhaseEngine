extends VoicePanel
class_name EGPanel

#export (NodePath) var chip_loc  #Location of PhaseEngine instance in code
#export(int,0,8) var operator = 0

onready var rTables = [$KSR, $Velocity, $KSL]

signal changed

func _ready():
	for o in $Tune.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setPG", [o.associated_property])
		

	for o in $Rates.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setEG", [o.associated_property])
		o.connect("value_changed", self, "update_env", [o])

#	for o in $Tweak.get_children():
#		if !o is Slider:  continue
#		o.connect("value_changed", self, "setEG", [o.associated_property])

	if $Tweak.has_node("Feedback"):  #Done manually to trigger the oscillator function check
		$Tweak/Feedback.connect("value_changed", self, "setFeedback")
	elif $More/P/V.has_node("Feedback"):  #Panel is a BitwiseOpPanel
		$More/P/V/Feedback.connect("value_changed", self, "setFeedback")
		
	if $Tweak.has_node("Func Type"):  #BitwiseOpPanels only
		$"Tweak/Func Type".connect("value_changed", self, "setBitwiseFunc")

	$Tweak/AMS.connect("value_changed", self, "setEG", [$Tweak/AMS.associated_property])
	$Duty.connect("value_changed", self, "setEG", ["duty"])
	
	$"More/P/V/Phase Offset".connect("value_changed", self, "setEG", ["phase_offset"])
	$"More/P/V/Increment Offset".connect("value_changed", self, "setPG", ["increment_offset"])
	$"More/P/V/Detune Randomness".connect("value_changed", self, "setPG", ["detune_randomness"])
	$More/P/V/OscSync.connect("toggled", self, "setEG", ["osc_sync"])


	for o in $Levels.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "setEG", [o.associated_property])
		o.connect("value_changed", self, "update_env", [o])


	$WavePanel/Wave.connect("value_changed", self, "set_oscillator")


	#Connect the rTables.
	for o in rTables:
		o.connect("table_updated", self, "update_table")
		o.connect("minmax_changed", self, "update_table_minmax")
		
		

	if !chip_loc.is_empty():
		set_from_op(operator)

##Used as a helper for the KanbanScroll control element.
#onready var limiter:SceneTreeTimer = get_tree().create_timer(0)
#func _gui_input(_event):
#	if limiter.time_left > 0:  return
#	var vp = get_viewport()
#	if !vp.gui_is_dragging():  return
#	#Since we're detecting a drag, might as well update the owner column's preview rect...
##	$"..".ownerColumn.update_preview_rect($"..".ownerColumn.get_local_mouse_position())
#	$"..".ownerColumn.reset_drop_preview()
#	$"..".set_drop_preview(false)
#	limiter = get_tree().create_timer(0.2)



#Bus operator values from the C# Chip handler.  
func set_from_op(op:int):
	var eg = get_node(chip_loc)
	var d = eg.GetOpValues(0, op)  #EG dictionary
	var d2 = eg.GetOpValues(1, op) #PG dictionary
	var type = clamp(eg.GetOscType(op), 0, $WavePanel/Wave.max_value)
	$WavePanel/Wave.value = type
	
	$Tune/Mult.value = d2["mult"]
	$Tune/Coarse.value = d2["coarse"]
	$Tune/Fine.value = d2["fine"]
	$Tune/Detune.value = d2["detune"]

#	$Frequency/H/Detune.value = d2["detune"]
	$FixedRatio.pressed = !d2["fixedFreq"]
	_on_FixedRatio_toggled(!d2["fixedFreq"], false)
	$Frequency/Frequency.value = d2["base_hz"]
	
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
	
	$Tweak/AMS.value = d["ams"]
	
	if $Tweak.has_node("Feedback"):  #Only set this if the control exists (it doesn't on a BitwiseOp)
		$Tweak/Feedback.value = d["feedback"]
	elif $More/P/V.has_node("Feedback"):
		$More/P/V/Feedback.value = d["feedback"]
	if $Tweak.has_node("Func Type"):
		$"Tweak/Func Type".value = d["aux_func"]
	$Duty.value = d["duty"]

	$"More/P/V/Phase Offset".value = d["phase_offset"] # Adjusted from EG
	$"More/P/V/Increment Offset".value = d2["increment_offset"] # Adjusted from PG
	$"More/P/V/Detune Randomness".value = d2["detune_randomness"] # ' '
	$More/P/V/OscSync.pressed = d["osc_sync"]
	
	$Mute.pressed = d["mute"]
	$Bypass.pressed = d["bypass"]

	refresh_envelope_preview()

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
#	match prop:
#		"tl", "al", "dl", "sl", "decay", "hold":
#			$EnvelopeDisplay.set(prop, sender.value)
#			return
#
#	if prop.ends_with("r") and len(prop) == 2:  #Rate
#		$EnvelopeDisplay.set(env_map[prop], sender.value)
	
	if prop.ends_with("l"):
		$EnvelopeDisplay.call("update_" + prop, value/global.L_MAX)
	else:
		$EnvelopeDisplay.call("update_" + prop, value)


func setOpProperty(value, property):
	get_node(chip_loc).SetOpProperty(operator, property, value)
	global.emit_signal("op_tab_value_changed")
func setBitwiseFunc(value):
	get_node(chip_loc).SetBitwiseFunc(operator, value)
	global.emit_signal("op_tab_value_changed")


func setEG(value, property):
	get_node(chip_loc).SetEG(operator, property, value)
	global.emit_signal("op_tab_value_changed")
func setPG(value, property):
#	#Keep the two detune properties in sync...
#	if property=="detune":  
#		$Frequency/H/Detune.value = value
#		$Tune/Detune.value = value
	get_node(chip_loc).SetPG(operator, property, value)
	global.emit_signal("op_tab_value_changed")

func set_oscillator(value):
	get_node(chip_loc).SetOscillator(operator, value)
	global.emit_signal("op_tab_value_changed")

func setFeedback(value):
	get_node(chip_loc).SetFeedback(operator, value)
	global.emit_signal("op_tab_value_changed")

func setDuty(value):
	get_node(chip_loc).SetDuty(operator, value)
	global.emit_signal("op_tab_value_changed")
	

onready var ab = [$Tune, $Frequency]
func _on_FixedRatio_toggled(button_pressed, update_chip=true):
	var i = int(button_pressed)
	ab[1-i].visible = true
	ab[i].visible = false
	
	if !chip_loc.is_empty() and update_chip:
		get_node(chip_loc).SetFixedFreq(operator, !button_pressed)
		global.emit_signal("op_tab_value_changed")

func setFreq(val):
	var freq = $Frequency/Frequency.value + $"Frequency/H/Fine Tune".value
	get_node(chip_loc).SetFrequency(operator, freq)
	
	$Frequency/H/Presets.select(global.notenum_from_hz(freq))
	global.emit_signal("op_tab_value_changed")


func _on_Mute_toggled(button_pressed, bypass:bool):
	if bypass:
		get_node(chip_loc).SetBypass(operator, button_pressed)
	else:
		get_node(chip_loc).SetMute(operator, button_pressed)
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




func _on_More_pressed():
	var r = $More.get_global_rect()
	r.position += Vector2(12, 16)
	r.position.y += $More.rect_size.y
	
	$More/P/V.rect_size.y = 1  #Reset minimum size so the next line doesn't keep expanding	
	r.size.y = max($More/P.rect_min_size.y, $More/P/V.rect_position.y + $More/P/V.rect_size.y+4)
	
	$More/P.popup(r)
	$More.grab_focus()
	
	var vpx = get_viewport_rect().size.x
	var popx = $More/P.rect_global_position.x + $More/P.rect_size.x
	if popx >= vpx:
		$More/P.rect_position.x = vpx - $More/P.rect_size.x - 16
	


