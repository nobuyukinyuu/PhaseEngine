extends Control
class_name VoiceIODialogs, "res://gfx/ui/godot_icons/Save.svg"

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code

signal voice_imported

var last_bank = {}

var busy = false #Used to prevent multiple load attempts when OS.Alert is used

onready var q = get_node("%QuickAccess")

func _ready():
	#Convert the chip location to an absolute location!!
	#Otherwise, the value specified in the editor will only be valid for us and none of our children.
	if get_node(chip_loc):
		chip_loc = get_node(chip_loc).get_path()  


	var c = get_node(chip_loc)
	if !c:  return

	#Poll the chip for all supported file types.
	for format in c.GetSupportedFormats():
		$Open.add_filter(format)

	#Connect to the bank selector.
	$BankSelect.connect("voice_selected", self, "load_bank")


	hook_quick_access($Open)
	hook_quick_access($Save)

	#DEBUG:  REMOVE ME
	$Open.current_path = "d:/music/mod/nerd/bambooTracker/"

func open():
	q.rect_position.x = $Open.rect_size.x
	$Open.popup_centered()

func save():
	$Save.popup_centered()

func reopen_bank():
	$BankSelect.popup_centered()


func _on_Open_file_selected(path):
	#Add MRUD to the quick dialog
	q.add_mrud(path.get_base_dir())
	q.save()
	
	var c = get_node(chip_loc)
	if !c:  return

	#When launching a modal dialog, this func gets called multiple times (why?)
	#Addresses this issue: https://github.com/godotengine/godot/issues/37472
	if busy:
		printerr("WARNING:  VoiceIODialogs already busy!")
		return
	busy = true

	var list = c.RequestVoiceImport(path)
	if !list: 
		OS.alert("Import failed.")
		busy = false
		return
	elif list.size() == 0:
		OS.alert("No voices found in this file.")
		busy = false
		return
	elif list.size() == 1:
		#Load this specific voice
		paste(list[0])
		busy = false
		return
		
	#If we got this far, we received a bank of voices.  Prompt for selection.
	last_bank = list
	$BankSelect.populate(list)
	$BankSelect.popup_centered()
	busy = false
	
func _on_Save_file_selected(path):
	pass # Replace with function body.

#Activated when $BankSelect/List has chosen a voice selection.
func load_bank(idx, normalize=false):
	paste(last_bank[idx], normalize)

func paste(data:String, normalize=false):
	var c = get_node(chip_loc)
	if !c:  return

	var err = get_node(chip_loc).PasteJSONData(data)
	if err != OK:
		print("PasteJSONData returned error %s" % err)
		return
	
	if normalize:
		c.NormalizeVoice()
	
	#Reinit everything.
	owner.reinit_all()
	global.emit_signal("wavebanks_reset")
	owner.get_node("WiringGrid").check_if_presets()



func hook_quick_access(dlg:FileDialog):
	#Find the hbox, we're about to get medieval
	for o in dlg.get_children():
		if not o is VBoxContainer:  continue
		var h = o.get_child(0)  #Should be the top hbox.
		var p = preload("res://ui/main/QuickAccessButton.tscn").instance()
		h.add_child(p)
		p.owner = self
		p.connect("pressed", self, "_on_quick_access_pressed", [dlg])


func _on_quick_access_pressed(caller:FileDialog):
	q.last_window = caller
	q.refresh(q.MRUDS)
	q.popup()
