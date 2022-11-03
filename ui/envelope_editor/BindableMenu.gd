extends PopupMenu
var caller  #EGSlider which calls this popup into existence.
enum {COPY=10, PASTE=20, BIND=40, UNBIND=50}

var preview_paste_value = true
enum BindAbilities {NONE=0, ENVELOPE=1, KEY_FOLLOW=2, VELOCITY_TABLE=4}
const MAX_BIND_ABILITIES = 3  #Change this if the number of bind abilites available change in the future

func _ready():
	connect("hide", self, "queue_free")
	set_item_submenu(get_item_index(40), "Bind")
	set_item_submenu(get_item_index(50), "Remove")

	set_item_disabled(get_item_index(50), true)  #Disable the remove menu for now

#	$Bind.set_item_accelerator(0, KEY_F1)
#	$Bind.set_item_accelerator(1, KEY_F2)
#	$Bind.set_item_accelerator(2, KEY_F3)


func setup(sender, bindable = BindAbilities.NONE, panel=null):
	#Restore default font
	restore_fonts()


	if not bindable:  set_non_bindable()  #Remove the bind menu altogether.
	else:  #Enable the menus items with appropriate bind abilities
		# Check if binds already exist for the given entities, to improve UX in the menus later
		# First, we need to make sure the panel the sender is in exists and can check this for us.
		var existing_binds = BindAbilities.NONE
		if panel and panel.has_method("existing_binds"):
			#Attempt to determine the location in the chip the panel is supposed to send bind requests
			var loc = panel.LOC_TYPE_EG  #Most requests are to the EG, so set it default.
			if sender.is_connected("bind_requested", panel, "bindPG"):  loc = panel.LOC_TYPE_PG

			print("Looking for existing binds.....")
			existing_binds = panel.existing_binds(loc, sender.associated_property)

		for i in MAX_BIND_ABILITIES:
			if not bindable>>i & 1:  continue  #Flag not enabled for this bind type.
			$Bind.set_item_disabled(i, false)
			
			#If the bind already exists for the given ability, mark it as such and enable the relevant unbind.
			if (existing_binds>>i & 1):
				set_item_disabled(get_item_index(50), false)  #Re-enable the unbind submenu.
				$Remove.set_item_disabled(i, false)
				$Bind.set_item_checked(i, true)  #Put a checkmark next to the existing bind.
			
			


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


func restore_fonts():
	add_font_override("font", owner.owner.get_font("font"))
	$Bind.add_font_override("font", owner.owner.get_font("font"))
	$Remove.add_font_override("font", owner.owner.get_font("font"))

	add_font_override("font_separator", owner.owner.get_font("font"))
	$Bind.add_font_override("font_separator", owner.owner.get_font("font"))
	$Remove.add_font_override("font_separator", owner.owner.get_font("font"))

	$Bind.rect_size.x = 168
	$Remove.rect_size.x = 136
	

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
			$"..".emit_signal("bind_requested", 0)

		UNBIND:
			$"..".emit_signal("unbind_requested", 0)

#Bind requests by ID type corresponding to the BindAbilities enum
func _on_Bind_id_pressed(id):
	$"..".emit_signal("bind_requested", id)
func _on_Remove_id_pressed(id):
	$"..".emit_signal("unbind_requested", id)
