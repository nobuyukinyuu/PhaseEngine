extends Control

var tickLen = 1.0 / ProjectSettings.get_setting("physics/common/physics_fps") * 1000

func _draw():
	var c = get_node(owner.owner.owner.chip_loc)
	if !c:  return
	
	var bounds = $"../TimeRuler"
	
	
	#TODO:  Calculate the position while taking loops and sustain into account.
	#		For loops, subtract the initial loop position time and modulo by the total loop time.
	#		NoteOff TTL time needs to be taken into account to determine how far past the loop
	#		The line goes if sustain loop's end extends beyond loop end.  
	
	#		At NoteOff, the end of the sustain loop should move back to the beginning of standard loop, if it exists.
	#		If the std loop is after the end of the sustain loop, proceed as normal until reaching it.
	#		Basically:  Sustain loop behavior completely overrides standard loop until NoteOff.
	
	#		Consider not looping back to standard loop if we passed it entirely after NoteOff
	#		and instead continue to move towards the end of the envelope.
	
	var ttl = c.channel_ttl
	for i in ttl.size():
#		draw_string(get_font(""), Vector2(0, 16*i), str(ttl[i]))
		var pos = ttl[i] * tickLen
		if pos < bounds.offset or pos > bounds.offset + bounds.zoom:  continue
		var x = int((pos-bounds.offset) / bounds.zoom * rect_size.x)
		draw_line(Vector2(x, 0), Vector2(x, rect_size.y), ColorN("white", 0.25))
