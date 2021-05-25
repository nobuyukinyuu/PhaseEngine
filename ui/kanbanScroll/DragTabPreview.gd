class_name DragTabPreview
extends Control

func _ready():
	pass

func _exit_tree():
	global.emit_signal("tab_dropped")
