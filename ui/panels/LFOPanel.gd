extends TabContainer

func _ready():
	for o in $LFO.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "set_lfo", [o.associated_property])

	$LFO/WavePanel/Duty.connect("value_changed", self, "set_lfo", [$LFO/WavePanel/Duty.associated_property])
	$LFO/WavePanel/Invert.connect("toggled", self, "set_lfo", ["invert"])

	for i in global.waves.size():
		$LFO/WavePanel/Popup/G.get_child(i).connect("pressed", self, "_on_Popup_button_pressed", [i])


func set_lfo(value, property):
	get_node(owner.chip_loc).SetLFO(property, value)


func _on_Sync_item_selected(index):
	var c = get_node(owner.chip_loc)

	match index:
		0:  #No sync.
			c.SetLFO("osc_sync", false)
			c.SetLFO("delay_sync", false)
		1:  #Osc sync.
			c.SetLFO("osc_sync", true)
			c.SetLFO("delay_sync", false)
		2:  #Delay sync.
			c.SetLFO("osc_sync", false)
			c.SetLFO("delay_sync", true)


func _on_Preview_gui_input(event):
	if event is InputEventMouseButton and event.pressed and event.button_index == BUTTON_LEFT:
		var popup = $LFO/WavePanel/Popup
		var grid = popup.get_node("G")
		var pos = get_global_mouse_position()
		if get_viewport().size.x - pos.x < grid.rect_size.x:  pos.x -= grid.rect_size.x/2
		if get_viewport().size.y - pos.y < grid.rect_size.y:  pos.y -= grid.rect_size.y/2
		popup.popup(Rect2(pos, popup.rect_size))

func _on_Popup_button_pressed(idx):
	get_node(owner.chip_loc).SetWaveform(-1, idx)
	$LFO/WavePanel/Preview.texture = global.wave_img[idx]
	$LFO/WavePanel/Popup.hide()
	pass

