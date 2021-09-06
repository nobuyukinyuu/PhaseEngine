tool
extends Control
const indicators = preload("res://gfx/ui/vu/rc_indicator.png")
const font = preload("res://gfx/fonts/spelunkid_font_bold.tres")
const font2 = preload("res://gfx/fonts/numerics_7seg.tres")

var bar = []
enum {BAR_NONE, BAR_EMPTY, BAR_FULL, BAR_PEAK}
enum ranges {rates, velocity, levels}
var rMax = {ranges.rates: 63, ranges.velocity: 1023, ranges.levels: 1023}

const COL_MAX=32
const ROW_MAX=128
const VAL_MAX=1023
const tick_size = Vector2(6,2)

var tbl = []
export (int, 0, 128) var split=128

var changing=-1

signal table_updated

func _ready():
	for i in 4:
		var p = AtlasTexture.new()
		p.atlas = indicators
		
		var pos1 = Vector2(0, i * 2)
		
		p.region = Rect2(pos1, tick_size)
		
		bar.append(p)
	

	for i in 128:
		tbl.append(0)
#		tbl.append( int(ease((127-i)/128.0, -2) * 128) )


func _gui_input(event):
	var mpos = get_local_mouse_position()
	mpos.x = clamp(mpos.x, 0, rect_size.x)
	mpos.y = clamp(mpos.y, 2, rect_size.y)  #Fixes some off by one issue
	
	if Input.is_mouse_button_pressed(BUTTON_LEFT):
		var pos = stepify(mpos.x/2, 4)
		if pos >= 128:  return
		for i in 4:
			tbl[pos+i] = (256-mpos.y)*4
			emit_signal("table_updated", pos+i, tbl[pos+i])
			
		changing = (256-mpos.y)*4
		update()

	if event is InputEventMouseButton and !event.pressed:
		owner.get_node("OctaveRuler").active = false
		changing = -1
		update()

func _draw():
	draw_rect(Rect2(Vector2.ZERO, rect_size), Color(0,0,0))
	
	for column in COL_MAX:
		for row in ROW_MAX:
			var pos = Vector2(column*(tick_size.x+2), row*tick_size.y)
			
			if row > (VAL_MAX - tbl[column * 4]) / 8:
				draw_texture(bar[BAR_FULL], pos)
#				draw_texture(bar[BAR_FULL if row%2!=0 else BAR_EMPTY], pos)
			else:
				if row%2==0: draw_texture(bar[BAR_NONE if row%16==0 else BAR_EMPTY], pos)
				
		var val = stepify( ROW_MAX-tbl[column*4]/8.0 , 2)
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

	#Draw changing indicator.  Be sure to scale to proper
	if changing>=0:
		var scaleVal = round((rMax[owner.intent] / global.RT_MINUS_ONE) * changing)
		draw_string(font2, get_local_mouse_position() + Vector2(16, 18), str(scaleVal), ColorN("black"))
		draw_string(font2, get_local_mouse_position() + Vector2(14, 16), str(scaleVal))
			



#Returns a vector of the table position and value percentage.
func get_table_pos(lock_x=false, mouse=get_local_mouse_position()) -> Vector2:
#	var mouse = get_local_mouse_position()
#	if lock_x:  mouse.x = last_column
	var arpos = clamp(int(lerp(0, 127, mouse.x / float(rect_size.x))) , 0, 127)
	var val = clamp(lerp(100,0, mouse.y / float(rect_size.y)) , 0, ROW_MAX)
	return Vector2(arpos, val)
	
func _make_custom_tooltip(_for_text):
	var p = $ToolTipProto.duplicate()
#	p.text = for_text
	var pos = get_table_pos()
	var hint2 = ""
	
	if pos.x >=12:  #Display a helpful octave indicator on A and C notes.
		if int(pos.x) % 12 == 0:  hint2 = "n. c-%s\n" % (int(pos.x/12)-1)
		if int(pos.x) % 12 == 9:  hint2 = "n. a-%s\n" % (int(pos.x/12)-1)
	
	var yValue = tbl[int(pos.x)]


#FIXME:   Alternative intents
	yValue *= rMax[owner.intent] / float(global.RT_MINUS_ONE)
	match owner.intent:
		ranges.rates:
			pass
		ranges.levels:
			pass
		ranges.velocity:
			pass

	#Set hint3 to scaled values if minmax has been altered
	var hint3 = ""
	

#	if owner.float_scale and owner.rate_scale:
#		yValue = 10000.0/ yValue if yValue>0 else 0
#
#		yValue = str(int(yValue)) + "%" if yValue>0 else "0"
#	elif owner.float_scale:
#		yValue = str(yValue).pad_decimals(2) + "%"
	
	p.text = "%sx: %s\ny: %s \n0n:%s%%" % [hint2, pos.x, round(yValue), String(pos.y).pad_decimals(2)]
	return p

