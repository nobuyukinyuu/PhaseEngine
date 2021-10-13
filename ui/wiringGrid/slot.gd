#tool
extends PanelContainer
class_name WiringGridSlot

enum opType {NONE, CARRIER, MODULATOR, FILTER, BITWISE, WAVEFOLDER}
export (opType) var slot_type setget set_slot_type

var s_norm = preload("res://ui/wiringGrid/slot.stylebox")
var s_hover = preload("res://ui/wiringGrid/slot_hover.stylebox")
var s_hilight = preload("res://ui/wiringGrid/slot_hilite.stylebox")

var id = -1 
var connections = []
var gridPos = Vector2.ZERO  #for height in modulation priority etc

var dragTree = preload("res://ui/wiringGrid/DragTree.tscn")

signal dropped
signal mid_clicked
signal right_clicked


func set_slot_type(val):
#	print("Setting Slot Type")
	slot_type = val
	match val:
		opType.NONE:
			self_modulate = Color(0.6,0.6,0.6,1)
		opType.CARRIER:
			self_modulate = Color(0,0.5,1,1)
		opType.MODULATOR:
			self_modulate = Color(1,0,0,1)
		opType.FILTER:
			self_modulate = Color(0,1,0.5,1)
		opType.BITWISE:
			self_modulate = Color(0.8,0.8,0.8,1)
		opType.WAVEFOLDER:
			self_modulate = Color(1,0.5,1,1)

func _ready():
	pass

func set_slot(_id, slotType):
	id = _id
	set_slot_type(slotType)
	if _id>=0:
		$Label.text = str(id+1)
	else:
		$Label.text = ""

func reset():
	id = -1
	set_slot_type(opType.NONE)
	$Label.text = ""
	unfocus()
	
func unfocus():
	release_focus()
	change_stylebox(s_norm)


func _on_gui_input(event):
	if event is InputEventMouseButton and event.pressed:
		match event.button_index:
			BUTTON_MIDDLE:
				emit_signal("mid_clicked", gridPos, event.pressed)
			BUTTON_RIGHT:
				emit_signal("right_clicked", gridPos, id)
		if has_focus():  
			change_stylebox(s_hilight)
			$"..".last_slot_focused = int(name)


func change_stylebox(box):
	add_stylebox_override("panel", box)

func _on_mouse_entered():
	change_stylebox(s_hover)

func _on_mouse_exited():
	if has_focus():
		change_stylebox(s_hilight)
	else:
		change_stylebox(s_norm)



func get_drag_data(_position):
	
	set_preview(id)

	return [gridPos]
	pass

func set_preview(source_id, target_type=0, target_id=0):
	if source_id < 0:  return
	var p = dragTree.instance()
	p.total_ops = get_parent_control().total_ops
	p.op.id = source_id
#	p.op.connections = connections
	p.op.connections = $"..".ops[source_id].connections
	set_drag_preview(p)
	p.target_type = target_type
	p.target_id = target_id
	p.update()
	
	
func can_drop_data(_position, data):
	if data is Array and data.size() > 0:
		var last_slot = $"..".get_node(str($"..".last_slot_focused))
#		if id >= 0 and last_slot.id != id:
#			set_preview(last_slot.id, 2, id)
#			print("blp")
#		else: 
#			set_preview(last_slot.id)

		if last_slot == null:
#			printerr("slot.gd:  last_slot_focused is null!")
			return
		var target = $"..".check_target_type(data[0], gridPos)
		set_preview(last_slot.id, target[0], target[1])

		
		return true
	else:  
		return false

func drop_data(position, dropdata):
#	print ("Dropped from ", dropdata, " into ", gridPos)
	emit_signal("dropped", dropdata[0], gridPos)  #Source, dest



