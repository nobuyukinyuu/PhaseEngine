tool
extends TextureRect

const PRECISION=12
const LINE_OPACITY = [0.1, 0.4]
export(bool) var note_off_indicator
#export (bool) var reverse
export (Color) var color = ColorN("white")
export (float, 0.5, 5, 0.5) var thickness = 1
export (float, -2, 5, 0.1) var curve=1 setget update_curve

export(float,0,1) var p1=0  
export(float,0,1) var p2=1  
export(float,0,1) var tl=0  

func update_curve(val):
	curve = val
	update()

func _ready():
	update()
	
#func _physics_process(_delta):
#	update()						#DON'T DO THIS


func _draw():
	var line_opacity = LINE_OPACITY[int(note_off_indicator)]
	var pts= []
	
	var h = rect_size.y/(PRECISION*2)
	for i in range(0, PRECISION*2, 2):
		
		draw_line(Vector2(rect_size.x, h*i), Vector2(rect_size.x, h*(i+1)), ColorN("white", line_opacity),0.5,false)

	var x = rect_size.x / PRECISION
	var lv_adjust = rect_size.y * tl
	for i in PRECISION:
		var pp1 = p1 * (1.0-tl)
		var pp2 = p2 * (1.0-tl)
		var level = lerp(pp1, pp2, ease(i/float(PRECISION), curve) )
		var level2 = lerp(pp1, pp2, ease((i+1)/float(PRECISION), curve) )
		var pos1= Vector2(x*i, (level * rect_size.y) )  
		var pos2= Vector2(x*(i+1), (level2 * rect_size.y) ) 
		
		pos1.y += lv_adjust
		pos2.y += lv_adjust
		
		pts.append(pos1) 
		if i == PRECISION -1:  pts.append(pos2)
		draw_line(pos1, pos2, color, thickness,true)


	pts.append(rect_size)
	pts.append(Vector2(0,rect_size.y))
	
	if pts.size() >= 2:
#		  draw_colored_polygon(pts, Color(color.r, color.g, color.b, 0.3))
		  draw_colored_polygon(pts, Color(0.08, 0.14, 0.27, 0.5))

	if note_off_indicator:
		var xOffset = 2 if p2 > 0.25 else 14
		draw_texture(preload("res://gfx/ui/note_off.png"), Vector2(rect_size.x+xOffset, 2), 
				Color(1,1,1,LINE_OPACITY[int(note_off_indicator)]))
