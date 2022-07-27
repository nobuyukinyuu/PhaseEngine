extends Node

var formats = {
	0: "Microsoft Unknown Wave Format",
	1: "Microsoft PCM Format",
	2: "Microsoft ADPCM Format",
	3: "IEEE Float",
	4: "Compaq Computer's VSELP",
	5: "IBM CVSD",
	6: "Microsoft ALAW",
	7: "Microsoft MuLAW",
	16: "OKI ADPCM",
	17: "Intel's DVI ADPCM",
	18: "Videologic's MediaSpace ADPCM",
	19: "Sierra ADPCM",
	20: "G.723 ADPCM",
	21: "DSP Solution's DIGISTD",
	22: "DSP Solution's DIGIFIX",
	23: "Dialogic OKI ADPCM",
	24: "MediaVision ADPCM",
	25: "HP CU",
	32: "Yamaha ADPCM",
	33: "Speech Compression's Sonarc",
	34: "DSP Group's True Speech",
	35: "Echo Speech's EchoSC1",
	36: "Audiofile AF36",
	37: "APTX",
	38: "AudioFile AF10",
	39: "Prosody 1612",
	40: "LRC",
	48: "Dolby AC2",
	49: "GSM610",
	50: "MSNAudio",
	51: "Antex ADPCME",
	52: "Control Res VQLPC",
	53: "Digireal",
	54: "DigiADPCM",
	55: "Control Res CR10",
	56: "NMS VBXADPCM",
	57: "Roland RDAC",
	58: "EchoSC3",
	59: "Rockwell ADPCM",
	60: "Rockwell Digit LK",
	61: "Xebec",
	64: "Antex Electronics G.721",
	65: "G.728 CELP",
	66: "MSG723",
	80: "MPEG",
	82: "RT24",
	83: "PAC",
	85: "MPEG Layer 3",
	89: "Lucent G.723",
	96: "Cirrus",
	97: "ESPCM",
	98: "Voxware",
	99: "Canopus Atrac",
	100: "G.726 ADPCM",
	101: "G.722 ADPCM",
	102: "DSAT",
	103: "DSAT Display",
	105: "Voxware Byte Aligned",
	112: "Voxware AC8",
	113: "Voxware AC10",
	114: "Voxware AC16",
	115: "Voxware AC20",
	116: "Voxware MetaVoice",
	117: "Voxware MetaSound",
	118: "Voxware RT29HW",
	119: "Voxware VR12",
	120: "Voxware VR18",
	121: "Voxware TQ40",
	128: "Softsound",
	129: "Voxware TQ60",
	130: "MSRT24",
	131: "G.729A",
	132: "MVI MV12",
	133: "DF G.726",
	134: "DF GSM610",
	136: "ISIAudio",
	137: "Onlive",
	145: "SBC24",
	146: "Dolby AC3 SPDIF",
	151: "ZyXEL ADPCM",
	152: "Philips LPCBB",
	153: "Packed",
	256: "Rhetorex ADPCM",
	257: "BeCubed Software's IRAT",
	273: "Vivo G.723",
	274: "Vivo Siren",
	291: "Digital G.723",
	512: "Creative ADPCM",
	514: "Creative FastSpeech8",
	515: "Creative FastSpeech10",
	544: "Quarterdeck",
	768: "FM Towns Snd",
	1024: "BTV Digital",
	1664: "VME VMPCM",
	4096: "OLIGSM",
	4097: "OLIADPCM",
	4098: "OLICELP",
	4099: "OLISBC",
	4100: "OLIOPR",
	4352: "LH Codec",
	5120: "Norris",
	5121: "ISIAudio",
	5376: "Soundspace Music Compression",
	8192: "AC3 DVM"
}

class Wave:
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
