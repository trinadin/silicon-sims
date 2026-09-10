// R202 probe: compile the exact vsVitaboy GLSL from the shipped OGL Vitaboy.xnb
// against this Mac's legacy (2.1) GL context — the context profile MonoGame
// DesktopGL runs the game on (GLSL is attribute/varying = 1.10 syntax).
#include <OpenGL/OpenGL.h>
#include <OpenGL/gl.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>

int main(int argc, char **argv) {
    if (argc < 2) { fprintf(stderr, "usage: %s shader.glsl\n", argv[0]); return 2; }
    FILE *f = fopen(argv[1], "rb");
    if (!f) { perror("open"); return 2; }
    fseek(f, 0, SEEK_END); long n = ftell(f); fseek(f, 0, SEEK_SET);
    char *src = malloc(n + 1); fread(src, 1, n, f); src[n] = 0; fclose(f);

    CGLPixelFormatAttribute attrs[] = { kCGLPFAOpenGLProfile, (CGLPixelFormatAttribute)kCGLOGLPVersion_Legacy, kCGLPFARendererID, (CGLPixelFormatAttribute)0x00020400 /* generic renderer */, (CGLPixelFormatAttribute)0 };
    CGLPixelFormatObj pf; GLint npv;
    CGLChoosePixelFormat(attrs, &pf, &npv);
    if (!pf) { fprintf(stderr, "no pixel format\n"); return 1; }
    CGLContextObj ctx;
    if (CGLCreateContext(pf, NULL, &ctx) != kCGLNoError) { fprintf(stderr, "no ctx\n"); return 1; }
    CGLSetCurrentContext(ctx);

    printf("GL_VERSION: %s\n", glGetString(GL_VERSION));
    printf("GL_SHADING_LANGUAGE_VERSION: %s\n", glGetString(GL_SHADING_LANGUAGE_VERSION));
    printf("GL_RENDERER: %s\n", glGetString(GL_RENDERER));
    GLint vv = 0, vc = 0;
    glGetIntegerv(0x8DFB /*GL_MAX_VERTEX_UNIFORM_VECTORS*/, &vv); // core name; may be absent on 2.1
    glGetIntegerv(GL_MAX_VERTEX_UNIFORM_COMPONENTS_ARB, &vc);
    printf("GL_MAX_VERTEX_UNIFORM_VECTORS(0x8DFB): %d  COMPONENTS_ARB: %d\n", vv, vc);

    GLuint sh = glCreateShader(GL_VERTEX_SHADER);
    const char *s = src;
    glShaderSource(sh, 1, &s, NULL);
    glCompileShader(sh);
    GLint ok = 0;
    glGetShaderiv(sh, GL_COMPILE_STATUS, &ok);
    printf("COMPILE_STATUS: %s\n", ok ? "SUCCESS" : "FAILURE");
    char log[8192]; GLsizei len = 0;
    glGetShaderInfoLog(sh, sizeof log, &len, log);
    if (len) printf("INFO LOG (%d bytes):\n%s\n", len, log);

    // also link a minimal program (some drivers only die at link)
    const char *fs = "void main() { gl_FragColor = vec4(1.0); }";
    GLuint fsh = glCreateShader(GL_FRAGMENT_SHADER);
    glShaderSource(fsh, 1, &fs, NULL);
    glCompileShader(fsh);
    GLint fok = 0; glGetShaderiv(fsh, GL_COMPILE_STATUS, &fok);
    printf("FS COMPILE: %s\n", fok ? "SUCCESS" : "FAILURE");
    GLuint p = glCreateProgram();
    glAttachShader(p, sh);
    glAttachShader(p, fsh);
    glLinkProgram(p);
    GLint lok = 0;
    glGetProgramiv(p, GL_LINK_STATUS, &lok);
    printf("LINK_STATUS: %s\n", lok ? "SUCCESS" : "FAILURE");
    len = 0; glGetProgramInfoLog(p, sizeof log, &len, log);
    if (len) printf("LINK LOG:\n%s\n", log);
    if (lok) {
        GLint loc = glGetUniformLocation(p, "vs_uniforms_vec4");
        printf("location(vs_uniforms_vec4): %d\n", loc);
        GLint loc2 = glGetUniformLocation(p, "vs_uniforms_vec4[0]");
        printf("location(vs_uniforms_vec4[0]): %d\n", loc2);
    }
    return ok ? 0 : 1;
}
