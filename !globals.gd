extends Node

# RBJ filter types
enum FilterType {NONE, LOWPASS, HIPASS, BANDPASS_CSG, BANDPASS_CZPG, NOTCH, ALLPASS, PEAKING, LOWSHELF, HISHELF}
const FilterNames = ["None", "Low Pass", "High Pass", "Bandpass (Skirt Gain)", "Bandpass (0dB Peak)", 
						"Notch", "All-pass", "Peaking" , "Low Shelf", "High Shelf"]

const mixRate = 48000.0  #Also set in Constants.cs for the c# backend.  This is used for UI calculations only
const NOTE_A4 = 69
const OPERATOR_TAB_GROUP = 8

var periods = []  #Size 128 +1

signal tab_dropped  #Emitted by a tab drop preview to signal columns to check their dirty state

func _ready():
	# Generate the period frequencies of every note based on center tuning (A-4) at 440hz
	# Calculated from the equal temperment note ratio (12th root of 2).
	periods.clear()
	periods.resize(129)  #Extra field accounts for G#9
	for i in periods.size():
		periods[i] = 440.0 * pow(2, (i-NOTE_A4) / 12.0 )
	

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
