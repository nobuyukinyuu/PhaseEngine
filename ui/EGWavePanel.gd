extends Panel


func _ready():
	
	for i in global.waves.size():
		$Popup/G.get_child(i).connect("pressed", self, "_on_Popup_button_pressed", [i])
	
	$Preview.texture = global.wave_img[0]
	$Popup.rect_size = $Popup/G.rect_size


func _on_Wave_value_changed(value):
	$Preview.texture = global.wave_img[value]


func _on_Preview_gui_input(event):
	if event is InputEventMouseButton and event.pressed and event.button_index == BUTTON_LEFT:
		var pos = get_global_mouse_position()
#		print(get_viewport().size)
		if get_viewport().size.x - pos.x < $Popup/G.rect_size.x:  pos.x -= $Popup/G.rect_size.x/2
		if get_viewport().size.y - pos.y < $Popup/G.rect_size.y:  pos.y -= $Popup/G.rect_size.y/2
		$Popup.popup(Rect2(pos, $Popup.rect_size))

func _on_Popup_button_pressed(idx):
	$Wave.value = idx
	$Popup.hide()
	pass
