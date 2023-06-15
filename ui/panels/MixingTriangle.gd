tool
extends Control
const SQ2 = 0.707106781  #sqrt(2)/2.0,  45 degrees
const SQ32 = 0.866025404

var stylebox:StyleBoxFlat = theme.get_stylebox("slider", "HSlider")
var shad = ColorN("black", 0.5)
var outline = Color(1,1,1)
var shad_offset = Vector2(3,2)

var colorA = Color("091623")
var colorB = Color("082A2B")

func _ready():
	shad = stylebox.shadow_color
	outline = stylebox.border_color
	outline.a = 0.5

	colorA = stylebox.bg_color
	colorA.a *= 0.5
	
	colorB = colorA
	colorB.h += 0.2

	if not Engine.editor_hint:
		get_parent().get_node("Balance").connect("value_changed", self,"value_changed")
		get_parent().get_node("Dry Mix").connect("value_changed", self,"value_changed")


func value_changed(_dummy):
	update()

func _gui_input(event):
	if Engine.editor_hint:  return
	if event is InputEventMouseButton or event is InputEventMouseMotion:
		if Input.is_mouse_button_pressed(BUTTON_LEFT):
			var pos = get_local_mouse_position()
			pos /= rect_size

			pos.y = clamp(pos.y, 0.0, 1.0)

			
			if pos.y > 0:
				pos.x -= 0.5
				pos.x /= pos.y
				pos.x += 0.5
			pos.x = clamp(pos.x, 0.0, 1.0)
			pos.y = 1.0-pos.y
			pos *= 10000
			
			get_parent().get_node("Balance").value = pos.x
			get_parent().get_node("Dry Mix").value = pos.y
		

func _physics_process(delta):
	if Engine.editor_hint:  update()

func get_indicator():  
	return (Vector2(get_parent().get_node("Balance").value/10000.0, 
			1.0-get_parent().get_node("Dry Mix").value/10000.0))
	

func _draw():
	var c = Vector2(rect_size.x * 0.5, 0.0)
	var a = Vector2(0.0, rect_size.y)
	var b = rect_size
	
	var t = get_viewport_transform().get_scale().x * 0.5
	
	
	draw_line(c+shad_offset, a+shad_offset, shad,t,true)
	draw_line(a+shad_offset, b+shad_offset, shad,t,true)
	draw_line(b+shad_offset, c+shad_offset, shad,t,true)

	draw_polygon([c,a,b], [Color(0), colorA, colorB])
	
	draw_line(c, a, outline,t,true)
	draw_line(a, b, outline,t,true)
	draw_line(b, c, outline,t,true)

	var indicator = get_indicator()
	indicator.x = 0.5 - indicator.y * (0.5-indicator.x)
#	indicator.y = ease(indicator.y, 0.5)
	draw_texture(preload("res://gfx/ui/marker11.png"), indicator * rect_size - Vector2(5.5,5.5))
