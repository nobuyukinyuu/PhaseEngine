tool
extends Control
const indicators = preload("res://gfx/ui/vu/rc_indicator.png")
const font = preload("res://gfx/fonts/spelunkid_font_bold.tres")

var bar = []
enum {BAR_NONE, BAR_EMPTY, BAR_FULL, BAR_PEAK}

const tick_size = Vector2(6,2)

var tbl = []
export (int, 0, 128) var split=128


func _ready():
	for i in 4:
		var p = AtlasTexture.new()
		p.atlas = indicators
		
		var pos1 = Vector2(0, i * 2)
		
		p.region = Rect2(pos1, tick_size)
		
		bar.append(p)
	
	for i in 128:
		tbl.append( int(ease((127-i)/128.0, -2) * 128) )

#func _physics_process(delta):
#	update()

func _gui_input(event):
	var mpos = get_local_mouse_position()
	mpos.x = clamp(mpos.x, 0, rect_size.x)
	mpos.y = clamp(mpos.y, 0, rect_size.y)
	
	if Input.is_mouse_button_pressed(BUTTON_LEFT):
		var pos = stepify(mpos.x/2, 4)
		if pos >= 128:  return
		for i in 4:
			tbl[pos+i] = mpos.y/2
		update()

	if event is InputEventMouseButton and !event.pressed:
		owner.get_node("OctaveRuler").active = false
		update()

func _draw():
	draw_rect(Rect2(Vector2.ZERO, rect_size), Color(0,0,0))
	
	for column in 32:
		for row in 128:
			var pos = Vector2(column*(tick_size.x+2), row*tick_size.y)
			
			if row > tbl[column * 4]:
				draw_texture(bar[BAR_FULL], pos)
#				draw_texture(bar[BAR_FULL if row%2!=0 else BAR_EMPTY], pos)
			else:
				if row%2==0: draw_texture(bar[BAR_NONE if row%16==0 else BAR_EMPTY], pos)
				
		var val = stepify(tbl[column*4], 2)
		var pos2 = Vector2(column*(tick_size.x+2), val*tick_size.y)
		draw_texture(bar[BAR_PEAK], pos2)

	if split < 128:
		var pos=Vector2(split*2, 0)
		var pos2=Vector2(split*2, rect_size.y)
		draw_line(pos,pos2,ColorN("yellow", 0.5))
		
		if owner.get_node("OctaveRuler").active:
			var note = global.note_name(split)
			draw_string(font, get_local_mouse_position() + Vector2(16, 18), note, ColorN("black"))
			draw_string(font, get_local_mouse_position() + Vector2(14, 16), note)




