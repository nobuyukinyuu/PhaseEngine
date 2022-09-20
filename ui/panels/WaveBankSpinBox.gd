extends SpinBox
var bankline:LineEdit  #LineEdit control for wave bank
var target = -1  #Operator target, or LFO
const tooltip_text= "No sample banks defined for the voice.\nAdd from the Waveform panel, or select a new oscillator."


func _ready():
	for o in get_children():
		if o is LineEdit:
			bankline = o
			break
	bankline.caret_blink = true
	bankline.connect("focus_entered", self, "focus", [true])
	bankline.connect("focus_exited", self, "focus", [false])

	global.connect("wavebanks_changed", self, "check_banks")

	#Set the operator target
	if owner is EGPanel:  target = owner.operator
	connect("value_changed", self, "_on_Bank_value_changed")

func focus(on):
	check_banks()
	var col = ColorN("yellow") if on and editable else ColorN("white")
	$lbl.modulate = col


func _on_Bank_value_changed(value):
	var c = get_node(owner.chip_loc)
	if !c:  return

	c.SetWaveBank(target, value)
	global.emit_signal("op_tab_value_changed")


func check_banks(removed_idx=-1):
	if !visible:  return  #Bank state not relevant because the oscillator is set to something else.
	
	var c = get_node(owner.chip_loc)
	if !c:  return
	
	var numBanks = c.NumBanks - 1
	var old_idx = value
	max_value = max(0, numBanks)
	editable = numBanks > 0

	bankline.hint_tooltip = "" if editable else tooltip_text

	if removed_idx >=0:  #Uh oh.  Banks may have shifted. Determine if we need to find the new index.
		if removed_idx == old_idx:  #Consider setting bank to 0 or an invalid value to default it out.
			pass
		elif removed_idx < old_idx:  #Our proper index has shifted.  Subtract 1.
			value = clamp(old_idx-1, 0, max_value)

	#Set the wave bank to whatever value is now valid.
	_on_Bank_value_changed(value)
