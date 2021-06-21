extends TextureRect
onready var vu = owner.get_node("VU")

var active=false

func _gui_input(event):
	if event is InputEventMouseMotion and Input.is_mouse_button_pressed(BUTTON_LEFT):
		active = true
		set_split()
	else:  active = false

	if event is InputEventMouseButton and !event.pressed:
		vu.update()

	
func _input(event):
	if !Input.is_mouse_button_pressed(BUTTON_LEFT):
		active = false

func set_split():
	if get_local_mouse_position().x < 0 or get_local_mouse_position().x>255:  
		vu.split = 128
		vu.update()
		return
	
	vu.split = get_local_mouse_position().x / 2
	vu.update()
