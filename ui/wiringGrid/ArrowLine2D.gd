tool
extends Line2D

export (float,0, 0xFFF) var arrow_length = 32 setget set_arrow_length
export (float,0, 3.14) var arrow_spread = PI/4 setget set_arrow_width
const a45 = PI/4.0


func set_arrow_length(val):
	arrow_length = val
	update()
func set_arrow_width(val):
	arrow_spread = val
	update()

export(bool) var arrow_begin
export(bool) var arrow_end


func _ready():
	pass # Replace with function body.


func _draw():
	if points.size() < 2:  return
	
	for i in points.size()-1:
		draw_line(points[i],points[i+1], default_color, width, antialiased)
		
	if arrow_end:
		draw_polyline(make_pts(points[points.size()-1], points[points.size()-2]),
						 default_color, width+0.0001, antialiased)

	if arrow_begin:
		draw_polyline(make_pts(points[0], points[1]),
						 default_color, width+0.0001, antialiased)
		

func make_pts(pos1, pos2):
	var pts:PoolVector2Array
	pts.resize(3)
	pts[1] = pos1

	var angle = atan2(pos1.y-pos2.y, pos1.x-pos2.x) + PI
	
	pts[0] = Vector2(pos1.x + arrow_length*cos(angle+arrow_spread), pos1.y + arrow_length*sin(angle+arrow_spread))
	pts[2] = Vector2(pos1.x + arrow_length*cos(angle-arrow_spread), pos1.y + arrow_length*sin(angle-arrow_spread))

	return pts
