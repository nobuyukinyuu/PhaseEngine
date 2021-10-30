#tool
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

export(float,0,1) var tl=0.0  setget update_tl #Total level
export(float,0,1) var al=0.0  setget update_al #Attack level
export(float,0,1) var dl=0.0  setget update_dl #Decay level
export(float,0,1) var sl=1.0  setget update_sl #sustain level

export(int, 0, 65535, 1) var Delay=0 setget update_delay
export(int, 0, 63) var Attack=63 setget update_ar
export(int, 0, 65535, 1) var Hold=0 setget update_hold
export(int, 0, 63) var Decay=63 setget update_dr
export(int, 0, 63) var Sustain=63 setget update_sr
export(int, 0, 63) var Release=15 setget update_rr

export(bool) var db_moves = true

var del = 1.0  #Delay
var ar = 1.0  #Between epsilon and 1. lerp between 63-0
var hr = 1.0  
var dr = 1.0  #Ratio of DR to SR should add up to 2.  Map the range on env update.
var sr = 1.0
var rr = 1.0  #Between epsilon and 1.  Lerp between 15-0

var font = preload("res://gfx/fonts/numerics_5x8.tres")


func update_tl(value):
	tl = value #/ 100.0
	update_vol()
func update_al(value):
	al = value #/ 100.0
	update_vol()
func update_dl(value):
	dl = value #/ 100.0
	update_vol()
func update_sl(value):
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
	del = lerp(2, 0, (65535-val)/65536.0) #if val >= 8192 else 0.2
	update_env()
	
func update_ar(val):
	Attack = val
	ar = lerp(1, 0, val/64.0)
	update_env()
func update_hold(val):
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
	for i in 5:
		$ADSR.get_node(str(i)).tl = pow(tl, 0.5)
	
	
	var al2 = pow(al,0.5)
	$ADSR/"0".p2 =  al2	  #Attack level end
	$ADSR/"1".p1 =  al2	  #Hold Level start
	$ADSR/"1".p2 =  al2	  #Hold Level end
	$ADSR/"2".p1 =  al2	  #Decay level start

	var dl2 = pow(dl,0.5)
	$ADSR/"2".p2 =  dl2	  #Decay Level end
	$ADSR/"3".p1 =  dl2	  #Sustain level start

	$ADSR/"3".p2 =  pow(sl, 0.5) if Sustain > 0 else dl2  #Sustain level final

	$ADSR/"4".p1 =  $ADSR/"3".p2						#Release level start
	$ADSR/"4".p2 = $ADSR/"3".p2 if Release == 0 else 1  #Release level end
	
	$ADSR/"0".size_flags_stretch_ratio = ar*vol_diff(1,al)
	$ADSR/"1".size_flags_stretch_ratio = hr
	$ADSR/"2".size_flags_stretch_ratio = dr*vol_diff(al,dl) if Decay>0 else 0
	$ADSR/"3".size_flags_stretch_ratio = sr*vol_diff(dl,sl) if Sustain>0 else 1
	$ADSR/"4".size_flags_stretch_ratio = rr
	
	if $ADSR/"2".size_flags_stretch_ratio==0:
		$ADSR/"2".visible=false
		$ADSR/"3".visible=false
		$ADSR/"4".p1=al2
	else:
		$ADSR/"2".visible=true
		$ADSR/"3".visible=true
		

	$ADSR/Delay.size_flags_stretch_ratio = del
	$ADSR/Delay.visible = Delay > 0

	update_labels()
	
	$ADSR/Spacer.size_flags_stretch_ratio = lerp(2,0, (ar+hr+dr+sr+rr) / 6.0)
	
	update()

func vol_diff(a, b):  	#Provides a level differential to tweak rate widths.
	return max(0.1, abs(a-b) )

func update_labels():
	update_readable_time(Delay, $ADSR/Delay/Label)
	update_readable_time(Hold, $"ADSR/1/Label")
	
	$ADSR/"1".visible = Hold != 0
	$NoPreview.visible = Attack == 0
	

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
	for i in 5:
		var o = $ADSR.get_node(str(i))
		o.thickness = thickness
		o.update()

#Sets all the envelope values at once.  Useful for generating a preview from scratch.
func set_all(a,d,s,r,  tl, al, dl, sl,  delay, hold): #,acurve,dcurve,scurve,rcurve):
	update_ar(a)
	update_dr(d)
	update_sr(s)
	update_rr(r)
	update_delay(delay)
	update_hold(hold)
	
	self.al = al
	self.dl = dl
	self.sl = sl
	self.tl = tl

	
#	ac = acurve
#	dc = dcurve
#	sc = scurve
#	rc = rcurve

#	$ADSR/"0".curve = acurve
#	$ADSR/"2".curve = dcurve
#	$ADSR/"3".curve = scurve
#	$ADSR/"4".curve = rcurve
		
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
	
	var R = $"ADSR/4"
	if R.visible and R.p2 > 0:
#		var xOffset = 4 if R.p1 > 0.1 else 8
		var xOffset = 8
		draw_texture(preload("res://gfx/ui/note_off.png"), Vector2(R.rect_position.x+xOffset, 2), 
				Color(1,1,1,0.5))

#		draw_string(preload("res://gfx/fonts/spelunkid_font.tres"), Vector2(R.rect_position.x+xOffset, 16),
#			"%s, %s" % [R.p1, R.p2])

	if tl > 0:  #Draw the TL decibel meter
		var h=rect_size.y * pow(tl, 0.5)
		var color = Color(1,1,1,0.5)
		var text = str(tl * -global.DB_MAX).pad_decimals(2) + "db"
		var sz = font.get_string_size(text)
		var x = rect_size.x-$ADSR/Spacer.rect_size.x  #Init to end of RR


		#Draw the TL arrow.  First, find the furthest it can point without intersecting the envelope.
		if db_moves and tl > 0.03 and tl < 0.96:
			if rr != 1:  
				x-= $"ADSR/4".rect_size.x - 8
				if al < dl: #Some typical decay phase happens.
					x = $"ADSR/2".rect_position.x + 8  #Decay Start
					if dl > sl and Sustain !=0 and sl < 0.03:  #SL is above DL. Stop at SL end.
						x = $"ADSR/4".rect_position.x + 16 #Release Start
					elif Sustain == 0 and dl <= 0.05:  #Sustain has a long low value.  Stop at DL end.
						x = $"ADSR/4".rect_position.x + 8 #Release Start

			
				elif dl < sl and Sustain > 0:  #Decay actually RAISES.  Sustain's typical and finite, move left
					#...But only if the envelope isn't too squished
					x = $"ADSR/3".rect_position.x + 16  #Sustain start

				#Envelope too squished to reliably avoid intersection.  Move right.
				if tl > 0.75:
					x = rect_size.x-$ADSR/Spacer.rect_size.x
				elif tl > 0.65 and Sustain != 0:
					x = $"ADSR/4".rect_position.x + 16  #Release start

			
			draw_arrow(Vector2(x, h),Vector2(rect_size.x, h), color)
			draw_string(font, Vector2(rect_size.x-sz.x-4, h-sz.y-2), text, color  )
		else:
			draw_string(font, Vector2(rect_size.x-sz.x-4, rect_size.y-sz.y-4), text, color  )

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
