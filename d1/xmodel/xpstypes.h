/*
THE COMPUTER CODE CONTAINED HEREIN IS THE SOLE PROPERTY OF PARALLAX
SOFTWARE CORPORATION ("PARALLAX").  PARALLAX, IN DISTRIBUTING THE CODE TO
END-USERS, AND SUBJECT TO ALL OF THE TERMS AND CONDITIONS HEREIN, GRANTS A
ROYALTY-FREE, PERPETUAL LICENSE TO SUCH END-USERS FOR USE BY SUCH END-USERS
IN USING, DISPLAYING,  AND CREATING DERIVATIVE WORKS THEREOF, SO LONG AS
SUCH USE, DISPLAY OR CREATION IS FOR NON-COMMERCIAL, ROYALTY OR REVENUE
FREE PURPOSES.  IN NO EVENT SHALL THE END-USER USE THE COMPUTER CODE
CONTAINED HEREIN FOR REVENUE-BEARING PURPOSES.  THE END-USER UNDERSTANDS
AND AGREES TO THE TERMS HEREIN AND ACCEPTS THE SAME BY USE OF THIS FILE.
COPYRIGHT 1993-1999 PARALLAX SOFTWARE CORPORATION.  ALL RIGHTS RESERVED.
*/

/*
 *
 * Common types for use in Descent
 *
 */

#ifndef _PSTYPES_H
#define _PSTYPES_H

#include <stdint.h>

#ifdef _WIN32
# include <stdlib.h> // this is where minand max are defined
#endif
//#ifndef min
//#	define min(a,b) (((a)>(b))?(b):(a))
//#endif
//#ifndef max
//#	define max(a,b) (((a)<(b))?(b):(a))
//#endif

#if defined(_WIN32)
# ifdef __MINGW32__
#  include <sys/types.h>
# else
#  define PATH_MAX _MAX_PATH
# endif
# define FNAME_MAX 256
#elif defined(__unix__) || defined(__macosx__)
# include <sys/types.h>
# ifndef PATH_MAX
#  define PATH_MAX 1024
# endif
# define FNAME_MAX 256
#endif

#ifdef __macosx__
#	define uint16_t uint16_t
#endif

#ifndef NULL
#	define NULL 0
#endif

#if 1 // packing no longer needed
#define __xpack__
#else
// the following stuff has nothing to do with types but needed everywhere,
// and since this file is included everywhere, it's here.
#ifdef __GNUC__
# define __xpack__ __attribute__((packed))
#elif defined(_WIN32)
# pragma pack(push, packing)
# pragma pack(1)
# define __xpack__
#else
# error d2x will not work without packed structures
#endif
#endif

#ifdef _WIN32
#	ifndef _CDECL_
#		define _CDECL_	_cdecl
#	endif
#	else
#	define _CDECL_
#endif

#endif //_PSTYPES_H
