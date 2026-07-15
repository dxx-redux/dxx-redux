#ifdef __cplusplus
extern "C" {
#endif
#include "vecmat.h"
#include "3d.h"

enum xmodel_type {
    XM_POLYOBJ,
    XM_POWERUP
};

void *xmodel_load(const char *filename);
void xmodel_free(void *model);
int xmodel_load_gl(void *model);
void xmodel_free_gl(void *model);

void xmodel_show(void *model, int team, g3s_lrgb *light);
void xmodel_show_at(void *model, vms_vector *pos, vms_matrix *orient, int team, g3s_lrgb *light);

void xmodel_load_all();
void xmodel_free_all();
void xmodel_load_gl_all();
void xmodel_free_gl_all();
int xmodel_show_if_loaded(enum xmodel_type mt, int modelnum, vms_vector *pos, vms_matrix *orient, int mpcolor, g3s_lrgb *light);
int xmodel_exists(enum xmodel_type mt, int modelnum);

#ifdef __cplusplus
}
#endif
