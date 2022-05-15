tool
class_name EGSlider
extends HSlider
const font = preload("res://gfx/fonts/spelunkid_font.tres")
const font2 = preload("res://gfx/fonts/numerics_8x10.tres")
const font3 = preload("res://gfx/fonts/numerics_5x8.tres")
const expTicks = preload("res://gfx/ui/expTicks.png")
const charw = 8
onready var lblPos = Vector2(rect_size.x - len(name)*charw + text_offset, -2)
onready var lblPos2 = Vector2(rect_size.x/2 - (len(str(value))+1)*charw/2.0, rect_size.y/2)

export(String) var associated_property = ""  #Determines the envelope property this slider's supposed to modify.
export(bool) var bindable

export(bool) var useExpTicks
enum SpecialDisplay {NONE, EG_HOLD, LFO_DELAY, LFO_SPEED, PERCENT, CUSTOM=0xFF}
export(SpecialDisplay) var special_display 
export(PoolStringArray) var display_strings

export(int,-32,32) var text_offset = 0 setget set_offset

export(bool) var disabled=false setget set_disabled

var needs_recalc=false

const grabber_disabled = preload("res://ui/hSlider_grabber_disabled.tres")
const style_disabled = preload("res://ui/EGSliderDisabled.stylebox")
const bind_icon = preload("res://gfx/ui/bind_indicator.png")

func set_offset(val):
	text_offset = val
	needs_recalc = true
	update()

func set_disabled(val):
	if !is_inside_tree():  return
	disabled = val
	editable = !disabled
	add_stylebox_override("slider", style_disabled if disabled else null)
	add_stylebox_override("grabber_area_highlight", grabber_disabled if disabled else null)
	add_stylebox_override("grabber_area", grabber_disabled if disabled else null)
	self_modulate.a = 0.5 if disabled else 1.0


func _ready():
	connect("resized", self, "_on_Resize")
	pass # Replace with function body.

func _gui_input(event):
	if event is InputEventMouseButton and event.button_index == BUTTON_RIGHT and !event.pressed:
		prints(name, "right clicked....")
	
	pass


func _draw():
	if needs_recalc:  recalc()
	var col = ColorN("yellow") if has_focus() and !disabled else ColorN("white")
	
	if useExpTicks:  draw_texture(expTicks, Vector2(0, 9), Color(1,1,1,0.35))
	
	if bindable:  draw_texture(bind_icon, Vector2(0,0))
	
	draw_string(font, lblPos, name, col)  #Draw name label

	#Draw value label
	var val
	var pos2:Vector2
	var vStr
	var drawFont = font2
	match special_display:
		SpecialDisplay.EG_HOLD:
			val = global.delay_frames_to_time(value) if not Engine.editor_hint else 00

			var s = "s"
			if val < 1:  
				val *=1000;
				s = "ms"
			vStr = str(val).pad_decimals(2)
			pos2 = calc_pos2(vStr)
			draw_string(font3, pos2 + Vector2((len(vStr))*charw*1.0 +2,2), s, col)
		SpecialDisplay.LFO_DELAY:
			val = value
			
			var s = "ms"
			if val / 1000.0 > 1:
				val /= 1000.0
				s = "s"
			vStr = str(val).pad_decimals(2)
			pos2 = calc_pos2(vStr)
			draw_string(font3, pos2 + Vector2((len(vStr))*charw*1.0 +2,2), s, col)
		SpecialDisplay.LFO_SPEED:
			val = global.lfo_speed_to_secs(value) if not Engine.editor_hint else 00
			var s = "s"
			if val < 1:  
				val *=1000;
				s = "ms"
			vStr = str(val).pad_decimals(2)
			pos2 = calc_pos2(vStr)
			draw_string(font3, pos2 + Vector2((len(vStr))*charw*1.0 +2,2), s, col)

		SpecialDisplay.PERCENT:
			val = value/float(max_value)
			vStr = str(val*100).pad_decimals(2) + "%"
			pos2 = calc_pos2(vStr)
			

		SpecialDisplay.CUSTOM:
			drawFont = font
			val = value
			vStr = display_strings[val] if val < display_strings.size() else "Setting %s" % val
			pos2 = calc_pos2(vStr)
			
		_:
			val = value
			vStr = str(val)
			pos2 = calc_pos2(vStr)
	
	
	draw_string(drawFont, pos2, vStr, col )
	


func _on_Resize():
	needs_recalc = true;

func recalc():
	lblPos = Vector2(rect_size.x - len(name)*charw + text_offset, -2)
	lblPos2 = Vector2(rect_size.x/2 - (len(str(value))+1)*charw/2.0, rect_size.y/2)
	needs_recalc = false
	
func calc_pos2(vStr):
	return Vector2(rect_size.x*0.5 - (len(vStr)+1)*charw*0.5, lblPos2.y)
