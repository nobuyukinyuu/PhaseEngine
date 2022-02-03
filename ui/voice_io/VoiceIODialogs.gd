extends Control
class_name VoiceIODialogs, "res://gfx/ui/godot_icons/Save.svg"

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code

signal voice_imported

var last_bank = {}

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


func open():
	$Open.popup_centered()



func _on_Open_file_selected(path):
	var c = get_node(chip_loc)
	if !c:  return

	var list = c.RequestVoiceImport(path)
	
	if !list: 
		OS.alert("Import failed.")
		return
	if list.size() == 0:
		OS.alert("No voices found in this file.")
		return
	elif list.size() == 1:
		#Load this specific voice
		paste(list[0])
		return
		
	#If we got this far, we received a bank of voices.  Prompt for selection.
	last_bank = list
	$BankSelect.populate(list)
	$BankSelect.popup_centered()


func _on_Save_file_selected(path):
	pass # Replace with function body.

#Activated when $BankSelect/List has chosen a voice selection.
func load_bank(idx):
	paste(last_bank[idx])

func paste(data:String):
	var c = get_node(chip_loc)
	if !c:  return

	var err = get_node(chip_loc).PasteJSONData(data)
	if err != OK:
		print("PasteJSONData returned error %s" % err)
		return
	
	#Reinit everything.
	owner.reinit_all()
#	check_if_presets()


