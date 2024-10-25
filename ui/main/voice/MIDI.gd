extends Node
#onready var note = owner.get_node("Audio/PreviewNote")

signal note_on
signal note_off
signal pitch_bend

#Default keys for keyboard controls.
const note_names = ["C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-", ]
const note_keys = [
		["q","w","e","r","t","y","u","i","o","p","bracketleft", "bracketright"],
		["a","s","d","f","g","h","j","k","l","semicolon", "Apostrophe", "backslash"],
		["z","x","c","v","b","n","m", "comma" ,"period", "slash"],
	]
var key_to_notenum = {}

func _init():
	var notenum = global.NOTE_A4 -9  #c-4
	for octave in note_keys.size():
		for i in note_keys[octave].size():
			var n = note_names[i] + str(octave)
			var action = InputEventKey.new()
			
			action.scancode = OS.find_scancode_from_string(note_keys[octave][i])
			key_to_notenum[n] = notenum + i + (octave*12) - 12  #Sets the correct note number for this keypress.
			InputMap.add_action(n)
			InputMap.action_add_event(n,action)


func _ready():
	OS.open_midi_inputs()
	
	yield (get_tree(), "idle_frame")
	yield (get_tree(), "idle_frame")
	


func _input(event):
	if event is InputEventMIDI:
		match event.message:
			MIDI_MESSAGE_NOTE_ON:

				print("Pitch (%s%s): %s\nVelocity: %s\n" % [
						note_names[event.pitch%12], event.pitch/12-1, event.pitch, event.velocity])
				emit_signal("note_on", event.pitch, event.velocity)
				owner.get_node("RLWarning").check_for_fixes() #Check to see if we have any stuck note issues.

			MIDI_MESSAGE_NOTE_OFF:
#				$"../Audio".TurnOffNote(event.pitch)
				emit_signal("note_off", event.pitch)

			MIDI_MESSAGE_PITCH_BEND:
				var pitch_amt = (event.pitch+0.5) / 8192.0 - 1
#				prints("P: ", event.pitch ,pitch_amt)
#				prints("P: ", pitch_amt, pow(2.0, pitch_amt * (2/12.0)) )
				emit_signal("pitch_bend", pitch_amt)
				
				

func _process(_delta):
	#Scan for note pressed.
	for octave in note_keys.size():
		for i in note_keys[octave].size():
			var n = note_names[i] + str(octave)
			var notenum = key_to_notenum[n]
			if Input.is_action_just_pressed(n):  #note was pressed or released.
				print(n, " Pitch: %s" % [notenum])
				emit_signal("note_on", notenum, 127)
				owner.get_node("RLWarning").check_for_fixes() #Check to see if we have any stuck note issues.

			elif Input.is_action_just_released(n):
				emit_signal("note_off", notenum)

