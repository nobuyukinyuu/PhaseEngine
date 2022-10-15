#Envelope editor main display surface
extends ColorRect
const QUAD_ROOT_OF_2 = 1.189207115
const hand_icon = preload("res://gfx/ui/pan_hand.png")
const grab_handles = preload("res://gfx/ui/envelope_editor/grab_handles.png")
const note_off = preload("res://gfx/ui/note_off.png")

var panning = false
const PICK_DISTANCE = 160.0  #Squared value so that we don't have to do a sqrt operation
var dragging = false
var drag_pt = -1
var last_closest_pt = -1
var drag_value = Vector2.ZERO  #Last value at dragged mouse position
var upper_bound = 0  #Drag bounds
var lower_bound = 0

var drag_font = preload("res://gfx/fonts/numerics_7seg.tres")

const STEP_X = 25  #Quantize
const STEP_Y = 0.125  #Fidelity
const PT_BOX_SIZE=Vector2.ONE * 7  #Size of a box representing an envelope point
const LOOP_INDICATOR_OFFSET=PT_BOX_SIZE.x/2  #How far from a box we should draw loop indicators
const LOOP_COLOR1 = Color(1,1,0.6, 0.5)
const LOOP_COLOR2 = Color(1,1,0.6, 0.7)
const SUSTAIN_COLOR1 = Color(0.8,1,1, 0.5)
const SUS_H = 48  #Sustain handle bar height
const SUSTAIN_COLOR2 = Color(0,0.5,1, 0.4)
const value_rulers = ["ValueRuler", "ValueRulerLog"]

var loop_handle_pos:Vector2 = Vector2.ONE * -1  #X and Y specifiy the start and end of the loop, in screen px.
var sus_handle_pos1:Vector2 = Vector2.ONE * -1  #First sustain loop handle pos, in screen px.
var sus_handle_pos2:Vector2 = Vector2.ONE * -1  #Last sustain loop handle pos, in screen px.
enum CanDrag {NO, FIRST, LAST}
var can_drag_loop = CanDrag.NO
var can_drag_sustain = CanDrag.NO
var loop_dragging = CanDrag.NO  #Active loop dragging handle.  Sustain values are << 2; Rsh to extract

#signal point_moved   # [idx:int, pos:Vector2]
#signal point_added   # [idx:int, pos:Vector2]
#signal point_removed # [idx:int]
#signal loop_enabled  # [which
#signal loop_moved


func _ready():
	pass # Replace with function body.


#TODO:  Consider events on MouseUp to update max offset and zoom level to reach 10000ms or the last offset, 
#		whichever is greater

func _gui_input(event):
	if event is InputEventMouseButton:
		
		#First, check if the window is in the background.  If so, move the window up.
		var parent = owner.get_parent()
		if event.pressed and not owner.is_in_front():
			print("window out of focus.  Refocusing!")
			owner._on_CustomEnvelope_gui_input(event)
#			return  #Uncomment this line if you'd rather input not propagate input to unfocused windows
		
		match event.button_index:
			BUTTON_WHEEL_DOWN:
				owner.get_node("ZoomBar/Slider").value *= QUAD_ROOT_OF_2
			BUTTON_WHEEL_UP:
				owner.get_node("ZoomBar/Slider").value /= QUAD_ROOT_OF_2
			BUTTON_MIDDLE:
				panning = event.pressed
				Input.set_custom_mouse_cursor(hand_icon if event.pressed else null)

			BUTTON_LEFT:
				dragging = event.pressed
				if event.pressed:  
					var pos = value_at(get_local_mouse_position())
					owner.last_clicked_position = pos
					var dragging_handle = (can_drag_sustain << 2) + can_drag_loop

					#Detect proximity to a point.
					var closest_idx=int(owner.search_closest(owner.data, pos.x))
					
					if dragging_handle == 0:
						last_closest_pt = closest_idx #Used to determine origin when setting point crosshair
#					last_closest_pt = -1
					var closest_dist=0xFFFFFF
					for i in range(max(closest_idx-1, 0), min(closest_idx+2, owner.data.size())):
						var pixel_pos = pt_to_display_coords(owner.data[i])
#						var dist = pos.distance_squared_to(owner.data[i])
						var dist = get_local_mouse_position().distance_squared_to(pixel_pos)
						print("Distance to %s: " % i, dist)
						if dist <= PICK_DISTANCE:  #Point is close to cursor. Check if it's the closest one.
							if dist < closest_dist:  #This point is the closest among the ones in the range.
								closest_dist = dist
								drag_pt = i
								last_closest_pt = i
								#Reset the cursor in case we're on loop handles.
								set_grab_handles(false)

					#Determine the valid X-range of the drag point. If the pt is 0, only a range of 0 is valid.
					if drag_pt > 0:
						lower_bound = owner.data[max(0, drag_pt-1)].x
						var furthest_pt = owner.data.size()-1
						if drag_pt != furthest_pt:
							upper_bound = owner.data[min(drag_pt+1, furthest_pt)].x
						else:
							upper_bound = 0xFFFF_FFFF
						print("Lower: %s, Upper: %s" % [lower_bound, upper_bound])
					else:
						lower_bound = 0
						upper_bound = 0

					#If not within range, detect proximity to a loop point (Y-axis).
					if dragging_handle == 0:  #We're not dragging a loop handle.  Set crosshair point.
						#Set a "potential point" for New Point.
						$PointCrosshair.should_display = drag_pt == -1
					else:  #We clicked on one of the loop handles.  Drag it.
						if drag_pt == -1: 
							loop_dragging = dragging_handle
						else:
							loop_dragging = CanDrag.NO
							
						# If the crosshair was displaying, the last_closest point only refers to the crosshair's
						# origin.  It's not valid anymore for any other context (ins/del pts), so clear it.
						if $PointCrosshair.should_display:
							last_closest_pt = -1
							$PointCrosshair.should_display = false
							
					$PointCrosshair.visible = visible and $PointCrosshair.should_display
					drag_value = owner.data[drag_pt]  #Initialize the displayed drag value.

					
				else:  #MouseUp
					if drag_pt>0:  #Don't update 1st point; handled in owner.set_initial_value() and in SetEG().
						owner.set_bind_value(drag_pt, drag_value)
					
					drag_pt = -1  #Reset drag point.
					loop_dragging = CanDrag.NO
					
					
					#Resize view window bounds.
					var newmax = int(max(10000, owner.data[owner.data.size()-1].x))
					owner.get_node("ZoomBar/Slider").max_value = newmax
					owner.get_node("Offset").max_value = newmax
				
				
				update()
	
	elif event is InputEventMouseMotion:
		#Update the value ruler ticks.
		owner.get_node("ValueRuler").update()
		if panning:
			#Pan the offset relative to the number of pixels moved.
			var one_px = $TimeRuler.ms_per_px()
			owner.get_node("Offset").value -= one_px * event.relative.x
		elif dragging and drag_pt>=0:  #Dragging a point around
			var pos = value_at(get_local_mouse_position())
			if Input.is_key_pressed(KEY_CONTROL):  pos.x = stepify(pos.x, STEP_X)
			if Input.is_key_pressed(KEY_SHIFT):  pos.y = stepify(pos.y, STEP_Y)
			pos.y = clamp(pos.y, 0.0, 1.0)
			pos.x = clamp(pos.x, lower_bound, upper_bound)
			drag_value = pos
			owner.data[drag_pt] = pos

			if drag_pt == 0:  owner.set_initial_value(owner.scale_up(pos.y))
			update()
		elif dragging and loop_dragging > 0:  #Dragging a loop handle.
			var pos = value_at(get_local_mouse_position())
			var closest_idx=int(owner.search_closest(owner.data, pos.x))
#			prints("Loop drag", closest_idx)
			owner.set_loop_pt(loop_dragging, closest_idx)
			update()

		else:
			#Check if we're near a sustain handle.
			if owner.has_sustain and not dragging:
				var mouse = get_local_mouse_position()
				var dist = PT_BOX_SIZE.x * 0.75				
				#First check the X position.  If we're in distance of either of these, check Y position.
				if abs(mouse.x-sus_handle_pos1.x) <= dist:
					if abs(mouse.y-sus_handle_pos1.y) <= SUS_H:
						set_grab_handles(true)
						can_drag_sustain = CanDrag.FIRST
				elif abs(mouse.x-sus_handle_pos2.x) <= dist:
					if abs(mouse.y-sus_handle_pos2.y) <= SUS_H:
						set_grab_handles(true)
						can_drag_sustain = CanDrag.LAST
				else:
					set_grab_handles(false)
					can_drag_sustain = CanDrag.NO
		
			#Check if we're near a loop handle.
			if !can_drag_sustain and owner.has_loop and not dragging:
				var mouseX = get_local_mouse_position().x
				var dist = PT_BOX_SIZE.x * 0.75
				if abs(mouseX-loop_handle_pos.x) <= dist:
					set_grab_handles(true)
					can_drag_loop = CanDrag.FIRST
				elif abs(mouseX-loop_handle_pos.y) <= dist:
					set_grab_handles(true)
					can_drag_loop = CanDrag.LAST
				else: 
					set_grab_handles(false)
					can_drag_loop = CanDrag.NO

	accept_event()


func set_grab_handles(enabled=true):
	if enabled and drag_pt==-1:
		Input.set_custom_mouse_cursor(grab_handles, 0, Vector2(12,12))
	else:
		Input.set_custom_mouse_cursor(null)

	
func _draw():
	#Draw reference lines.
	for i in 8:
		var v = rect_size.y * (i/8.0)
		draw_line(Vector2(0, v), Vector2(rect_size.x,v), color*2)
		if i==4:  draw_line(Vector2(0, v), Vector2(rect_size.x,v), color*3)

	#Grab the drawable area chunk from owner.
	var d = owner.get_display_bounds()
	if d.empty():  return
	#Set bounds for what lines we draw.
	var first_pt = int(max(0, d["first"]))
	var last_pt = int(min(owner.data.size(), d["last"]))

	#Draw the crosshair.
	if $PointCrosshair.should_display:
		$PointCrosshair.position = pt_to_display_coords(owner.last_clicked_position)
		var posx = $PointCrosshair.position.x
		$PointCrosshair.visible = posx > 0 and posx < rect_size.x

		#Draw dotted lines to indicate potential addition of a new point.
		var c = $PointCrosshair.modulate / 2
#		var c2 = Color(c.r,c.g,c.b, 0.5)
		if last_closest_pt >= first_pt:
			draw.dotted_line(self, $PointCrosshair.position, pt_to_display_coords(owner.data[last_closest_pt]),
					c , 1, true, 2,2)
		if last_closest_pt < last_pt:
			draw.dotted_line(self, $PointCrosshair.position, pt_to_display_coords(owner.data[last_closest_pt+1]),
					c , 1, true, 2,2)

	#Draw the loop and sustain lines.
	if owner.has_loop:
		var firstX = pt_to_display_coords(owner.data[owner.loopStart]).x - LOOP_INDICATOR_OFFSET
		var lastX = pt_to_display_coords(owner.data[owner.loopEnd]).x + LOOP_INDICATOR_OFFSET
		
		loop_handle_pos.x = firstX
		loop_handle_pos.y = lastX
		if firstX >= -LOOP_INDICATOR_OFFSET and firstX <= rect_size.x:
			var x = max(0.5, firstX) #Account for point 0 offset annoyances
			draw.dotted_line(self, Vector2(x, 0), Vector2(x, rect_size.y), 
					LOOP_COLOR1, 1, true, 2, 2)
		if lastX >= 0 and lastX <= rect_size.x:
			draw.dotted_line(self, Vector2(lastX, 0), Vector2(lastX, rect_size.y), 
					LOOP_COLOR1, 1, true, 2, 2)

		#Draw the loop arrow indicator?
		firstX = clamp(firstX, 0, rect_size.x)
		lastX = clamp(lastX, 0, rect_size.x)
		draw.arrow(self, Vector2(firstX, rect_size.y), Vector2(lastX, rect_size.y), LOOP_COLOR2)
	if owner.has_sustain:
		var first = pt_to_display_coords(owner.data[owner.susStart])
		first.x -= LOOP_INDICATOR_OFFSET
		var last = pt_to_display_coords(owner.data[owner.susEnd])
		last.x += LOOP_INDICATOR_OFFSET
		
		sus_handle_pos1 = first
		sus_handle_pos2 = last
		if first.x >= -LOOP_INDICATOR_OFFSET and first.x <= rect_size.x:
			var x = max(0.5, first.x) #Account for point 0 offset annoyances
			draw.dotted_line(self, Vector2(x, max(0, first.y-SUS_H)), 
									Vector2(x, min(first.y+SUS_H, rect_size.y)), 
									SUSTAIN_COLOR1, 1, true, 1, 2, SUSTAIN_COLOR2)
		if last.x >= 0 and last.x <= rect_size.x:
			var top = max(0.5, last.y-SUS_H)
			draw.dotted_line(self, Vector2(last.x, top), 
									Vector2(last.x, min(last.y+SUS_H, rect_size.y)), 
									SUSTAIN_COLOR1, 1, true, 1, 2, SUSTAIN_COLOR2)
			if last.x - note_off.get_width() <= rect_size.x:  
				draw_texture(note_off, Vector2(last.x+2, 0), SUSTAIN_COLOR1)

		#Draw the sustain arrow indicator?
		first.x = clamp(first.x, 4, rect_size.x)
		last.x = clamp(last.x, 4, rect_size.x)
		draw.arrow(self, Vector2(last.x, 0), Vector2(first.x, 0), SUSTAIN_COLOR2)


	#Attempt to draw the lines in the display bounds.
	#Check if both points are out of bounds.  CALCULATE A LINE SEGMENT IF SO
	if first_pt==0 and last_pt==0:  first_pt = min(1, owner.data.size()-1)
	if first_pt < 0:  return  #Data envelope is empty!! Abort!!
	if last_pt < first_pt:  #BOTH POINTS OUT OF BOUNDS
		if first_pt >= owner.data.size():  return
		var pt:Vector2 = pt_to_display_coords(owner.data[first_pt])
		var pt2:Vector2 = pt_to_display_coords(owner.data[last_pt])

		var angle = (pt2-pt).angle()
		pt2.y -= tan(angle) * pt2.x
		pt.y -= tan(angle) * (pt.x-rect_size.x)
		pt2.x = 0
		pt.x = rect_size.x
		
		draw_line(pt, pt2, ColorN("yellow", 0.5))
		return 
	else:  #At least one point is in bounds.  Draw the points in bounds.
		#Draw the lines inside the window borders.
		for i in range(first_pt, last_pt):
			#Draw the point.
			var pt:Vector2 = pt_to_display_coords(owner.data[i])
			var pt2:Vector2 = pt_to_display_coords(owner.data[i+1])
			draw_pt(pt2, drag_pt==i+1 or (!$PointCrosshair.should_display and last_closest_pt==i+1))
			
			#Draw the line that connects to the next point.
			draw_line(pt, pt2, ColorN("white", 0.5))
				
	#	#Draw lines extending to the borders of the window bounds.
		if d.has("clip_left") and first_pt <= owner.data.size()-1:
			var origin = pt_to_display_coords(owner.data[first_pt])
			var dest = pt_to_display_coords(owner.data[first_pt-1])
			draw_pt(origin, drag_pt==first_pt or 
				(!$PointCrosshair.should_display and last_closest_pt==first_pt) )

			var angle = (origin-dest).angle()
			var x = -origin.x
			var y = tan(angle) * x

			if abs(x) < rect_size.x:
	#			draw_line(origin, origin+Vector2(x,y), ColorN("yellow", 0.5))
				draw_line(origin, origin+Vector2(x,y), ColorN("white", 0.5))

		if d.has("clip_right") and last_pt < owner.data.size()-1:
			var origin = pt_to_display_coords(owner.data[last_pt])
			var dest = pt_to_display_coords(owner.data[last_pt+1])
			
			var angle = (dest-origin).angle()
			var x = float(rect_size.x - origin.x)
			var y = tan(angle) * x

			if abs(x) < rect_size.x:
	#			draw_line(origin, origin+Vector2(x,y), ColorN("magenta", 0.5))
				draw_line(origin, origin+Vector2(x,y), ColorN("white", 0.5))
		else:
			draw_pt(pt_to_display_coords(owner.data[last_pt]), 
					drag_pt==last_pt or (!$PointCrosshair.should_display and last_closest_pt==last_pt))

	#Draw the Overlay if dragging
	if dragging and drag_pt >=0:
		var secs = $TimeRuler.format_secs(drag_value.x, "ms", " s")
		var true_val = int(lerp(owner.lo, owner.hi, drag_value.y))
		var c = ColorN("black")
		draw_string(drag_font, get_local_mouse_position() - Vector2(-1, 39), "x:%s" % [secs],c)
		draw_string(drag_font, get_local_mouse_position() - Vector2(-1, 23), "y:%s" % [true_val],c)

		draw_string(drag_font, get_local_mouse_position() - Vector2(0, 40), "x:%s" % [secs])
		draw_string(drag_font, get_local_mouse_position() - Vector2(0, 24), "y:%s" % [true_val])
	


#Convert to/from local coordinates and true envelope values
func pt_to_display_coords(pt):  return Vector2($TimeRuler.ms2offset(pt.x), rect_size.y - pt.y * rect_size.y)
func value_at(mousePt):
	return Vector2($TimeRuler.ms_per_px() * mousePt.x + $TimeRuler.offset, 1.0 - mousePt.y / rect_size.y)


func draw_pt(origin:Vector2, hilite=false):
		var color = ColorN("white") if !hilite else ColorN("yellow")
		draw_box(origin, PT_BOX_SIZE, color, 1, true)

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
	owner.get_node("ZoomBar/Slider").update()
	update()


func _on_Offset_value_changed(value):
	$TimeRuler.offset = value
	$TimeRuler.update()
	owner.recalc_display_bounds()
	update()


func _on_Display_mouse_exited():
	if not dragging:
		set_grab_handles(false)
