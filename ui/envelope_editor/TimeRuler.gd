extends Control

var zoom = 1200  #Number of millisecs to display on ruler at once
var offset = 0  #Offset from 0ms to start with
var e = exp(1)

var col = Color("#185160")
var col2 = Color("#40d6ff")
var font = preload("res://gfx/fonts/numerics_5x8.tres")
var font2 = preload("res://gfx/fonts/thin_font_6x8.tres")

func _ready():
	pass # Replace with function body.


func ms_per_px() -> float:  return zoom / float(rect_size.x) #number of millisecs in one pixel
func ms2offset(ms):  return (ms - offset) / ms_per_px()  #Hypothetical px offset for a given ms

const BASE_WIDTH=24.0 #px
func _draw():
	draw_line(Vector2.ZERO, Vector2(rect_size.x, 0), col)
	
	#Determine the nearest order of magnitude for the zoom level 
	var mag = order(zoom)
#	var stretch = (mag -log10(zoom))

	var unit = pow(10, mag - 1) #Unit multiples at which labeled ticks should appear
	var h = rect_size.y / 2

	#Calculate based on the zoom what the width of a unit is
	var one_px:float = ms_per_px()
	var uw:float = (unit) / one_px  #Unit Width of labeled tick distances, in px
	
	#Subdivide the units until at least 4 of them can fit on the widget
	while uw > rect_size.x/4:
		uw /=2.0
		unit /=2.0

	var mOffset = offset / one_px
	var pxOffset = fmod(mOffset, uw)


#	draw_string(font2, Vector2.ZERO,"unit: %sms, uw: %s, one_px: %s" % [unit, uw, one_px])

	#Determine the number of subticks we can fit in between marked ticks before reaching base width (power of 2)
	var subticks:float = 0 if uw < BASE_WIDTH else pow(2,round(log(uw/24)/log(2)))

	#First, determine if we can fit a label on the left side by measuring it.
	var lbl = format_secs( offset )
	if len(lbl)*5 + 2.5 < uw-pxOffset:  #continue
		#Draw the label and associated tick.
		draw_string(font, Vector2(0, h+2), lbl, Color("#ffff80"))
		draw_line(Vector2.ZERO, Vector2(0, h) , col)


	for i in range(0, rect_size.x+uw+1, uw):
		var xpos = i - pxOffset
		var true_value = i #+ floor(offset/unit)

		#If our offset tick is out of bounds, skip the first tick draw and label the far left by the offset value.

#		else:
		#Draw labels.
		var ms = format_secs( stepify(true_value * one_px, unit/2.0) + floor(offset/unit)*unit)
		if xpos + len(ms) * 5 <= rect_size.x and i>0:
			draw_string(font, Vector2(xpos, h+2), ms)

		#Draw ticks
		for j in subticks:
			var xxpos = xpos+j/subticks * uw
			if xxpos<0:  continue
			if xxpos>rect_size.x+1:  break
			var ty = tick_height(j)
			var h2 = h if ty==1 else h / (4.25-tick_height(j) )  #Actual tick height
			draw_line(Vector2(xxpos, 0), Vector2(xxpos, h2) , col)
			draw_line(Vector2(xxpos, 0), Vector2(xxpos, 1), ColorN("black"))

#	draw_string(font2, Vector2(0, 24), str(subticks))

#Formats input in millisecs to a nice readable label
func format_secs(input, ms_lbl="ms", s_lbl="s", m_lbl="m"):
	if input==0: return "0" + ms_lbl
	if input > 60000:  #Position is over a minute long.  Use 0m00s format.
		var minutes = int(input/60000)
		var seconds = (int(input) % 60000) / 1000
		
		return str(minutes) + m_lbl + str(seconds).pad_zeros(2) + s_lbl
	else:  #60 seconds or under.
		var in_seconds = input >= 1000
		var output = str(input if not in_seconds else input/1000.0)
		if in_seconds or input < 10:
			output = output.pad_decimals(2)
		else:
			output = output.pad_decimals(0)
		if output.ends_with(".00"):  output = output.substr(0, len(output)-3) #Near integer. No pad needed.
		
		output += s_lbl if in_seconds else ms_lbl
	
		return output

func order(n):  return ceil(log10(n))
func log10(n):  return log(n) / log(10)

func tick_height(n):
	n = int(n)
	for i in 32:
		if n&1==1:  return i+2
		n >>= 1
	return 1
