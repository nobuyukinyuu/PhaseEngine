extends Panel
var bankline:LineEdit  #LineEdit control for wave bank

const CUSTOM=9  #Microsample oscillator index

func _ready():
	
#	for i in global.waves.size():
	for i in $Popup/G.get_child_count():
		$Popup/G.get_child(i).connect("pressed", self, "_on_Popup_button_pressed", [i])
	
	$Preview.texture = global.wave_img[0]
	$Popup.rect_size = $Popup/G.rect_size

	for o in $Bank.get_children():
		if o is LineEdit:
			bankline = o
			break
	bankline.caret_blink = true
	bankline.connect("focus_entered", self, "focus", [true])
	bankline.connect("focus_exited", self, "focus", [false])

	global.connect("wavebanks_changed", self, "check_banks")

func _on_Wave_value_changed(value):
	$Preview.texture = global.wave_img[value]
	switch_bank_ui(value==CUSTOM)


func _on_Preview_gui_input(event):
	if event is InputEventMouseButton and event.pressed and event.button_index == BUTTON_LEFT:
		var pos = get_global_mouse_position()
#		print(get_viewport().size)
		if get_viewport().size.x - pos.x < $Popup/G.rect_size.x:  pos.x -= $Popup/G.rect_size.x/2
		if get_viewport().size.y - pos.y < $Popup/G.rect_size.y:  pos.y -= $Popup/G.rect_size.y/2
		$Popup.popup(Rect2(pos, $Popup.rect_size))

func _on_Popup_button_pressed(idx):
	$Wave.value = idx
	$Popup.hide()

	switch_bank_ui(idx==CUSTOM)

func switch_bank_ui(on):
	$Wave.visible = !on
	$Bank.visible = on

	if on:  check_banks()


func focus(on):
	check_banks()
	
	var col = ColorN("yellow") if on and $Bank.editable else ColorN("white")
	$Bank/lbl.modulate = col

#	if numBanks >= 0:
#		c.SetWaveBank(owner.operator, $Bank.value)


func _on_Bank_value_changed(value):
	var c = get_node(owner.chip_loc)
	if !c:  return

#	print("op%s Bank changed to %s" % [owner.operator+1, value])
	c.SetWaveBank(owner.operator, value)


const tooltip_text= "No sample banks defined for the voice.\nAdd from the Waveform panel, or select a new oscillator."
func check_banks(removed_idx=-1):
	if !$Bank.visible:  return  #Bank state not relevant because the oscillator is set to something else.
	
	var c = get_node(owner.chip_loc)
	if !c:  return
	
	var numBanks = c.NumBanks - 1
	var old_idx = $Bank.value
	$Bank.max_value = max(0, numBanks)
	$Bank.editable = numBanks > 0

	bankline.hint_tooltip = "" if $Bank.editable else tooltip_text

	if removed_idx >=0:  #Uh oh.  Banks may have shifted. Determine if we need to find the new index.
		if removed_idx == old_idx:  #Consider setting bank to 0 or an invalid value to default it out.
			pass
		elif removed_idx < old_idx:  #Our proper index has shifted.  Subtract 1.
			$Bank.value = clamp(old_idx-1, 0, $Bank.max_value)

	#Set the wave bank to whatever value is now valid.
	_on_Bank_value_changed($Bank.value)
