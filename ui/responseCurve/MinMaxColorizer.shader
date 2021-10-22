shader_type canvas_item;

uniform int disabled : hint_range(0,4) = 0;

vec4 colorize(vec4 c, float t)
{	
	vec4 output = c;
	output.r *= 2.5;
	output.rg *= 1.25;
	output.g *= 0.25;

	output.rgb = mix(output.rgb, c.rgb, t*0.75);

	return output;
}

void fragment()
{
	float t = (sin(TIME*4.0)+1.0) * 0.5;
	vec4 c = textureLod(SCREEN_TEXTURE, SCREEN_UV, 0);

	if (disabled==1)  //Draw left
		{if(UV.x<0.5) c = colorize(c,t);}
	else if (disabled==2) //Draw right
		{
			if(UV.x>0.5 && UV.y <0.95) c = colorize(c,t);
			if(UV.y>0.95 && UV.x>0.45) c = colorize(c,t);
		}
	else if (disabled==3) //Draw both
		{c = colorize(c,t);}
	else if (disabled ==4) //Draw a different overlay
	{
		vec4 a = c;
		a.r *= 2.25;
		a.gb = vec2(0);
		c = mix(c,a,t+1.0);
		
	}
	

	COLOR=c;
}