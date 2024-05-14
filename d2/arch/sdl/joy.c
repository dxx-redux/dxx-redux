/*
 *
 * SDL joystick support
 *
 */

#include <string.h>   // for memset

#include "joy.h"
#include "dxxerror.h"
#include "timer.h"
#include "console.h"
#include "event.h"
#include "text.h"
#include "u_mem.h"
#include "playsave.h"
#include "kconfig.h"

int num_joysticks = 0;
int joy_num_axes = 0;

/* This struct is a "virtual" joystick, which includes all the axes
 * and buttons of every joystick found.
 */
static struct joyinfo {
	int n_axes;
	int n_buttons;
	int axis_value[JOY_MAX_AXES];
	ubyte button_state[JOY_MAX_BUTTONS];
	ubyte button_last_state[JOY_MAX_BUTTONS]; // for HAT movement only
} Joystick;

typedef struct d_event_joystickbutton
{
	event_type type;
	int button;
} d_event_joystickbutton;

typedef struct d_event_joystick_moved
{
	event_type	type;	// EVENT_JOYSTICK_MOVED
	int		axis;
	int 		value;
} d_event_joystick_moved;

/* This struct is an array, with one entry for each physical joystick
 * found.
 */
static struct {
	SDL_Joystick *handle;
	int n_axes;
	int n_buttons;
	int n_hats;
	int hat_map[MAX_HATS_PER_JOYSTICK];  //Note: Descent expects hats to be buttons, so these are indices into Joystick.buttons
	int axis_map[MAX_AXES_PER_JOYSTICK];
	int button_map[MAX_BUTTONS_PER_JOYSTICK];
	int axis_button_map[MAX_AXES_PER_JOYSTICK];
} SDL_Joysticks[MAX_JOYSTICKS];

void joy_button_handler(SDL_JoyButtonEvent *jbe)
{
	int button;
	d_event_joystickbutton event;

	button = SDL_Joysticks[jbe->which].button_map[jbe->button];

	Joystick.button_state[button] = jbe->state;

	event.type = (jbe->type == SDL_JOYBUTTONDOWN) ? EVENT_JOYSTICK_BUTTON_DOWN : EVENT_JOYSTICK_BUTTON_UP;
	event.button = button;
	con_printf(CON_DEBUG, "Sending event %s, button %d\n", (jbe->type == SDL_JOYBUTTONDOWN) ? "EVENT_JOYSTICK_BUTTON_DOWN" : "EVENT_JOYSTICK_JOYSTICK_UP", event.button);
	event_send((d_event *)&event);
}

void joy_hat_handler(SDL_JoyHatEvent *jhe)
{
	int hat = SDL_Joysticks[jhe->which].hat_map[jhe->hat];
	int hbi;
	d_event_joystickbutton event;

	//Save last state of the hat-button
	Joystick.button_last_state[hat  ] = Joystick.button_state[hat  ];
	Joystick.button_last_state[hat+1] = Joystick.button_state[hat+1];
	Joystick.button_last_state[hat+2] = Joystick.button_state[hat+2];
	Joystick.button_last_state[hat+3] = Joystick.button_state[hat+3];

	//get current state of the hat-button
	Joystick.button_state[hat  ] = ((jhe->value & SDL_HAT_UP)>0);
	Joystick.button_state[hat+1] = ((jhe->value & SDL_HAT_RIGHT)>0);
	Joystick.button_state[hat+2] = ((jhe->value & SDL_HAT_DOWN)>0);
	Joystick.button_state[hat+3] = ((jhe->value & SDL_HAT_LEFT)>0);

	//determine if a hat-button up or down event based on state and last_state
	for(hbi=0;hbi<4;hbi++)
	{
		if( !Joystick.button_last_state[hat+hbi] && Joystick.button_state[hat+hbi]) //last_state up, current state down
		{
			event.type = EVENT_JOYSTICK_BUTTON_DOWN;
			event.button = hat+hbi;
			con_printf(CON_DEBUG, "Sending event EVENT_JOYSTICK_BUTTON_DOWN, button %d\n", event.button);
			event_send((d_event *)&event);
		}
		else if(Joystick.button_last_state[hat+hbi] && !Joystick.button_state[hat+hbi])  //last_state down, current state up
		{
			event.type = EVENT_JOYSTICK_BUTTON_UP;
			event.button = hat+hbi;
			con_printf(CON_DEBUG, "Sending event EVENT_JOYSTICK_BUTTON_UP, button %d\n", event.button);
			event_send((d_event *)&event);
		}
	}
}

int joy_axis_handler(SDL_JoyAxisEvent *jae)
{
	int axis;
	d_event_joystick_moved event;

	axis = SDL_Joysticks[jae->which].axis_map[jae->axis];

	// inaccurate stick is inaccurate. SDL might send SDL_JoyAxisEvent even if the value is the same as before.
	if (Joystick.axis_value[axis] == jae->value/256)
		return 0;

	event.type = EVENT_JOYSTICK_MOVED;
	event.axis = axis;
	event.value = Joystick.axis_value[axis] = jae->value/256;
	con_printf(CON_DEBUG, "Sending event EVENT_JOYSTICK_MOVED, axis: %d, value: %d\n",event.axis, event.value);
	event_send((d_event *)&event);

	return 1;
}

int joy_apply_deadzone(int value, int deadzone)
{
	if (value > deadzone)
		return ((value - deadzone) * 128) / (128 - deadzone);
	else if (value < -deadzone)
		return ((value + deadzone) * 128) / (128 - deadzone);
	else
		return 0;
}

static int send_axis_button_event(unsigned button, event_type e)
{
	d_event_joystickbutton event;

	Joystick.button_state[button] = (e == EVENT_JOYSTICK_BUTTON_UP) ? 0 : 1;
	event.type = e;
	event.button = button;
	con_printf(CON_DEBUG, "Sending event %sEVENT_JOYSTICK_BUTTON_DOWN, button %d\n",
		(e == EVENT_JOYSTICK_BUTTON_UP ? "EVENT_JOYSTICK_BUTTON_UP" : "EVENT_JOYSTICK_BUTTON_DOWN"), button);
	event_send((d_event *)&event);
	return 1;
}

int joy_axisbutton_handler(SDL_JoyAxisEvent *jae)
{
	int button;
	int sent = 0;

	button = SDL_Joysticks[jae->which].axis_button_map[jae->axis];

	// We have to hardcode a deadzone here. It's not mapped into the settings.
	// We could add another deadzone slider called "axis button deadzone".
	// I think it's safe to assume a 30% deadzone on analog button presses for now.
	int deadzone = 38;
	int prev_value = joy_apply_deadzone(Joystick.axis_value[jae->axis], deadzone);
	int new_value = joy_apply_deadzone(jae->value/256, deadzone);

	if (prev_value <= 0 && new_value >= 0) // positive pressed
	{
		if (prev_value < 0) // Do previous direction release first if the case
			sent |= send_axis_button_event(button + 1, EVENT_JOYSTICK_BUTTON_UP);
		if (new_value > 0)
			sent |= send_axis_button_event(button, EVENT_JOYSTICK_BUTTON_DOWN);
	}
	else if (prev_value >= 0 && new_value <= 0) // negative pressed
	{
		if (prev_value > 0) // Do previous direction release first if the case
			sent |= send_axis_button_event(button, EVENT_JOYSTICK_BUTTON_UP);
		if (new_value < 0)
			sent |= send_axis_button_event(button + 1, EVENT_JOYSTICK_BUTTON_DOWN);
	}

	return sent;
}


/* ----------------------------------------------- */

void joy_init()
{
	int i,j,n;
	char temp[10];

	if (SDL_Init(SDL_INIT_JOYSTICK) < 0) {
		con_printf(CON_NORMAL, "sdl-joystick: initialisation failed: %s.",SDL_GetError());
		return;
	}

	memset(&Joystick,0,sizeof(Joystick));
	memset(joyaxis_text, 0, JOY_MAX_AXES * sizeof(char *));
	memset(joybutton_text, 0, JOY_MAX_BUTTONS * sizeof(char *));

	n = SDL_NumJoysticks();

	con_printf(CON_NORMAL, "sdl-joystick: found %d joysticks\n", n);
	for (i = 0; i < n; i++) {
		con_printf(CON_NORMAL, "sdl-joystick %d: %s\n", i, SDL_JoystickName(i));
		SDL_Joysticks[num_joysticks].handle = SDL_JoystickOpen(i);
		if (SDL_Joysticks[num_joysticks].handle) {

			SDL_Joysticks[num_joysticks].n_axes
				= SDL_JoystickNumAxes(SDL_Joysticks[num_joysticks].handle);
			if(SDL_Joysticks[num_joysticks].n_axes > MAX_AXES_PER_JOYSTICK)
			{
				Warning("sdl-joystick: found %d axes, only %d supported.\n", SDL_Joysticks[num_joysticks].n_axes, MAX_AXES_PER_JOYSTICK);
				Warning("sdl-joystick: found %d axes, only %d supported.\n", SDL_Joysticks[num_joysticks].n_axes, MAX_AXES_PER_JOYSTICK);
				SDL_Joysticks[num_joysticks].n_axes = MAX_AXES_PER_JOYSTICK;
			}

			SDL_Joysticks[num_joysticks].n_buttons
				= SDL_JoystickNumButtons(SDL_Joysticks[num_joysticks].handle);
			if(SDL_Joysticks[num_joysticks].n_buttons > MAX_BUTTONS_PER_JOYSTICK)
			{
				Warning("sdl-joystick: found %d buttons, only %d supported.\n", SDL_Joysticks[num_joysticks].n_buttons, MAX_BUTTONS_PER_JOYSTICK);
				SDL_Joysticks[num_joysticks].n_buttons = MAX_BUTTONS_PER_JOYSTICK;
			}

			SDL_Joysticks[num_joysticks].n_hats
				= SDL_JoystickNumHats(SDL_Joysticks[num_joysticks].handle);
			if(SDL_Joysticks[num_joysticks].n_hats > MAX_HATS_PER_JOYSTICK)
			{
				Warning("sdl-joystick: found %d hats, only %d supported.\n", SDL_Joysticks[num_joysticks].n_hats, MAX_HATS_PER_JOYSTICK);
				SDL_Joysticks[num_joysticks].n_hats = MAX_HATS_PER_JOYSTICK;
			}

			con_printf(CON_NORMAL, "sdl-joystick: %d axes\n", SDL_Joysticks[num_joysticks].n_axes);
			con_printf(CON_NORMAL, "sdl-joystick: %d buttons\n", SDL_Joysticks[num_joysticks].n_buttons);
			con_printf(CON_NORMAL, "sdl-joystick: %d hats\n", SDL_Joysticks[num_joysticks].n_hats);

			for (j=0; j < SDL_Joysticks[num_joysticks].n_axes; j++)
			{
				sprintf(temp, "J%d A%d", i + 1, j + 1);
				joyaxis_text[Joystick.n_axes] = d_strdup(temp);
				SDL_Joysticks[num_joysticks].axis_map[j] = Joystick.n_axes++;
			}
			for (j=0; j < SDL_Joysticks[num_joysticks].n_buttons; j++)
			{
				sprintf(temp, "J%d B%d", i + 1, j + 1);
				joybutton_text[Joystick.n_buttons] = d_strdup(temp);
				SDL_Joysticks[num_joysticks].button_map[j] = Joystick.n_buttons++;
			}
			for (j=0; j < SDL_Joysticks[num_joysticks].n_hats; j++)
			{
				if (Joystick.n_buttons + 4 > MAX_BUTTONS_PER_JOYSTICK)
					break;
				SDL_Joysticks[num_joysticks].hat_map[j] = Joystick.n_buttons;
				//a hat counts as four buttons
				sprintf(temp, "J%d H%d%c", i + 1, j + 1, 0202);
				joybutton_text[Joystick.n_buttons++] = d_strdup(temp);
				sprintf(temp, "J%d H%d%c", i + 1, j + 1, 0177);
				joybutton_text[Joystick.n_buttons++] = d_strdup(temp);
				sprintf(temp, "J%d H%d%c", i + 1, j + 1, 0200);
				joybutton_text[Joystick.n_buttons++] = d_strdup(temp);
				sprintf(temp, "J%d H%d%c", i + 1, j + 1, 0201);
				joybutton_text[Joystick.n_buttons++] = d_strdup(temp);
			}
			for (j=0; j < SDL_Joysticks[num_joysticks].n_axes; j++)
			{
				if (Joystick.n_buttons + 2 > MAX_BUTTONS_PER_JOYSTICK)
					break;
				SDL_Joysticks[num_joysticks].axis_button_map[j] = Joystick.n_buttons;
				//an axis count as 2 buttons. negative - and positive +
				sprintf(temp, "J%d -A%d", i + 1, j + 1);
				joybutton_text[Joystick.n_buttons++] = d_strdup(temp);
				sprintf(temp, "J%d +A%d", i + 1, j + 1);
				joybutton_text[Joystick.n_buttons++] = d_strdup(temp);
			}

			num_joysticks++;
		}
		else
			con_printf(CON_NORMAL, "sdl-joystick: initialization failed!\n");

		con_printf(CON_NORMAL, "sdl-joystick: %d axes (total)\n", Joystick.n_axes);
		con_printf(CON_NORMAL, "sdl-joystick: %d buttons (total)\n", Joystick.n_buttons);
	}

	joy_num_axes = Joystick.n_axes;
}

void joy_close()
{
	SDL_JoystickClose(SDL_Joysticks[num_joysticks].handle);

	while (Joystick.n_axes--)
		d_free(joyaxis_text[Joystick.n_axes]);
	while (Joystick.n_buttons--)
		d_free(joybutton_text[Joystick.n_buttons]);
}

void event_joystick_get_axis(d_event *event, int *axis, int *value)
{
	Assert(event->type == EVENT_JOYSTICK_MOVED);

	*axis  = ((d_event_joystick_moved *)event)->axis;
	*value = ((d_event_joystick_moved *)event)->value;
}

void joy_flush()
{
	int i;

	if (!num_joysticks)
		return;

	for (i = 0; i < Joystick.n_buttons; i++)
		Joystick.button_state[i] = SDL_RELEASED;
}

int event_joystick_get_button(d_event *event)
{
	Assert((event->type == EVENT_JOYSTICK_BUTTON_DOWN) || (event->type == EVENT_JOYSTICK_BUTTON_UP));
	return ((d_event_joystickbutton *)event)->button;
}
