extends Control

var pts = []

var should_be_visible:bool
var previous_visible_state:bool
var should_recalc:bool
onready var anim:AnimationPlayer = $"../VisFlip"

func _ready():
	owner.get_node("MIDI Control").connect("note_on", self, "flip_on")
	global.connect("op_tab_value_changed", self, "cache_recalc")
	global.connect("algorithm_changed", self, "cache_recalc")

func _physics_process(delta):
	if should_recalc:  recalc()

func _draw():
	draw_line(rect_size, Vector2(rect_size.x,0), ColorN("white", 0.5))
	draw_line(Vector2(0, rect_size.y/2), Vector2(rect_size.x, rect_size.y/2), ColorN("white", 0.3))

	var c = get_node(owner.chip_loc)

	for i in max(0, pts.size()-1):
		var h = rect_size.y/2
		var h2 = 0.75 if not c else 4.0/float(c.connections_to_output()) * 0.75
		var a = Vector2(i, pts[i] * h2 * h + h)
		var b = Vector2(i+1, pts[i+1] * h2 * h + h)
		draw_line(a, b, ColorN("yellow", 0.6),1.0, true)
		draw_line(a, Vector2(i, h), ColorN("yellow", 0.05))

func _on_Timer_timeout():
	var c = get_node(owner.chip_loc)
	if !c:  return
	#is_quiet procs PriorityScore for all channels so we only want to do it sparingly.
	flip_on( c.is_quiet() )
#	print ("Timeout")


func cache_recalc():
	should_recalc = true

func recalc(op_size_changed=false):
	if !should_be_visible:  return
	var c = get_node(owner.chip_loc)
	if !c:  return
	if op_size_changed:  c.RecalcPreviewFilters()
	pts = c.CalcPreview()
	update()
	should_recalc = false
	

func flip_on(var preview_on, var shim_from_midi=null):
#	if preview_on:  $Timer.paused = true
	if preview_on and not shim_from_midi:  #Show FM Preview.  Timer-activated
		should_be_visible = true
		if should_be_visible != previous_visible_state:  
			anim.play_backwards("NoteOn")
			recalc()
#			print("Rendering")
			$Timer.paused = true
	else:  #Show oscilloscope.  Function was called from midi event or otherwise 
		should_be_visible = false
		$Timer.paused = false
		if should_be_visible != previous_visible_state:  
			anim.play("NoteOn")
#			print("Playing")
			

	previous_visible_state = should_be_visible
		
