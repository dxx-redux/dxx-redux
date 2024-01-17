// Adapted from https://github.com/Eleobert/MessageBox-X11/blob/46f1f690f3f32dd26044f022f64ca3e051a3ecdc/messagebox.c
/*
 * Copyright (c) 2018 Eleobert Espírito Santo eleobert@hotmail.com
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */


#include <X11/Xlib.h>
#include <X11/Xatom.h>
#include <X11/Xutil.h>
#include <stdio.h>
#include <string.h>
#include <stdlib.h>
#include "x11messagebox.h"

struct Dimensions{
	//window
	unsigned int winMinWidth;
	unsigned int winMinHeight;
	//vertical space between lines
	unsigned int lineSpacing;
	unsigned int barHeight;
	//padding
	unsigned int pad_up;
	unsigned int pad_down;
	unsigned int pad_left;
	unsigned int pad_right;
	//button
	unsigned int btSpacing;
	unsigned int btMinWidth;
	unsigned int btMinHeight;
	unsigned int btLateralPad;

};

typedef struct ButtonData{ //<----see static
	const char* button;
	GC *gc;
	XRectangle rect;
}ButtonData;

//these values can be changed to whatever you prefer
static struct Dimensions dim = {400, 0, 5, 50, 30, 10, 30, 30, 20, 75, 25, 8};

static void setWindowTitle(const char* title, const Window *win, Display *dpy){
	Atom wm_Name = XInternAtom(dpy, "_NET_WM_NAME", False);
	Atom utf8Str = XInternAtom(dpy, "UTF8_STRING", False);

	Atom winType = XInternAtom(dpy, "_NET_WM_WINDOW_TYPE", False );
	Atom typeDialog = XInternAtom(dpy, "_NET_WM_WINDOW_TYPE_DIALOG", False );

	XChangeProperty(dpy, *win, wm_Name, utf8Str, 8, PropModeReplace, (const unsigned char*)title, (int)strlen(title));

	XChangeProperty( dpy,*win, winType, XA_ATOM,
					 32, PropModeReplace, (unsigned char *)&typeDialog,
					 1);
}

static void splitLines(const char* text, char ***str, int *count){
	const char *p, *next;
	char *lp;
	*count = 0;

	if (*text)
		(*count)++;
	for (p = text; (p = (const char *)strchr(p, '\n')); p++)
		(*count)++;

	*str = (char **)malloc((size_t)(*count)*sizeof(char*));

	p = text;
	for (int i = 0; ; i++) {
		if (!(next = strchr(p, '\n')))
			next = p + strlen(p);
		char *line = (*str)[i] = malloc(next - p + 1);
		memcpy(line, p, next - p);
		line[next - p] = 0;
		// convert tabs to spaces
		for (lp = line; (lp = strchr(lp, '\t')); lp++)
			*lp = ' ';
		if (!*next)
			break;
		p = next + 1;
	}
}

static void computeTextSize(XFontSet* fs, char** texts, int size,
		int lineHeight,
		unsigned int spaceBetweenLines,
		unsigned int *w,  unsigned int *h)
{
	int i;
	XRectangle rect	 = {0,0,0,0};
	*h = 0;
	*w = 0;
	for (i = 0; i < size; i++){
		const char *line = texts[i];
		Xutf8TextExtents(*fs, line, (int)strlen(line), &rect, NULL);
		*w = (rect.width > *w) ? (rect.width): *w;
		*h += lineHeight + spaceBetweenLines;
	}
}

static void createGC(GC* gc, const Colormap* cmap, Display* dpy, const	 Window* win,
			  unsigned char red, unsigned char green, unsigned char blue){
	float coloratio = (float) 65535 / 255;
	XColor color;
	*gc = XCreateGC (dpy, *win, 0,0);
	memset(&color, 0, sizeof(color));
	color.red	= (unsigned short)(coloratio * red	);
	color.green = (unsigned short)(coloratio * green);
	color.blue	= (unsigned short)(coloratio * blue );
	color.flags = DoRed | DoGreen | DoBlue;
	XAllocColor (dpy, *cmap, &color);
	XSetForeground (dpy, *gc, color.pixel);
}

static int isInside(int x, int y, XRectangle rect){
	if (x < rect.x || x > (rect.x + rect.width) || y < rect.y || y > (rect.y + rect.height))
		return 0;
	return 1;
}

int x11MessageBox(const char* title, const char* text, const char ** buttons, int numButtons)
{
	//setlocale(LC_ALL,"");

	//---- convert the text in list (to draw in multiply lines)---------------------------------------------------------
	char** text_splitted = NULL;
	int textLines = 0;
	splitLines(text, &text_splitted, &textLines);
	//------------------------------------------------------------------------------------------------------------------

	Display* dpy = NULL;
	dpy = XOpenDisplay(0);
	if (dpy == NULL){
		fprintf(stderr, "Error opening display display.\n");
		return -1;
	}

	int ds = DefaultScreen(dpy);
	Window win = XCreateSimpleWindow(dpy, RootWindow(dpy, ds), 0, 0, 800, 100, 1,
									 BlackPixel(dpy, ds), WhitePixel(dpy, ds));


	XSelectInput(dpy, win, ExposureMask | PointerMotionMask | ButtonPressMask | ButtonReleaseMask | KeyPressMask);
	XMapWindow(dpy, win);

	//allow windows to be closed by pressing cross button (but it wont close - see ClientMessage on switch)
	Atom WM_DELETE_WINDOW = XInternAtom(dpy, "WM_DELETE_WINDOW", False);
	XSetWMProtocols(dpy, win, &WM_DELETE_WINDOW, 1);

	//--create the gc for drawing text----------------------------------------------------------------------------------
	XGCValues gcValues;
	gcValues.font = XLoadFont(dpy, "7x13");
	gcValues.foreground = BlackPixel(dpy, 0);
	GC textGC = XCreateGC(dpy, win, GCFont + GCForeground, &gcValues);
	XFontStruct *fontInfo = XQueryFont(dpy, gcValues.font);
	XUnmapWindow( dpy, win );
	int lineHeight = (fontInfo->ascent + fontInfo->descent) * 7 / 5;
	//------------------------------------------------------------------------------------------------------------------

	//----create fontset-----------------------------------------------------------------------------------------------
	char **missingCharset_list = NULL;
	int missingCharset_count = 0;
	XFontSet fs;
	fs = XCreateFontSet(dpy,
			"-*-*-medium-r-*-*-*-140-75-75-*-*-*-*" ,
			&missingCharset_list, &missingCharset_count, NULL);

	if (missingCharset_count) {
		fprintf(stderr, "Missing charsets :\n");
		for (int i = 0; i < missingCharset_count; i++){
			fprintf(stderr, "%s\n", missingCharset_list[i]);
		}
		XFreeStringList(missingCharset_list);
		missingCharset_list = NULL;
	}
	//------------------------------------------------------------------------------------------------------------------

	Colormap cmap = DefaultColormap (dpy, ds);


	//resize the window according to the text size-----------------------------------------------------------------------
	unsigned int winW, winH;
	unsigned int textW, textH;

	//calculate the ideal window's size
	computeTextSize(&fs, text_splitted, textLines, lineHeight, dim.lineSpacing, &textW, &textH);
	unsigned int newWidth = textW + dim.pad_left + dim.pad_right;
	unsigned int newHeight = textH + dim.pad_up + dim.pad_down + dim.barHeight;
	winW = (newWidth > dim.winMinWidth)? newWidth: dim.winMinWidth;
	winH = (newHeight > dim.winMinHeight)? newHeight: dim.winMinHeight;

	//set windows hints
	XSizeHints hints;
	hints.flags		 = PSize | PMinSize | PMaxSize;
	hints.min_width	 = hints.max_width	= hints.base_width	= winW;
	hints.min_height = hints.max_height = hints.base_height = winH;

	XSetWMNormalHints( dpy, win, &hints );
	XMapRaised( dpy, win );
	//------------------------------------------------------------------------------------------------------------------

	GC barGC;
	GC buttonGC;
	GC buttonGC_underPointer;
	GC buttonGC_onClick;							   // GC colors
	createGC(&barGC, &cmap, dpy, &win,				   242, 242, 242);
	createGC(&buttonGC, &cmap, dpy, &win,			   202, 202, 202);
	createGC(&buttonGC_underPointer, &cmap, dpy, &win, 192, 192, 192);
	createGC(&buttonGC_onClick, &cmap, dpy, &win,	   182, 182, 182);

	//---setup the buttons data-----------------------------------------------------------------------------------------
	ButtonData *btsData;
	btsData = (ButtonData*)malloc((size_t)numButtons * sizeof(ButtonData));

	int pass = 0;
	for (int i = 0; i < numButtons; i++){
		btsData[i].button = buttons[i];
		btsData[i].gc = &buttonGC;
		XRectangle btTextDim;
		Xutf8TextExtents(fs, btsData[i].button, (int)strlen(btsData[i].button),
					   &btTextDim, NULL);
		btsData[i].rect.width = (btTextDim.width < dim.btMinWidth) ? dim.btMinWidth:
								   (btTextDim.width + 2 * dim.btLateralPad);
		btsData[i].rect.height = dim.btMinHeight;
		btsData[i].rect.x = winW - dim.pad_left - btsData[i].rect.width - pass;
		btsData[i].rect.y = textH + dim.pad_up + dim.pad_down + ((dim.barHeight - dim.btMinHeight)/ 2) ;
		pass += btsData[i].rect.width + dim.btSpacing;
	}
	//------------------------------------------------------------------------------------------------------------------

	setWindowTitle(title, &win, dpy);

	XFlush( dpy );


	int quit = 0;
	int res = -1;

	while (!quit) {
		KeySym keysym;
		XEvent e;
		XNextEvent( dpy, &e );

		switch (e.type) {
			case MotionNotify:
			case ButtonPress:
			case ButtonRelease:
				for (int i = 0; i < numButtons; i++) {
					btsData[i].gc = &buttonGC;
					if (isInside(e.xmotion.x, e.xmotion.y, btsData[i].rect)) {
						btsData[i].gc = &buttonGC_underPointer;
						if (e.type == ButtonPress && e.xbutton.button == Button1) {
							btsData[i].gc = &buttonGC_onClick;
							res = i;
							quit = 1;
						}
					}
				}

			case Expose:
				//draw the text in multiply lines----------------------------------------------------------------------
				for (int i = 0; i < textLines; i++) {
					Xutf8DrawString( dpy, win, fs, textGC, dim.pad_left, dim.pad_up + i * (lineHeight + dim.lineSpacing),
								   text_splitted[i], (int)strlen(text_splitted[i]));
				}
				//------------------------------------------------------------------------------------------------------
				XFillRectangle(dpy, win, barGC, 0, textH + dim.pad_up + dim.pad_down, winW, dim.barHeight);

				for (int i = 0; i < numButtons; i++) {
					XFillRectangle(dpy, win, *btsData[i].gc, btsData[i].rect.x, btsData[i].rect.y,
								   btsData[i].rect.width, btsData[i].rect.height);

					XRectangle btTextDim;
					Xutf8TextExtents(fs, btsData[i].button, (int)strlen(btsData[i].button),
								   &btTextDim, NULL);
					Xutf8DrawString( dpy, win, fs, textGC,
								   btsData[i].rect.x + (btsData[i].rect.width  - btTextDim.width ) / 2,
								   btsData[i].rect.y + (btsData[i].rect.height + btTextDim.height) / 2,
								   btsData[i].button, (int)strlen(btsData[i].button));
				}
				XFlush(dpy);

				break;

			case ClientMessage:
				//if window's cross button pressed does nothing
				//if((unsigned int)(e.xclient.data.l[0]) == WM_DELETE_WINDOW)
					//quit = true;
				break;

			case KeyPress:
				keysym = XLookupKeysym(&e.xkey, 0);
				if (keysym == XK_Escape) {
					res = -1;
					quit = 1;
				}
				if (keysym == XK_Return || keysym == XK_space) {
					res = 0;
					quit = 1;
				}
				break;

			default:
				break;
		}
	}

	for (int i = 0; i < textLines; i++) {
		free(text_splitted[i]);
	}
	free(text_splitted);
	free(btsData);
	if (missingCharset_list)
		XFreeStringList(missingCharset_list);
	XDestroyWindow(dpy, win);
	XFreeFontSet(dpy, fs);
	XFreeGC(dpy, textGC);
	XFreeGC(dpy, barGC);
	XFreeGC(dpy, buttonGC);
	XFreeGC(dpy, buttonGC_underPointer);
	XFreeGC(dpy, buttonGC_onClick);
	XFreeColormap(dpy, cmap);
	XCloseDisplay(dpy);

	return res;
}

#ifdef TEST
int main() {
	const char *buttons[] = {"Ok"};
	int ret = x11MessageBox("Hello World", "XThere are\nXmultiple lines\nXhere\n\nFinish", buttons, 1);
	printf("ret=%d\n", ret);
}
#endif
