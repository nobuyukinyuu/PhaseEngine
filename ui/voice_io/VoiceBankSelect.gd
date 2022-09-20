extends ConfirmationDialog
var alg_icons= [
	preload("res://gfx/ui/alg/al0.png"),
	preload("res://gfx/ui/alg/al1.png"),
	preload("res://gfx/ui/alg/al2.png"),
	preload("res://gfx/ui/alg/al3.png"),
	preload("res://gfx/ui/alg/al4.png"),
	preload("res://gfx/ui/alg/al5.png"),
	preload("res://gfx/ui/alg/al6.png"),
	preload("res://gfx/ui/alg/al7.png"),
]
var alg_custom = preload("res://gfx/ui/alg/al_custom.png")

signal voice_selected

func _ready():
#	for i in 128:
#		$V/List.add_item("%s: Instrument %s" % [i,i], preload("res://gfx/ui/ops/icon_fm.svg"))

#	visible = true
	pass


func populate(bank):
	$V/List.clear()
	for i in bank.size():
#		$V/List.add_item(bank[i], preload("res://gfx/ui/ops/icon_fm.svg"))
		
		#TODO:  VALIDATE JSON
		var p = parse_json(bank[i])

		var alg:Dictionary = p["algorithm"]
		var preset=-1
		if alg.has("compatiblePreset"): preset=alg["compatiblePreset"]
		
		var name = "no Name"
		if p.has("name"):  name = p["name"]
		
		#Check for validity of a bank.  We ignore banks that have a default name in OPM files, etc.
		var is_valid = true
		if name.ends_with("no Name") and preset == 0:  
			is_valid = false
			preset=-1
		
		$V/List.add_item("%s: %s" % [i, name], alg_icons[preset] if preset >=0 else alg_custom, is_valid)
		$V/List.set_item_disabled(i, !is_valid)


func _on_List_item_activated(index):
	emit_signal("voice_selected", index, $V/Normalize.pressed)
	hide()

