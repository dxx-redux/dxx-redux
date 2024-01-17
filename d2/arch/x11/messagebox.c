/*
 *  messagebox.c
 *  d1x-rebirth
 *
 *  Display an error or warning messagebox using the OS's window server.
 *
 */

#include "window.h"
#include "event.h"
#include "messagebox.h"
#include "x11messagebox.h"

void display_x11_alert(const char *message, int error)
{
	d_event	event;
	window	*wind;
	int		fullscreen;

	// Handle Descent's windows properly
	if ((wind = window_get_front()))
		WINDOW_SEND_EVENT(wind, EVENT_WINDOW_DEACTIVATED);

	if (grd_curscreen && (fullscreen = gr_check_fullscreen()))
		gr_toggle_fullscreen();

	const char *title = error?"Sorry, a critical error has occurred.":"Attention!";
	const char *buttons[] = {"Ok"};
	x11MessageBox(title, message, buttons, 1);

	if ((wind = window_get_front()))
		WINDOW_SEND_EVENT(wind, EVENT_WINDOW_ACTIVATED);
	
	if (grd_curscreen && !error && fullscreen)
		gr_toggle_fullscreen();
}

void msgbox_warning(char *message)
{
	display_x11_alert(message, 0);
}

void msgbox_error(const char *message)
{
	display_x11_alert(message, 1);
}
