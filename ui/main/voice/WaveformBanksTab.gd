extends Control
export(NodePath) var chip_loc #Location of PhaseEngine instance in code

const ALL=-1

var wavetable_size = 1024

func _ready():
	#Convert the chip location to an absolute location!!
	#Otherwise, the value specified in the editor will only be valid for us and none of our children.
	if chip_loc:
		chip_loc = get_node(chip_loc).get_path()

	for o in $Menu/Grid.get_child(0).group.get_buttons():
		var size = 1 << int(o.name)  #Number of bits in name converted to table size
		o.set_meta("size", size)
		o.connect("pressed", self, "_on_Sample_size_toggle", [size])
		if o.pressed:  wavetable_size = size

	populate()

	global.connect("wavebanks_reset", self, "populate")


func populate():
	var c = get_node(chip_loc)
	if not c:  
		print("WaveformBanksTab.gd:  Can't find chip_loc!")
		return
	
	$Items.clear()
	var data = c.GetWave(ALL)
	for i in data.size():
		$Items.add_item("%s:  [%s]" % [i, data[i].size()], make_icon(data[i]))

	global.emit_signal("wavebanks_changed")
	
func update_bank(which):
	var c = get_node(chip_loc)
	if not c:  
		print("WaveformBanksTab.gd:  Can't find chip_loc!")
		return

	$Items.set_item_icon(which, make_icon(c.GetWave(which)))


#onready var stride=global.WAVETABLE_SIZE/$Items.fixed_icon_size.x
func make_icon(data):
	if not (data is Array) or data.size() < 32:
		return load("res://gfx/ui/wave_preview_fail.png")
#	if data.size() != global.WAVETABLE_SIZE:
#		return load("res://gfx/ui/wave_preview_fail.png")

	var stride = data.size()/$Items.fixed_icon_size.x
	
	var output = ImageTexture.new()
	var w = $Items.fixed_icon_size.x
	var h = $Items.fixed_icon_size.y
	var img = Image.new()
	img.create(w, h, false,Image.FORMAT_RGBA8)

	img.fill(ColorN("black"))
	img.lock()
	var vRatio2 = 65535.0/(h-1)
	
#	for y in h:  #Set the edge
#		img.set_pixel(w-1, y, ColorN("dimgray"))

	
	var highest = data[0]+32768
	var lowest = data[0]+32768
	for x in w:
		#Draw the centerline
		img.set_pixel(x, h/2, ColorN("dimgray"))
		var last_highest=highest
		var last_lowest=lowest
		lowest=data[x*stride]+32768
		highest=lowest
		
		for i in stride:
			lowest = min((data[x*stride+i]+32768), lowest)
			highest = max((data[x*stride+i]+32768), highest)

		img.set_pixel(x,lowest/vRatio2, ColorN("yellow", 0.5))
		img.set_pixel(x,highest/vRatio2, ColorN("yellow", 0.5))

		for y in h:
#			if y*vRatio2 >=lowest and y*vRatio2 <= highest:
#				img.set_pixel(x,y, ColorN("yellow", 0.5))
			if y*vRatio2 >= min(lowest, last_lowest) and y*vRatio2 <= max(highest, last_highest):
				#Draw a lerp line
				img.set_pixel(x,y, ColorN("yellow", 0.25))

	
	img.unlock()
	img.flip_y()
	output.create_from_image(img)
	return output



func _on_Add_pressed():
	var c = get_node(chip_loc)
	if not c:  
		print("WaveformBanksTab.gd:  Can't find chip_loc!")
		return
	var input = c.AddWave(wavetable_size)  #Grab the waveform from the sample we just added.
	
#	var size_label = "1K" if input.size()==1024 else input.size()
	var size_label = input.size()
	$Items.add_item("%s:  [%s]" % [$Items.get_item_count(), size_label], make_icon(input))
	global.emit_signal("wavebanks_changed")

func _on_Remove_pressed():
	var idx = $Items.get_selected_items()
	if idx.empty():  return
	else:  idx = idx[0]

	var c = get_node(chip_loc)
	if not c:  
		print("WaveformBanksTab.gd:  Can't find chip_loc!")
		return

	c.RemoveWave(idx)
	$Items.remove_item(idx)

	#Rename all of the banks after the one that was deleted.
	for i in range(idx, $Items.get_item_count()):
		var sz = $Items.get_item_text(i)
		sz = sz.substr(sz.find("["))  #Yoink the size info from the previous label.
		$Items.set_item_text(i, "%s:  %s" % [i, sz])

	global.emit_signal("wavebanks_changed", idx)


	_on_Items_nothing_selected()



func edit(bank):
	$Disabled.visible = true
	var p = preload("res://ui/customWaveform/Waveform.tscn").instance()
	add_child(p)
	p.owner = owner
#	p.popup(Rect2(get_global_mouse_position()-Vector2(0,p.rect_size.y), p.rect_size))
	p.rect_position=rect_global_position + Vector2(8,8)
	if p.rect_position.y+p.rect_size.y > get_viewport_rect().size.y:
		p.rect_position.y = get_viewport_rect().size.y - p.rect_size.y - 8
	p.connect("wave_updated", self, "finish_edit", [p])
	p.fetch_table(bank)
	p.show()
func finish_edit(bank, sender):
	$Disabled.visible = false
	
	var c=get_node(chip_loc)
	if bank >=0:  $Items.set_item_icon(bank, make_icon(c.GetWave(bank)))
	sender.queue_free()


func _on_Items_nothing_selected():
	$Items.release_focus()
	$Remove.disabled = true
	$Edit.disabled = true


func _on_Items_item_selected(_index):
	$Remove.disabled = false
	$Edit.disabled = false

func _on_Items_item_activated(index):
	edit(index)
	pass # Replace with function body.


func _on_Sample_size_toggle(size):
	wavetable_size = size
	print ("Sample size changed to ", size)

func _on_Edit_pressed():
	$Edit.release_focus()
	edit($Items.get_selected_items()[0])



