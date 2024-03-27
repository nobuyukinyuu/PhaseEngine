extends ConfirmationDialog
enum {MULT, DIV, NORMALIZE, RESET}


func _ready():
	for o in $Margin/V/H.get_children():
		if not o is Button:  continue
		o.connect("pressed", self, "_on_btn_pressed", [o])
	pass

func _on_btn_pressed(sender):
	if not sender.name.is_valid_integer():  return
	var spin = $Margin/V/SpinBox

	match int(sender.name):
		MULT:
			spin.value *= 2.0
		DIV:
			if spin.value == 0:  pass
			spin.value /= 2.0
		NORMALIZE:
			spin.value = get_normalized_value()
		RESET:
			spin.value = 100


func get_normalized_value():
	var loudest=0
	var VU = owner.get_node("%VU")
	
	for i in VU.tbl.size():
		var val = range_lerp(VU.tbl[i], 0,100,-1,1)
		loudest = max(loudest, abs(val))

	if loudest == 0:  return 0
	return 1.0/loudest * 100
	
func amplify():
	var spin = $Margin/V/SpinBox
	var VU = owner.get_node("%VU")
	var tmp = []

	for i in VU.tbl.size():
		var val = range_lerp(VU.tbl[i], 0,100,-1,1)
		tmp.append(val)

	for i in VU.tbl.size():
		VU.tbl[i] = clamp(range_lerp(tmp[i]*(spin.value/100.0), -1, 1, 0, 100), 0, 100)

	if owner.get_node("H2/Smooth").pressed:  VU.smooth()
	VU.update()
	owner.update_table(owner.ALL)




func _on_Amplify_confirmed():
	amplify()
	$Margin/V/SpinBox.value = 100
	pass # Replace with function body.
