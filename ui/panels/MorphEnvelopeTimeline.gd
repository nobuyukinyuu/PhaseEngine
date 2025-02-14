extends ColorRect
const QUAD_ROOT_OF_2 = 1.189207115
const hand_icon = preload("res://gfx/ui/pan_hand.png")
const grab_handles = preload("res://gfx/ui/envelope_editor/grab_handles.png")
const note_off = preload("res://gfx/ui/note_off.png")
const chevron = preload("res://gfx/ui/linear_panel/chevron5px.png")

const LOOP_COLOR1 = Color(1,1,0.6, 0.5)
const LOOP_COLOR2 = Color(1,1,0.6, 0.7)
const SUSTAIN_COLOR1 = Color(0.8,1,1, 0.5)
const SUSTAIN_COLOR2 = Color(0,0.5,1, 0.4)
const font = preload("res://gfx/fonts/numerics_5x8.tres")


var panning = false
const PICK_DISTANCE = 160.0  #Squared value so that we don't have to do a sqrt operation
var dragging = false
var drag_pt = -1
var last_closest_pt = -1
var drag_value = Vector2.ZERO  #Last value at dragged mouse position
var upper_bound = 0  #Drag bounds
var lower_bound = 0

var zoom = 1200  #Number of millisecs to display on timeline at once
var offset = 0  #Offset from 0ms to start with
var offset_accumulation = 0  #Used for panning very small values

export (Color) var element_color = Color(1,1,0)

var pts = []

func _ready():
	if owner is LinearPanel:
		owner.get_node("%ZoomBar/Slider").connect("value_changed", self, "set_zoom")
		owner.get_node("%Offset").connect("value_changed", self, "set_offset")
	pass

	for i in 3:
		var x = randi() % 1200
		pts.append(x)

func ms_per_px() -> float:  return zoom / float(rect_size.x) #number of millisecs in one pixel
func ms2offset(ms):  return (ms - offset) / ms_per_px()  #Hypothetical px offset for a given ms


func set_zoom(value):  
	zoom = value
	update()
func set_offset(value):  
	offset = value
	offset_accumulation = value
	update()


func _gui_input(event):
	if event is InputEventMouseButton:
		
		#First, check if the window is in the background.  If so, move the window up.
		var parent = owner.get_parent()
	
		if owner is LinearPanel:
			match event.button_index:
				BUTTON_WHEEL_DOWN:
					owner.get_node("%ZoomBar/Slider").value *= QUAD_ROOT_OF_2
				BUTTON_WHEEL_UP:
					owner.get_node("%ZoomBar/Slider").value /= QUAD_ROOT_OF_2
		match event.button_index:
			BUTTON_MIDDLE:
				panning = event.pressed
				Input.set_custom_mouse_cursor(hand_icon if event.pressed else null)
	elif event is InputEventMouseMotion:
		if panning:
			#Pan the offset relative to the number of pixels moved.
			var one_px = owner.get_node("%TimeRuler").ms_per_px()
			var delta = one_px * event.relative.x
			var offset = owner.get_node("%Offset")
			
			#Accumulate sub-millisecond values
			offset_accumulation = clamp(offset_accumulation - delta, offset.min_value, offset.max_value)
			offset.value = offset_accumulation



func _draw():
	var h = ruler_pos()
	for i in 3:
		var x = ms2offset(pts[i])
		if x < 0 or x > rect_size.x:  continue
		draw_line(Vector2(x, 0), Vector2(x, h), element_color)
		draw_pt(i, x)

func draw_pt(n, x):
	draw_rect(Rect2(Vector2(x-5, 0),Vector2(10,12)), ColorN("black"))
	draw_string(font, Vector2(x-5, 4), str(n).pad_zeros(2))
	draw_texture(chevron, Vector2(x-3,ruler_pos()-3), element_color)

func ruler_pos(): return rect_size.y if not owner is LinearPanel else owner.dist_to_time_ruler(self)
