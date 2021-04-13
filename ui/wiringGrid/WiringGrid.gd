extends Control

var fnt = preload("res://gfx/fonts/spelunkid_font.tres")
const MAX_OPS = 8

func _ready():
	pass

func _physics_process(delta):
#	update()
	pass

func _on_Add_pressed():
	if $SlotIndicator.total_ops < MAX_OPS:
		$SlotIndicator.total_ops +=1


func _on_Remove_pressed():
	if $SlotIndicator.total_ops > 2:
		$SlotIndicator.total_ops -=1
		
#	#Stupid hack cuz the size goes wacky for some reason
#	yield(get_tree(),"idle_frame")
#	rect_size.y = rect_min_size.y

func _draw():
	var grid = Vector2($SlotIndicator._grid.size(), 0)
	if grid.x > 0: grid.y = $SlotIndicator._grid[0].size()
	for y in grid.y:
		for x in grid.x:
			var pos = Vector2(x,y)
			draw_string(fnt, pos*24 + Vector2(256, 64), str($SlotIndicator.getGridID(pos)))

	for op in $SlotIndicator.ops:
		var s = "%s:  %s" % [op.id, op.gridPos]
		draw_string(fnt, Vector2(256, 24* op.id + 320), s)
