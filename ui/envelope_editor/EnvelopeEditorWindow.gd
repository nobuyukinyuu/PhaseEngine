extends WindowDialog
class_name EnvelopeEditorWindow, "res://gfx/ui/envelope_editor/icon_envelope.svg"

var invoker:NodePath  #The control that spawned us.  Typically an EGSlider.  References invalidate when tab's moved
var lo=0  #Minimum value of env output
var hi=63 #Max value of env output
var log_scale = false  #Used to configure this window for operating in the log domain.
var using_floats = false #Used to configure this window for floating point operations
var step = 1.0  #Used to snapify floating point units for display purposes

var data=[Vector2.ZERO]  #Intermediate point data.  Vec2 where x=ms, y=0-1.
var cached_display_bounds = {}

var last_clicked_position = Vector2.ZERO

#Loop stuff
enum LoopHandle {None, LoopStart, LoopEnd, SusStart = 4, SusEnd = 8}
enum LoopType {None=0, Basic=1, Sustain=2, Compound=3}
var susStart:int = -1
var susEnd:int = -1
var loopStart:int= -1
var loopEnd:int = -1
var has_loop=false
var has_sustain=false

#Data source stuff
var DataSource = {"Envelope": 0, "Increments": 1}  #Used for knowing where to send points. Based on c# class names.
var data_source_type:int
var associated_value:String
var operator = -1

var selected = -1  #Selected point (or area between points, if Vector2) -- for add/remove pts

#Toolbar selector enum
enum{NEW_PT, REMOVE_PT, COPY, PASTE, SET_LOOP, SET_SUSTAIN}


func compare(a:Vector2,b:Vector2):  #Compares 2 vec2's in the data block for bsearch_custom
	return a.x < b.x
func sort():  data.sort_custom(self, "compare")

func setup(title:String, d:Dictionary, invoker:NodePath=""):
	#TODO:  Receive data packet from core, set up minmax, translate core data to intermediate data.
	if title is String:  $H/lblTitle.text = title
	else:  $H/lblTitle.text = "Envelope editor"

	#Set minmax values
	set_minmax(d.get("minValue", 0), d.get("maxValue", 63))
	$ValueRuler/Log.visible = log_scale
	$ValueRuler/Lin.visible = !log_scale
	
	#Set the source location so we know where to chooch edits
	data_source_type = DataSource[ d["dataSource"] ]
	associated_value = d["memberName"]
	operator = get_node(invoker).owner.operator

	#Set loops
	if d.has("loopStart"):
		$Btn.get_node(str(SET_LOOP)).set_pressed_no_signal(true)
		has_loop = true
		loopStart = d["loopStart"]
		loopEnd = d["loopEnd"]
	if d.has("sustainStart"):
		$Btn.get_node(str(SET_SUSTAIN)).set_pressed_no_signal(true)
		has_sustain = true
		susStart = d["sustainStart"]
		susEnd = d["sustainEnd"]
	
	#Grab the point data from the dict
	var pts = d.get("pts", [0,0])
	assert(pts.size() % 2 == 0)

	data = []
	for i in range(0, pts.size(), 2):  #Set initial value
		data.append( Vector2(pts[i], scale_down(pts[i+1])) )
	recalc_display_bounds()  #Determine how to draw the points visible in the display window.
	$Display.update()

	#Check if we should be attaching to an invoker
	var success = rebind_to(invoker)
	if success:  set_initial_value(pts[1], true)


#Set up a bind such that changing the value of the slider updates the first data point.
func rebind_to(invoker:NodePath) -> bool:
	self.invoker = invoker
	if !invoker.is_empty():
		var p:Slider = get_node(invoker)
		if p:
			#Set up the invoker to update our initial value.
			if !p.is_connected("value_changed", self, "set_initial_value"):
				p.connect("value_changed", self, "set_initial_value", [true])
			return true
	else:
		print("EnvelopeEditor: Can't find invoker path to rebind to! ", invoker)
	return false
	
func _ready():
	#Associate the buttons.
	for o in $Btn.get_children():
		if not o is Button:  continue
		if o.toggle_mode == false:
			o.connect("pressed", self, "_on_ToolButton_pressed", [false, int(o.name)] )
		else:
			o.connect("toggled", self, "_on_ToolButton_pressed", [int(o.name)] )

	#Hacky workaround to our modeless show() call not triggering the popup_hide signal
	get_close_button().connect("pressed", self, "_on_CustomEnvelope_popup_hide")
	
	global.connect("algorithm_changed", self, "_on_algorithm_changed")  #Used to check if we need to close
	
	recalc_display_bounds()  #Determine how to draw the points visible in the display window.

func _physics_process(_delta):
	if !visible:  return
	if !owner.owner.get_node("%FMPreview").should_be_visible:  #Notes are active, update the visual overlay
		$Display/NotePositions.visible = true
		$Display/NotePositions.update()
	else:
		$Display/NotePositions.visible = false


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
	lo = lowest
	hi = highest
	$lblValue.lbls = []
#	$lblValue.vals = []
	var top = 8.0 if !log_scale else $lblValue.vals.size()
	if log_scale:  $lblValue.lbls.append(format_val(highest))
	for i in range(0,top):
		var midval = stepify(global.xerp(lowest, highest, $lblValue.vals[i]/256.0) if log_scale else 
									lerp(highest+(0.4 if step>=1 else 0), lowest, i/top), step)
		
		var mids = format_val(midval)
#		$lblValue.vals.append(midval)
		$lblValue.lbls.append(mids)
		if len(mids) > 4:  $lblValue.thin_font()
	
	$lblValue.lbls.append(format_val(lowest))
#	$lblValue.vals.append(lowest)

	$lblValue.update()

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
	if not is_in_front():  
#		bring_to_front()
		return  #Ignore shortcut input from windows not in front.
	
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

			var c = get_node(owner.owner.chip_loc)
			var success = c.AddBindEnvelopePoint(data_source_type, operator, associated_value, 
									last_closest, last_clicked_position, log_scale)

			#Readjust the loop bounds to account for the change.
			if loopEnd >= last_closest: shift_loop_pt(LoopHandle.LoopEnd, +1)
			if loopStart >= last_closest: shift_loop_pt(LoopHandle.LoopStart, +1)
			if susEnd >= last_closest: shift_loop_pt(LoopHandle.SusEnd, +1)
			if susStart >= last_closest: shift_loop_pt(LoopHandle.SusStart, +1)
#			clamp_loops()

			recalc_display_bounds()
			$Display.update()
			

			if success!=0:  printerr("Adding bind point failed!  Code ", success)

		REMOVE_PT:
			if data.size() == 1:  return
			if $Display/PointCrosshair.should_display:  return
			if $Display.last_closest_pt<0 or $Display.last_closest_pt>=data.size():  return
			data.remove($Display.last_closest_pt)
			
			#Make sure the new point 0 is always at 0ms.
			data[0].x = 0

			var c = get_node(owner.owner.chip_loc)
			c.RemoveBindEnvelopePoint(data_source_type, operator, associated_value, $Display.last_closest_pt)


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
			set_bind_loop_enable()
			set_bind_loop_pts(LoopType.Basic)
			$Display.update()

		SET_SUSTAIN:
			has_sustain=toggled
			var closest = $Display.last_closest_pt if $Display.last_closest_pt!=-1 else 0
			if susStart == -1 or susStart >= data.size():  susStart = closest
			if susEnd == -1 or susEnd >= data.size():  susEnd = closest
			set_bind_loop_enable()
			set_bind_loop_pts(LoopType.Sustain)
			$Display.update()

	global.emit_signal("op_tab_value_changed")  #Update the preview

func shift_loop_pt(handle, amt):
	#Shift a loop point forward or backward. Used when adding or removing points to preserve user intent.
	match handle:
		LoopHandle.LoopStart:
			loopStart += amt
			loopEnd = max(loopStart, loopEnd)
			set_bind_loop_pts(LoopType.Basic)
		LoopHandle.LoopEnd:
			loopEnd += amt
			loopStart = min(loopStart, loopEnd)
			set_bind_loop_pts(LoopType.Basic)
		LoopHandle.SusStart:
			susStart += amt
			susEnd = max(susStart, susEnd)
			set_bind_loop_pts(LoopType.Sustain)
		LoopHandle.SusEnd:
			susEnd += amt
			susStart = min(susStart, susEnd)
			set_bind_loop_pts(LoopType.Sustain)

func set_loop_pt(handle, value):  #Sets a loop point based on a handle value from the display.
	#This is usually called every mousemove, so we call set_bind_loop_pt() in Display on MouseUp instead.
	match handle:
		LoopHandle.LoopStart:
			loopStart = value
			loopEnd = max(loopStart, loopEnd)
			set_bind_loop_pts(LoopType.Basic)
		LoopHandle.LoopEnd:
			loopEnd = value
			loopStart = min(loopStart, loopEnd)
			set_bind_loop_pts(LoopType.Basic)
		LoopHandle.SusStart:
			susStart = value
			susEnd = max(susStart, susEnd)
			set_bind_loop_pts(LoopType.Sustain)
		LoopHandle.SusEnd:
			susEnd = value
			susStart = min(susStart, susEnd)
			set_bind_loop_pts(LoopType.Sustain)

func clamp_loops():
	loopStart = clamp(loopStart, 0, data.size()-1)
	loopEnd = clamp(loopEnd, 0, data.size()-1)
	susStart = clamp(susStart, 0, data.size()-1)
	susEnd = clamp(susEnd, 0, data.size()-1)


#Called from display or egslider to move both values at once
func set_initial_value(val, from_invoker=false):
	# If this func was called from the invoker slider, then we simply update our value.
	# If not, then our display wants to update the invoker instead.

	if not from_invoker:  #We were called from the display
		if !invoker.is_empty():
			var p:Slider = get_node(invoker)
			if p:
				p.value = val
				
			set_bind_value(0, data[0])  #This is ignored for point 0 in Display.gd so do it here.
		#TODO:  CHECK IF THIS EMITS A SIGNAL FROM THE SLIDER
	else:  #The invoker slider called us
		data[0].y = scale_down(val)
		$Display.update()

func set_bind_value(index, pt):  #Update the data structure on the c# end
	var c = get_node(owner.owner.chip_loc)
	c.SetBindValue(data_source_type, operator, associated_value, index, pt, log_scale)
func set_bind_loop_enable():
	var mask = 0
	if has_loop:  mask |= 1
	if has_sustain:  mask |= 2
	var c = get_node(owner.owner.chip_loc)
	c.SetBindLoop(data_source_type, operator, associated_value, mask)
func set_bind_loop_pt(pt_marker, index=-1):
	if index < 0:
		match pt_marker:
			LoopHandle.LoopStart: index = loopStart
			LoopHandle.LoopEnd:   index = loopEnd
			LoopHandle.SusStart:  index = susStart
			LoopHandle.SusEnd:    index = susEnd

	var c = get_node(owner.owner.chip_loc)
	var err = c.SetBindLoopPoint(data_source_type, operator, associated_value, pt_marker, index)
	if err:  print("Warning:  SetBindLoopPoint returned code ", err)
func set_bind_loop_pts(loop_type, indices:Vector2=Vector2.ONE*-1):
	if loop_type == LoopType.None:  
		print("EnvelopeEditorWindow:  Attempting to set loop points when loops aren't enabled")
		return
	elif loop_type == LoopType.Compound:
		print("EnvelopeEditorWindow:  Attempting to set loop points for both loops at once. This is not supported")
		return
	if indices.x < 0:
		match loop_type:
			LoopType.Basic:
				indices = Vector2(loopStart, loopEnd)
			LoopType.Sustain:
				indices = Vector2(susStart, susEnd)
	var c = get_node(owner.owner.chip_loc)
	var err = c.SetBindLoopPoints(data_source_type, operator, associated_value, loop_type, indices)
	if err:  print("Warning:  SetBindLoopPoints returned code ", err)
	


#Scales raw data down to 0-1 display values.
func scale_down(val):
	if log_scale:
		return 1.0-global.inv_xerp(lo, hi, lo, hi, val)
	else:
		return range_lerp(val, lo, hi, 0, 1)
func scale_up(val):
	if log_scale:
#		return round(global.xerp(lo, hi, 1.0-val))
#		return stepify(global.xerp(lo, hi, 1.0-val), step)
		return global.xerp(lo, hi, 1.0-val)
	else:
#		return stepify(range_lerp(val, 0.0, 1.0, lo, hi), step)
		return range_lerp(val, 0.0, 1.0, lo, hi)

####################### Window Management #######################
func _on_CustomEnvelope_popup_hide():
	print("Closing ", name)
	queue_free()

func is_in_front():  #Returns true if we're at the front of the modeless window manager.
	var parent = get_parent()
	return get_position_in_parent() == parent.get_child_count()-1
func bring_to_front():
	var parent = get_parent()
	parent.move_child(self, parent.get_child_count()-1)

func _on_CustomEnvelope_gui_input(event):
	if event is InputEventMouseButton and event.pressed:
		#Move to top of node list so this window displays over the others.
		bring_to_front()
		#Don't accept_event() here, it'll lock input

func _on_algorithm_changed(binds_invalidated=false):
	var c = get_node(owner.owner.chip_loc)
	if c==null:  _on_CustomEnvelope_popup_hide()
	prints( c.GetOpCount(), operator)
	if operator >= c.GetOpCount() or binds_invalidated:  _on_CustomEnvelope_popup_hide()  #Invalidate window
