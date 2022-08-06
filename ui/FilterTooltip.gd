extends EGTooltip
class_name FilterTooltip
const icons = [
	preload("res://gfx/ui/filter/0.svg"),
	preload("res://gfx/ui/filter/1.svg"),
	preload("res://gfx/ui/filter/2.svg"),
	preload("res://gfx/ui/filter/3.svg"),
	preload("res://gfx/ui/filter/4.svg"),
	preload("res://gfx/ui/filter/5.svg"),
	preload("res://gfx/ui/filter/6.svg"),
	preload("res://gfx/ui/filter/7.svg"),
	preload("res://gfx/ui/filter/8.svg"),
	preload("res://gfx/ui/filter/9.svg"),
]

#func _ready():
#	#Tweak position if the bounds are offscreen.
#	var edge = rect_global_position + rect_size
#	var vp = get_viewport_rect()
#	if edge.x > vp.size.x:  
#		yield(get_tree(), "idle_frame")
#		rect_global_position.x -= edge.x - vp.size.x +16
#	if edge.y > vp.size.y:  
#		yield(get_tree(), "idle_frame")
#		rect_global_position.y -= edge.y - vp.size.y + 16

#	pass


func set_from_op(op:int):
	if !chip_loc:  
		print("FilterTooltip:  Can't find chip... Goodbye")
		queue_free()
		return

	var eg = get_node(chip_loc)
	var opCount = eg.GetOpCount()
	if opCount < op+1 or op<0:  #Uh oh.  invalid op.
		print("FilterTooltip:  Invalid op %s.  Chip reports %s ops.  Goodbye!" % [op, opCount])
		queue_free()
		return

	var d = eg.GetOpValues(0, op)  #EG dictionary

	#Set the envelope.
	var type = clamp(eg.GetOscTypeOrFunction(op), 0, 9)
	
	var btn = $V/Panel/Filter
	btn.icon = icons[type]
	btn.text = " " + global.FilterNames[type]

	if type > 6:
		if d["gain"] != 1:
			btn.text += " (%*.*fx gain)" % [0, 1, d["gain"]]
	else:
		btn.text += " (%*.*f%% wet)" % [0, 1, 100 - d["duty"] / 655.350]

	#Set the labels.
	$V/H/Op.bbcode_text = OPTEXT % (op+1)
	$V/H/Q.text = "%*.*fx" % [0, 2, d.get("resonance", 1)]
	
	var freq = d.get("cutoff", 22050)
	if freq >= 1000:
		$V/H/Hz.text = "%shz" % int(freq) 
	else: 
		$V/H/Hz.text = "%*.*fhz" % [3, 1, freq]


