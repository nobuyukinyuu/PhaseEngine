extends PopupPanel
var presets = preload("res://gfx/ui/algorithm_presets.png")
var TILE_SIZE = Vector2.ONE * 64

enum AlgType {FOUR_OP, SIX_OP}
export(AlgType) var intent=AlgType.SIX_OP

func _ready():
	create_context()
	
	

func create_context():
	var list = $VBox/Scroll/ItemList
	
	var origin = Vector2(0, 0x100) if intent==AlgType.SIX_OP else Vector2(0, 64)
	for i in (32 if intent==AlgType.SIX_OP else 12):
		var icon = AtlasTexture.new()
		icon.atlas = presets
		var xy = Vector2(i % 8, int(i / 8))

		icon.region = Rect2(origin + xy*TILE_SIZE, TILE_SIZE)

		list.add_item(str(i+1), icon)
	


func _on_Popup_about_to_show():
	create_context()
	pass # Replace with function body.


func _on_Popup_popup_hide():
	$VBox/Scroll/ItemList.clear()
	pass # Replace with function body.
