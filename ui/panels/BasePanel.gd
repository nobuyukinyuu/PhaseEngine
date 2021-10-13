extends Control
class_name VoicePanel

export (NodePath) var chip_loc  #Location of PhaseEngine instance in code
export(int,0,8) var operator = 0


func _ready():
	pass # Replace with function body.

func set_from_op(op:int):
	printerr("BasePanel.gd:  set_from_op() not defined for derived panel type! Op", op)
