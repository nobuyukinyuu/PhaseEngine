extends PopupMenu
enum {ID_COPY=80, ID_PASTE=81}


func _ready():
	#Populate the operator types popup menu.
	var lbl=["Nothing", "FM (Default)", "Filter", "Bitwise Op", "Wave Folder"]
	add_separator("Operator X")
	
#	$OpType.add_separator()
	for i in range(1, global.OpIntentIcons.size()):
		add_icon_radio_check_item(global.OpIntentIcons[i], lbl[i], i)

	var check = AtlasTexture.new()
	var uncheck = AtlasTexture.new()
	
	check.atlas = preload("res://gfx/ui/radio_check.png")
	uncheck.atlas = preload("res://gfx/ui/radio_check.png")
	uncheck.region = Rect2(0,0,16,16)
	check.region = Rect2(16,0,16,16)
	
	add_icon_override("radio_unchecked", uncheck)
	add_icon_override("radio_checked", check)

	add_separator("Values", 79)
	add_icon_item(preload("res://gfx/ui/godot_icons/icon_action_copy.svg"), "Copy", ID_COPY)
	add_icon_item(preload("res://gfx/ui/godot_icons/icon_action_paste.svg"), "Paste", ID_PASTE)
	set_item_disabled(get_item_index(ID_PASTE), true)
	
	#Connect to global request.  NOTE:  Only one instance of this scene is supported!!
	global.connect("request_op_intent_menu", self, "popup_intent_menu")

	
	
func popup_intent_menu(id):  #Spawns the popup to view/change the operator type.
	#Determine the operator's intent and select the default item.
	var intent = get_node(owner.chip_loc).GetOpIntent(id)
	if intent <=0:
		printerr("OpIntentMenu.gd:  Invalid intent %s returned from operator %s..." % [intent, id])
		return

	for i in get_item_count():
		set_item_checked(i, false)

	set_item_text(0, "Operator %s Type" % (id+1))
	set_item_checked(get_item_index(intent), true)
	popup(Rect2(get_global_mouse_position(), Vector2.ONE))
	set_meta("id", id)

	#TODO:  Detect whether we should enable the paste menu!!


func _on_OpType_id_pressed(intent):
	if intent >= ID_COPY:  return

	var opTarget = get_meta("id")
	print ("Setting Op%s to ID %s..." % [opTarget + 1, intent])
	get_node(owner.chip_loc).SetOpIntent(opTarget, intent)
	
	global.emit_signal("op_intent_changed", opTarget, intent, self)
	
