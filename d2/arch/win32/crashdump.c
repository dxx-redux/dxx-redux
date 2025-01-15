// Contains handlers for automatically generating crash dumps on Windows.

#include <windows.h>
#include <DbgHelp.h>
#include "dxxerror.h"

void win32_create_dump(EXCEPTION_POINTERS* exceptionPointers)
{
	// Append the current date and time to the dump file name.
	char dumpFileName[MAX_PATH];
	SYSTEMTIME stLocalTime;
	GetLocalTime(&stLocalTime);
	wsprintf(dumpFileName, "d2x-redux_%04d%02d%02d_%02d%02d%02d.dmp",
			 stLocalTime.wYear, stLocalTime.wMonth, stLocalTime.wDay,
			 stLocalTime.wHour, stLocalTime.wMinute, stLocalTime.wSecond);

	HANDLE hDumpFile = CreateFile(dumpFileName, GENERIC_WRITE, 0, NULL, CREATE_ALWAYS, FILE_ATTRIBUTE_NORMAL, NULL);
	if (hDumpFile != INVALID_HANDLE_VALUE) {
		MINIDUMP_EXCEPTION_INFORMATION exceptionInfo;
		exceptionInfo.ThreadId = GetCurrentThreadId();
		exceptionInfo.ExceptionPointers = exceptionPointers;
		exceptionInfo.ClientPointers = FALSE;

		MiniDumpWriteDump(GetCurrentProcess(), GetCurrentProcessId(), hDumpFile, MiniDumpWithDataSegs, &exceptionInfo, NULL, NULL);
		CloseHandle(hDumpFile);
	}
}

LONG WINAPI win32_exception_handler(EXCEPTION_POINTERS* exceptionPointers)
{
	win32_create_dump(exceptionPointers);
	return EXCEPTION_CONTINUE_SEARCH;
}
