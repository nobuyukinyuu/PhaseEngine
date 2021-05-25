extends Node
#onready var note = owner.get_node("Audio/PreviewNote")

signal note_on
signal note_off
signal pitch_bend

#Default keys for keyboard controls.
const note_names = ["C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#", "A-", "A#", "B-", ]
const note_keys = [
		["q","w","e","r","t","y","u","i","o","p","braceleft", "braceright"],
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

#				$"../Audio".AddNote(event.pitch, event.velocity, self)
#				print("Pitch: %s\nVelocity: %s\nPressure: %s\n" % [event.pitch, event.velocity, event.pressure])
				emit_signal("note_on", event.pitch, event.velocity)

			MIDI_MESSAGE_NOTE_OFF:
#				$"../Audio".TurnOffNote(event.pitch)
				emit_signal("note_off", event.pitch)
				owner.get_node("Audio").TurnOffNote(event.pitch)
				

			MIDI_MESSAGE_PITCH_BEND:
				var pitch_amt = (event.pitch+0.5) / 8192.0 - 1
#				prints("P: ", event.pitch ,pitch_amt)
#				prints("P: ", pitch_amt, pow(2.0, pitch_amt * (2/12.0)) )
				emit_signal("pitch_bend", pitch_amt)
				
				

func _process(delta):
	#Scan for note pressed.
	for octave in note_keys.size():
		for i in note_keys[octave].size():
			var n = note_names[i] + str(octave)
			var notenum = key_to_notenum[n]
			if Input.is_action_just_pressed(n):  #note was pressed or released.			
#				$"../Audio".AddNote(notenum, 127, self)
				print(n, " Pitch: %s" % [notenum])
				emit_signal("note_on", notenum, 127)
					
			elif Input.is_action_just_released(n):
				emit_signal("note_off", notenum)
#				owner.get_node("Audio").TurnOffNote(notenum)


