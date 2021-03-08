extends Label

var json_string : String = "[0,8,8,0]"
var json_array : Array

func _ready():
	json_array = JSON.parse(json_string).result
	print (json_array.count(8.0)) # expected result is 2, returns 0
	
	pass
