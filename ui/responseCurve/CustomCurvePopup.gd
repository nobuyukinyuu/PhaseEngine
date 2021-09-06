extends PopupDialog
var tbl = []
var offset = Vector2(20, 8)
var mainColor = Color("6fd0f1")

export(bool) var invert=false
signal changed
var dirty=false

func _ready():
	tbl.resize(128)
	for i in tbl.size():
		tbl[i] = i * 8
	if invert:  tbl.invert()

	$Curve.connect("value_changed", self, "curve_changed")
	connect("hide", self, "about_to_hide")
	
#	visible = true

func _gui_input(ev):
	if ev is InputEventMouseButton and ev.pressed and ev.button_index == BUTTON_LEFT:
		invert = !invert
		curve_changed($Curve.value)
		dirty = true

func _draw():
	for i in tbl.size()-1:
		var a = Vector2(i, 128-scaleTbl(i)) + offset
		var b = Vector2(i+1, 128-scaleTbl(i+1)) + offset
		draw_line(a,b, mainColor,1.0, true)

func scaleTbl(idx:int):
	return tbl[idx] / 8.0

func curve_changed(val):
	for i in tbl.size():
		if invert:
			tbl[i] = int(ease((127-i)/127.0, val) * global.RT_MINUS_ONE)
		else:
			tbl[i] = int(ease(i/127.0, val) * global.RT_MINUS_ONE)

	dirty=true
	update()


func about_to_hide():
	if dirty:
		emit_signal("changed", tbl)
		dirty = false


