extends VoicePanel
class_name LinearPanel, "res://gfx/ui/ops/icon_linear.svg"

var lo=0  #Minimum value of env output
var hi=63 #Max value of env output
var log_scale = false  #Used to configure this window for operating in the log domain.
var using_floats = false #Used to configure this window for floating point operations
var step = 1.0  #Used to snapify floating point units for display purposes

var data=[Vector2.ZERO]  #Intermediate point data.  Vec2 where x=ms, y=0-1.
var cached_display_bounds = {}

func dist_to_time_ruler(control:Control): return $P/TimeRuler.rect_position.y - control.rect_position.y


func _ready():
	get_node("%ZoomBar/Slider").connect("value_changed", $P/TimeRuler, "set_zoom")
	get_node("%Offset").connect("value_changed", $P/TimeRuler, "set_offset")
	

func set_disable_buttons(disabled:bool):  #Enables or disables timeline edit buttons.
	for button in $P/Btn.get_children():
		if not button is Button:  continue
		button.disabled = disabled
