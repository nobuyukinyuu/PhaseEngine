#tool
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


func set_minmax_default(_intent):
	match _intent:
		ranges.velocity, ranges.rates:
			$MinMax/sldMin.value = 0
			$MinMax/sldMax.value = 0
		_:
			$MinMax/sldMin.value = 0
			$MinMax/sldMax.value = 100
	update_minmax()


func set_table_default(_intent):
	match _intent:
		ranges.velocity, ranges.rates:
			for i in 128:
				$VU.tbl[i] = 127-i
		_:
			for i in 128:
				$VU.tbl[i] = 0

	set_minmax_default(_intent)
	$VU.update()

func _on_btnPresets_id_pressed(id):
	match id:
		0xFF:
			set_table_default(intent)

		_:
			print ("Preset menu pressed, unknown value.  Intent: ", intent, "; ID: ", id)


#Update MinMax labels
func _on_sldMinMax_value_changed(value, isMax:bool):	
	update_minmax()
	emit_signal("minmax_changed", value, isMax)


func update_minmax():
	var a = str($MinMax/sldMin.value)
	var b = str($MinMax/sldMax.value)

	$MinMax/lblMinMax.text = "%s/%s" % ["[]" if a=="100" else a.pad_zeros(2), "[]" if b=="100" else b.pad_zeros(2)]
	
	if int(a)>int(b):
		$MinMax/Colorizer.material.set_shader_param("disabled", 4)
	else:
		var amt = 0
		amt |= 1 if a == "100" else 0
		amt |= 2 if b == "0" else 0
		$MinMax/Colorizer.material.set_shader_param("disabled", amt)
		
		
