tool
class_name EGSlider
extends HSlider
var font = preload("res://gfx/fonts/spelunkid_font.tres")
var font2 = preload("res://gfx/fonts/numerics_8x10.tres")
var font3 = preload("res://gfx/fonts/numerics_5x8.tres")
var expTicks = preload("res://gfx/ui/expTicks.png")
const charw = 8
onready var lblPos = Vector2(rect_size.x - len(name)*charw + text_offset, -2)
onready var lblPos2 = Vector2(rect_size.x/2 - (len(str(value))+1)*charw/2.0, rect_size.y/2)

export(String) var associated_property = ""  #Determines the envelope property this slider's supposed to modify.

export(bool) var useExpTicks
enum TimeDisplay {NONE, EG_HOLD, LFO_DELAY, LFO_SPEED}
export(TimeDisplay) var useHoldTime 


export(int,-32,32) var text_offset = 0 setget set_offset
var needs_recalc=false

func set_offset(val):
	text_offset = val
	needs_recalc = true
	update()


func _ready():
	connect("resized", self, "_on_Resize")
	pass # Replace with function body.


func _draw():
	if needs_recalc:  recalc()
	var col = ColorN("yellow") if has_focus() else ColorN("white")
	
	if useExpTicks:  draw_texture(expTicks, Vector2(0, 9), Color(1,1,1,0.35))
	
	draw_string(font, lblPos, name, col)  #Draw name label

	#Draw value label
	var val
	var pos2:Vector2
	var vStr
	match useHoldTime:
		TimeDisplay.EG_HOLD:
			val = global.delay_frames_to_time(value) if not Engine.editor_hint else 00

			var s = "s"
			if val < 1:  
				val *=1000;
				s = "ms"
			vStr = str(val).pad_decimals(2)
			pos2 = calc_pos2(vStr)
			draw_string(font3, pos2 + Vector2((len(vStr))*charw*1.0 +2,2), s, col)
		TimeDisplay.LFO_DELAY:
			val = value
			
			var s = "ms"
			if val / 1000.0 > 1:
				val /= 1000.0
				s = "s"
			vStr = str(val).pad_decimals(2)
			pos2 = calc_pos2(vStr)
			draw_string(font3, pos2 + Vector2((len(vStr))*charw*1.0 +2,2), s, col)
		TimeDisplay.LFO_SPEED:
			val = global.lfo_speed_to_secs(value) if not Engine.editor_hint else 00
			var s = "s"
			if val < 1:  
				val *=1000;
				s = "ms"
			vStr = str(val).pad_decimals(2)
			pos2 = calc_pos2(vStr)
			draw_string(font3, pos2 + Vector2((len(vStr))*charw*1.0 +2,2), s, col)

			
		_:
			val = value
			vStr = str(val)
			pos2 = calc_pos2(vStr)
	
	
	draw_string(font2, pos2, vStr, col )
	


func _on_Resize():
	needs_recalc = true;

func recalc():
	lblPos = Vector2(rect_size.x - len(name)*charw + text_offset, -2)
	lblPos2 = Vector2(rect_size.x/2 - (len(str(value))+1)*charw/2.0, rect_size.y/2)
	needs_recalc = false
	
func calc_pos2(vStr):
	return Vector2(rect_size.x*0.5 - (len(vStr)+1)*charw*0.5, lblPos2.y)
