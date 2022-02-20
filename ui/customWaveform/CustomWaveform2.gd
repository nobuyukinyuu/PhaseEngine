extends Control

# warning-ignore:unused_signal
signal value_changed

var wave = Riff.Wave.new()
var import_path = ""

const font = preload("res://gfx/fonts/numerics_7seg.tres")


func _ready():
	$H/MenuButton.get_popup().theme = $CPMenu.theme
#	$H/Banks.get_popup().theme = $CPMenu.theme

	var check = AtlasTexture.new()
	var uncheck = AtlasTexture.new()
	
	check.atlas = preload("res://gfx/ui/radio_check.png")
	uncheck.atlas = preload("res://gfx/ui/radio_check.png")
	uncheck.region = Rect2(0,0,16,16)
	check.region = Rect2(16,0,16,16)
	
#	$H/Banks.get_popup().add_icon_override("radio_unchecked", uncheck)
#	$H/Banks.get_popup().add_icon_override("radio_checked", check)

	connect("value_changed", self, "_on_value_changed")

	var pop = $H/MenuButton.get_popup()
	pop.connect("id_pressed", self, "_on_menu_item_selected")

	for o in $WaveImport.get_children():
		if !(o is HBoxContainer):  continue
		o.rect_min_size.y = 24
		break


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
#	if !global.currentPatch:  return
	pass


func _on_menu_item_selected(index):  #Called when needing to add or remove banks
	match index:
		4:  #Import bank from wave file
			$WaveImport/Dialog.popup_centered()
		2:  #Smooth waveform
			$SmoothDialog.tbl = $VU.tbl
			$SmoothDialog.popup(Rect2(get_local_mouse_position(), $SmoothDialog.rect_size))
			


func _on_CPMenu_index_pressed(index):
#	var bank = $H/Banks.selected
#	if bank < 0:  return
	
	if index == 0:  #Copy
#		global.currentPatch.CopyWaveformBank(bank)
		pass
	elif index == 2:  #Paste
#		fetch_table(bank)
		pass

#Wave import file dialog
func _on_Dialog_file_selected(path):
	var f = File.new()
	f.open(path, File.READ)

	#Check the format to see if it's valid
	var header = f.get_32()
	if header == 0x46464952:  # "RIFF"
		var EOF = f.get_32()  #When this bullshit is expected to end
		header =  f.get_32()  #Check if the file is wave....
		
		if header == 0x45564157: # "WAVE"
			#Definitely a wave file.  Parse through the header.
			print ("RIFF WAVE")
			
			while f.get_position() < EOF:
				var nextChunkName = f.get_32()  #almost certainly "fmt " chunk marker.
				var chunkSize = f.get_32()  #How far we should seek ahead to get to next chunk?
				
				var start_of_chunk = f.get_position()
				
				var bits = global.swap32(nextChunkName)
				var n = char(
					bits >> 24) + char(
					bits >> 16 & 0xFF) + char(
					bits >> 8 & 0xFF) + char(
					bits & 0xFF)
				prints (n, "chunk:", chunkSize, "bytes")

				match nextChunkName:
					0x20746D66:  #'fmt ' chunk
						var audio_format = f.get_16()
						var desc = "Unknown"
						if Riff.formats.has(audio_format):  desc = Riff.formats[audio_format]
						if audio_format != 1:  
							print("WaveImport:  WARNING, unsupported audio format %s (%s).." % [audio_format, desc])
							OS.alert("WaveImport:  Unsupported audio format %s (%s).." % [audio_format, desc])
						
						wave.description = "RIFF Wave (%s)" % desc
						wave.channels = f.get_16()
						wave.hz = f.get_32()
						
						wave.byteRate = f.get_32() #Avg bytes/sec
						wave.bytesPerSample = f.get_16()  #data block size per sample (8-bit = 1, 16-bit = 2, etc)
						wave.bits = f.get_16()  #Bits per sample.  8-bit / 16-bit etc
						
						#End of a 16-byte format chunk.  If we still have data to seek, seek now.
						if f.get_position() < start_of_chunk + chunkSize: f.seek(start_of_chunk + chunkSize)

					0x61746164:  #'data' chunk.  We did it!  All other chunks can go to heck
						wave.chunkSize = chunkSize
						wave.dataStartPos = f.get_position()
						f.seek_end()
						_on_btnSquish_pressed()

					0x6C766177:  #'wavl' chunk.  The file is cursed.  Abandon hope
						f.seek_end()						
					0x74636166:  #'fact' chunk.
						#Next 4 bytes would specify number of samples per channel, for extended waves.
						#Floating-point formats probably require this chunk but we don't support those
						pass
					0x6C706D73:  #'smpl' chunk
						pass
					0x6C62616C, 0x65746F6E:  #'labl' or 'note' chunk.
						#Next 4 bytes here would be a GUID, then a null-terminated string.
						pass
					_:  #Unrecognized chunk.  Abort?
						print("Warning:  Unrecognized chunk '%s'! %s" % [n, nextChunkName])
						pass
				if f.get_position() < start_of_chunk + chunkSize: f.seek(start_of_chunk + chunkSize)
			#End While

		else:
			print ("RIFF (Unknown type)")
			wave.description = "RIFF (Unknown type)"
			wave = Riff.new()
	else:
		print ("Raw.....")
		wave = Riff.new()
		wave.chunkSize = f.get_len()
	
	import_path = path
	f.close()

#	owner.modulate.a = 0.5
	$WaveImport/Margin/V/Header.text = wave.to_string()
	$WaveImport.popup_centered()
	


func _on_WaveImport_popup_hide():
#	owner.modulate.a = 1.0
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



func _on_Smooth_Apply_pressed():
	$VU.tbl = $SmoothDialog/V/TextureRect.tbl
	$VU.update()
	$SmoothDialog.hide()


func _on_btnSquish_pressed():
	$WaveImport/Margin/V/HBoxContainer/txtStride.text = str(wave.chunkSize / 128.0)


func _on_Fidelity_value_changed(value):
	$VU.fidelity_step = 1.0 / (1<<int(value))
