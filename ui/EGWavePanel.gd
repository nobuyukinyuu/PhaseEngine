extends Panel

var waves=[0,2,1,3,5,6,7, "8a", 8, 9]  #Make sure this matches the waveFuncs list in Oscillator.cs
var wave_img = []

func _ready():
	
	for i in waves.size():
		wave_img.append(load("res://gfx/wave/%s.png" % waves[i]))
		$Preview.texture = wave_img[0]
	
		$Popup/G.get_child(i).connect("pressed", self, "_on_Popup_button_pressed", [i])
	
	$Popup.rect_size = $Popup/G.rect_size


func _on_Wave_value_changed(value):
	$Preview.texture = wave_img[value]


func _on_Preview_gui_input(event):
	if event is InputEventMouseButton and event.pressed and event.button_index == BUTTON_LEFT:
		var pos = get_global_mouse_position()
		print(get_viewport().size)
		if get_viewport().size.x - pos.x < $Popup/G.rect_size.x:  pos.x -= $Popup/G.rect_size.x/2
		if get_viewport().size.y - pos.y < $Popup/G.rect_size.y:  pos.y -= $Popup/G.rect_size.y/2
		$Popup.popup(Rect2(pos, $Popup.rect_size))

func _on_Popup_button_pressed(idx):
	$Wave.value = idx
	$Popup.hide()
	pass
