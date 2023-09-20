extends Control

func _draw():
	var mPos = get_local_mouse_position()
	if mPos.y >=0 and mPos.y <= rect_size.y:
		draw.dotted_line(self,Vector2(0, mPos.y), Vector2(rect_size.x, mPos.y), ColorN("yellow"), 1.0, false, 
				2.0, 2.0, Color(1,0,0,0.75))

	#Drawing over the time ruler here reduces the number of string redraws needed for the time ruler itself
	var time_ruler = owner.get_node("Display/TimeRuler")
	var origin = owner.get_node("Display").rect_position + time_ruler.rect_position - rect_position
	var x = origin.x + mPos.x-rect_position.x
	if x-origin.x >=0 and x-origin.x <= time_ruler.rect_size.x:
		draw.dotted_line(self,Vector2(x, origin.y), Vector2(x, origin.y + time_ruler.rect_size.y/2), ColorN("yellow"))

