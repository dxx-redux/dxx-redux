/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "cpu.h"

/*
  This is ORed with CPU flags, to mark the CPU flags as valid.
*/
#define LFR_CPU_FLAGS_SET 0x80000000u

const struct lfr_cpuf LFR_CPUF[] = {
#if defined(LFR_CPU_X86)
    { "mmx", LFR_CPUF_MMX },
    { "sse", LFR_CPUF_SSE },
    { "sse2", LFR_CPUF_SSE2 },
    { "sse3", LFR_CPUF_SSE3 },
    { "ssse3", LFR_CPUF_SSSE3 },
    { "sse4_1", LFR_CPUF_SSE4_1 },
    { "sse4_2", LFR_CPUF_SSE4_2 },
#elif defined(LFR_CPU_PPC)
    { "altivec", LFR_CPUF_ALTIVEC },
#endif
    { "", 0 }
};

#if defined(CPU_HASFLAGS)

unsigned lfr_cpuflags;

unsigned
lfr_setcpufeatures(unsigned flags)
{
    unsigned x;
    x = (lfr_getcpuflags() & flags) | LFR_CPU_FLAGS_SET;
    lfr_cpuflags = x;
    return x & ~LFR_CPU_FLAGS_SET;
}

#else

unsigned
lfr_setcpufeatures(unsigned flags)
{
    (void) flags;
    return 0;
}

#endif

/* ========================================
   OS X sysctl
   ======================================== */

#if !defined(CPUF) && defined(__APPLE__)
#define CPUF 1
#include <sys/sysctl.h>
#include <string.h>

/* The sysctl names are the same as the flag names we chase, with the
   exception of ssse3, whose sysctl name is supplementalsse3.  */

unsigned
lfr_getcpuflags(void)
{
    int enabled, flags = 0, r, i, k;
    size_t length;
    char name[32];
    const char *fname, *pfx = "hw.optional.";

    k = strlen(pfx);
    memcpy(name, pfx, k);
    for (i = 0; LFR_CPUF[i].name[0]; ++i) {
        fname = LFR_CPUF[i].name;
#if defined(LFR_CPU_X86)
        if (LFR_CPUF[i].flag == LFR_CPUF_SSSE3)
            fname = "supplementalsse3";
#endif
        strcpy(name + k, fname);
        length = sizeof(enabled);
        r = sysctlbyname(name, &enabled, &length, NULL, 0);
        if (r == 0 && enabled != 0)
            flags |= LFR_CPUF[i].flag;
    }
    return LFR_CPU_FLAGS_SET | flags;
}

#endif

/* ========================================
   x86 CPUID
   ======================================== */

#if !defined(CPUF) && defined(LFR_CPU_X86)
#define CPUF 1

struct lfr_cpu_idmap {
    signed char idflag;
    unsigned char cpuflag;
};

static const struct lfr_cpu_idmap LFR_CPU_EDX[] = {
    { 23, LFR_CPUF_MMX },
    { 25, LFR_CPUF_SSE },
    { 26, LFR_CPUF_SSE2 },
    { -1, 0 }
};

static const struct lfr_cpu_idmap LFR_CPU_ECX[] = {
    { 0, LFR_CPUF_SSE3 },
    { 9, LFR_CPUF_SSSE3 },
    { 19, LFR_CPUF_SSE4_1 },
    { 20, LFR_CPUF_SSE4_2 },
    { -1, 0 }
};

static unsigned
lfr_getcpuflags_x86_1(unsigned reg, const struct lfr_cpu_idmap *mp)
{
    int i;
    unsigned fl = 0;
    for (i = 0; mp[i].idflag >= 0; ++i) {
        if ((reg & (1u << mp[i].idflag)) != 0 && mp[i].cpuflag > 0)
            fl |= mp[i].cpuflag;
    }
    return fl;
}

static unsigned
lfr_getcpuflags_x86(unsigned edx, unsigned ecx)
{
    return LFR_CPU_FLAGS_SET |
        lfr_getcpuflags_x86_1(edx, LFR_CPU_EDX) |
        lfr_getcpuflags_x86_1(ecx, LFR_CPU_ECX);
}

#if defined(__GNUC__)

unsigned
lfr_getcpuflags(void)
{
    unsigned a, b, c, d;
#if defined(__i386__) && defined(__PIC__)
    /* %ebx is used by PIC, so we can't clobber it */
    __asm__(
        "xchgl\t%%ebx, %1\n\t"
        "cpuid\n\t"
        "xchgl\t%%ebx, %1"
        : "=a"(a), "=r"(b), "=c"(c), "=d"(d)
        : "0"(1));
#else
    __asm__(
        "cpuid"
        : "=a"(a), "=b"(b), "=c"(c), "=d"(d)
        : "0"(1));
#endif
    return lfr_getcpuflags_x86(d, c);
}

#elif defined(_MSC_VER)

unsigned
lfr_getcpuflags(void)
{
    int info[4];
    __cpuid(info, 1);
    return lfr_getcpuflags_x86(info[3], info[2]);
}

#else
#warning "Unknown compiler, no CPUID support"

unsigned
lfr_getcpuflags(void)
{
    return LFR_CPU_FLAGS_SET;
}

#endif

#endif

/* ========================================
   Fallback
   ======================================== */

#if !defined(CPUF) && defined(CPU_HASFLAGS)
#warning "Unknown OS, cannot determine cpu features at runtime"

unsigned
lfr_getcpuflags(void)
{
    return LFR_CPU_FLAGS_SET;
}

#endif
