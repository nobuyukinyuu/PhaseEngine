extends WindowDialog
class_name EnvelopeEditorWindow, "res://gfx/ui/bind_indicator.png"

var lo=0
var hi=63

var data=[Vector2.ZERO]  #Intermediate point data.  Vec2 where x=ms, y=0-1.
var cached_display_bounds = {}

var last_clicked_position = Vector2.ZERO

enum LoopHandle {None, LoopStart, LoopEnd, SusStart = 4, SusEnd = 8}
var susStart:int = -1
var susEnd:int = -1
var loopStart:int= -1
var loopEnd:int = -1
var has_loop=false
var has_sustain=false

var selected = -1  #Selected point (or area between points, if Vector2) -- for add/remove pts

#Toolbar selector enum
enum{NEW_PT, REMOVE_PT, COPY, PASTE, SET_LOOP, SET_SUSTAIN}

func compare(a:Vector2,b:Vector2):  #Compares 2 vec2's in the data block for bsearch_custom
	return a.x < b.x
func sort():  data.sort_custom(self, "compare")

func setup(title, value):
	#TODO:  Receive data packet from core, set up minmax, translate core data to intermediate data.
	if title is String:  $H/lblTitle.text = title
	data[0].y = value


func _ready():
	#DEBUG, REMOVE
	visible = true
	rect_position += Vector2.ONE * 32

	for o in $lblValue.get_children():
		o.connect("value_changed", self, "up", [o])

#	randomize()
#	for i in 4:
#		data.append(Vector2(500*i, randf()))
##		data.append(Vector2(500*i, i/10.0))
#	sort()
	#END DEBUG
	
	#Associate the buttons.
	for o in $Btn.get_children():
		if not o is Button:  continue
		if o.toggle_mode == false:
			o.connect("pressed", self, "_on_ToolButton_pressed", [false, int(o.name)] )
		else:
			o.connect("toggled", self, "_on_ToolButton_pressed", [int(o.name)] )

	recalc_display_bounds()  #Determine how to draw the points visible in the display window.


#DEBUG, REMOVE.  Used by the debug minmax spinners to update the label
func up(val, which):
	if which==$lblValue/minn:  lo = val 
	else: hi = val
	set_minmax(lo, hi)


func get_display_bounds():
	return cached_display_bounds
func recalc_display_bounds():
	var output = {}	
	#Find the first point that would be visible in our drawing window.
	#Assume data is sorted.  update() should NOT be called if data is not sorted.
	output["first"] = search_closest(data, $Display/TimeRuler.offset, true )
	output["last"] = search_closest(data, ($Display/TimeRuler.offset + $Display/TimeRuler.zoom) )

	#Append crop indicators if either the first or last point is off-screen.
	if typeof(output["first"]) == TYPE_REAL:  output["clip_left"] = true
	if typeof(output["last"]) == TYPE_REAL:  output["clip_right"] = true

	cached_display_bounds = output
	return output

#Find the closest value in the data array to the value given.  Expects sorted array
func search_closest(arr, val, first=false):
	var low = 0
	var high = arr.size() - 1
	var mid = 0 
	var mid2 = 0.0

	while low <= high:
		mid = int((high + low) / 2)
		mid2 = ((high + low) / 2.0)
		var arrVal = arr[mid].x 

		# If x is greater, ignore left half
		if arrVal < val:
			low = mid + 1 
		# If x is smaller, ignore right half
		elif arrVal > val:
			high = mid - 1 
		# means x is present at mid
		else:
#			print ("found ", mid)
			return mid

	# If we reach here, then the element was not present
	# Return closest element as TYPE_REAL to indicate imperfect match that needs special calcs
#	prints("First: " if first else "Last: ", low,mid,high)
	return max(low,high)+0.1 if first else min(low,high)+0.1

#Sets the bound labels of the Y-Axis.
func set_minmax(lowest, highest):
	var s = ""
	for i in range(0,8):
#		s += "\n\n\n\n"
		var midval = round(lerp(highest+0.5, lowest, i/8.0))
		var mids = format_val(midval)
		s += mids
		s += "\n\n\n\n"
	s += format_val(lowest)
		
	$lblValue.text = s

#Formats the value labels on the Y-axis.
func format_val(val):
	var mids = str(abs(val))
	if abs(val) > 1024: mids = mids.substr(0,len(mids)-3) + "k"
	elif mids.begins_with("12"):  mids = repl_first(mids, "12", "}")
	elif mids.begins_with("100"):  mids = repl_first(mids, "100", "[]")
	elif mids.begins_with("102"): mids = repl_first(mids, "102", "[}")
	if sign(val) == -1:  mids = "-" + mids
	return mids
#Cheap Replace the first instance of what in input with replacement for format_val()
func repl_first(input:String, what, replacement):
	var l=len(what)
	var found = input.find(what)
	if found>=0:
		var slice = input.substr(found+l)
		return replacement + slice
	return input

#Perform various actions.
func _on_ToolButton_pressed(toggled=false, which_button=-1):
	match which_button:
		NEW_PT:
			if !$Display/PointCrosshair.should_display:  return
			var last_closest = $Display.last_closest_pt
			if last_closest<0 or last_closest>data.size():  return
			data.insert(last_closest+1, last_clicked_position)
			$Display.last_closest_pt += 1 #Select the newly created point.
			last_closest +=1  
			$Display/PointCrosshair.should_display=false
			$Display/PointCrosshair.visible=false

			#Readjust the loop bounds to account for the change.
			if loopEnd >= last_closest: shift_loop_pt(LoopHandle.LoopEnd, +1)
			if loopStart >= last_closest: shift_loop_pt(LoopHandle.LoopStart, +1)
			if susEnd >= last_closest: shift_loop_pt(LoopHandle.SusEnd, +1)
			if susStart >= last_closest: shift_loop_pt(LoopHandle.SusStart, +1)
#			clamp_loops()

			recalc_display_bounds()
			$Display.update()


		REMOVE_PT:
			if data.size() == 1:  return
			if $Display/PointCrosshair.should_display:  return
			if $Display.last_closest_pt<0 or $Display.last_closest_pt>=data.size():  return
			data.remove($Display.last_closest_pt)
			
			#Make sure the new point 0 is always at 0ms.
			data[0].x = 0

#			$Display.last_closest_pt = -1
			$Display.last_closest_pt = min(data.size()-1, $Display.last_closest_pt)
			var last_closest = $Display.last_closest_pt
			#Readjust the loop bounds to account for the change.
			if loopStart > last_closest: shift_loop_pt(LoopHandle.LoopStart, -1)
			if loopEnd > last_closest: shift_loop_pt(LoopHandle.LoopEnd, -1)
			if susStart > last_closest: shift_loop_pt(LoopHandle.SusStart, -1)
			if susEnd > last_closest: shift_loop_pt(LoopHandle.SusEnd, -1)
#			clamp_loops()

			recalc_display_bounds()
			$Display.update()
			
		COPY:
			pass
		PASTE:
			pass
		SET_LOOP:
			has_loop=toggled
			var closest = $Display.last_closest_pt if $Display.last_closest_pt!=-1 else data.size()-1
			if loopStart < 0 or loopStart >= data.size():  loopStart = closest
			if loopEnd < 0 or loopEnd >= data.size():  loopEnd = closest
			$Display.update()

		SET_SUSTAIN:
			has_sustain=toggled
			var closest = $Display.last_closest_pt if $Display.last_closest_pt!=-1 else 0
			if susStart == -1 or susStart >= data.size():  susStart = closest
			if susEnd == -1 or susEnd >= data.size():  susEnd = closest
			$Display.update()

func shift_loop_pt(handle, amt):
	#Shift a loop point forward or backward. Used when adding or removing points to preserve user intent.
	match handle:
		LoopHandle.LoopStart:
			loopStart += amt
			loopEnd = max(loopStart, loopEnd)
		LoopHandle.LoopEnd:
			loopEnd += amt
			loopStart = min(loopStart, loopEnd)
		LoopHandle.SusStart:
			susStart += amt
			susEnd = max(susStart, susEnd)
		LoopHandle.SusEnd:
			susEnd += amt
			susStart = min(susStart, susEnd)

func set_loop_pt(handle, value):  #Sets a loop point based on a handle value from the display.
	match handle:
		LoopHandle.LoopStart:
			loopStart = value
			loopEnd = max(loopStart, loopEnd)
		LoopHandle.LoopEnd:
			loopEnd = value
			loopStart = min(loopStart, loopEnd)
		LoopHandle.SusStart:
			susStart = value
			susEnd = max(susStart, susEnd)
		LoopHandle.SusEnd:
			susEnd = value
			susStart = min(susStart, susEnd)

func clamp_loops():
	loopStart = clamp(loopStart, 0, data.size()-1)
	loopEnd = clamp(loopEnd, 0, data.size()-1)
	susStart = clamp(susStart, 0, data.size()-1)
	susEnd = clamp(susEnd, 0, data.size()-1)
