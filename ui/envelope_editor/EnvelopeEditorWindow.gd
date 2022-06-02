extends WindowDialog
class_name EnvelopeEditorWindow, "res://gfx/ui/bind_indicator.png"

var lo=0
var hi=63

var data=[]  #Intermediate point data.  Vec2 where x=ms, y=0-1.
var cached_display_bounds = {}

var last_clicked_position = Vector2.ZERO

var susStart:int = -1
var susEnd:int = -1
var loopStart:int= -1
var loopEnd:int = -1
var has_loop=false
var has_sustain=false

var selected = -1  #Selected point (or area between points, if Vector2) -- for add/remove pts

enum{NEW_PT, REMOVE_PT, COPY, PASTE, SET_LOOP, SET_SUSTAIN}

func compare(a:Vector2,b:Vector2):  #Compares 2 vec2's in the data block for bsearch_custom
	return a.x < b.x
func sort():  data.sort_custom(self, "compare")

func _ready():

	#DEBUG, REMOVE
	visible = true
	rect_position += Vector2.ONE * 32

	for o in $lblValue.get_children():
		o.connect("value_changed", self, "up", [o])

	randomize()
	for i in 40:
		data.append(Vector2(500*i, randf()))
#		data.append(Vector2(500*i, i/10.0))
	sort()
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
	
#	if data.empty():  return output
	
	#Find the first point that would be visible in our drawing window.
	#Assume data is sorted.  update() should NOT be called if data is not sorted.


	output["first"] = search_closest(data, $Display/TimeRuler.offset, true )
	output["last"] = search_closest(data, ($Display/TimeRuler.offset + $Display/TimeRuler.zoom) )

	#Add crop indicators.  Draw routine for display will check if these exist and work accordingly.
#	if output["first"] > 0:  output["crop_left"] = true
#	if output["last"] >= data.size():  
#		output["crop_right"] = true
#		output["last"] = min(data.size()-1, output["last"])

	if typeof(output["first"]) == TYPE_REAL:  output["clip_left"] = true
	if typeof(output["last"]) == TYPE_REAL:  output["clip_right"] = true

	cached_display_bounds = output
	return output

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


func setup(input):
	#TODO:  Receive data packet from core, set up minmax, translate core data to intermediate data.
	pass


#Sets the bound labels of the Y-Axis.
func set_minmax(lowest, highest):
	var s = ""#format_val(highest)
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
			pass
		REMOVE_PT:
			pass
		COPY:
			pass
		PASTE:
			pass
		SET_LOOP:
			has_loop=toggled
			if loopStart < 0 or loopStart >= data.size():  loopStart = data.size()-1
			if loopEnd < 0 or loopEnd >= data.size():  loopEnd = data.size()-1
			$Display.update()

		SET_SUSTAIN:
			has_sustain=toggled
			if susStart == -1 or susStart >= data.size():  susStart = 0
			if susEnd == -1 or susEnd >= data.size():  susEnd = 0
			$Display.update()
