extends MenuButton

enum {AR, DR, SR, RR} #Rate indices
enum {AL, DL, SL, RL, TL} #Level indices
const ALL_LEVELS = 240
const ALL_RATES = 250


var levels_to_fix = [] #Simple list since the only levels we'd need to fix are release level.
var rates_to_fix = []  #Array of arrays.  format:  [OpNum,  RateNum]

onready var s = $Submenu
var icon_adsr = preload("res://gfx/ui/icon_adsr.svg")

var icon_ops = []  #Operator number menu icons.  Set on ready

enum Problems {NONE=0, RL=1, RR=2, AR=4, }
var problem_strings = {
	Problems.RL: "A carrier to output which has an audible release level.",
	Problems.RR: "A carrier to output has a release time of zero (infinite release time)",
	Problems.AR: "An operator has an infinite attack time and will never activate.",
}
var problems = Problems.NONE

func _ready():
	#Add shortcut for the panic button.
	var p = get_popup()
	p.set_item_accelerator(1, KEY_MASK_ALT | KEY_END)
	
	#Add the fix submenu.
	remove_child(s)
	p.add_child(s, true)
#	p.get_node("Submenu").owner = owner
	p.add_submenu_item("Fix...", "Submenu")

	s.connect("index_pressed", self, "_on_Submenu_index_pressed")

	#Generate icons for the fix submenu.
	for i in range(1, 9):
		var o = AtlasTexture.new()
		
#		o.atlas = preload("res://gfx/fonts/numerics_16x20.png")
#		o.region = Rect2(i*16+32,0,16,20)
		o.atlas = preload("res://gfx/fonts/noteFont.png")
		o.region = Rect2(i*10,10,10,10)
		icon_ops.append(o)

func check_for_fixes():
	var c = owner.get_node(owner.chip_loc)
	if !c:  pass

	problems = Problems.NONE
	levels_to_fix.clear()
	rates_to_fix.clear()
	
	
	for i in c.GetOpCount():
		var d = c.GetOpValues(0, i)  #Get the Envelope description from the op.
		var levels = d["levels"]
		var rates = d["rates"]
		
		if levels[RL] != 1023:  #The release level isn't max attenuation. Add to the fix list.
			problems |= Problems.RL
			levels_to_fix.append(i)
			prints("op", i+1, " RL:", levels[RL])
		if rates[AR]==0:  #Attack rate is zero. Add to the fix list.
			problems |= Problems.AR
			rates_to_fix.append( [i, AR] )
		if rates[RR]==0:  #Release rate is zero.  Add to the fix list.
			problems |= Problems.RL
			rates_to_fix.append( [i, RR] )

	visible = !levels_to_fix.empty() or !rates_to_fix.empty()
	if problems != Problems.NONE:  #Add problem list to the tooltip.
		hint_tooltip = "Problems:"
		for i in problem_strings.keys():
			if problems & i == i:  #Houston, we have a problem.
				hint_tooltip += "\n> " + problem_strings[i]
				
		#Add the generic problem message footer.
		hint_tooltip += "\n\nSounds may get stuck and note priorities may not work."
		hint_tooltip += "\nTo silence all notes or check for fixes, click here."

func _on_RLWarning_about_to_show():
	#Generate a list of the issues in each operator and add it to a list.
	check_for_fixes()

	#Rebuild the submenu.
	s.clear()
	s.add_separator("Levels")
	s.add_icon_item(icon_adsr, "All Levels", ALL_LEVELS)
	
	var runcount=10  #We use this to get unique IDs for each custom menu item.
	if not levels_to_fix.empty():
		for i in levels_to_fix:
			s.add_icon_item(icon_ops[i], "Op.%s  %s" % [i+1, "Release Level"], runcount)
			s.set_item_metadata(s.get_item_index(runcount), i)
			runcount += 1
	else:  s.set_item_disabled(s.get_item_index(ALL_LEVELS), true)


	s.add_separator("Rates")
	s.add_icon_item(icon_adsr, "All Rates", ALL_RATES)
	if not rates_to_fix.empty():
		for op in rates_to_fix:
			s.add_icon_item(icon_ops[op[0]], 
				"Op.%s  %s" % [op[0]+1, "Release Rate" if op[1]==RR else "Attack Rate"], runcount)
			s.set_item_metadata(s.get_item_index(runcount), op )
			runcount +=1
	else:  s.set_item_disabled(s.get_item_index(ALL_RATES), true)


func _on_Submenu_index_pressed(index):
	match index:
		ALL_LEVELS:
			print("all levels")
		ALL_RATES:
			print("all rates")
		_:
			#Determine the metadata.
			var v = s.get_item_metadata(index)
			if v is Dictionary:  #This is a rate.
				pass
			else:  #Release level.
				pass
	visible = !levels_to_fix.empty() or !rates_to_fix.empty()

