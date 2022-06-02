extends Node
const WHITE=Color(1,1,1,1)
const BLACK=Color(0,0,0,1)
const TRANS=Color(1,1,1,0)

func dotted_line(c:CanvasItem, origin:Vector2, dest:Vector2, color:Color = WHITE, 
				 width:float=1.0, antialiased:bool=false, dotlen=1.0, gaplen=2.0, color2 = BLACK):
	var angle = (dest-origin).normalized()
	var dist = origin.distance_to(dest)
	var step = dotlen+gaplen
	
	for i in range(0, dist, step):
		var startPos = origin + angle*i
		var length = dotlen 
		var endPos
		
		if i+dotlen > dist:
			length = dist-i
			endPos = startPos + angle*length
		else:
			endPos = startPos + angle*length
			var length2 = gaplen if i+dotlen+gaplen < dist else dist-i-dotlen
			var endPos2 = endPos + angle*length2
			#Color 2
			c.draw_line(endPos, endPos2, color2, width, antialiased)
			
		c.draw_line(startPos, endPos, color, width, antialiased)

const DEFAULT_ARROW_SPREAD=PI/6
func arrow(c:CanvasItem, a, b, color=Color(1,1,1,1), width=1.0, antialiased=true,
		arrow_length=4, arrow_spread=DEFAULT_ARROW_SPREAD):

	var angle = atan2(a.y-b.y, a.x-b.x) + PI
	
	var dest1 = Vector2(a.x + arrow_length*cos(angle+arrow_spread), a.y + arrow_length*sin(angle+arrow_spread))
	var dest2 = Vector2(a.x + arrow_length*cos(angle-arrow_spread), a.y + arrow_length*sin(angle-arrow_spread))

	c.draw_line(a,b,color,width, antialiased)
	c.draw_line(a,dest1,color,width, antialiased)
	c.draw_line(a,dest2,color,width, antialiased)
