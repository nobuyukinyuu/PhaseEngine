#Envelope editor main display surface
extends ColorRect
const QUAD_ROOT_OF_2 = 1.189207115
const hand_icon = preload("res://gfx/ui/pan_hand.png")

var panning = false

func _ready():
	pass # Replace with function body.


func _gui_input(event):
	if event is InputEventMouseButton:
		match event.button_index:
			BUTTON_WHEEL_DOWN:
				owner.get_node("Zoom").value *= QUAD_ROOT_OF_2
			BUTTON_WHEEL_UP:
				owner.get_node("Zoom").value /= QUAD_ROOT_OF_2
			BUTTON_MIDDLE:
				panning = event.pressed
				Input.set_custom_mouse_cursor(hand_icon if event.pressed else null)
	
	if event is InputEventMouseMotion:
		#Update the value ruler ticks.
		owner.get_node("ValueRuler").update()
		if panning:
			#Pan the offset relative to the number of pixels moved.
			var one_px = $TimeRuler.ms_per_px()
			owner.get_node("Offset").value -= one_px * event.relative.x

	accept_event()

func _draw():

	#Draw reference lines.
	for i in 8:
		var v = rect_size.y * (i/8.0)
		draw_line(Vector2(0, v), Vector2(rect_size.x,v), color*2)
		if i==4:  draw_line(Vector2(0, v), Vector2(rect_size.x,v), color*3)

	#TODO:  While mouse holding, draw lines on the X/Y rulers....
	
	#Grab the drawable area chunk from owner.
	var d = owner.get_display_bounds()
	if d.empty():  return
	
	var first_pt = int(max(0, d["first"]))
	var last_pt = int(min(owner.data.size(), d["last"]))


	#Uh oh, both points are out of bounds.  CALCULATE A LINE SEGMENT
	if first_pt==0 and last_pt==0:  first_pt = min(1, owner.data.size()-1)
	if last_pt < first_pt: 
		if first_pt >= owner.data.size():  return
		var pt:Vector2 = pt_to_display_coords(owner.data[first_pt])
		var pt2:Vector2 = pt_to_display_coords(owner.data[last_pt])

		var angle = (pt2-pt).angle()
		pt2.y -= tan(angle) * pt2.x
		pt.y -= tan(angle) * (pt.x-rect_size.x)
		pt2.x = 0
		pt.x = rect_size.x
		
		draw_line(pt, pt2, ColorN("yellow", 0.5))
#		draw_line(a, b, ColorN("white", 0.5))
		return 
	else:
		#Draw the lines inside the window borders.
		for i in range(first_pt, last_pt):
			#Draw the point.
			var pt:Vector2 = pt_to_display_coords(owner.data[i])
			var pt2:Vector2 = pt_to_display_coords(owner.data[i+1])
			draw_pt(pt2)
			
			#Draw the line that connects to the next point.
			draw_line(pt, pt2, ColorN("white", 0.5))
				
	#	#Draw lines extending to the borders of the window bounds.
		if d.has("clip_left") and first_pt <= owner.data.size()-1:
			var origin = pt_to_display_coords(owner.data[first_pt])
			var dest = pt_to_display_coords(owner.data[first_pt-1])
			draw_pt(origin)

			#tan(angle) = y/x
			var angle = (origin-dest).angle()
			var x = -origin.x
			var y = tan(angle) * x

			if abs(x) < rect_size.x:
	#			draw_line(origin, origin+Vector2(x,y), ColorN("yellow", 0.5))
				draw_line(origin, origin+Vector2(x,y), ColorN("white", 0.5))

		if d.has("clip_right") and last_pt < owner.data.size()-1:
			var origin = pt_to_display_coords(owner.data[last_pt])
			var dest = pt_to_display_coords(owner.data[last_pt+1])
			
			#tan(angle) = y/x
			var angle = (dest-origin).angle()
			var x = float(rect_size.x - origin.x)
			var y = tan(angle) * x

			if abs(x) < rect_size.x:
	#			draw_line(origin, origin+Vector2(x,y), ColorN("magenta", 0.5))
				draw_line(origin, origin+Vector2(x,y), ColorN("white", 0.5))
		else:
			draw_pt(pt_to_display_coords(owner.data[last_pt]))



func pt_to_display_coords(pt):  return Vector2($TimeRuler.ms2offset(pt.x), rect_size.y - pt.y * rect_size.y)

func draw_pt(origin:Vector2, hilite=false):
		draw_box(origin, Vector2.ONE * 7, ColorN("white"), 1, true)

func draw_box(origin:Vector2, sz:Vector2, color=ColorN("white"), width=1.0, antialiased=false):
	var xoff = Vector2(sz.x/2.0, 0)
	var yoff = Vector2(0, sz.y/2.0)
	var lower_right = origin+xoff+yoff + Vector2.ONE
	draw_line(origin-xoff-yoff, origin-xoff+yoff, color, width, antialiased)
	draw_line(origin-xoff-yoff, origin+xoff-yoff, color, width, antialiased)
	draw_line(origin+xoff-yoff, origin+xoff+yoff, color, width, antialiased)
	draw_line(origin-xoff+yoff, origin+xoff+yoff, color, width, antialiased)




func _on_Zoom_value_changed(value):
	$TimeRuler.zoom = value
	$TimeRuler.update()
	owner.recalc_display_bounds()
	update()


func _on_Offset_value_changed(value):
	$TimeRuler.offset = value
	$TimeRuler.update()
	owner.recalc_display_bounds()
	update()
