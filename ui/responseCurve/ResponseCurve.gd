tool
extends Panel

enum ranges {rates, velocity, levels}
export (ranges) var intent = ranges.rates

var intent_range_strings= {ranges.rates: "63\n\n\n\n56\n\n\n\n48\n\n\n\n40\n\n\n\n32\n\n\n\n24\n\n\n\n16\n\n\n\n8\n\n\n\n",
#					ranges.velocity:  "127\n\n\n\n87\n\n\n\n75\n\n\n\n62\n\n\n\n50\n\n\n\n37\n\n\n\n25\n\n\n\n12\n\n\n\n",
				ranges.velocity:  "[}4\n\n\n\n896\n\n\n\n768\n\n\n\n640\n\n\n\n512\n\n\n\n384\n\n\n\n256\n\n\n\n128\n\n\n\n",
		ranges.levels: "[}4\n\n\n\n896\n\n\n\n768\n\n\n\n640\n\n\n\n512\n\n\n\n384\n\n\n\n256\n\n\n\n128\n\n\n\n",
#				ranges.levels: "127\n\n\n\n112\n\n\n\n96\n\n\n\n80\n\n\n\n64\n\n\n\n48\n\n\n\n32\n\n\n\n16\n\n\n\n",
							}

signal minmax_changed

func set_intent(val):
	intent = val
	if !is_inside_tree():  return
	$lblValue.text = intent_range_strings[val]
	
	if intent == ranges.velocity:
		#Default is NO velocity sensitivity.  Set attenuation ceiling to 0.
		$Label.text = "Velocity"
		$Label.rect_position.y = 286
		$OctaveRuler.texture = preload("res://gfx/ui/vu/ruler32.png")
		$lblOctave.visible = false
		$lblVelocity.visible = true
	else:
		$Label.text = "Octave"
		$Label.rect_position.y = 288
		$OctaveRuler.texture = preload("res://gfx/ui/vu/ruler24.png")
		$lblOctave.visible = true
		$lblVelocity.visible = false

	set_minmax_default(intent)
	

func _ready():
	set_intent(intent)
	
	$CustomCurvePopup.connect("changed", self, "set_custom_curve_preset")


func set_minmax_default(_intent):
#	match _intent:
#		ranges.velocity, ranges.rates:
			$MinMax/sldMin.value = 0
			$MinMax/sldMax.value = 0
			update_minmax()
#		_:
#			$MinMax/sldMin.value = 0
#			$MinMax/sldMax.value = 100

#	update_minmax()


func set_table_default(_intent):
	var SCALE = global.RTABLE_SIZE / 128.0
	match _intent:
		ranges.velocity:
			for i in 64:
#				$VU.tbl[i] = global.RTABLE_SIZE -SCALE -i*SCALE
#				$VU.tbl[i] = global.RTABLE_SIZE * ease((127-i)/127.0, 3)
				$VU.tbl[i] = global.RTABLE_SIZE * pow((127-i)/127.0, 3) * 0.75
				$VU.tbl[i+64] = global.RTABLE_SIZE * (127-i-64)/127.0 * 0.1875

		ranges.rates:
			for i in 128:
				$VU.tbl[i] = i*SCALE / 2

		ranges.levels:
			var RATIO = 64/12.0 #64 units of attenuation == 6dB per octave
			var START_NOTE=24  #Probably 8 actually to produce -60dB at highest octave
			for i in 128:
				$VU.tbl[i] = max(0, round((i-START_NOTE) * RATIO))

		_:
			for i in 128:
				$VU.tbl[i] = 0

	set_minmax_default(_intent)
	$VU.update()


func set_from_placeholder(p):
	#Expecting a dictionary with all the stuff we need and the tbl already converted from base64.
	$MinMax/sldMin.value = p["floor"]
	$MinMax/sldMax.value = p["ceiling"]
	for i in $VU.tbl.size():
		$VU.tbl[i] = p["tbl"][i]
	
	$VU.update()




func _on_btnPresets_id_pressed(id):
	match id:
		0xFF:  #Reset button hit.  Set a default table
			set_table_default(intent)

		0x40:  #Custom button hit.  Popup custom curve.
			var r = Rect2(get_global_mouse_position(), $CustomCurvePopup.rect_size)
			$CustomCurvePopup.popup(r)

		_:
			print ("Preset menu pressed, unknown value.  Intent: ", intent, "; ID: ", id)


#Update MinMax labels
func _on_sldMinMax_value_changed(value, isMax:bool):
	update_minmax()
	emit_signal("minmax_changed", value, isMax)


func update_minmax():
	#Update the labels and colorizers.
	var a = str($MinMax/sldMin.value)
	var b = str($MinMax/sldMax.value)

	$MinMax/lblMinMax.text = "%s/%s" % ["[]" if a=="100" else a.pad_zeros(2), "[]" if b=="100" else b.pad_zeros(2)]
	
	if int(a)>int(b):
		$MinMax/Colorizer.material.set_shader_param("disabled", 4)  #RED on both
		$VU.can_draw_minmax = false
	else:
		var amt = 0
		amt |= 1 if a == "100" else 0
		amt |= 2 if b == "0" else 0
		$MinMax/Colorizer.material.set_shader_param("disabled", amt)  #Sets L or R disabled
		$VU.can_draw_minmax = true if amt==0 else false

	$VU.lastmax = stepify((100-int(b)) * 1.28, 2.0)
	$VU.lastmin = stepify((100-int(a)) * 1.28, 2.0)
	$VU.update()

func set_custom_curve_preset(table):
	$VU.tbl = table
	$VU.update()
	$VU.emit_signal("table_updated", -1, -1, intent)  #-1 tells EGPanel to blast the update all message to the chip.
