extends TabContainer
const CUSTOM=9  #Microsample oscillator index


func _ready():
	#Set up Metadata.
	$Metadata/Name.connect("text_changed", self, "set_meta", ["name"])
	$Metadata/Desc.connect("text_changed", self, "set_meta", [null, "desc"])
	
	for o in [$Metadata/Gain, $Metadata/Pan]:
#		o.connect("value_changed", self, "set_meta", [o.name.to_lower()])
		o.connect("value_changed", self, "set_meta", [o.name])

	#Set up LFO.
	for o in $LFO.get_children():
		if !o is Slider:  continue
		o.connect("value_changed", self, "set_lfo", [o.associated_property])

	$LFO/WavePanel/Duty.connect("value_changed", self, "set_lfo", [$LFO/WavePanel/Duty.associated_property])
	$LFO/WavePanel/Invert.connect("toggled", self, "set_lfo", ["invert"])

	for i in global.waves.size():
		$LFO/WavePanel/Popup/G.get_child(i).connect("pressed", self, "_on_Popup_button_pressed", [i])


	if Engine.get_idle_frames() == 0:  yield(get_tree(), "idle_frame")
	reinit()


func set_lfo(value, property):
	get_node(owner.chip_loc).SetLFO(property, value)

func set_meta(text, property):
	if property == "desc":  text = $Metadata/Desc.text
	get_node(owner.chip_loc).SetVoiceData(property, text)


func reinit():
	var c = get_node(owner.chip_loc)
	if !c:  return
	var d = parse_json(c.VoiceAsJSONString())   #Get the voice metadata

	#Load up the Metadata.
	$Metadata/Name.text = pull(d, "name", "")
	$Metadata/Desc.text = pull(d, "desc", "")
	$Metadata/Gain.value = pull(d,"gain", 1)
	$Metadata/Pan.value = pull(d,"pan", 0)

	#Load the LFO.
	var l=d["lfo"]
	$LFO/WavePanel/Preview.texture = global.wave_img[c.GetOscType(global.OpIntent.LFO)]
	$LFO/WavePanel/Wave.value = c.GetOscType(global.OpIntent.LFO)
	$LFO/WavePanel/Duty.value = pull(l, "duty", 0x7FFF)
	$LFO/WavePanel/Invert.pressed = pull(l, "invert", false)

	$"LFO/+Delay".value = pull(l, "delay", 0)
	$LFO/Speed.value = pull(l,"speed", 19)
	$"LFO/Pitch Depth".value = pull(l, "pmd", 0)
	$"LFO/Amplitude Depth".value = pull(l, "amd", 0x3FF)

	$LFO/Sync.selected = (pull(l, "syncType", 0))

	#Load waveforms and etc.  TODO
	switch_bank_ui($LFO/WavePanel/Wave.value == CUSTOM)

func pull(dict, key, default):
	if dict.has(key): return dict[key] 
	else: return default
	

func _on_Sync_item_selected(index):
	var c = get_node(owner.chip_loc)

	match index:
		0:  #No sync.
			c.SetLFO("osc_sync", false)
			c.SetLFO("delay_sync", false)
			c.SetLFO("release_sync", false)
		1:  #Osc sync.
			c.SetLFO("osc_sync", true)
			c.SetLFO("delay_sync", false)
			c.SetLFO("release_sync", false)
		2:  #Delay sync.
			c.SetLFO("osc_sync", false)
			c.SetLFO("delay_sync", true)
			c.SetLFO("release_sync", false)
		3:  #Release sync.
			c.SetLFO("osc_sync", false)
			c.SetLFO("delay_sync", true)
			c.SetLFO("release_sync", true)



func _on_Preview_gui_input(event):
	if event is InputEventMouseButton and event.pressed and event.button_index == BUTTON_LEFT:
		var popup = $LFO/WavePanel/Popup
		var grid = popup.get_node("G")
		var pos = get_global_mouse_position()
		if get_viewport().size.x - pos.x < grid.rect_size.x:  pos.x -= grid.rect_size.x/2
		if get_viewport().size.y - pos.y < grid.rect_size.y:  pos.y -= grid.rect_size.y/2
		popup.popup(Rect2(pos, popup.rect_size))

func _on_Popup_button_pressed(idx):
	get_node(owner.chip_loc).SetOscillator(global.OpIntent.LFO, idx)
	$LFO/WavePanel/Preview.texture = global.wave_img[idx]
	$LFO/WavePanel/Popup.hide()
	switch_bank_ui(idx==CUSTOM)

func switch_bank_ui(on):
	$LFO/WavePanel/Duty.visible = !on
	$LFO/WavePanel/Bank.visible = on

	if on:  $LFO/WavePanel/Bank.check_banks()
