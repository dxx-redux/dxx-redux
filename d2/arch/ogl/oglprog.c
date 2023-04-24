#include "ogl_init.h"
#include "oglprog.h"
#include "dxxerror.h"

GLuint ogl_prog_tex2, ogl_prog_tex2m;
GLuint ogl_tex2_mat, ogl_tex2m_mat;

GLfloat ogl_mat_ortho[16] = {
	1, 0, 0, 0,
    0, 1, 0, 0,
    0, 0, 1, 0,
    0, 0, 0, 1 };

GLuint ogl_mk_prog(const char *vert_src, const char *frag_src) {
	char msg[2048];
	GLint val = 0;
	GLuint vert = glCreateShader(GL_VERTEX_SHADER);
	if (!vert) {
		Error("creating vert failed");
		return 0;
	}
	glShaderSource(vert, 1, &vert_src, NULL);
	glCompileShader(vert);
	glGetShaderiv(vert, GL_COMPILE_STATUS, &val);
	if (!val) {
        glGetShaderInfoLog(vert, sizeof(msg), NULL, msg);
		Error("compiling vert failed: %s", msg);
		return 0;
	}
	GLuint frag = glCreateShader(GL_FRAGMENT_SHADER);
	if (!frag) {
		Error("creating frag failed");
		return 0;
	}
	glShaderSource(frag, 1, &frag_src, NULL);
	glCompileShader(frag);
	val = 0;
	glGetShaderiv(frag, GL_COMPILE_STATUS, &val);
	if (!val) {
        glGetShaderInfoLog(frag, sizeof(msg), NULL, msg);
        Error("compiling frag failed: %s", msg);
        return 0;
	}
	GLuint prog = glCreateProgram();
	if (!prog) {
		Error("creating prog failed");
		return 0;
	}
	glAttachShader(prog, vert);
	glAttachShader(prog, frag);
	glBindAttribLocation(prog, OGL_APOS, "apos");
	glBindAttribLocation(prog, OGL_ACOLOR, "acolor");
	glBindAttribLocation(prog, OGL_ATEXCOORD, "atexcoord");
	glBindAttribLocation(prog, OGL_ATEXCOORD2, "atexcoord2");
	glLinkProgram(prog);
	glGetProgramiv(prog, GL_LINK_STATUS, &val);
	if (!val) {
        glGetProgramInfoLog(prog, sizeof(msg), NULL, msg);
		Error("linking prog failed: %s", msg);
	}
	glDeleteShader(frag);
	glDeleteShader(vert);
	return prog;
}

void ogl_init_prog() {
	ogl_prog_tex2 = ogl_mk_prog("attribute vec3 apos;"
		"\n attribute vec4 acolor;"
		"\n attribute vec2 atexcoord;"
		"\n attribute vec2 atexcoord2;"
		"\n varying vec2 vtexcoord;"
		"\n varying vec2 vtexcoord2;"
		"\n varying vec4 vcolor;"
		"\n uniform mat4 umat;"
		"\n void main() {"
		"\n  gl_Position = umat * vec4(apos, 1.0);"
		"\n  vcolor = acolor; vtexcoord = atexcoord; vtexcoord2 = atexcoord2;"
		"\n }",
		//"precision mediump float;"
		"\n varying vec2 vtexcoord;"
		"\n varying vec2 vtexcoord2;"
		"\n varying vec4 vcolor;"
		"\n uniform sampler2D utex;"
		"\n uniform sampler2D utex2;"
		"\n void main() {"
		"\n  vec4 bot = texture2D(utex, vtexcoord), ovl = texture2D(utex2, vtexcoord2);"
		"\n  vec4 c = vec4(mix(bot.rgb, ovl.rgb, ovl.a), bot.a + ovl.a - bot.a * ovl.a);" // same as 1 - (1 - bot.a) * (1 - ovl.a)
		"\n  gl_FragColor = vcolor * c;"
		"\n }");

	ogl_prog_tex2m = ogl_mk_prog("attribute vec3 apos;"
		"\n attribute vec4 acolor;"
		"\n attribute vec2 atexcoord;"
		"\n attribute vec2 atexcoord2;"
		"\n varying vec2 vtexcoord;"
		"\n varying vec2 vtexcoord2;"
		"\n varying vec4 vcolor;"
		"\n uniform mat4 umat;"
		"\n void main() {"
		"\n  gl_Position = umat * vec4(apos, 1.0);"
		"\n  vcolor = acolor; vtexcoord = atexcoord; vtexcoord2 = atexcoord2;"
		"\n }",
		//"precision mediump float;"
		"\n varying vec2 vtexcoord;"
		"\n varying vec2 vtexcoord2;"
		"\n varying vec4 vcolor;"
		"\n uniform sampler2D utex;"
		"\n uniform sampler2D utex2;"
		"\n uniform sampler2D utex2m;"
		"\n void main() {"
		"\n  vec4 bot = texture2D(utex, vtexcoord), ovl = texture2D(utex2, vtexcoord2);"
		"\n  vec4 c = vec4(mix(bot.rgb, ovl.rgb, ovl.a), bot.a + ovl.a - bot.a * ovl.a);" // same as 1 - (1 - bot.a) * (1 - ovl.a)
		"\n  vec4 mask = texture2D(utex2m, vtexcoord2);"
		"\n  gl_FragColor = vcolor * vec4(c.rgb, c.a * mask.a);"
		"\n }");

	ogl_tex2_mat = glGetUniformLocation(ogl_prog_tex2, "umat");
	ogl_tex2m_mat = glGetUniformLocation(ogl_prog_tex2m, "umat");

	glUseProgram(ogl_prog_tex2);
	glUniform1i(glGetUniformLocation(ogl_prog_tex2, "utex"), 0);
	glUniform1i(glGetUniformLocation(ogl_prog_tex2, "utex2"), 1);

	glUseProgram(ogl_prog_tex2m);
	glUniform1i(glGetUniformLocation(ogl_prog_tex2m, "utex"), 0);
	glUniform1i(glGetUniformLocation(ogl_prog_tex2m, "utex2"), 1);
	glUniform1i(glGetUniformLocation(ogl_prog_tex2m, "utex2m"), 2);

	glUseProgram(0);
}

void ogl_done_prog() {
	if (ogl_prog_tex2m) {
		glDeleteProgram(ogl_prog_tex2m);
		ogl_prog_tex2m = 0;
	}
	if (ogl_prog_tex2) {
		glDeleteProgram(ogl_prog_tex2);
		ogl_prog_tex2 = 0;
	}
}

void ogl_prog_set_matrix(GLfloat *mat) {
	if (ogl_prog_tex2) {
		glUseProgram(ogl_prog_tex2);
		glUniformMatrix4fv(ogl_tex2_mat, 1, GL_FALSE, mat);
	}

	if (ogl_prog_tex2m) {
		glUseProgram(ogl_prog_tex2m);
		glUniformMatrix4fv(ogl_tex2m_mat, 1, GL_FALSE, mat);
	}

	glUseProgram(0);
}
