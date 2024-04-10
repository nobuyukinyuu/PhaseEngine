extends ConfirmationDialog
var alg_icons= []
var alg_custom = preload("res://gfx/ui/alg/al_custom.png")

signal voice_selected

enum {YM2xxx, DX, REFACE}
func _ready():
	# Preload with 4-OP algorithms.  We'll swap this out if we detect DX/SY import
	set_alg_icons()

func set_alg_icons(how=YM2xxx):
	match how:
		YM2xxx:
			alg_icons = [
			preload("res://gfx/ui/alg/al0.png"),
			preload("res://gfx/ui/alg/al1.png"),
			preload("res://gfx/ui/alg/al2.png"),
			preload("res://gfx/ui/alg/al3.png"),
			preload("res://gfx/ui/alg/al4.png"),
			preload("res://gfx/ui/alg/al5.png"),
			preload("res://gfx/ui/alg/al6.png"),
			preload("res://gfx/ui/alg/al7.png"),
			]
		DX:
			alg_icons = [
			preload("res://gfx/ui/alg/dx7/al0.png"),
			preload("res://gfx/ui/alg/dx7/al1.png"),
			preload("res://gfx/ui/alg/dx7/al2.png"),
			preload("res://gfx/ui/alg/dx7/al3.png"),
			preload("res://gfx/ui/alg/dx7/al4.png"),
			preload("res://gfx/ui/alg/dx7/al5.png"),
			preload("res://gfx/ui/alg/dx7/al6.png"),
			preload("res://gfx/ui/alg/dx7/al7.png"),
			preload("res://gfx/ui/alg/dx7/al8.png"),
			preload("res://gfx/ui/alg/dx7/al9.png"),
			preload("res://gfx/ui/alg/dx7/al10.png"),
			preload("res://gfx/ui/alg/dx7/al11.png"),
			preload("res://gfx/ui/alg/dx7/al12.png"),
			preload("res://gfx/ui/alg/dx7/al13.png"),
			preload("res://gfx/ui/alg/dx7/al14.png"),
			preload("res://gfx/ui/alg/dx7/al15.png"),
			preload("res://gfx/ui/alg/dx7/al16.png"),
			preload("res://gfx/ui/alg/dx7/al17.png"),
			preload("res://gfx/ui/alg/dx7/al18.png"),
			preload("res://gfx/ui/alg/dx7/al19.png"),
			preload("res://gfx/ui/alg/dx7/al20.png"),
			preload("res://gfx/ui/alg/dx7/al21.png"),
			preload("res://gfx/ui/alg/dx7/al22.png"),
			preload("res://gfx/ui/alg/dx7/al23.png"),
			preload("res://gfx/ui/alg/dx7/al24.png"),
			preload("res://gfx/ui/alg/dx7/al25.png"),
			preload("res://gfx/ui/alg/dx7/al26.png"),
			preload("res://gfx/ui/alg/dx7/al27.png"),
			preload("res://gfx/ui/alg/dx7/al28.png"),
			preload("res://gfx/ui/alg/dx7/al29.png"),
			preload("res://gfx/ui/alg/dx7/al30.png"),
			preload("res://gfx/ui/alg/dx7/al31.png"),
			]
		REFACE:
			pass  #TODO:  make icons for this and figure out if a format for Reface carts exists


func populate(bank):
	$V/List.clear()
	var firstCheck = true
	for i in bank.size():
#		$V/List.add_item(bank[i], preload("res://gfx/ui/ops/icon_fm.svg"))
		
			
		
		var preset=-1
		var is_valid = true
		var name = "-"

		#TODO:  VALIDATE JSON
		var p = parse_json(bank[i]) if bank[i] != null else null
		if typeof(p) == TYPE_DICTIONARY:  
			var alg:Dictionary = p["algorithm"]
			if alg.has("compatiblePreset"): preset=alg["compatiblePreset"]
			
			if p.has("name"):  name = p["name"]
			
			#Check for validity of a bank.  We ignore banks that have a default name in OPM files, etc.
			if name.ends_with("no Name") or name==("INIT VOICE") and preset == 0:  
				is_valid = false
				preset=-1
		else:  #Probably a null entry in this bank.  Set validity to false.
			is_valid = false

		# On first run, let's determine if we're dealing with 32-preset algorithm formats.
		if firstCheck and is_valid:
			if p["algorithm"].get("opCount", 4) == 6:
				set_alg_icons(DX)
			firstCheck = false

		$V/List.add_item("%s: %s" % [i, name], alg_icons[preset] if preset >=0 else alg_custom, is_valid)
		$V/List.set_item_disabled(i, !is_valid)


func _on_List_item_activated(index):
	emit_signal("voice_selected", index, $V/Normalize.pressed)
	hide()

