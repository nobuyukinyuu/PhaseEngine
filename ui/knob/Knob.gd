tool
extends HSlider
class_name Knob, "icon_knob.svg"

export(String) var title setget set_title
export(float, 1, 10, 0.5) var travel_multiplier = 1.0 setget set_travel
export(float, 0, 1) var notch=0.0 setget set_notch
export(float, 0.05, 1, 0.05) var thickness = 1.0 setget set_thickness
export(bool) var point_outwards setget set_orientation

export(Vector2) var title_align = Vector2(0.5, -0.1) setget set_title_align
export(Vector2) var value_align = Vector2(0.5, 0.5) setget set_value_align
export(int, -1, 9) var decimal_pad =-1 setget set_decimal_pad
export(Font) var value_font setget set_value_font

export(bool) var disabled setget set_disabled

var travel = 1.0
var not_ready:bool  #Used to signal to properties to step on the brakes because the scene doesn't exist.

func _enter_tree():
#	#If the user adds a control from the new node dialog, the scene won't have any children.
#	#Stop any further processing and replace ourselves with an actual scene.
	if !get_node_or_null("lblTitle"):
		print("%s (Knob.gd):  Scene missing. Attempting replacement..." % name)
		not_ready = true

func _replace():
	#HACKY METHOD TO AVOID EDITORPLUGINS. WE REPLACE OURSELVES WITH A NEW KNOB AND DISAPPEAR
	#This is not -exactly- compatible with UndoRedo! No scene binds to class_name, so this is what u get.	
	if !Engine.editor_hint:  
		print("Knob.gd:  Something is seriously wrong with %s. Instance a scene instead!" % name)
		return
		
	var q = load(self.get_script().get_path().get_base_dir() + "/Knob.tscn").instance()

	var parent = get_parent()
	var pos_in_parent = get_position_in_parent()
	var oldname = name
	name = "_PlaceholderKnob"

	parent.add_child(q, true)
	parent.move_child(q, pos_in_parent)
	q.name = oldname

	yield(get_tree(), "idle_frame")  #Necessary for us to have an owner to give to our replacement.
	q.owner = owner
	
	#Generate a fake input event since we don't have access to the scene inspector.  No one will notice.....
	yield(get_tree(), "idle_frame")  
	var a = InputEventAction.new()
	a.action = "ui_up"
	a.pressed = true
	Input.parse_input_event(a)
	
	self.queue_free()  #I was never here.

func set_disabled(val):
	disabled = val
	var bg = get_node("BG")
	editable = !disabled
	if bg:
		adjust(bg, "desaturate", val)
		bg.modulate = ColorN("dimgray") if disabled else ColorN("white")
		get_node("lblVal").modulate = ColorN("darkgray") if disabled else ColorN("white")
	if disabled:
		Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
		captured = false

func set_decimal_pad(val):
	decimal_pad = val
	if !is_inside_tree() or not_ready:  return
	_on_Knob_value_changed(value)

func set_title(val):
	title = val
	if !is_inside_tree() or not_ready:  return
	$lblTitle.text = val

func set_travel(val):
	#The travel multiplier is how much mouse movement relative to the original control's size
	#Would be needed to produce the same change in value.
	travel_multiplier = val
	var short_end = min(rect_size.x, rect_size.y)
	travel = abs(max_value - min_value) / float(max(short_end,32))  #Unit size
	travel /= travel_multiplier

func set_value_font(val):
	value_font = val
	$lblVal.add_font_override("font", val)

func set_value_align(val):
	value_align = val
	update_alignments()
func set_title_align(val):
	title_align = val
	update_alignments()

func update_alignments():
	if not_ready:  return
	$lblVal.rect_position = rect_size * value_align
	$lblVal.rect_position -= $lblVal.rect_size / 2.0
	$lblTitle.rect_position = rect_size * title_align
	$lblTitle.rect_position -= $lblTitle.rect_size / 2.0
	

func set_notch(val:float):
	notch = val
	if not_ready:  return
	adjust(get_node("BG"), "zoom_angle", 1.0-val)

func set_thickness(val:float):
	if val == 0:  return
	thickness = val
	if not_ready:  return
	$VP.size.y = 16 / val

func set_orientation(outwards:bool):
	point_outwards = outwards

	if not_ready:  return
	$VP/Slider.rect_scale.y = -1.0 if outwards else 1.0
	$VP/Slider.rect_position.y = 16 if outwards else 0

func _ready():
	if not_ready:  #Occurs if a Knob is instanced from the New Node dialog without its scene.
		_replace()
		return

	if !Engine.editor_hint and title == "":  
		$lblTitle.text = name
	else:
		set_title(title)

	connect("resized", self, "_on_Knob_resized")
	connect("changed", self, "_on_Knob_changed")
	connect("value_changed", self, "_on_Knob_value_changed")
	
	get_node("VP/Slider").theme = theme
	if !Engine.editor_hint and Engine.get_idle_frames()==0:  
		yield(get_tree(), "idle_frame")
		update_theming()
	_on_Knob_changed()
	set_decimal_pad(decimal_pad)

	_on_Knob_resized()
	update()


var captured:bool
var lastMousePos:Vector2
var delta:float
const _135_DEGREES = 3*PI/4.0 
const _315_DEGREES = 7*PI/4.0

func _input(event):
	if !Engine.editor_hint and event is InputEventMouseButton and event.button_index == BUTTON_LEFT: 
		if editable and event.pressed and get_rect().has_point(get_parent().get_local_mouse_position()):
			set_grabber(NONE, YES)
			if travel_multiplier != 1.0:
				#Custom override!  Begin drag
				lastMousePos = get_local_mouse_position()
				Input.set_mouse_mode(Input.MOUSE_MODE_CAPTURED)
				captured = true
				accept_event()
		elif !event.pressed and event.button_index == BUTTON_LEFT:
			set_grabber(NONE, NO)
			if captured:
				Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
				warp_mouse(lastMousePos)
				grab_focus()  #Makes sure user can still adjust value with keyboard after releasing
				captured = false

	elif captured and event is InputEventMouseMotion:  #Adjust the slider using custom travel distance
		accept_event()
		var magnitude = event.relative.length() 
		var dir = (event.relative.angle() + PI)
		if dir < _135_DEGREES or dir > _315_DEGREES:  magnitude *= -1
		
		delta += magnitude * travel

		if abs(delta)>step:
			value += delta
			_on_Knob_value_changed(value + delta)
			delta = delta - stepify(delta, step)

func _notification(what):
	if not_ready:  return
	match what:
		NOTIFICATION_MOUSE_ENTER, NOTIFICATION_FOCUS_ENTER:
			set_grabber(YES)
		NOTIFICATION_MOUSE_EXIT:
			if !has_focus():  set_grabber(NO)
		NOTIFICATION_FOCUS_EXIT:
			set_grabber(NO)
		NOTIFICATION_VISIBILITY_CHANGED:
			if visible:  
				if Engine.editor_hint and owner:  update_theming()
				update_alignments()

func _on_Knob_resized():
	var m = min(rect_size.x, rect_size.y)
	var BG = get_node("BG")
	BG.rect_size = Vector2(m,m)
	BG.rect_position = rect_size/2.0 - BG.rect_size/2.0
	adjust(BG, "rect_size", BG.rect_size)
	update_alignments()

	set_travel(travel_multiplier)  #Travel multiplier relies on size to function correctly


func adjust(node, what:String, val):
	node.material.set_shader_param(what, val)

#Used to pass a pseudo-highlight / focused state to the viewport slider
enum {NONE, YES, NO}
func set_grabber(highlight, pressed=NONE):
	var sld:HSlider = get_node("VP/Slider")
	match highlight:
		YES:
			var style = get_stylebox("grabber_area_highlight")
			sld.add_stylebox_override("grabber_area", style)
		NO:
			var style = get_stylebox("grabber_area")
			sld.add_stylebox_override("grabber_area", style)
	match pressed:
		YES:
			sld.add_icon_override("grabber", get_icon("grabber_highlight"))
		NO:
			sld.add_icon_override("grabber", get_icon("grabber"))

	sld.update()

#Usually only necessary on Ready and in the editor when visibility changes, to help preview styles.
func update_theming():
	var sld:HSlider = get_node("VP/Slider")
	sld.add_stylebox_override("slider", get_stylebox("slider"))
	sld.add_stylebox_override("grabber_area", get_stylebox("grabber_area"))
	sld.add_stylebox_override("grabber_area_highlight", get_stylebox("grabber_area_highlight"))
	sld.add_icon_override("tick", get_icon("tick"))
	sld.add_icon_override("grabber", get_icon("grabber"))
	sld.add_icon_override("grabber_disabled", get_icon("grabber_disabled"))
	sld.add_icon_override("grabber_highlight", get_icon("grabber_highlight"))


func _on_Knob_changed():
	$VP/Slider.min_value = min_value
	$VP/Slider.max_value = max_value
	$VP/Slider.step = step
	$VP/Slider.page = page
	$VP/Slider.value = value
	$VP/Slider.tick_count = tick_count
	$VP/Slider.exp_edit = exp_edit
	
	#Changes to min or max value affect travel.  Update it.
	set_travel(travel_multiplier)

func _on_Knob_value_changed(val):
	$VP/Slider.value = val
	var amt = str(value) if decimal_pad==-1 else str(value).pad_decimals(decimal_pad)
	$lblVal.text = amt
