extends PopupPanel
var presets = preload("res://gfx/ui/algorithm_presets.png")
var TILE_SIZE = Vector2.ONE * 64

enum AlgType {FOUR_OP=4, SIX_OP=6}
export(AlgType) var intent=AlgType.SIX_OP

const alg_descs = {4: [
						"Four serial connections.",
						"One carrier and one modulator serially connected\nto dual modulators at the top of the stack.",
						"One carrier with dual modulators.\nOne modulator is stacked with another operator.",
						"One carrier with dual modulators.\nBoth modulators are stacked by a third modulator.",
						"Three parallel modulators on one carrier.",
						"Two carriers; one is stacked with 2 serial modulators.",
						"Two carriers; both are stacked by the same modulator chain.",
						"Two carriers with one modulator each.",
						"Three parallel carriers sharing a common modulator.",
						"Three parallel carriers, with two sharing a common modulator.",
						"Three parallel carriers, with one having a modulator.",
						"Four parallel carriers.  (Additive sine synthesis)",
]
}

func _ready():
#	create_context()
	pass
	
	

func create_context():
	var list = $VBox/Scroll/ItemList
	
	var origin = Vector2(0, 0x100) if intent==AlgType.SIX_OP else Vector2(0, 64)
	for i in (32 if intent==AlgType.SIX_OP else 12):
		var icon = AtlasTexture.new()
		icon.atlas = presets
		var xy = Vector2(i % 8, int(i / 8))

		icon.region = Rect2(origin + xy*TILE_SIZE, TILE_SIZE)

		list.add_item(str(i+1), icon)
		if intent==AlgType.FOUR_OP:
			list.set_item_tooltip(i, "Algorithm %s: \n%s" % [ i, alg_descs[4][i] ])


func _on_Popup_about_to_show():
	create_context()
	pass # Replace with function body.


func _on_Popup_popup_hide():
	$VBox/Scroll/ItemList.clear()
	pass # Replace with function body.
