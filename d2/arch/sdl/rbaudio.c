/*
 *
 * SDL CD Audio functions
 *
 * Redbook audio support has been removed for SDL2.
 *
 */

#include "rbaudio.h"

void RBAExit(void)
{
}

void RBAInit(void)
{
}

int RBAEnabled(void)
{
	return 0;
}

int RBAPlayTrack(int track)
{
	(void)track;
	return -1;
}

void (*redbook_finished_hook)(void) = NULL;

void RBAStop(void)
{
	redbook_finished_hook = NULL;
}

void RBAEjectDisk(void)
{
}

void RBASetVolume(int volume)
{
	(void)volume;
}

void RBAPause(void)
{
}

int RBAResume(void)
{
	return -1;
}

int RBAPauseResume(void)
{
	return 0;
}

int RBAGetNumberOfTracks(void)
{
	return -1;
}

void RBACheckFinishedHook(void)
{
}

int RBAPlayTracks(int first, int last, void (*hook_finished)(void))
{
	(void)first;
	(void)last;
	redbook_finished_hook = hook_finished;
	return 0;
}

int RBAGetTrackNum(void)
{
	return 0;
}

int RBAPeekPlayStatus(void)
{
	return 0;
}

unsigned long RBAGetDiscID(void)
{
	return 0;
}

void RBAList(void)
{
}
