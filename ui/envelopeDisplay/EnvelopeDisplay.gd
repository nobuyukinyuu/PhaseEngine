tool
class_name EnvelopeDisplay
extends Panel

export (float, 0.5, 5, 0.1) var thickness = 1 setget update_thickness
#export (float, -2, 5, 0.1) var ac=-2 setget update_ac
#export (float, -2, 5, 0.1) var dc=-1 setget update_dc 
#export (float, -2, 5, 0.1) var sc= 1 setget update_sc
#export (float, -2, 5, 0.1) var rc= 2 setget update_rc
var ac=-2 setget update_ac
var dc=-1 setget update_dc 
var sc= 1 setget update_sc
var rc= 2 setget update_rc

export(float,0,1) var tl=1.0  setget update_tl #Total level
export(float,0,1) var sl=1.0  setget update_sl #sustain level

export(int, 0, 65535, 1) var Delay=0 setget update_delay
export(int, 0, 63) var Attack=63 setget update_ar
export(int, 0, 65535, 1) var Hold=0 setget update_hr
export(int, 0, 63) var Decay=63 setget update_dr
export(int, 0, 63) var Sustain=63 setget update_sr
export(int, 0, 63) var Release=15 setget update_rr

var dl = 1.0
var ar = 1.0  #Between epsilon and 1. lerp between 63-0
var hr = 1.0  
var dr = 1.0  #Ratio of DR to SR should add up to 2.  Map the range on env update.
var sr = 1.0
var rr = 1.0  #Between epsilon and 1.  Lerp between 15-0

func update_tl(value):
#	if value >= 1:  
#		value = log(value)/log(10) / 2
#		tl = value
#	else:
	tl = value #/ 100.0
		
	update_vol()
func update_sl(value):
#	if value >= 1:  
#		var lin = log(value)/log(10) / 2
#		value = lerp(lin, value / 100.0, 0.5)
#		sl = value
#	else:
	sl = value #/ 100.0
	update_vol()

func update_ac(val):
	var n = $ADSR/"0"
	n.curve = val
func update_dc(val):
	if val!=0:  val = 1/val
	var n = $ADSR/"2"
	n.curve = val
func update_sc(val):
	if val!=0:  val = 1/val
	var n = $ADSR/"3"
	n.curve = val
func update_rc(val):
	if val!=0:  val = 1/val
	var n = $ADSR/"4"
	n.curve = val
	

func update_delay(val):
	Delay = val
	dl = lerp(2, 0, (65535-val)/65536.0) #if val >= 8192 else 0.2
	update_env()
	
func update_ar(val):
	Attack = val
	ar = lerp(1, 0, val/64.0)
	update_env()
func update_hr(val):
	Hold = val
	hr = lerp(2, 0, (65535-val)/65536.0) if val >= 8192 else 0.2
	update_env()
func update_dr(val):
	Decay = val
	dr = lerp(2, 0, val/64.0) 
	update_env()
func update_sr(val):
	Sustain = val
	sr = lerp(1, 0, val/64.0) 
	update_env()
func update_rr(val):
	Release = val
	rr = lerp(1, 0, val/64.0) 
	update_env()

##Calculate ratio between decay and sustain
#func update_dsr():
#	var d = lerp(1, 0, Decay/64.0)  +0.1
#	var s = lerp(1, 0, Sustain/64.0) +1.0
#
#	dr = range_lerp(d, 0, d+s, 0, 1)
#	sr = range_lerp(s, 0, d+s, 0, 1)
#	update_env()
	

func update_env():
	if !self.is_inside_tree():  return
	$ADSR/"0".tl = tl		  #Attack level end
	$ADSR/"1".tl = tl		  #Hold Level start
	$ADSR/"1".sl = 0		  #Hold Level end
	$ADSR/"2".tl = tl		  #Decay level start

	var sl2 = sl * tl
	$ADSR/"2".sl = 1.0-sl	  #Decay Level end
	$ADSR/"3".tl = sl2	 #Sustain level start
	
	var rl= sl
	
	$ADSR/"3".sl = 1.0-sr*sr   #Sustain level final
	$ADSR/"4".tl = sr*sr*sl*tl   #Release level start

	if Release < 1:
		$ADSR/"4".sl = 0.5-(rr/2.0)  #Release level end  (Lerp between 0 and rlev at rrate)
	else:
		$ADSR/"4".sl = 1  #Release level end  (Lerp between 0 and rlev at rrate)		
	
	$ADSR/"0".size_flags_stretch_ratio = ar
	$ADSR/"1".size_flags_stretch_ratio = hr
	$ADSR/"2".size_flags_stretch_ratio = dr if Decay>0 else 0
	$ADSR/"3".size_flags_stretch_ratio = sr
	$ADSR/"4".size_flags_stretch_ratio = rr
	
	if $ADSR/"2".size_flags_stretch_ratio==0:
		$ADSR/"2".visible=false
		$ADSR/"0".tl=sl*tl
		$ADSR/"1".tl=sl*tl
	else:
		$ADSR/"2".visible=true
		

	$ADSR/Delay.size_flags_stretch_ratio = dl
	$ADSR/Delay.visible = Delay > 0

	update_labels()
	
	$ADSR/Spacer.size_flags_stretch_ratio = lerp(2,0, (ar+hr+dr+sr+rr) / 6.0)
	
	update()

func update_labels():
#	if Delay >= 1000:
#		$ADSR/Delay/Label.text = str(Delay/1000.0).pad_decimals(1) + "s"
#	else:
#		$ADSR/Delay/Label.text = str(Delay) + "ms"
	
	update_readable_time(Delay, $ADSR/Delay/Label)
	update_readable_time(Hold, $"ADSR/1/Label")
	
	$ADSR/"1".visible = Hold != 0
	$NoPreview.visible = Attack == 0
	
#	if Hold >= 1000:
#		$ADSR/"1"/Label.text = str(Hold/1000.0).pad_decimals(1) + "s"
#	else:
#		$ADSR/"1"/Label.text = str(Hold) + "ms"

func update_readable_time(val, label:Label):
	var t = delay_frames_to_time(val)
	if t >= 1:
		label.text = str(t).pad_decimals(1) + "s"
	else:
		label.text = str(int(t * 1000)) + "ms"
	
func delay_frames_to_time(nFrames:int):  #Converts a delay/hold value into its time in seconds.
	return (nFrames<<2) / (global.mixRate/3)


func update_vol():
	if !self.is_inside_tree():  return
	for o in $ADSR.get_children():
		o.update()
	update_env()

func update_thickness(val):
	thickness = val
	
	if !self.is_inside_tree():  return
	for i in 4:
		var o = $ADSR.get_node(str(i))
		o.thickness = thickness
		o.update()


func set_all(a,d,s,r, svol,vol, acurve,dcurve,scurve,rcurve):
	update_ar(a)
	update_dr(d)
	update_sr(s)
	update_rr(r)
	
	sl = svol / 100.0
	tl = vol / 100.0
	
	ac = acurve
	dc = dcurve
	sc = scurve
	rc = rcurve
	
	
	$ADSR/"0".curve = acurve
	$ADSR/"2".curve = dcurve
	$ADSR/"3".curve = scurve
	$ADSR/"4".curve = rcurve
		
#	update_env()
	update_vol()

func _ready():
#	update_env()
	update_vol()


func _on_EnvelopeDisplay_resized():
	update_labels()
	pass # Replace with function body.
	

func _draw():
	var col = ColorN("white", 0.45)
	var PRECISION = 20
	var y = rect_size.y / 1.75 + 8
	if $ADSR/Delay.visible:
		draw_line(Vector2(4, y), Vector2($ADSR/Delay.rect_size.x+4, y), col)
		draw_line(Vector2(4, y-4), Vector2(4, y+4), col)
		draw_line(Vector2($ADSR/Delay.rect_size.x+4, y-4), Vector2($ADSR/Delay.rect_size.x+4, y+4), col)

		var h = rect_size.y/(PRECISION*2)
		for i in range(0, PRECISION, 2):
			
			draw_line(Vector2($ADSR/Delay.rect_size.x+4, h*i), Vector2($ADSR/Delay.rect_size.x+4, h*(i+1)), col,0.5,false)

	if $"ADSR/1".visible:
		var x = 4 + $"ADSR/1".rect_position.x
		draw_line(Vector2(x, y), Vector2($"ADSR/1".rect_size.x+x, y), col)
		draw_line(Vector2(x, y-4), Vector2(x, y+4), col)
		draw_line(Vector2($"ADSR/1".rect_size.x+x, y-4), Vector2($"ADSR/1".rect_size.x+x, y+4), col)
		
