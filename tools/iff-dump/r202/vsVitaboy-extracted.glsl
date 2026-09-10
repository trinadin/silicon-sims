#ifdef GL_ES
precision highp float;
precision mediump int;
#endif

uniform vec4 vs_uniforms_vec4[212];
uniform vec4 posFixup;
const vec4 vs_c212 = vec4(4.0, 1.0, 0.0, 0.0);
const vec4 vs_c213 = vec4(0.5, -0.5, 1.0, 0.0);
vec4 vs_r0;
ivec4 vs_a0;
vec4 vs_r1;
vec4 vs_r2;
vec4 vs_r3;
vec4 vs_r4;
vec4 vs_r5;
vec4 vs_r6;
#define ARRAYBASE_0 0
#define vs_c0 vs_uniforms_vec4[0]
#define vs_c1 vs_uniforms_vec4[1]
#define vs_c2 vs_uniforms_vec4[2]
#define vs_c3 vs_uniforms_vec4[3]
#define vs_c200 vs_uniforms_vec4[200]
#define vs_c201 vs_uniforms_vec4[201]
#define vs_c202 vs_uniforms_vec4[202]
#define vs_c203 vs_uniforms_vec4[203]
#define vs_c204 vs_uniforms_vec4[204]
#define vs_c205 vs_uniforms_vec4[205]
#define vs_c206 vs_uniforms_vec4[206]
#define vs_c207 vs_uniforms_vec4[207]
#define vs_c208 vs_uniforms_vec4[208]
#define vs_c209 vs_uniforms_vec4[209]
#define vs_c210 vs_uniforms_vec4[210]
#define vs_c211 vs_uniforms_vec4[211]
attribute vec4 vs_v0;
#define vs_o0 gl_Position
attribute vec4 vs_v1;
varying vec4 vTexCoord0;
#define vs_o1 vTexCoord0
attribute vec4 vs_v2;
varying vec4 vTexCoord1;
#define vs_o2 vTexCoord1
attribute vec4 vs_v3;
varying vec4 vTexCoord2;
#define vs_o3 vTexCoord2
attribute vec4 vs_v4;
varying vec4 vTexCoord3;
#define vs_o4 vTexCoord3

void main()
{
	vs_r0.xy = vec2(lessThan(vs_v3.xy, -vs_v3.xy));
	vs_r0.zw = fract(vs_v3.xy);
	vs_r1.xy = -vs_r0.zw + vs_v3.xy;
	vs_r0.zw = vec2(lessThan(-vs_r0.zw, vs_r0.zw));
	vs_r0.xy = (vs_r0.xy * vs_r0.zw) + vs_r1.xy;
	vs_r0.xy = vs_r0.xy * vs_c212.xx;
	vs_a0.xy = ivec2(floor(abs(vs_r0.xy) + vec2(0.5)) * sign(vs_r0.xy));
	vs_r0.x = dot(vs_v4.xyz, vs_uniforms_vec4[ARRAYBASE_0 + vs_a0.x].xyz);
	vs_r0.y = dot(vs_v4.xyz, vs_uniforms_vec4[(ARRAYBASE_0 + 1) + vs_a0.x].xyz);
	vs_r0.z = dot(vs_v4.xyz, vs_uniforms_vec4[(ARRAYBASE_0 + 2) + vs_a0.x].xyz);
	vs_o3.x = dot(vs_r0.xyz, vs_c200.xyz);
	vs_o3.y = dot(vs_r0.xyz, vs_c201.xyz);
	vs_o3.z = dot(vs_r0.xyz, vs_c202.xyz);
	vs_r0.x = dot(vs_v0, vs_uniforms_vec4[ARRAYBASE_0 + vs_a0.x]);
	vs_r0.y = dot(vs_v0, vs_uniforms_vec4[(ARRAYBASE_0 + 1) + vs_a0.x]);
	vs_r0.z = dot(vs_v0, vs_uniforms_vec4[(ARRAYBASE_0 + 2) + vs_a0.x]);
	vs_r0.w = dot(vs_v0, vs_uniforms_vec4[(ARRAYBASE_0 + 3) + vs_a0.x]);
	vs_r1 = (vs_v2.xyzx * vs_c212.yyyz) + vs_c212.zzzy;
	vs_r2.x = dot(vs_r1, vs_uniforms_vec4[ARRAYBASE_0 + vs_a0.y]);
	vs_r2.y = dot(vs_r1, vs_uniforms_vec4[(ARRAYBASE_0 + 1) + vs_a0.y]);
	vs_r2.z = dot(vs_r1, vs_uniforms_vec4[(ARRAYBASE_0 + 2) + vs_a0.y]);
	vs_r2.w = dot(vs_r1, vs_uniforms_vec4[(ARRAYBASE_0 + 3) + vs_a0.y]);
	vs_r1 = mix(vs_r0, vs_r2, vs_v3.zzzz);
	vs_r0.x = dot(vs_r1, vs_c200);
	vs_r0.y = dot(vs_r1, vs_c201);
	vs_r0.z = dot(vs_r1, vs_c202);
	vs_r0.w = dot(vs_r1, vs_c203);
	vs_r1 = vs_c205;
	vs_r2 = vs_r1 * vs_c208.yyyy;
	vs_r3 = vs_c204;
	vs_r2 = (vs_r3 * vs_c208.xxxx) + vs_r2;
	vs_r4 = vs_c206;
	vs_r2 = (vs_r4 * vs_c208.zzzz) + vs_r2;
	vs_r5 = vs_c207;
	vs_r2 = (vs_r5 * vs_c208.wwww) + vs_r2;
	vs_r2.x = dot(vs_r0, vs_r2);
	vs_r6 = vs_r1 * vs_c209.yyyy;
	vs_r6 = (vs_r3 * vs_c209.xxxx) + vs_r6;
	vs_r6 = (vs_r4 * vs_c209.zzzz) + vs_r6;
	vs_r6 = (vs_r5 * vs_c209.wwww) + vs_r6;
	vs_r2.y = dot(vs_r0, vs_r6);
	vs_r6 = vs_r1 * vs_c210.yyyy;
	vs_r6 = (vs_r3 * vs_c210.xxxx) + vs_r6;
	vs_r6 = (vs_r4 * vs_c210.zzzz) + vs_r6;
	vs_r6 = (vs_r5 * vs_c210.wwww) + vs_r6;
	vs_r2.z = dot(vs_r0, vs_r6);
	vs_r1 = vs_r1 * vs_c211.yyyy;
	vs_r1 = (vs_r3 * vs_c211.xxxx) + vs_r1;
	vs_r1 = (vs_r4 * vs_c211.zzzz) + vs_r1;
	vs_r1 = (vs_r5 * vs_c211.wwww) + vs_r1;
	vs_r2.w = dot(vs_r0, vs_r1);
	vs_o4 = vs_r0;
	vs_o0 = vs_r2;
	vs_o2 = (vs_r2 * vs_c213.xyzz) + vs_c213.xxww;
	vs_o1.xy = vs_v1.xy;
	gl_Position.y = gl_Position.y * posFixup.y;
	gl_Position.xy += posFixup.zw * gl_Position.ww;
	gl_Position.z = gl_Position.z * 2.0 - gl_Position.w;
}