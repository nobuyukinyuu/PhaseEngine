extends Node

# RBJ filter types
enum FilterType {NONE, LOWPASS, HIPASS, BANDPASS_CSG, BANDPASS_CZPG, NOTCH, ALLPASS, PEAKING, LOWSHELF, HISHELF}
const FilterNames = ["None", "Low Pass", "High Pass", "Bandpass (Skirt Gain)", "Bandpass (0dB Peak)", 
						"Notch", "All-pass", "Peaking" , "Low Shelf", "High Shelf"]

const mixRate = 48000.0  #Also set in Constants.cs for the c# backend.  This is used for UI calculations only
const NOTE_A4 = 69
const note_names = ["A-", "A#", "B-", "C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#"]
const OPERATOR_TAB_GROUP = 8

var periods = []  #Size 128 +1

var waves=[0,2,1,3,5,6,7, "8a", 8, 9]  #Make sure this matches the waveFuncs list in Oscillator.cs
var wave_img = []

#Consts for defining TL->Decibel calcs and the like
const TL_MAX=2048.0  #Is actually 1920 in the engine but values over this cause unclamped rollover noise
const L_MAX=1024.0
const R_MAX=64.0
const DB_MAX=48.0

signal tab_dropped  #Emitted by a tab drop preview to signal columns to check their dirty state
signal window_resized
signal op_tooltip_needs_data

func _ready():
	# Generate the period frequencies of every note based on center tuning (A-4) at 440hz
	# Calculated from the equal temperment note ratio (12th root of 2).
	periods.clear()
	periods.resize(129)  #Extra field accounts for G#9
	for i in periods.size():
		periods[i] = 440.0 * pow(2, (i-NOTE_A4) / 12.0 )

	for i in waves.size():
		wave_img.append(load("res://gfx/wave/%s.png" % waves[i]))


func arr_replace(arr:Array, a, b):
	var idx = arr.find(a)
	if idx >= 0:
		arr[idx] = b
		return true
	return false

func arr_remove_all(arr:Array, item):
	while arr.has(item):
		var idx = arr.find(item)
		arr.remove(idx)

func delay_frames_to_time(nFrames:int):  #Converts a delay/hold value into its time in seconds.
	return (nFrames<<2) / (mixRate/3)

func note_name(midi_note:int):
	if midi_note < 0 or midi_note >= 128:
		return ""

	var octave = "" if midi_note < 12 else floor((midi_note-12)/12)
	var note = note_names[(midi_note+3) % 12]
		
	return note + str(octave)


func tl_to_db(tl:int):
	#Converts the total level of an OP to decibels of attenuation.
	return range_lerp(tl, 0, TL_MAX, 0, DB_MAX)
	
