extends PopupMenu
var caller  #EGSlider which calls this popup into existence.
enum {COPY=10, PASTE=20, BIND=40, UNBIND=50}

var preview_paste_value = true

func _ready():
	connect("hide", self, "queue_free")


func setup(sender, bindable=false):
	#Restore default font
	add_font_override("font", owner.owner.get_font("font"))
	if not bindable:  set_non_bindable()
	caller = sender

	if OS.clipboard.is_valid_float() and !owner.disabled:
		set_item_disabled(get_item_index(PASTE), false)

		#Give preview of paste value
		if !preview_paste_value:  return
		var s = OS.clipboard.to_float()
		
#		if !owner is Range:  return
#		if s < owner.min_value or s > owner.max_value:  return
		
		if s != floor(s):  #Decimals.  Reformat to 2 decimal places.
			s = "%.2f" % s
		else:
			s = str(s)
		set_item_text(get_item_index(PASTE), "Paste (%s)" % s)



func set_non_bindable():
	#TODO:  Assign ID to separator and remove group by ID instead of index
	remove_item(5)
	remove_item(4)
	remove_item(3)
	rect_size.y = 56


func _on_PopupMenu_id_pressed(id):
	if not owner is Range:  return
	match id:
		COPY:
			OS.clipboard = str(owner.value)
		PASTE:
			if OS.clipboard.is_valid_float():
				owner.value = OS.clipboard.to_float()
		BIND:
			print("Bind!")
			pass
		UNBIND:
			pass
