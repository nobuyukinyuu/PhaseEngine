shader_type canvas_item;  //For controls

uniform float xSkew: hint_range(-1.0, 1.0) = 0.0;
uniform float ySkew: hint_range(-1.0, 1.0) = 0.0;

//void vertex(){
//	VERTEX *= 2.0;
//}

void fragment(){

	float xSkew2 = (xSkew * sin(TIME*58.0) * 0.75);
	float ySkew2 = (ySkew * cos(TIME*58.0) * 0.75);
	vec2 s = vec2((xSkew2 * UV.y + xSkew2 * -0.5),
				 (ySkew2 * UV.x + ySkew2 * -0.5));

	
	vec2 uv2 = (UV+ s) * 1.0;

	vec4 c;

	if ( uv2.x > 1.0 || uv2.y > 1.0 || uv2.x < 0.0 || uv2.y < 0.0 )
	{
		c = vec4(0);
		return;
	} else {
		 c = textureLod(TEXTURE, uv2, 0.0f).rgba
	}

	COLOR = c;
}