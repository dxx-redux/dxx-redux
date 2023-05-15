/*
 *
 * SDL library timer functions
 *
 */

#include <SDL.h>

#include "maths.h"
#include "timer.h"
#include "config.h"

static fix64 F64_RunTime = 0;
static int64_t usec_runtime = 0;

#ifdef WIN32
#include <windows.h>
#ifndef CREATE_WAITABLE_TIMER_HIGH_RESOLUTION
#define CREATE_WAITABLE_TIMER_HIGH_RESOLUTION 2
#endif

static DWORD create_timer_flags = CREATE_WAITABLE_TIMER_HIGH_RESOLUTION;
static LARGE_INTEGER start, freq;
#else
#include <unistd.h>
#include <errno.h>
#include <time.h>
#ifdef __linux__
#include <sys/prctl.h>
#endif

static struct timespec start;
#endif

void timer_init(void)
{
#ifdef WIN32
	DWORD test_flags = CREATE_WAITABLE_TIMER_HIGH_RESOLUTION;
	HANDLE timer = CreateWaitableTimerExW(NULL, NULL, test_flags, TIMER_ALL_ACCESS);
	if (timer) {
		create_timer_flags = CREATE_WAITABLE_TIMER_HIGH_RESOLUTION;
        CloseHandle(timer);
	} else
		create_timer_flags = 0;
	if (!QueryPerformanceCounter(&start) || !QueryPerformanceFrequency(&freq))
		Error("QueryPerformanceCounter not working");
#else
	if (clock_gettime(CLOCK_MONOTONIC, &start))
		Error("clock_gettime CLOCK_MONOTONIC not working");
#ifdef __linux__
	prctl(PR_SET_TIMERSLACK, 1000);
#endif
#endif
}

void timer_delay_usec(int64_t usec)
{
#ifdef WIN32
	LARGE_INTEGER timeout;
	HANDLE timer;

	if (!(timer = CreateWaitableTimerExW(NULL, NULL, create_timer_flags, TIMER_ALL_ACCESS)) &&
		create_timer_flags) {
		create_timer_flags = 0;
		timer = CreateWaitableTimerExW(NULL, NULL, create_timer_flags, TIMER_ALL_ACCESS);
	}

	if (!timer) {
		Sleep(usec / 1000);
		return;
	}

	timeout.QuadPart = -(usec * 10); // negative for relative timeout
	if (!SetWaitableTimerEx(timer, &timeout, 0, NULL, NULL, NULL, 0)) {
		CloseHandle(timer);
		Sleep(usec / 1000);
		return;
	}

	if (WaitForSingleObject(timer, INFINITE) == WAIT_FAILED)
		Sleep(usec / 1000);

	CloseHandle(timer);
#else
	struct timespec ts, rem;

	ts.tv_sec = usec / 1000000;
	ts.tv_nsec = (usec % 1000000) * 1000;
	while (nanosleep(&ts, &rem) && errno == EINTR)
		ts = rem;
#endif
}

static int64_t timer_current_usec()
{
#ifdef WIN32
	LARGE_INTEGER counter;

	QueryPerformanceCounter(&counter);
	counter.QuadPart -= start.QuadPart;
	return counter.QuadPart * 1000000 / freq.QuadPart;
#else
	struct timespec ts;

	clock_gettime(CLOCK_MONOTONIC, &ts);
	ts.tv_sec -= start.tv_sec;
	if ((ts.tv_nsec -= start.tv_nsec) < 0) {
		ts.tv_nsec += 1000000000;
		ts.tv_sec--;
	}
	return ts.tv_sec * 1000000 + ts.tv_nsec / 1000;
#endif
}

void timer_update(void)
{
	static ubyte init = 1;
	static fix64 last_tv = 0;
	fix64 cur_tv;

	usec_runtime = timer_current_usec();
	cur_tv = usec_runtime*F1_0/1000000;

	if (init)
	{
		last_tv = cur_tv;
		init = 0;
	}

	if (last_tv < cur_tv) // in case SDL_GetTicks wraps, don't update and have a little hickup
		F64_RunTime += (cur_tv - last_tv); // increment! this value will overflow long after we are all dead... so why bother checking?
	last_tv = cur_tv;
}

fix64 timer_query(void)
{
	return (F64_RunTime);
}

int64_t timer_query_usec(void)
{
	return usec_runtime;
}

void timer_delay(fix seconds)
{
	SDL_Delay(f2i(fixmul(seconds, i2f(1000))));
}

// Replacement for timer_delay which considers calc time the program needs between frames (not reentrant)
void timer_delay2(int fps)
{
	static u_int32_t FrameStart=0;
	u_int32_t FrameLoop=0;

	while (FrameLoop < 1000/(GameCfg.VSync?MAXIMUM_FPS:fps))
	{
		u_int32_t tv_now = SDL_GetTicks();
		if (FrameStart > tv_now)
			FrameStart = tv_now;
		if (!GameCfg.VSync)
			SDL_Delay(1);
		FrameLoop=tv_now-FrameStart;
	}

	FrameStart=SDL_GetTicks();
}
