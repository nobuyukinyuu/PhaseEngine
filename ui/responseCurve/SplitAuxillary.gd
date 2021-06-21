extends Label

onready var ruler = owner.get_node("OctaveRuler")

func _gui_input(event):
	if event is InputEventMouseMotion and Input.is_mouse_button_pressed(BUTTON_LEFT):
		ruler.active = true
		ruler.set_split()
	else:  ruler.active = false

	if event is InputEventMouseButton and !event.pressed:
		owner.get_node("VU").update()
