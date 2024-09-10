extends WindowDialog
const MRUD_MAX = 10
const q = "Quick Access"

onready var config = ConfigFile.new()
var mruds = []
var faves = []

var last_window:FileDialog

enum {NONE, FAVES, MRUDS, ALL}

func _ready():
	$V/Faves/List.add_font_override("font", get_font(""))
	$V/MRUDs/List.add_font_override("font", get_font(""))
	
	var loaded = config.load("user://quick_access.cfg")
	
	if loaded == ERR_FILE_NOT_FOUND:  #Create new config
		config.save("user://quick_access.cfg")
	
	elif loaded == OK:  #Load the MRUDs.
		faves = config.get_value(q, "faves", [])
		mruds = config.get_value(q, "mruds")
		
		refresh(ALL)


func add_mrud(path):  add_dir(MRUDS, path, true, MRUD_MAX)
func add_fave(path):  add_dir(FAVES, path)
func add_dir(which_list, name, push_to_front=false, limit=0xFFFF):
	var arr:Array
	match which_list:
		FAVES:
			arr = faves
		MRUDS:
			arr = mruds
		_:
			printerr("QuickSelect.gd:  Invalid list specified to add to!")
			return
			
	#Scan for existing, if so, bring it to the top.
	var existing_index = arr.find(name)
	if existing_index >= 0:
		arr.remove(existing_index)
		arr.push_front(name)

	if push_to_front:  arr.push_front(name)
	else:  arr.push_back(name)

		
	while arr.size() > limit:
		arr.pop_back()

	refresh(which_list)
		
func refresh(what):
	if what & FAVES == FAVES:
		var icon = preload("res://gfx/ui/godot_icons/icon_favorites.svg")
		var l = $V/Faves/List
		l.clear()
		
		for o in faves:
			l.add_item(o, icon)

	if what & MRUDS == MRUDS:
		var l = $V/MRUDs/List
		l.clear()

		for o in mruds:
			var icon = preload("res://gfx/ui/godot_icons/icon_non_favorite.svg")
			if faves.find(o) >= 0:  icon = preload("res://gfx/ui/godot_icons/icon_favorites.svg")
			l.add_item(o, icon)
		

func save():
	config.set_value(q, "faves", faves)
	config.set_value(q, "mruds", mruds)
	config.save("user://quick_access.cfg")


func _on_QuickAccess_visibility_changed():
#	if !visible:  $Timer.stop()
#	else:  $Timer.start()
	pass


func _on_Timer_timeout():
	var p = get_parent()
	if p is WindowDialog:
		rect_position = Vector2(p.rect_position.x + p.rect_size.x, p.rect_position.y)


func _on_List_item_activated(index, which_list):
	if last_window == null:  
		printerr("QuickAccess.gd:  No FileDialog to switch current_dir to!")
		self.hide()
		return
	
	var list:ItemList = $V/Faves/List if which_list==FAVES else $V/MRUDs/List
	var path = list.get_item_text(index)
	
	last_window.current_dir = path
	last_window = null
	self.hide()


func _on_btnRmDir_pressed():
	var selected = $V/MRUDs/List.get_selected_items()
	if selected.empty():  return
	mruds.remove(selected[0])
	$V/MRUDs/List.remove_item(selected[0])

	save()

func _on_btnAddFave_pressed():
	var selected = $V/MRUDs/List.get_selected_items()
	if selected.empty():  return
#	faves.append(selected[0])
#	$V/Faves/List.add_item(mruds[selected[0]], preload("res://gfx/ui/godot_icons/icon_favorites.svg"))
	add_fave(mruds[selected[0]])

	save()

func _on_btnRemove_pressed():
	var selected = $V/Faves/List.get_selected_items()
	if selected.empty():  return
	faves.remove(selected[0])
	$V/Faves/List.remove_item(selected[0])

	save()


func _on_btnUp_pressed():
	var selected = $V/Faves/List.get_selected_items()
	if selected.empty():  return
	if selected[0] == 0:  return

	var text = faves.pop_at(selected[0])
	faves.insert(selected[0] -1, text)
	
	refresh(FAVES)
	$V/Faves/List.select(selected[0] -1)
	save()
	
func _on_btnDown_pressed():
	var selected = $V/Faves/List.get_selected_items()
	if selected.empty():  return
	if selected[0] == $V/Faves/List.get_item_count() -1:  return

	var text = faves.pop_at(selected[0])
	faves.insert(selected[0] +1, text)
	
	refresh(FAVES)
	$V/Faves/List.select(selected[0] +1)
	save()
