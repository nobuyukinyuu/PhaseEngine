extends PanelContainer
var chip_loc  #Location of PhaseEngine instance in code
const OPTEXT = "0p[i].[/i][color=#ffff00][b]%s[/b][/color]"
class_name EGTooltip

func _ready():
	var edge = rect_global_position + rect_size
	var vp = get_viewport_rect()
	if edge.x > vp.size.x:  
		yield(get_tree(), "idle_frame")
		rect_global_position.x -= edge.x - vp.size.x +16
	if edge.y > vp.size.y:  
		yield(get_tree(), "idle_frame")
		rect_global_position.y -= edge.y - vp.size.y + 16

	pass


func setup(chip, opNum):
	yield(self, "tree_entered")

	chip_loc = chip
	set_from_op(opNum)
	

func set_from_op(op:int):
	if !chip_loc:  
		print("EGTooltip:  Can't find chip... Goodbye")
		queue_free()
		return

	var eg = get_node(chip_loc)
	var opCount = eg.GetOpCount()
	if opCount < op+1 or op<0:  #Uh oh.  invalid op.
		print("EGTooltip:  Invalid op %s.  Chip reports %s ops.  Goodbye!" % [op, opCount])
		queue_free()
		return

	var d = eg.GetOpValues(0, op)  #EG dictionary
	var d2 = eg.GetOpValues(1, op) #PG dictionary
	

	#Set the envelope.
	var type = clamp(eg.GetOpType(op), 0, 9)   #Probably should be getting this from GetOpValues?
	$V/EnvelopeDisplay/Wave.texture = global.wave_img[type]

	var rates = d["rates"]
	var levels = d["levels"]
	
	$V/EnvelopeDisplay.Attack = rates[0]
	$V/EnvelopeDisplay.Decay = rates[1]
	$V/EnvelopeDisplay.Sustain = rates[2]
	$V/EnvelopeDisplay.Release = rates[3]

	$V/EnvelopeDisplay.Delay = d["delay"]
	$V/EnvelopeDisplay.Hold = d["hold"]
	
	$V/EnvelopeDisplay.tl = levels[4] / global.TL_MAX
	$V/EnvelopeDisplay.al = levels[0] / global.L_MAX
	$V/EnvelopeDisplay.dl = levels[1] / global.L_MAX
	$V/EnvelopeDisplay.sl = levels[2] / global.L_MAX
#	$V/EnvelopeDisplay.update()

	#Set the labels.
	$V/H/Op.bbcode_text = OPTEXT % (op+1)
	$V/H/Level.text = "%*.*f%%" % [0, 1, (1.0-$V/EnvelopeDisplay.tl) * 100]
	if d2["fixedFreq"]:  #Show hz.
		var freq = d2["base_hz"]  #tuned_hz might also work here but only detune is applied (too small to represent), so...
		if freq >= 1000:
			$V/H/Hz.text = "%shz" % int(freq) 
		else: 
			$V/H/Hz.text = "%*.*fhz" % [3, 1, freq]

	else:  #Show multiplier.
		var mult = d2["tuned_hz"] / float(d2["base_hz"])  #Detune is not considered here since we only have 2 sigdigs..
		$V/H/Hz.text = "%*.*fx" % [3, 2, mult]
