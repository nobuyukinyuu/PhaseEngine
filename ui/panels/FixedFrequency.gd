extends GridContainer

const note_names = ["A-", "A#", "B-", "C-", "C#", "D-", "D#", "E-", "F-", "F#", "G-", "G#"]
const noteColors = preload("res://gfx/noteColors/5th.png")
const noteFont = preload("res://gfx/fonts/NoteFont.tres")

const ratios = [ #0.71,
	0.78, 0.87, 1, 1.41, 1.57, 1.73, 2, 2.82, 3, 
	3.14, 3.46, 4, 4.24, 4.71, 5, 5.19, 5.65,
	6, 6.28, 6.92, 7, 7.07, 7.85, 8, 8.48, 
	8.65, 9, 9.42, 9.89, 10, 10.38, 10.99, 11,
	11.3, 12, 12.11, 12.56, 12.72, 13, 13.84, 14,
	14.1, 14.13, 15, 15.55, 15.57, 15.7, 16.96, 17.27,
	17.3, 18.37, 18.84, 19.03, 19.78, 20.41, 20.76, 
	21.2, 21.98, 22.49, 23.55, 24.22, 25.95, 34, 
	]

func _ready():
	
	#Populate the preset dropdown with note names
	
	for i in range(128):
		var octave = "-" if i < 12 else floor((i-12)/12)
		var note = note_names[(i+3) % 12]
		
		var tex = AtlasTexture.new()
		tex.atlas = noteColors
		tex.region = Rect2(0,(i+3)%12*16,16,16)
		
		$H/Presets.add_item(note + str(octave), i)
		$H/Presets.set_item_icon(i, tex)
	
	$H/Presets.selected = global.NOTE_A4
	$H/Presets.get_popup().add_font_override("font", noteFont)

	var check = AtlasTexture.new()
	var uncheck = AtlasTexture.new()
	
	check.atlas = preload("res://gfx/ui/radio_check.png")
	uncheck.atlas = preload("res://gfx/ui/radio_check.png")
	uncheck.region = Rect2(0,0,16,16)
	check.region = Rect2(16,0,16,16)
	
	$H/Presets.get_popup().add_icon_override("radio_unchecked", uncheck)
	$H/Presets.get_popup().add_icon_override("radio_checked", check)


func _on_Presets_item_selected(index):
	$Frequency.value = global.periods[index]
	$"H/Fine Tune".value = fmod(global.periods[index],1)
	owner.setFreq(global.periods[index])


func _on_Presets_pressed():
	#Scroll the popup to the active item.  Why is this not built into OptionButton?
	var popup = $H/Presets.get_popup()
	var amt = max($H/Presets.selected * 16, -popup.rect_size.y + owner.rect_size.y)
	popup.rect_position.y -= amt
