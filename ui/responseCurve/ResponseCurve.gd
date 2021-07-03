#tool
extends Panel

enum ranges {rates, velocity, levels}
export (ranges) var intent = ranges.rates

var intent_range_strings= {ranges.rates: "63\n\n\n\n56\n\n\n\n48\n\n\n\n40\n\n\n\n32\n\n\n\n24\n\n\n\n16\n\n\n\n8\n\n\n\n",
					ranges.velocity:  "[]\n\n\n\n87\n\n\n\n75\n\n\n\n62\n\n\n\n50\n\n\n\n37\n\n\n\n25\n\n\n\n12\n\n\n\n",
		"ranges.levels.old": "[}4\n\n\n\n896\n\n\n\n768\n\n\n\n640\n\n\n\n512\n\n\n\n384\n\n\n\n256\n\n\n\n128\n\n\n\n",
				ranges.levels: "127\n\n\n\n112\n\n\n\n96\n\n\n\n80\n\n\n\n64\n\n\n\n48\n\n\n\n32\n\n\n\n16\n\n\n\n",
							}

func set_intent(val):
	intent = val
	if !is_inside_tree():  return
	$lblValue.text = intent_range_strings[val]
	

func _ready():
	set_intent(ranges.velocity)
	pass # Replace with function body.

