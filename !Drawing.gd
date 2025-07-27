extends Node
const WHITE=Color(1,1,1,1)
const BLACK=Color(0,0,0,1)
const TRANS=Color(1,1,1,0)

const SQ2 = 0.707106781  #sqrt(2)/2.0,  45 degrees
const MAX_ARC_POINT_COUNT = 21
func rounded_rect(c:CanvasItem, origin:Vector2, dest:Vector2, radius=1.0, point_count=0, color:Color = WHITE,
				 width:float=1.0, antialiased:bool=false):
					
	radius = max(0, radius)
	#If radius is between 0-1, make the corner radius relative to that percentage of the rect.
	#Otherwise, assume a corner radius in px.
	if radius > 0 and radius < 1.0: #Find the shortest axial distance.
		var x = max(origin.x, dest.x) - min(origin.x, dest.x)
		var y = max(origin.y, dest.y) - min(origin.y, dest.y)
		radius = min(x,y) * radius
		
	#Determine the number of points in each arc. If the user didn't specify a point_count, let's
	#Make one based on the size of the corner radius relative to the rest.
	if point_count < 2:
		point_count = int(min(MAX_ARC_POINT_COUNT, radius))

	#Start by drawing the arcs. The center of the arc is relative to the radius.
	var UR = Vector2(dest.x, origin.y)
	var LL = Vector2(origin.x, dest.y)
	c.draw_arc(Vector2.ONE*radius + origin, radius, PI, PI + PI/2.0, point_count,color,width,antialiased)
	c.draw_arc(UR + Vector2(-radius, radius), radius, PI + PI/2.0, TAU, point_count,color,width,antialiased)
	c.draw_arc(LL + Vector2(radius, -radius), radius, PI/2, PI, point_count,color,width,antialiased)
	c.draw_arc(dest - Vector2.ONE*radius, radius, 0.0, PI/2.0, point_count,color,width,antialiased)

	#Now draw the lines.
	c.draw_line(origin + Vector2(0, radius), LL + Vector2(0, -radius), color, width, antialiased)
	c.draw_line(UR + Vector2(0, radius), dest + Vector2(0, -radius), color, width, antialiased)
	c.draw_line(origin + Vector2(radius, 0), UR + Vector2(-radius, 0), color, width, antialiased)
	c.draw_line(LL + Vector2(radius, 0), dest + Vector2(-radius, 0), color, width, antialiased)

	return radius #For functions that want it

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
