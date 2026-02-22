#ifndef OGLFBO_H
#define OGLFBO_H
#include <SDL.h>

SDL_bool ogl_fbo_setup(SDL_Window *VideoWindow, const int w, const int h);
void ogl_fbo_swap(SDL_Window *VideoWindow);
int ogl_fbo_is_active();
void ogl_fbo_release();

#endif
