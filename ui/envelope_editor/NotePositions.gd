extends Control

var tickLen = 1.0 / ProjectSettings.get_setting("physics/common/physics_fps") * 1000
enum BusyState{BUSY=128, RELEASED=512, FREE=1024}

func _draw():
#	if owner.owner == null:  return
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
	var off = c.channel_release_tick
	
	for i in ttl.size():
#		draw_string(get_font(""), Vector2(0, 16*i), str(ttl[i]))
		var busy = c.ChannelBusyState(i)
		if busy == BusyState.FREE:  continue
		var pos = 0
		
		if owner.has_loop and owner.has_sustain:
			if busy == BusyState.BUSY:  #Note is still pressed, process the sustain loop only.
				pos = proc_loop(proc_normal(ttl[i]), owner.data[owner.susStart].x, owner.data[owner.susEnd].x)
			else:  #First, get the true release position from the sustain process.
				var releasePos = proc_release(ttl[i], off[i])
				pos = proc_loop(releasePos)  #Then, process the loop from the time of release

		elif owner.has_sustain:
			if busy == BusyState.BUSY:  #Note is still pressed, process as if it were a loop
				pos = proc_loop(proc_normal(ttl[i]), owner.data[owner.susStart].x, owner.data[owner.susEnd].x)
			else:  #Note was released.  Determine the current frame.
				pos = proc_release(ttl[i], off[i])
				
		elif owner.has_loop:
			pos = proc_loop(proc_normal(ttl[i]))

		else:
			pos = proc_normal(ttl[i])
		
		if pos < bounds.offset or pos > bounds.offset + bounds.zoom:  continue
		var x = int((pos-bounds.offset) / bounds.zoom * rect_size.x)
		draw_line(Vector2(x, 0), Vector2(x, rect_size.y), ColorN("white", 0.5 if busy == BusyState.BUSY else 0.25))


func proc_normal(currentFrame):  return currentFrame * tickLen
func proc_loop(pos, start=-1, end=-1):
	if start == -1:  start = owner.data[owner.loopStart].x
	if end == -1:  end = owner.data[owner.loopEnd].x
	var frontBit = pos
	if frontBit < start:  return frontBit  #Still in normal part of loop
	if start==end:  return start
	else:
		return start + fmod(frontBit-start, end-start)
func proc_release(currentFrame, releaseFrame):   #Position at release after a sustain loop
	var frontBit = proc_normal(currentFrame)
	var start = owner.data[owner.susStart].x
	var end = owner.data[owner.susEnd].x
	var offTime = proc_normal(releaseFrame)
	
	if frontBit <= end or offTime <= end:  #Process as normal.
		return frontBit
	else:  #First get the correct position of the release bit
		var releasePoint = start if start==end else start + fmod(offTime-start, end-start)
		return releasePoint + (frontBit-offTime)
	
