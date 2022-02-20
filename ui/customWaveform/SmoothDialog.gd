extends PopupDialog

var tbl=[]

var smooth_tbl=[]
var wrap=true

func _ready():
	pass # Replace with function body.


func _on_SmoothDialog_about_to_show():
	smooth()
	pass # Replace with function body.

func smooth():
	smooth_tbl = global.arr_smooth(tbl, $V/Amount.value, wrap)
	
	var val = $"V/Preserve Center".value
	var tmp = []
	var dist:float = 64-val

	if val > 0:
		tmp.append_array(tbl)
		for i in dist:  #Limit dist to half of tbl size if increasing range of the val slider
			var j = tbl.size()-1-i
			var percent = i/dist
			tmp[i] = lerp(smooth_tbl[i], tbl[i], percent)
			tmp[j] = lerp(smooth_tbl[j], tbl[j], percent)
		for i in tbl.size():
			var percent = val/64.0
			tmp[i] = lerp(smooth_tbl[i], tmp[i], percent)
	else:  tmp.append_array(smooth_tbl)
	
	$V/TextureRect.tbl = tmp
	$V/TextureRect.update()
	

func _on_Amount_value_changed(value):
	smooth()
	pass # Replace with function body.


func _on_Wrap_toggled(button_pressed):
	wrap = button_pressed
	smooth()
	pass # Replace with function body.
