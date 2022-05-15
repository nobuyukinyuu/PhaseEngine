extends TextureRect

func _draw():
	var mPos = get_local_mouse_position()
	if mPos.y >=0 and mPos.y <= rect_size.y:
		dotted_line(Vector2(0, mPos.y), Vector2(rect_size.x, mPos.y), ColorN("yellow"), 1.0, false, 
				2.0, 2.0, Color(1,0,0,0.75))

	var time_ruler = owner.get_node("Display/TimeRuler")
	var origin = owner.get_node("Display").rect_position + time_ruler.rect_position - rect_position
	var x = origin.x + mPos.x-rect_position.x
	if x-origin.x >=0 and x-origin.x <= time_ruler.rect_size.x:
		dotted_line(Vector2(x, origin.y), Vector2(x, origin.y + time_ruler.rect_size.y/2), ColorN("yellow"))


func dotted_line(origin:Vector2, dest:Vector2, color:Color=Color("#FFFFFF"), 
				 width:float=1.0, antialiased:bool=false, dotlen=1.0, gaplen=2.0, color2=Color(0,0,0,0)):
	var angle = (dest-origin).normalized()
	var dist = origin.distance_to(dest)
	var step = dotlen+gaplen
	
	for i in range(0, dist, step):
		var startPos = origin + angle*i
		var length = dotlen 
		var endPos
		
		if i+dotlen > dist:
			length = dist-i
			endPos = startPos + angle*length
		else:
			endPos = startPos + angle*length
			var length2 = gaplen if i+dotlen+gaplen < dist else dist-i-dotlen
			var endPos2 = endPos + angle*length2
			#Color 2
			draw_line(endPos, endPos2, color2, width, antialiased)
			
		draw_line(startPos, endPos, color, width, antialiased)


