extends Control
class_name VoiceIODialogs, "res://gfx/ui/godot_icons/Save.svg"

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code

signal voice_imported

var last_bank = {}

var busy = false #Used to prevent multiple load attempts when OS.Alert is used

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

	#DEBUG:  REMOVE ME
	$Open.current_path = "d:/music/mod/nerd/bambooTracker/"

func open():
	$Open.popup_centered()

func save():
	$Save.popup_centered()

func reopen_bank():
	$BankSelect.popup_centered()


func _on_Open_file_selected(path):
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
	owner.get_node("WiringGrid").check_if_presets()




