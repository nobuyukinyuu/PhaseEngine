shader_type canvas_item;
//render_mode skip_vertex_transform;

uniform vec2 rect_size = vec2(32);

uniform float zoom_len : hint_range(0.1, 4.0) = 1.0;
uniform float zoom_angle : hint_range(0.0, 1.0) = 1.0;
uniform bool point_outwards;

uniform bool desaturate = false;

const float PI = 3.1415926535897932384626433832795;
const float TAU = PI*2.0;
const float HALF_PI = PI/2.0;

vec2 rotateUV(vec2 uv, vec2 pivot, float rotation)
{
    mat2 rotation_matrix=mat2(  vec2(sin(rotation),-cos(rotation)),
                                vec2(cos(rotation),sin(rotation))
                                );    uv -= pivot;
    uv= uv*rotation_matrix;
    uv += pivot;
    return uv;
}

void vertex() {
//    VERTEX = (EXTRA_MATRIX * (WORLD_MATRIX * vec4(VERTEX, 0.0, 1.0))).xy;
	VERTEX = rotateUV(VERTEX, rect_size*0.5, zoom_angle*PI - HALF_PI);
}



vec2 polar_coordinates(vec2 uv)
{
	vec2 center = vec2(0.5);
	vec2 delta = uv - center;
	float radius = abs((point_outwards? 1.0 : 0.0) - (length(delta*zoom_len) * 2.0 ));
//	float angle = mod(atan(delta.y, delta.x) - HALF_PI, TAU) / (TAU*zoom_angle);
	float angle = mod(atan(delta.y, delta.x) - HALF_PI, TAU) / (TAU*zoom_angle);
	vec2 polar = vec2(angle, radius);
	
	return vec2(angle, radius);

}

void fragment(){
	vec2 polar_uv = polar_coordinates(UV);
	if(polar_uv.x < 0.0 || polar_uv.x > 1.0 || polar_uv.y < 0.0 || polar_uv.y > 1.0 )
		COLOR = vec4(0);
//	COLOR = texture(TEXTURE, mod(polar_uv, vec2(repeat)));
	else
	{
		vec4 c = texture(TEXTURE, polar_uv);
		if (desaturate) c.rgb = vec3(c.r+c.g+c.b / 3.0);
		 COLOR = c
	}
}