#define OGL_APOS 0
#define OGL_ACOLOR 1
#define OGL_ATEXCOORD 2
#define OGL_ATEXCOORD2 3

extern GLuint ogl_prog_tex2, ogl_prog_tex2m;
extern GLfloat ogl_mat_ortho[];
void ogl_init_prog();
void ogl_done_prog();
void ogl_prog_set_matrix(GLfloat *mat);
