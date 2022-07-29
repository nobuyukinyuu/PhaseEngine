class_name DragTabPreview
extends Control

const wave_desc = ["Sin", "Tri", "Saw", "Sqr", "Wht", "Pnk", "Brn", "Ns1", "APU", "Wav"]
const filt_desc = ["Flt", "LPF", "HPF", "BPF", "BPz", "Not", "All", "Pk" , "LoS", "HiS"]
const bit_desc  = ["And", "Or", "Xor", "R\\g"]
const intent_cols = ["ffffff", "36d5ff", "38dfc8", "b3efff", "ffb9b5"]

const bit_icons = [
	preload("res://gfx/ui/ops/icon_and.svg"),
	preload("res://gfx/ui/ops/icon_or.svg"),
	preload("res://gfx/ui/ops/icon_xor.svg"),
	preload("res://gfx/ui/ops/icon_rectifier.svg"),
]

func _ready():
	pass

func _exit_tree():
	global.emit_signal("tab_dropped")


func setup(intent, osc_type):
	yield(self, "tree_entered")

	var desc = set_desc(intent, osc_type)
	if desc != "Fld":  $P/D.text = desc 
	$P/D.icon = set_icon(intent, osc_type)
	$P/D.modulate = intent_cols[intent]

func set_desc(intent, osc_type):
	
	match intent:
		global.OpIntent.FM_OP:
			return wave_desc[osc_type]
		global.OpIntent.FILTER:
			return filt_desc[osc_type]
		global.OpIntent.BITWISE:
			return bit_desc[osc_type]
		global.OpIntent.WAVEFOLDER:
			return "Fld"  #Gain override is done in TabGroup.gd   __set_drag_preview()
		_:
			return "?"

func set_text(t):  #Sets the main label to whatever specified
#	print("okiki ", $P/D.text)
	$P/D.text = t
#	print("now ", $P/D.text)

func set_icon(intent, osc_type):
	var style = $PanelBG.get_stylebox("panel")
	var style2 = $Tab.get_stylebox("panel")
	style.border_color = Color("1da8b2") #Approx h value of 0.511185
	style2.border_color = Color("1da8b2")

	match intent:
		global.OpIntent.FM_OP:
			#Use waveform icons.
			return load("res://gfx/ui/ops/%s.svg" % min(osc_type,4))
		global.OpIntent.FILTER:
			return load("res://gfx/ui/filter/%s.svg" % osc_type)
		global.OpIntent.BITWISE:
			style.border_color.b = 0.85
			style2.border_color.b = 0.85
			return bit_icons[osc_type]
		global.OpIntent.WAVEFOLDER:
			style.border_color.h = 0.85
			style2.border_color.h = 0.85

			return preload("res://gfx/ui/ops/icon_wavefolder.svg")
		_:
			return preload("res://gfx/ui/icon_invalid.svg")
