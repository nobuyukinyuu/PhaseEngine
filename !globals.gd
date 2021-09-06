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
#const TL_MAX=1024.0  #Is actually 1920 in the engine but values over this cause unclamped rollover noise
const L_MAX=1024.0
const R_MAX=64.0
const DB_MAX=48.0

const RTABLE_SIZE = 1024
const RT_MINUS_ONE = 1023

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


#Converts a base64 string first to a raw table of bytes, and then converts every 2 bytes to a short.
func base64_to_table(s:String):
	var bytes = Marshalls.base64_to_raw(s)
	var output = []
	
	for i in range(0, bytes.size(), 2):
		var lo = bytes[i]  #Low byte
		var hi = bytes[i+1] #High byte
		output.append( (hi << 8) | lo )
		
	return output


func tl_to_db(tl:int):
	#Converts the total level of an OP to decibels of attenuation.
	return range_lerp(tl, 0, L_MAX, 0, DB_MAX)



const LFO_SPEED = [
0.026,	0.042,	0.084,	0.126,	0.168,	0.210,	0.252,	0.294,	0.336,	0.372,	0.412,	0.456,	0.505,	0.542,	0.583,	0.626,
0.673,	0.711,	0.752,	0.795,	0.841,	0.880,	0.921,	0.964,	1.009,	1.049,	1.090,	1.133,	1.178,	1.218,	1.259,	1.301,
1.345,	1.386,	1.427,	1.470,	1.514,	1.554,	1.596,	1.638,	1.681,	1.722,	1.764,	1.807,	1.851,	1.891,	1.932,	1.975,
2.018,	2.059,	2.101,	2.143,	2.187,	2.227,	2.269,	2.311,	2.354,	2.395,	2.437,	2.480,	2.523,	2.564,	2.606,	2.648,
2.691,	2.772,	2.854,	2.940,	3.028,	3.108,	3.191,	3.275,	3.362,	3.444,	3.528,	3.613,	3.701,	3.858,	4.023,	4.194,
4.372,	4.532,	4.698,	4.870,	5.048,	5.206,	5.369,	5.537,	5.711,	6.024,	6.353,	6.701,	7.067,	7.381,	7.709,	8.051,
8.409,	8.727,	9.057,	9.400,	9.756,	10.291,	10.855,	11.450,	12.077,	12.710,	13.376,	14.077,	14.815,	15.440,	16.249,	17.100,
17.476,	18.538,	19.663,	20.857,	22.124,	23.338,	24.620,	25.971,	27.397,	28.902,	30.303,	31.646,	33.003,	34.364,	37.037,	39.682,
					]

func lfo_speed_to_secs(val):
	return 1.0 / LFO_SPEED[val]

