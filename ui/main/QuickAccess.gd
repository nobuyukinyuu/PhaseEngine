extends WindowDialog
const MRUD_MAX = 10
const q = "Quick Access"  #Config category name

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
		
		clean(ALL)  #This forces a refresh
#		refresh(ALL)

#Cleans lists of any duplicate entries.
func clean(which):
	var items:Dictionary

	if which & FAVES == FAVES:  	items[FAVES] = faves
	if which & MRUDS == MRUDS:  	items[MRUDS] = mruds

	if items.empty():
		printerr("QuickSelect.gd:  Invalid list specified to clean!")
		return

	var replacements:Dictionary
	for target in items.keys():  #Get each list to be cleaned.
		var arr = items[target]
		var uniques = []

		if arr.empty():  continue
		for i in range(0, arr.size()): #If we can't find the last entry checked in the list, add unique.
			var next_unique = arr[i]
			if OS.get_name() == "Windows" or OS.get_name() == "OSX":  #Case-insensitive filesystem
				next_unique = next_unique.to_lower()
			if find_in(uniques, next_unique)>=0:  continue  #Already have this unique. Move on.
			
			var is_unique = find_in(arr, next_unique, 0.99, i+1)
#			prints("index", i, ":", next_unique, "found at", is_unique)
			if is_unique == -1:  uniques.append(next_unique)
			
			#This means that the final entry in the list wasn't caught by our check earlier. Add it.
			if is_unique == -5:  uniques.append(next_unique)
		if not uniques.empty():  replacements[target] = uniques

	for k in replacements.keys():
		match k:
			FAVES:
#				faves.clear()
#				faves.append_array(replacements[k])
				faves = replacements[k]
			MRUDS:
				print("replace mruds")
#				mruds.clear()
#				mruds.append_array(replacements[k])
				mruds = replacements[k]

	if not replacements.empty():  refresh(which)

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
#	var existing_index = arr.find(name)
	var existing_index = find_in(arr, name)
	if existing_index >= 0:
		arr.remove(existing_index)
		arr.push_front(name)

	if push_to_front:  arr.push_front(name)
	else:  arr.push_back(name)

		
	while arr.size() > limit:
		arr.pop_back()

	refresh(which_list)

#Finds a string in an array
func find_in(arr, s:String, similarity=0.99, start_at=0, end_at=-1):
	if end_at == -1:  end_at = arr.size()

	match OS.get_name():
		"Windows", "UWP":
			s = s.to_lower()

	if start_at >= arr.size():  return -ERR_PARAMETER_RANGE_ERROR
	for i in range(start_at, end_at):
		var a = arr[i]
		match OS.get_name():
			"Windows", "UWP":
				a = a.to_lower()
				
		a = a.trim_suffix("\\").trim_suffix("/")
		s = s.trim_suffix("\\").trim_suffix("/")
		if s.similarity(a) >= similarity:
			return i
	return -1

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
			if find_in(faves, o) >= 0:  icon = preload("res://gfx/ui/godot_icons/icon_favorites.svg")
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
