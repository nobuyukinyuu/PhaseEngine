extends ColorRect

var mins = []
var maxes = []

func _draw():
	var h = rect_size.y / 2
	draw_line(Vector2(0,h), Vector2(rect_size.x, h), Color(1,1,1,0.1))
	
	for i in rect_size.x:
		if i>=mins.size():  break
		var origin = Vector2(i, h)
		draw_line(origin + Vector2(0, mins[i]*h), origin + Vector2(0, maxes[i]*h), ColorN("cyan", 0.5))
