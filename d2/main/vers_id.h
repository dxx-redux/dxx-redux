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

#define D2XMAJOR __stringize(DXX_VERSION_MAJORi)
#define D2XMINOR __stringize(DXX_VERSION_MINORi)
#define D2XMICRO __stringize(DXX_VERSION_MICROi)

#define BASED_VERSION "Full Version v1.2"
#define VERSION D2XMAJOR "." D2XMINOR "." D2XMICRO
#define DESCENT_VERSION "D2X-Redux " RH_VERSION "-mnu"

extern const char g_descent_build_datetime[21];

#endif /* _VERS_ID */
