// R202 probe 2: EXECUTION test — render a fullscreen triangle through the exact
// vsVitaboy GLSL extracted from the shipped OGL Vitaboy.xnb, in a pure GL 2.1
// context (FBO offscreen), with hand-set uniforms. If 0 pixels, Apple's driver
// mis-executes dynamic uniform array indexing (or the array limits). If pixels
// appear, the shader executes fine and the failure is MonoGame/engine-side.
#include <OpenGL/OpenGL.h>
#include <OpenGL/gl.h>
#include <OpenGL/glext.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

static char *slurp(const char *p, long *n) {
    FILE *f = fopen(p, "rb"); if (!f) { perror("open"); exit(2); }
    fseek(f, 0, SEEK_END); *n = ftell(f); fseek(f, 0, SEEK_SET);
    char *s = malloc(*n + 1); fread(s, 1, *n, f); s[*n] = 0; fclose(f); return s;
}

static void check(GLuint sh, const char *what) {
    GLint ok = 0; char log[4096]; GLsizei len = 0;
    if (strstr(what, "shader") && !strstr(what, "program")) {
        glGetShaderiv(sh, GL_COMPILE_STATUS, &ok);
        glGetShaderInfoLog(sh, sizeof log, &len, log);
    } else {
        glGetProgramiv(sh, GL_LINK_STATUS, &ok);
        glGetProgramInfoLog(sh, sizeof log, &len, log);
    }
    printf("%s: %s%s", what, ok ? "OK" : "FAIL", len ? "\n" : "");
    if (len) printf("%s\n", log);
    if (!ok) exit(1);
}

int main(int argc, char **argv) {
    long n; char *vsrc = slurp(argv[1], &n);
    CGLPixelFormatAttribute attrs[] = { kCGLPFAOpenGLProfile, (CGLPixelFormatAttribute)kCGLOGLPVersion_Legacy,
                                        kCGLPFARendererID, (CGLPixelFormatAttribute)0x00020400, (CGLPixelFormatAttribute)0 };
    CGLPixelFormatObj pf; GLint npv;
    CGLChoosePixelFormat(attrs, &pf, &npv);
    CGLContextObj ctx; CGLCreateContext(pf, NULL, &ctx); CGLSetCurrentContext(ctx);
    printf("GL %s / GLSL %s / %s\n", glGetString(GL_VERSION), glGetString(GL_SHADING_LANGUAGE_VERSION), glGetString(GL_RENDERER));

    GLuint vs = glCreateShader(GL_VERTEX_SHADER);
    const char *v = vsrc; glShaderSource(vs, 1, &v, NULL); glCompileShader(vs); check(vs, "shader");
    const char *fs = "void main() { gl_FragColor = vec4(0.0, 1.0, 0.0, 1.0); }";
    GLuint fsh = glCreateShader(GL_FRAGMENT_SHADER);
    glShaderSource(fsh, 1, &fs, NULL); glCompileShader(fsh); check(fsh, "fshader");
    GLuint p = glCreateProgram();
    glAttachShader(p, vs); glAttachShader(p, fsh); glLinkProgram(p); check(p, "program");
    glUseProgram(p);

    // offscreen FBO 256x256
    GLuint tex, fbo;
    glGenTextures(1, &tex);
    glBindTexture(GL_TEXTURE_2D, tex);
    glTexImage2D(GL_TEXTURE_2D, 0, GL_RGBA, 256, 256, 0, GL_RGBA, GL_UNSIGNED_BYTE, NULL);
    glTexParameteri(GL_TEXTURE_2D, GL_TEXTURE_MIN_FILTER, GL_NEAREST);
    glGenFramebuffersEXT(1, &fbo);
    glBindFramebufferEXT(GL_FRAMEBUFFER_EXT, fbo);
    glFramebufferTexture2DEXT(GL_FRAMEBUFFER_EXT, GL_COLOR_ATTACHMENT0_EXT, GL_TEXTURE_2D, tex, 0);
    glViewport(0, 0, 256, 256);

    // uniforms: whole array zeroed, then:
    //  bone0 [0..3]   = identity, bone1 [4..7] = identity, bone49 [196..199] = identity
    //  World [200..203] = View [204..207] = Projection [208..211] = identity
    static float u[212][4];
    for (int i = 0; i < 212; i++) { u[i][0]=u[i][1]=u[i][2]=0; u[i][3]=0; }
    // bone matrices: D3D-style mul(v, m) row-vector => set rows of identity
    // vs_v0 * array[0..3] via dot columns... per GLSL: dot(vs_v0, u[k]) — row k of matrix.
    // identity: rows (1,0,0,0),(0,1,0,0),(0,0,1,0),(0,0,0,1)
    int bones[] = {0, 4, 8, 12, 196}; // bone0, bone1, bone2, bone3, bone49 starts
    for (int b = 0; b < 5; b++) {
        int base = bones[b];
        u[base+0][0]=1; u[base+1][1]=1; u[base+2][2]=1; u[base+3][3]=1;
    }
    u[200][0]=1; u[201][1]=1; u[202][2]=1; u[203][3]=1; // World identity
    u[204][0]=1; u[205][1]=1; u[206][2]=1; u[207][3]=1; // View identity
    u[208][0]=1; u[209][1]=1; u[210][2]=1; u[211][3]=1; // Projection identity

    GLint loc = glGetUniformLocation(p, "vs_uniforms_vec4");
    GLint posFix = glGetUniformLocation(p, "posFixup");
    printf("loc(vs_uniforms_vec4)=%d loc(posFixup)=%d\n", loc, posFix);
    glUniform4fv(loc, 212, &u[0][0]);
    if (posFix != -1) glUniform4f(posFix, 1.0f, 1.0f, 0.0f, 0.0f); // no flip, no offset

    // attribs: vs_v0 position(float4), vs_v1 (float4), vs_v2, vs_v3, vs_v4 — all vec4
    // big triangle in clip space; params (v3) = (boneIdx, boneIdx, 0)
    float pos[] = { -1,-1,0,1,   3,-1,0,1,   -1,3,0,1 };
    float zero4[] = { 0,0,0,0, 0,0,0,0, 0,0,0,0 };
    int boneIdx = argc > 2 ? atoi(argv[2]) : 0;
    float par[12];
    for (int i = 0; i < 3; i++) { par[i*4]=boneIdx; par[i*4+1]=boneIdx; par[i*4+2]=0; par[i*4+3]=0; }
    float nrm[] = { 0,1,0,0, 0,1,0,0, 0,1,0,0 };

    const char *names[] = {"vs_v0","vs_v1","vs_v2","vs_v3","vs_v4"};
    float *data[] = { pos, zero4, zero4, par, nrm };
    GLuint bufs[5];
    glGenBuffers(5, bufs);
    for (int i = 0; i < 5; i++) {
        GLint a = glGetAttribLocation(p, names[i]);
        printf("attrib %s -> %d\n", names[i], a);
        if (a < 0) continue;
        glBindBuffer(GL_ARRAY_BUFFER, bufs[i]);
        glBufferData(GL_ARRAY_BUFFER, 48, data[i], GL_STATIC_DRAW);
        glEnableVertexAttribArray(a);
        glVertexAttribPointer(a, 4, GL_FLOAT, GL_FALSE, 0, 0);
    }

    glClearColor(0.5f, 0.5f, 0.5f, 1);
    glClear(GL_COLOR_BUFFER_BIT);
    glDrawArrays(GL_TRIANGLES, 0, 3);
    glFinish();

    unsigned char px[256*256*4];
    glReadPixels(0, 0, 256, 256, GL_RGBA, GL_UNSIGNED_BYTE, px);
    int colored = 0, total = 256*256;
    for (int i = 0; i < total; i++)
        if (px[i*4+1] > 200 && px[i*4] < 50) colored++; // green fragments
    printf("BONE=%d colored=%d / %d  %s\n", boneIdx, colored, total, colored > 1000 ? "RENDERED" : "EMPTY");

    // read back a couple of uniforms to confirm driver stored our values
    float back[4] = {9,9,9,9};
    glGetUniformfv(p, glGetUniformLocation(p, "vs_uniforms_vec4[208]"), back);
    printf("readback u[208] = %g %g %g %g (expect 1 0 0 0)\n", back[0], back[1], back[2], back[3]);
    return colored > 1000 ? 0 : 1;
}
