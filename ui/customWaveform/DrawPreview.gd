extends TextureRect

var tbl = []
const full16 = preload("res://gfx/ui/vu/full16.png")
const indicator = preload("res://gfx/ui/vu/indicator16.png")

var elementWidth = 5  #How wide is a column?

export(int,1,255) var maxValue = 100

var cursor = preload("res://gfx/ui/vu/cursor.png").get_data()
var numbers = preload("res://gfx/fonts/small_numerics_thin.png").get_data()
var cursor_img = Image.new()
var cursor_texture=ImageTexture.new()
var custom_texture=ImageTexture.new()

#Held values to allow keyboard tweaking.
var last_column = 0
var last_value = 0

enum {NO, PROCESS_LEFT, PROCESS_MID, PROCESS_RIGHT, PROCESS_MIDDRAG, PROCESS_MIDUP}  #For input events


var last_x=0   #Position to lock X to when holding shift.

var lineA=Rect2()  #Line positions for line-drawing (midclick)
var lineB=Rect2()  #Position is real x/y, size is table array pos + value



func _ready():
#	elementWidth = texture.get_width()
	if elementWidth == null:  elementWidth = 5

	for i in 128:
		tbl.append(50)

	cursor_img.create(24,16,false,Image.FORMAT_RGBA8)
	var tex = ImageTexture.new()

	cursor_img.lock()
	cursor_img.blit_rect(cursor,Rect2(Vector2.ZERO, cursor.get_size()),Vector2.ZERO)
	cursor_img.unlock()
#	set_cursor("01.9")
	cursor_texture.create_from_image(cursor_img,0)
	custom_texture.create_from_image(cursor_img,0)
#	cursor_texture = tex

	owner.connect("gui_input", self, "owner_input")
#	owner.get_node("Panel").connect("gui_input", self, "owner_input")


func _gui_input(event):	
	
	var process = NO
	if event is InputEventMouseMotion:
		if Input.is_mouse_button_pressed(BUTTON_LEFT): process = PROCESS_LEFT
		if Input.is_mouse_button_pressed(BUTTON_MIDDLE): process = PROCESS_MIDDRAG
	if event is InputEventMouseButton: 
		if event.button_index == BUTTON_LEFT and event.pressed: 
			process = PROCESS_LEFT
			last_x = get_local_mouse_position().x
		if event.button_index == BUTTON_MIDDLE:
			if event.pressed:  
				process = PROCESS_MID  
			else:
				process = PROCESS_MIDUP
		if event.button_index == BUTTON_RIGHT and !event.pressed:
			#Popup copipe menu
			process = PROCESS_RIGHT
			var pop:PopupMenu = owner.get_node("CPMenu")
			pop.popup(Rect2(get_global_mouse_position(), pop.rect_size))
	
	if process == PROCESS_LEFT:
		var locking = Input.is_key_pressed(KEY_SHIFT)
		var xy = get_table_pos(locking)
		var arpos = xy.x
		var val = xy.y

		var vol = maxValue * (val/100.0)
		
		#Generate a cursor to help user set proper map val
		set_cursor(String(vol))
		Input.set_custom_mouse_cursor(cursor_texture,0,Vector2(0,0)) #Temporarily blank to update
		Input.set_custom_mouse_cursor(custom_texture,0,Vector2(0,0))


		tbl[arpos] = val  #Array position set to value.

		#Determine grouping.  Ideally we'd lerp the values between the one the user selected
		#and the values next to it on the VU meter display.  Rudimentary set is also fine..
		var numElements = int(rect_size.x / elementWidth)
		var groupWidth = numElements / float(tbl.size())  #Value used to stepify between 1/(arraySize) to 1/(VisualElements)
		var startPos = int(arpos * groupWidth) * (1/groupWidth)  #Stepified position.
#		prints("Elements:", numElements, "groupWidth:", groupWidth, "startPos:",startPos)
#

		#Interpolation methods.
		for i in range(startPos, min(tbl.size(), startPos+ (1/groupWidth))):
#				print(i)
			tbl[i] = val
			owner.emit_signal("value_changed", i, val)

		update()
		return
	else:
		Input.set_custom_mouse_cursor(null)

	match process:
		PROCESS_MID:  #Start drawing a line.
			var xy = get_table_pos()
			lineA.position = get_local_mouse_position()
			lineA.size = xy
		PROCESS_MIDDRAG:
			lineB.position = get_local_mouse_position()
			update()
			pass
		PROCESS_MIDUP:
			process_line()


func owner_input(event):
	if event is InputEventMouseButton: 
		if event.button_index == BUTTON_MIDDLE and !event.pressed: 
			process_line()
	

func process_line():
	lineB.position.x = clamp(lineB.position.x, 0, rect_size.x)
	lineB.position.y = clamp(lineB.position.y, 0, rect_size.y)
	
	lineB.size = get_table_pos(false, lineB.position)
	

	#Get start and end values and flip them if the end position was less than the start.	
	var startval = lineA.size.y
	var endval = lineB.size.y
	
	if lineB.size.x < lineA.size.x:
		var temp = startval
		startval = endval
		endval = temp
	
	var startpos = min(lineA.size.x, lineB.size.x)
	var endpos = max(lineA.size.x, lineB.size.x)
#	var startval = min(lineA.size.y, lineB.size.y)
#	var endval = max(lineA.size.y, lineB.size.y)

	for i in range(startpos, endpos):
		var val = lerp(startval, endval, (i-startpos) / float(endpos-startpos) )
		tbl[i] = val
		owner.emit_signal("value_changed", i, val)
	
	#finally,
	lineA = Rect2(0,0,0,0)
	lineB = Rect2(0,0,0,0)
	update()

#Returns a vector of the table position and value.
func get_table_pos(lock_x=false, mouse=get_local_mouse_position()) -> Vector2:
#	var mouse = get_local_mouse_position()
	if lock_x:  
		mouse.x = last_x
		
	var arpos = clamp(int(lerp(0, tbl.size(), mouse.x / float(rect_size.x))) , 0, tbl.size()-1)
	var val = clamp(lerp(100,0, mouse.y / float(rect_size.y)) , 0, 100)
	return Vector2(arpos, val)


func _draw():
	#Draw centerline
	draw_line(Vector2(0,rect_size.y/2), Vector2(rect_size.x, rect_size.y/2), ColorN('teal', 0.5))

	#Draw the bars.
	for i in range(0, rect_size.x, elementWidth):
		var val =  tbl[ lerp(0, tbl.size(), i / float(rect_size.x)) ]
		var pos = Vector2(i, rect_size.y - int( lerp(0, rect_size.y,val/200.0) ) * 2 )
		var sz = Vector2(elementWidth, pos.y - (rect_size.y / 2) )
		
		var rect = Rect2(pos,sz)
		
		#Swap rect's y pos and y size so the lower value comes first
		if val < 50:
			var temp = rect.size.y
			rect.position.y -= rect.size.y
#			rect.size.y = rect.position.y
#			rect.position.y = temp
		
		draw_texture_rect(full16, rect,true, Color(1,1,1,0.5))
		draw_texture_rect(indicator,Rect2(pos, Vector2(elementWidth,indicator.get_height())),false)
#		if i == 310:  prints("drawrect", i, ":", pos, sz)

	#Grid lines
	draw_line(Vector2(0,rect_size.y/4), Vector2(rect_size.x, rect_size.y/4), ColorN('teal', 0.1))
	draw_line(Vector2(0,rect_size.y/4 + rect_size.y/2), 
				Vector2(rect_size.x, rect_size.y/4 + rect_size.y/2), ColorN('teal', 0.1))

	draw_line(Vector2(rect_size.x/2, 0), Vector2(rect_size.x/2, rect_size.y), ColorN('teal', 0.5))

	#Draw the special process line.
	draw_line(lineA.position, lineB.position, ColorN("yellow"),1.0,true)


#Sets the mouse cursor to something useful
func set_cursor(volume:String):
	cursor_img.lock()

	for i in range(1,4):
		cursor_img.blit_rect(numbers,Rect2(4,0,4,8), Vector2(i*4 +8,8))
	
	for i in min(4, String(int(volume)).length()):
		var n = int(volume.ord_at(i))
		
		var pos = Vector2(4,0)
		if n == 46:
			pos = Vector2.ZERO
		elif (n>=48 and n<58):
			pos = Vector2((n-48)*4 + 8, 0)
		cursor_img.blit_rect(numbers,Rect2(pos, Vector2(4,8)),Vector2(i*4 +8,8))
	
#	if volume.begins_with("100"):  cursor_img.blit_rect(numbers,Rect2(4,0,4,8), Vector2(12,8))
	
	cursor_img.unlock()
	custom_texture.create_from_image(cursor_img,0)


func set_table(table):
	tbl = table
	update()


func _make_custom_tooltip(_for_text):
	var p = $ToolTipProto.duplicate()
#	p.text = for_text
	var pos = get_table_pos()
	var hint2 = ""
	

	var yValue = (tbl[int(pos.x)]-50) / 50.0
	

	yValue = str(yValue).pad_decimals(2) 

	var currentY = String((get_table_pos().y - 50)/50.0).pad_decimals(2)
	p.text = "%sx: %s\ny: %s\n0n:%s" % [hint2, pos.x, yValue, currentY]
	return p

