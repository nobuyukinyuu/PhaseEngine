tool
extends Slider
class_name Knob, "res://ui/knob/icon_knob.svg"

export(float, 0, 1) var notch=0.0 setget set_notch
export(float, 0.05, 1, 0.05) var thickness = 1.0 setget set_thickness
export(bool) var point_outwards setget set_orientation

export(Vector2) var value_align = Vector2(0.5, 0.5) setget set_value_align


func set_value_align(val):
	value_align = val
	update_alignments()

func update_alignments():
	$lblVal.rect_position = rect_size * value_align
	$lblVal.rect_position -= $lblVal.rect_size / 2.0

func set_notch(val:float):
	notch = val
	adjust(get_node("BG"), "zoom_angle", 1.0-val)

func set_thickness(val:float):
	if val == 0:  return
	thickness = val
	$VP.size.y = 16 / val

func set_orientation(outwards:bool):
	point_outwards = outwards

	$VP/Slider.rect_scale.y = -1.0 if outwards else 1.0
	$VP/Slider.rect_position.y = 16 if outwards else 0
#	adjust("point_outwards", outwards)

#	$VP/Slider.rect_position.y = $VP.size.y - 16 if outwards else 0
#	$VP/Slider.set_anchors_preset(Control.PRESET_BOTTOM_LEFT if outwards else Control.PRESET_TOP_LEFT)

# Called when the node enters the scene tree for the first time.
func _ready():
	
	$BG.texture.viewport_path = $VP.get_path()

	$VP/Slider.theme = theme

	connect("resized", self, "_on_Knob_resized")
	connect("changed", self, "_on_Knob_changed")
	connect("value_changed", self, "_on_Knob_value_changed")
	
	_on_Knob_value_changed(value)

func _on_Knob_resized():
	var m = min(rect_size.x, rect_size.y)
	var BG = get_node("BG")
	BG.rect_size = Vector2(m,m)
	BG.rect_position = rect_size/2.0 - BG.rect_size/2.0
	adjust(BG, "rect_size", BG.rect_size)
	update_alignments()


func adjust(node, what:String, val):
	node.material.set_shader_param(what, val)



func _on_Knob_changed():
	$VP/Slider.min_value = min_value
	$VP/Slider.max_value = max_value
	$VP/Slider.step = step
	$VP/Slider.page = page
	$VP/Slider.value = value
	$VP/Slider.tick_count = tick_count

func _on_Knob_value_changed(val):
	$VP/Slider.value = val
	$lblVal.text = str(val)
