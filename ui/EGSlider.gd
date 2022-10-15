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
export(int, FLAGS, "Envelope", "Key Follow", "Velocity Table") var bind_abilities
var is_bound:bool = false

export(bool) var useExpTicks setget set_exp_tick_display
enum SpecialDisplay {NONE, EG_HOLD, LFO_DELAY, LFO_SPEED, PERCENT, CUSTOM=0xFF}
export(SpecialDisplay) var special_display 
export(PoolStringArray) var display_strings

export(int,-32,32) var text_offset = 0 setget set_offset

export(bool) var disabled=false setget set_disabled

var needs_recalc=false

const grabber_disabled = preload("res://ui/hSlider_grabber_disabled.tres")
const style_disabled = preload("res://ui/EGSliderDisabled.stylebox")
const bind_icon = preload("res://gfx/ui/bind_indicator.png")


var envelope_editor:NodePath  #Used to hold the envelope editor node active 
signal bind_requested
signal unbind_requested

func set_exp_tick_display(val):
	useExpTicks = val
	if useExpTicks:
		
		theme_type_variation = "EGSliderExpH"  #Variation in hSlider.theme blanks the default ticks
	else:
		theme_type_variation = ""

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
	connect("changed", self, "_on_Resize")
	recalc()
	pass # Replace with function body.

func _gui_input(event):
	if event is InputEventMouseButton and event.button_index == BUTTON_RIGHT and !event.pressed:
		prints(name, "right clicked....")
		var p = preload("res://ui/envelope_editor/BindableMenu.tscn").instance()
		add_child(p)
		p.owner = self
		p.preview_paste_value =  special_display == SpecialDisplay.NONE
		p.setup(self, bindable)
		p.set_item_text(0, name)
		
		p.popup(Rect2(get_global_mouse_position(), p.rect_size))
		
		accept_event()

#Used for binds.  Use the proper context depending on what tab it's used in.
func request_envelope_editor(title:String, data:Dictionary, context=global.Contexts.NONE, op:int=-1):
	if Engine.editor_hint:  return
	
	var existing_popup = global.get_modeless_popup(title, context)
	
	if not existing_popup and !envelope_editor.is_empty():  #Try the node path instead.
		existing_popup = get_node_or_null(envelope_editor)
	
	print("Requesting env editor; path is ", envelope_editor,". existing popup is ", existing_popup)
	if not existing_popup:  #The editor was closed and freed.  Make a new one.
		var p:EnvelopeEditorWindow = global.ENV_EDITOR_SCENE.instance()
		p.name = title
		var new_owner = global.add_modeless_popup(p, context) #Add node to scene tree.
		p.owner = get_node(new_owner)  #TODO:  Determine whether it's better to use self or owner
		envelope_editor = p.get_path()  #Set this so where we know where to get it to update stuff later
		
		p.log_scale = useExpTicks
		p.setup(title, data, get_path())
#		p.set_minmax(data.get("minValue", min_value), data.get("maxValue", max_value))
	
		p.rect_position = local_popup_pos(p.rect_size)
		p.show()
	else:  #Popup still exists.  Show it.
		print("Requesting existing popup for ", name)
		existing_popup.rect_position = local_popup_pos(existing_popup.rect_size)
		envelope_editor = existing_popup.get_path()
		existing_popup.show()
#	else:
#		var p = get_node(envelope_editor)
#		p.rect_position = local_popup_pos(p.rect_size)
#		p.show()
#	pass

func local_popup_pos(size:Vector2=Vector2.ZERO, buffer:Vector2=Vector2(16,16)):
	var pos = rect_global_position
	var vp = get_viewport_rect().size
	pos.x = clamp(pos.x, 0, vp.x - size.x - buffer.x)
	pos.y = clamp(pos.y, 0, vp.y - size.y - buffer.y)
	return pos

func _draw():
	if needs_recalc:  recalc()
	var col = ColorN("yellow") if has_focus() and !disabled else ColorN("white")
	
	if useExpTicks and tick_count>0:  
#		draw_texture(expTicks, Vector2(0, 9), Color(1,1,1,0.35))
		var top = max(32, tick_count)
		for i in range(1, top):
			var h = rect_size.x - xerp(0, rect_size.x, i/float(tick_count)) - 1
#			var h = rev_xerp(0, rect_size.x, i/float(tick_count)) - 1
#			var h = inv_xerp(1, rect_size.x, (i/float(tick_count)) * rect_size.x) * rect_size.x - 1
#			draw_texture(preload("res://gfx/ui/tick.png"), Vector2(h, 9))
			if i % 4 == 2:
				draw_texture(preload("res://gfx/ui/tick2x.png"), Vector2(h, 9))
			else:
				draw_texture(preload("res://gfx/ui/tick.png"), Vector2(h, 9))
		pass
	
	if is_bound:  draw_texture(bind_icon, Vector2(-1,-1))
	
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
	
	
	draw_string(drawFont, pos2, vStr, col)
	


func _on_Resize():
	needs_recalc = true;

func recalc():
	lblPos = Vector2(rect_size.x - len(name)*charw + text_offset, -2)
	lblPos2 = Vector2(rect_size.x/2 - (len(str(value))+1)*charw/2.0, rect_size.y/2)
	
	#Calculate exponential limits
	exp_min = 0 if  min_value == 0 else log(min_value) / log(2.0)
	exp_max = log(rect_size.x) / log(2.0)
	
	needs_recalc = false
	
func calc_pos2(vStr):
	return Vector2(rect_size.x*0.5 - (len(vStr)+1)*charw*0.5, lblPos2.y)



#Exponential interpolation funcs....... 
var exp_min=0  #Cached
var exp_max=1
func xerp(A,B,percent):  #Specizlied xerp specific to this control
	return pow(2, exp_min + (exp_max - exp_min) * percent)
