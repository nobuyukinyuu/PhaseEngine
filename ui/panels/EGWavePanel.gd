extends Panel
var bankline:LineEdit  #LineEdit control for wave bank

const CUSTOM=9  #Microsample oscillator oscType index

func _ready():
	
#	for i in global.waves.size():  #Can't use this because we have a different oscillator list depending on opType
	for i in $Popup/G.get_child_count():
		var idx = $Popup/G.get_child(i).name
		idx= int(idx.substr(len(idx)-1))  #Use Wave idx name to assign, for Bitwise doesn't use Noise
		$Popup/G.get_child(i).connect("pressed", self, "_on_Popup_button_pressed", [idx])
	
	$Preview.texture = global.wave_img[0]
	$Popup.rect_size = $Popup/G.rect_size


func _on_Wave_value_changed(value):
	$Preview.texture = global.wave_img[value]
	switch_bank_ui(value==CUSTOM)


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

	switch_bank_ui(idx==CUSTOM)

func switch_bank_ui(on):
	$Wave.visible = !on
	$Bank.visible = on

	if on:  $Bank.check_banks()
