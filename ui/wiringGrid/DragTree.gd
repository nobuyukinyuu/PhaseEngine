extends Control

const spk = preload("res://gfx/ui/icon_speaker.svg")
var tree = {"1": "2,4"}
var font = preload("res://gfx/fonts/NoteFont.tres")

var total_ops = 4 setget set_total_ops
var opsPerLevel = [[],[],[],[]]  #n-elements long where n is the total number of operators
var op:opNode = opNode.new()

enum targets {nothing, output, operator, swap}
const action_str = ["", "//\nOut:" ,">>\nMods", "Swaps\nwith"]
export(targets) var target_type = targets.nothing
var target_id = 0

export (int, 16, 0xFFFF) var tile_size = 32

func set_total_ops(val):
	total_ops = val
	resetLevels()

func _ready():
#	$C.rect_size.x = tile_size * 1.5
	pass
	
func _draw():
	$C.visible = true
	match target_type:
		targets.swap:
			draw_op(op.id, Vector2.ZERO,0, op.connections, Vector2(-tile_size, -tile_size), 0.1)
			draw_box(op.id, Vector2(-tile_size, -tile_size))
			draw_box(target_id, Vector2(-tile_size*1.5, tile_size ))
		targets.operator:
			draw_op(op.id, Vector2.ZERO,0, op.connections, Vector2(-tile_size, -tile_size))
#			draw_box(target_id, Vector2(tile_size * 1.5, -tile_size))
			draw_box(target_id, Vector2(-tile_size*1.5, tile_size ))
		targets.nothing:
			$C.visible = false
			draw_op(op.id, Vector2.ZERO,0, op.connections, -Vector2.ONE * tile_size, 0.4)
			draw_box(op.id, Vector2(-tile_size, -tile_size))
		targets.output:
			draw_op(op.id, Vector2.ZERO,0, op.connections, Vector2(-tile_size, -tile_size), 0.7)
			draw_box(op.id, Vector2(-tile_size, -tile_size))
			var pos = Vector2(-tile_size*1.5, tile_size)
			var half = Vector2.ONE * tile_size / 2
			draw_circle(pos+half, tile_size/2 + 2, ColorN("black", 0.75))
			draw_arc(pos+half, tile_size/2, 0, TAU,24,ColorN("white", 0.5))
			draw_texture(spk,pos+half - spk.get_size()/2)
	$C/Label.text = action_str[target_type]
	resetLevels()


func draw_op(id, pos:Vector2, level=0, connections=[], offset=Vector2.ZERO, alpha=1.0):
	#Consider opsPerLevel to contain arrays of the ops that wer used on each level,
	#Then we can check where to point to them based on position, otherwise we add a free slot
	
	var pos2=pos+offset
	
	draw_box(id, pos2, alpha)


	var half_tile = tile_size / 2
	var qtr_tile = tile_size / 4
	
	for connection in connections:
		var slot_pos:Vector2 = get_slot_pos(connection.id, level) + offset #+ Vector2(pos.x,0)
		var a = pos2 + Vector2(half_tile - qtr_tile*sign(pos2.x-slot_pos.x), qtr_tile/2)
		var b = slot_pos + Vector2(half_tile + qtr_tile*sign(pos2.x-slot_pos.x), tile_size - qtr_tile/2)

		draw_op(connection.id, slot_pos-offset, level+1, connection.connections, offset, alpha)
		draw_arrow(a,b, ColorN("white", alpha))

func get_slot_pos(id, level):
	var output
	var tile_spaced = tile_size + tile_size/4
	var idx = opsPerLevel[level].find(id)
	if idx >=0:
		output = Vector2((idx) * tile_spaced, (level+1) * -tile_spaced)
	else:
		output = Vector2(opsPerLevel[level].size() * tile_spaced, (level+1) * -tile_spaced)
		opsPerLevel[level].append(id)
	return output

func resetLevels():
	opsPerLevel.clear()
	for i in total_ops:
		opsPerLevel.append([])

func draw_arrow(a, b, color=Color(1,1,1,1), width=1.0):
	var arrow_spread= PI/6
	var arrow_length = 4
	var pts:PoolVector2Array
	pts.resize(3)
	pts[1] = a

	var angle = atan2(a.y-b.y, a.x-b.x) + PI
	
	pts[0] = Vector2(a.x + arrow_length*cos(angle+arrow_spread), a.y + arrow_length*sin(angle+arrow_spread))
	pts[2] = Vector2(a.x + arrow_length*cos(angle-arrow_spread), a.y + arrow_length*sin(angle-arrow_spread))


	draw_line(a,b,color,width, true)
	draw_line(a,pts[0],color,width, true)
	draw_line(a,pts[2],color,width, true)

func draw_box(id, pos, alpha=1.0):
	draw_rect(Rect2(pos-Vector2.ONE, Vector2.ONE * (tile_size+2)), ColorN("black", alpha), false)
	draw_rect(Rect2(pos, Vector2.ONE * tile_size), ColorN("black", alpha*0.75), true)
	draw_rect(Rect2(pos, Vector2.ONE * tile_size), ColorN("white", alpha*0.5), false)
	
	var font_pos = Vector2.ONE * tile_size/2 - Vector2(4,4)
	
	draw_string(font, pos + font_pos + Vector2.ONE, str(id+1), ColorN("black", alpha))
	draw_string(font, pos + font_pos, str(id+1), ColorN("white", alpha))


class opNode:
	var id = 0  #Must be nonzero
	var connections = []
