/*
  Partially copied from:
  
  Simple DirectMedia Layer
  Copyright (C) 1997-2022 Sam Lantinga <slouken@libsdl.org>

  This software is provided 'as-is', without any express or implied
  warranty.	 In no event will the authors be held liable for any damages
  arising from the use of this software.

  Permission is granted to anyone to use this software for any purpose,
  including commercial applications, and to alter it and redistribute it
  freely, subject to the following restrictions:

  1. The origin of this software must not be misrepresented; you must not
	 claim that you wrote the original software. If you use this software
	 in a product, an acknowledgment in the product documentation would be
	 appreciated but is not required.
  2. Altered source versions must be plainly marked as such, and must not be
	 misrepresented as being the original software.
  3. This notice may not be removed or altered from any source distribution.
*/

#include <SDL.h>
#include <GL/glew.h>
#include "oglfbo.h"

static SDL_bool WantScaleMethodNearest = SDL_TRUE;
static int OpenGLLogicalScalingWidth = 0;
static int OpenGLLogicalScalingHeight = 0;
static GLuint OpenGLLogicalScalingFBO = 0;
static GLuint OpenGLLogicalScalingColor = 0;
static GLuint OpenGLLogicalScalingDepth = 0;
static int OpenGLLogicalScalingSamples = 0;
static GLuint OpenGLLogicalScalingMultisampleFBO = 0;
static GLuint OpenGLLogicalScalingMultisampleColor = 0;
static GLuint OpenGLLogicalScalingMultisampleDepth = 0;
static GLuint OpenGLCurrentReadFBO = 0;
static GLuint OpenGLCurrentDrawFBO = 0;

int ogl_fbo_is_active()
{
	return OpenGLLogicalScalingFBO != 0;
}

void ogl_fbo_release()
{
	if (!OpenGLLogicalScalingFBO)
		return;
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
	glDeleteRenderbuffers(1, &OpenGLLogicalScalingColor);
	glDeleteRenderbuffers(1, &OpenGLLogicalScalingDepth);
	glDeleteFramebuffers(1, &OpenGLLogicalScalingFBO);
	OpenGLLogicalScalingFBO = OpenGLLogicalScalingColor = OpenGLLogicalScalingDepth = 0;
	OpenGLCurrentReadFBO = OpenGLCurrentDrawFBO = 0;
} 

// for first time and for resizing
SDL_bool ogl_fbo_setup(SDL_Window *VideoWindow, const int w, const int h)
{
	int alpha_size = 0;
	int depth_size = 0;
	int stencil_size = 0;

	SDL_assert(VideoWindow);

	/* Support the MOUSE_RELATIVE_SCALING hint from SDL 2.0 for OpenGL scaling. */
	//UseMouseRelativeScaling = SDL12Compat_GetHintBoolean("SDL_MOUSE_RELATIVE_SCALING", SDL_TRUE);

	//if (!SUPPORTS_GL_ARB_framebuffer_object) {
	//	  return SDL_FALSE;	 /* no FBOs, no scaling. */
	//}

	glBindFramebuffer(GL_FRAMEBUFFER, 0);
	//glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
	//SDL_GL_SwapWindow(VideoWindow);

	SDL_GL_GetAttribute(SDL_GL_ALPHA_SIZE, &alpha_size);
	SDL_GL_GetAttribute(SDL_GL_DEPTH_SIZE, &depth_size);
	SDL_GL_GetAttribute(SDL_GL_STENCIL_SIZE, &stencil_size);

	if (!OpenGLLogicalScalingFBO) {
		glGenFramebuffers(1, &OpenGLLogicalScalingFBO);
	}

	if (!OpenGLLogicalScalingColor) {
		glGenRenderbuffers(1, &OpenGLLogicalScalingColor);
	}

	if (!OpenGLLogicalScalingDepth) {
		glGenRenderbuffers(1, &OpenGLLogicalScalingDepth);
	}

	glBindFramebuffer(GL_FRAMEBUFFER, OpenGLLogicalScalingFBO);
	glBindRenderbuffer(GL_RENDERBUFFER, OpenGLLogicalScalingColor);
	glRenderbufferStorageMultisample(GL_RENDERBUFFER, OpenGLLogicalScalingSamples, (alpha_size > 0) ? GL_RGBA8 : GL_RGB8, w, h);
	glFramebufferRenderbuffer(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_RENDERBUFFER, OpenGLLogicalScalingColor);

	if (depth_size || stencil_size) {
		glBindRenderbuffer(GL_RENDERBUFFER, OpenGLLogicalScalingDepth);
		glRenderbufferStorageMultisample(GL_RENDERBUFFER, OpenGLLogicalScalingSamples, GL_DEPTH24_STENCIL8, w, h);
		if (depth_size) {
			glFramebufferRenderbuffer(GL_FRAMEBUFFER, GL_DEPTH_ATTACHMENT, GL_RENDERBUFFER, OpenGLLogicalScalingDepth);
		}
		if (stencil_size) {
			glFramebufferRenderbuffer(GL_FRAMEBUFFER, GL_STENCIL_ATTACHMENT, GL_RENDERBUFFER, OpenGLLogicalScalingDepth);
		}
	}

	glBindRenderbuffer(GL_RENDERBUFFER, 0);

	if ((glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE) || glGetError()) {
		glBindFramebuffer(GL_FRAMEBUFFER, 0);
		glDeleteRenderbuffers(1, &OpenGLLogicalScalingColor);
		glDeleteRenderbuffers(1, &OpenGLLogicalScalingDepth);
		glDeleteFramebuffers(1, &OpenGLLogicalScalingFBO);
		OpenGLLogicalScalingFBO = OpenGLLogicalScalingColor = OpenGLLogicalScalingDepth = 0;
		return SDL_FALSE;
	}

	if (OpenGLLogicalScalingSamples) {
		if (!OpenGLLogicalScalingMultisampleFBO) {
			glGenFramebuffers(1, &OpenGLLogicalScalingMultisampleFBO);
		}
		if (!OpenGLLogicalScalingMultisampleColor) {
			glGenRenderbuffers(1, &OpenGLLogicalScalingMultisampleColor);
		}
		if (!OpenGLLogicalScalingMultisampleDepth) {
			glGenRenderbuffers(1, &OpenGLLogicalScalingMultisampleDepth);
		}

		glBindFramebuffer(GL_FRAMEBUFFER, OpenGLLogicalScalingMultisampleFBO);
		glBindRenderbuffer(GL_RENDERBUFFER, OpenGLLogicalScalingMultisampleColor);
		glRenderbufferStorage(GL_RENDERBUFFER, (alpha_size > 0) ? GL_RGBA8 : GL_RGB8, w, h);
		glFramebufferRenderbuffer(GL_FRAMEBUFFER, GL_COLOR_ATTACHMENT0, GL_RENDERBUFFER, OpenGLLogicalScalingMultisampleColor);

		if (depth_size || stencil_size) {
			glBindRenderbuffer(GL_RENDERBUFFER, OpenGLLogicalScalingMultisampleDepth);
			glRenderbufferStorage(GL_RENDERBUFFER, GL_DEPTH24_STENCIL8, w, h);
			if (depth_size) {
				glFramebufferRenderbuffer(GL_FRAMEBUFFER, GL_DEPTH_ATTACHMENT, GL_RENDERBUFFER, OpenGLLogicalScalingMultisampleDepth);
			}
			if (stencil_size) {
				glFramebufferRenderbuffer(GL_FRAMEBUFFER, GL_STENCIL_ATTACHMENT, GL_RENDERBUFFER, OpenGLLogicalScalingMultisampleDepth);
			}
		}

		glBindRenderbuffer(GL_RENDERBUFFER, 0);

		if ((glCheckFramebufferStatus(GL_FRAMEBUFFER) != GL_FRAMEBUFFER_COMPLETE) || glGetError()) {
			glBindFramebuffer(GL_FRAMEBUFFER, 0);
			glDeleteRenderbuffers(1, &OpenGLLogicalScalingMultisampleColor);
			glDeleteRenderbuffers(1, &OpenGLLogicalScalingMultisampleDepth);
			glDeleteFramebuffers(1, &OpenGLLogicalScalingMultisampleFBO);
			OpenGLLogicalScalingMultisampleFBO = OpenGLLogicalScalingMultisampleColor = OpenGLLogicalScalingMultisampleDepth = 0;
		}
		glBindFramebuffer(GL_DRAW_FRAMEBUFFER, OpenGLLogicalScalingFBO);
		glBindFramebuffer(GL_READ_FRAMEBUFFER, OpenGLLogicalScalingMultisampleFBO);
	}

	/* initialise the cached current FBO bindings properly */
	OpenGLCurrentReadFBO = OpenGLLogicalScalingMultisampleFBO ? OpenGLLogicalScalingMultisampleFBO : OpenGLLogicalScalingFBO;
	OpenGLCurrentDrawFBO = OpenGLLogicalScalingFBO;

	glViewport(0, 0, w, h);
	glScissor(0, 0, w, h);
	OpenGLLogicalScalingWidth = w;
	OpenGLLogicalScalingHeight = h;

	glClear(GL_COLOR_BUFFER_BIT | GL_DEPTH_BUFFER_BIT);
	return SDL_TRUE;
}

/* Calculates the Logical Scaling viewport based on a given window size.
   We pass the DPI-unscaled pixel size in when using this for rendering, and
   the DPI-scaled window size when using it to transform mouse coordinates. */
static SDL_Rect
GetOpenGLLogicalScalingViewport(int physical_width, int physical_height)
{
	float want_aspect, real_aspect;
	SDL_Rect dstrect;

	want_aspect = ((float) OpenGLLogicalScalingWidth) / ((float) OpenGLLogicalScalingHeight);
	real_aspect = ((float) physical_width) / ((float) physical_height);

	if (SDL_fabs(want_aspect-real_aspect) < 0.0001) {
		/* The aspect ratios are the same, just scale appropriately */
		dstrect.x = 0;
		dstrect.y = 0;
		dstrect.w = physical_width;
		dstrect.h = physical_height;
	} else if (want_aspect > real_aspect) {
		/* We want a wider aspect ratio than is available - letterbox it */
		const float scale = ((float) physical_width) / OpenGLLogicalScalingWidth;
		dstrect.x = 0;
		dstrect.w = physical_width;
		dstrect.h = (int)SDL_floorf(OpenGLLogicalScalingHeight * scale);
		dstrect.y = (physical_height - dstrect.h) / 2;
	} else {
		/* We want a narrower aspect ratio than is available - use side-bars */
		const float scale = ((float)physical_height) / OpenGLLogicalScalingHeight;
		dstrect.y = 0;
		dstrect.h = physical_height;
		dstrect.w = (int)SDL_floorf(OpenGLLogicalScalingWidth * scale);
		dstrect.x = (physical_width - dstrect.w) / 2;
	}

	return dstrect;
}

void ogl_fbo_swap(SDL_Window *VideoWindow)
{
	const GLboolean has_scissor = glIsEnabled(GL_SCISSOR_TEST);
	int physical_w, physical_h;
	GLfloat clearcolor[4];
	SDL_Rect dstrect;

	/* use the drawable size, which is != window size for HIGHDPI systems */
	SDL_GL_GetDrawableSize(VideoWindow, &physical_w, &physical_h);
	dstrect = GetOpenGLLogicalScalingViewport(physical_w, physical_h);

	glGetFloatv(GL_COLOR_CLEAR_VALUE, clearcolor);

	if (has_scissor) {
		glDisable(GL_SCISSOR_TEST);	 /* scissor test affects framebuffer_blit */
	}

	glBindFramebuffer(GL_READ_FRAMEBUFFER, OpenGLLogicalScalingFBO);

	/* Resolve the multisample framebuffer if required. */
	if (OpenGLLogicalScalingMultisampleFBO) {
		glBindFramebuffer(GL_DRAW_FRAMEBUFFER, OpenGLLogicalScalingMultisampleFBO);
		glBlitFramebuffer(0, 0, OpenGLLogicalScalingWidth, OpenGLLogicalScalingHeight,
						  0, 0, OpenGLLogicalScalingWidth, OpenGLLogicalScalingHeight,
						  GL_COLOR_BUFFER_BIT, GL_NEAREST);
		glBindFramebuffer(GL_READ_FRAMEBUFFER, OpenGLLogicalScalingMultisampleFBO);
	}

	glBindFramebuffer(GL_DRAW_FRAMEBUFFER, 0);
	glClearColor(0.0f, 0.0f, 0.0f, 1.0f);
	glClear(GL_COLOR_BUFFER_BIT);
	glBlitFramebuffer(0, 0, OpenGLLogicalScalingWidth, OpenGLLogicalScalingHeight,
					  dstrect.x, dstrect.y, dstrect.x + dstrect.w, dstrect.y + dstrect.h,
					  GL_COLOR_BUFFER_BIT, WantScaleMethodNearest ? GL_NEAREST : GL_LINEAR);
	glBindFramebuffer(GL_FRAMEBUFFER, 0);
	SDL_GL_SwapWindow(VideoWindow);
	glClearColor(clearcolor[0], clearcolor[1], clearcolor[2], clearcolor[3]);
	if (has_scissor) {
		glEnable(GL_SCISSOR_TEST);
	}
	glBindFramebuffer(GL_READ_FRAMEBUFFER, OpenGLCurrentReadFBO);
	glBindFramebuffer(GL_DRAW_FRAMEBUFFER, OpenGLCurrentDrawFBO);
}
