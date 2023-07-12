extends PopupMenu
enum {ID_COPY=80, ID_PASTE=81}

#User setting for whether pasting a different op type changes the target intent or just updates values.
#TODO:  Determine if this should be a global or moved somewhere else....
var force_change_intent=false  


func _ready():
	#Populate the operator types popup menu.
	var lbl=["Nothing", "FM (Default)", "FM (HiQual)", "Filter", "Bitwise Op", "Wave Folder", "Linear"]
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

	#Detect whether we should enable the paste menu!!
	var err = validate_json(OS.clipboard)
	if err:  #Should produce nothing if JSON is valid, otherwise an error
#		print("OpIntentMenu.gd:  Clipboard validation failed; ", err)
		set_item_disabled(get_item_index(ID_PASTE), true)
		return #Early so JSON.parse doesn't throw a stupid error to console

	var result:JSONParseResult = JSON.parse(OS.clipboard)

	#For some reason, CSVs pass JSON validation without enclosing brackets. So, we need to make sure
	#that parsing only produces a Dictionary!  CSV's inexplicably return float
	var desc = result.result

	var is_valid_operator = result.error==OK and desc is Dictionary
	if is_valid_operator: 
		is_valid_operator = desc.has_all( ["envelope", "intent"] )

		#Consider only disabling paste if the intent types are a terrible mismatch...
	if is_valid_operator:
		if desc["intent"] != global.OpIntentNames[intent]:
			#Determine if both are NOT filters.  If one is a filter we disable pasting unless BOTH are.
			var A = intent == global.OpIntent.FILTER
			var B = true if desc["intent"]=="FILTER" else false
			is_valid_operator= A == B
#			is_valid_operator=false
	
	set_item_disabled(get_item_index(ID_PASTE), not is_valid_operator)



func _on_OpType_id_pressed(intent):
	var opTarget = get_meta("id")

	match intent:
		ID_COPY:
			OS.clipboard = get_node(owner.chip_loc).OperatorAsJSONString(opTarget)

		ID_PASTE:
			var old_intent = get_node(owner.chip_loc).GetOpIntent(opTarget)
			get_node(owner.chip_loc).PasteJSONData(OS.clipboard, opTarget)

			var new_intent = get_node(owner.chip_loc).GetOpIntent(opTarget)
			global.emit_signal("op_intent_changed", opTarget, new_intent if force_change_intent else old_intent, self)

		_:  #Some kinda intent setter probably
			print ("Setting Op%s to ID %s..." % [opTarget + 1, intent])
			get_node(owner.chip_loc).SetOpIntent(opTarget, intent)
	
			global.emit_signal("op_intent_changed", opTarget, intent, self)
	
