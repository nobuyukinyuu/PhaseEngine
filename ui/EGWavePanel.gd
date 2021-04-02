extends Panel

var wave_img = []

func _ready():
	
	for i in 10:
		wave_img.append(load("res://gfx/wave/%s.png" % i))
		$Preview.texture = wave_img[0]
	
	pass


func _on_Wave_value_changed(value):
	$Preview.texture = wave_img[value]
	pass # Replace with function body.
