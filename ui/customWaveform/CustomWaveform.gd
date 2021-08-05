extends Control

# warning-ignore:unused_signal
signal value_changed

var wave = Riff.new()
var import_path = ""

func _ready():
	$H/MenuButton.get_popup().theme = $CPMenu.theme
	$H/Banks.get_popup().theme = $CPMenu.theme

	var check = AtlasTexture.new()
	var uncheck = AtlasTexture.new()
	
	check.atlas = preload("res://gfx/ui/radio_check.png")
	uncheck.atlas = preload("res://gfx/ui/radio_check.png")
	uncheck.region = Rect2(0,0,16,16)
	check.region = Rect2(16,0,16,16)
	
	$H/Banks.get_popup().add_icon_override("radio_unchecked", uncheck)
	$H/Banks.get_popup().add_icon_override("radio_checked", check)

	connect("value_changed", self, "_on_value_changed")

	var pop = $H/MenuButton.get_popup()
	pop.connect("id_pressed", self, "_on_menu_item_selected")

func reload():
#	if !global.currentPatch:  return
#	var sz = global.currentPatch.WaveformBankSize
#
#	$H/Banks.clear()
#
#	if sz == 0:
#		print ("CustomWaveform:  Waveform bank size is 0...")
##		add_bank()
#
#	for i in sz:
#		$H/Banks.add_item(str(i), i)
#
	fetch_table()

func fetch_table(index=0):
	if !global.currentPatch:  return
	var input:Dictionary = global.currentPatch.GetWaveformBank(index,false) 
		
	if input:
		$VU.set_table(input.get("values"))
		return input
		
	else:
		print("Waveform: Can't find Patch's custom wavetable bank at %s." % index)

func _on_value_changed(idx, val):
	if !global.currentPatch:  return


func _on_menu_item_selected(index):  #Called when needing to add or remove banks
	match index:
		4:  #Import bank from wave file
			$WaveImport/Dialog.popup_centered()



func _on_CPMenu_index_pressed(index):
	var bank = $H/Banks.selected
	if bank < 0:  return
	
	if index == 0:  #Copy
		global.currentPatch.CopyWaveformBank(bank)
	elif index == 2:  #Paste
		var err = global.currentPatch.PasteWaveformBank(bank, true)
		if err !=0:  print("RTable paste status: ", err)
		fetch_table(bank)

#Wave import file dialog
func _on_Dialog_file_selected(path):
	var f = File.new()
	f.open(path, File.READ)
	f.endian_swap = true  #Set big endian mode

	#Check the format to see if it's valid
	var header = f.get_32()
	if header == 0x52494646:  # "RIFF"
		f.get_32()  #Trash the chunk size
		header =  f.get_32()  #Check if the file is wave....
		if header == 0x57415645: # "WAVE"
			print ("RIFF WAVE")
			
			#Definitely a wave file.  Parse through the header.
			f.get_32()  #"fmt " chunk marker.  Trash.
			f.get_32()  #"fmt " chunk size.  Trash.
			f.endian_swap = false  #Resume little endian mode.
			
			var audio_format = f.get_16()
			if audio_format != 1:  print("WaveImport:  WARNING, non-PCM audio format %s.." % audio_format)
			wave.channels = f.get_16()
			wave.hz = f.get_32()
			
			wave.byteRate = f.get_32()
			wave.bytesPerSample = f.get_16()
			wave.bits = f.get_16()
			
			#Okay, now we should be at the "data" part of the header.  Let's see if we can sap data
			if f.get_32() != 0x61746164:  
				print("WaveImport:  Warning, can't find 'data' chunk at offset %s.." % f.get_position())  #Trash
				
				#Uh oh.  Guess we should've been reading the extra param size bytes?
				#Backup and check.
				f.seek(f.get_position() - 4)
				var extraParamSize = f.get_16()
				
				print("Extra parameter size: ", extraParamSize)
				f.seek(f.get_position() + extraParamSize)
				
				if f.get_32() != 0x61746164: # should read "data" in ascii
					print ("WaveImport: Attempt to seek past extra parameters failed!")
				
			wave.chunkSize = f.get_32()
			wave.dataStartPos = f.get_position()
			
			wave.description = "RIFF Wave"
			_on_btnSquish_pressed()
		else:
			print ("RIFF (Unknown type)")
			wave.description = "RIFF (Unknown type)"
			wave = Riff.new()
	else:
		print ("Raw.....")
		wave = Riff.new()
		wave.chunkSize = f.get_len()
	
	import_path = path
#	prints(f.get_16(), f.get_16(), f.get_16(), f.get_16())
	f.close()

	owner.modulate.a = 0.5
	$WaveImport/Margin/V/Header.text = wave.to_string()
	$WaveImport.popup_centered()
	
class Riff:
	var bits = 8
	var channels = 1
	var hz = 44100
	var data = []
	
	var byteRate = 44100  #Byte rate per second.  Hz*Channels*Bits/8.
	var bytesPerSample = 1  #channels*bits/8.  Number of bytes in one sample.
	var chunkSize = 0  #Size of the data in bytes
	var dataStartPos = 0  #Seek position when deciding where to start striding from.
	
	var description = "(Raw data...)"
	
	func _to_string() -> String:
		var output = "Format:  " + description + "\n"
		output += "Sample Rate: %s, %s-bit\n" % [hz, bits]
		output += "Channels: %s\n" % channels
		output += "Size: %s bytes\n" % chunkSize
		
		return output


func _on_WaveImport_popup_hide():
	owner.modulate.a = 1.0
	pass # Replace with function body.


func _on_WaveImport_about_to_show():
	#Set the default settings based on what we detected from the file.
	if wave.bits == 16:
		#Signed integer is default.
		$WaveImport/Margin/V/chkSigned.pressed = true
		$WaveImport/Margin/V/chkBits.pressed = true
	else:
		#Treat as 8-bit unsigned data.
		$WaveImport/Margin/V/chkSigned.pressed = false
		$WaveImport/Margin/V/chkBits.pressed = false

	if wave.channels == 2:
		$WaveImport/Margin/V/chkStereo.pressed = true
	else:
		$WaveImport/Margin/V/chkStereo.pressed = false




func _on_WaveImport_confirmed():
	var f = File.new()
	f.open(import_path, File.READ)

	#Start reading in based on the stride.
	var pos = 0
	var stride = float($WaveImport/Margin/V/HBoxContainer/txtStride.text)
	
	if stride <= 0:  stride = 1
	for i in 128:
		f.seek(wave.dataStartPos + pos)
		$VU.tbl[i] = readbits(f, $WaveImport/Margin/V/chkBits.pressed, 
								$WaveImport/Margin/V/chkStereo.pressed,
								$WaveImport/Margin/V/chkSigned.pressed)

		pos = fmod(pos+stride, wave.chunkSize)

	f.close()

	#Send the table to the patch.
	for i in 128:
		_on_value_changed(i, $VU.tbl[i])
	$VU.update()

func readbits(f:File, wide=false, stereo=false, signed=true):
	var output = 0
	var width:float = pow(2, 16 if wide else 8) - 1

	output = read(f,signed, wide) / width * 100  #Map 0-100 as rTable expects
	
	if stereo:  
		output += read(f,signed, wide) / width * 100
		output /= 2.0

	return output
	
#Reads a bit, signed or unsigned
func read(f:File, signed=true, wide=true):
	var output = f.get_16() if wide else f.get_8()
	if signed:  #Offset the data value and perform signed conversion..
		var bits = 16 if wide else 8
		var width = pow(2, bits)  #Get the maximum value for an unsigned int at the given bitrate.
		output += width / 2.0
		output = fmod(output,width)
	return output


func _on_btnSquish_pressed():
	$WaveImport/Margin/V/HBoxContainer/txtStride.text = str(wave.chunkSize / 128.0)
