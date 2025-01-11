/* Version defines */

#ifndef _VERS_ID
#define _VERS_ID

#define __stringize2(X)	#X
#define __stringize(X)	__stringize2(X)

#define DXX_VERSION_MAJOR __stringize(DXX_VERSION_MAJORi)
#define DXX_VERSION_MINOR __stringize(DXX_VERSION_MINORi)
#define DXX_VERSION_MICRO __stringize(DXX_VERSION_MICROi)

#if DXX_VERSION_MICROi
#define RH_VERSION DXX_VERSION_MAJOR "." DXX_VERSION_MINOR "." DXX_VERSION_MICRO
#else
#define RH_VERSION DXX_VERSION_MAJOR "." DXX_VERSION_MINOR
#endif

#define BASED_VERSION "Registered v1.5 Jan 5, 1996"
#define VERSION DXX_VERSION_MAJOR "." DXX_VERSION_MINOR "." DXX_VERSION_MICRO
#define DESCENT_VERSION "D1X-Redux " RH_VERSION

extern const char g_descent_build_datetime[21];

#endif /* _VERS_ID */
