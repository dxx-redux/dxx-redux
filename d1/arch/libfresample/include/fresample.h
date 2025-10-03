/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#ifndef FRESAMPLE_H
#define FRESAMPLE_H
#include <stddef.h>
#ifdef __cplusplus
extern "C" {
#endif

#define LFR_PUBLIC
#if defined(LFR_IMPLEMENTATION)
# define LFR_PRIVATE
#endif

#if defined(_MSC_VER)
# define LFR_INT64 __int64
#endif

#if defined(__GNUC__)
# undef LFR_PRIVATE
# undef LFR_PUBLIC
# if defined(LFR_IMPLEMENTATION)
#  define LFR_PRIVATE __attribute__((visibility("hidden")))
#  if defined(__ELF__)
#   define LFR_PUBLIC __attribute__((visibility("protected")))
#  else
#   define LFR_PUBLIC __attribute__((visibility("default")))
#  endif
# else
#  define LFR_PUBLIC
# endif
# define LFR_INT64 long long
#endif

#if defined(_M_X64) || defined(__x86_64__)
# define LFR_CPU_X64 1
# define LFR_CPU_X86 1
#elif defined(_M_IX86) || defined(__i386__)
# define LFR_CPU_X86 1
#elif defined(__ppc64__)
# define LFR_CPU_PPC64 1
# define LFR_CPU_PPC 1
#elif defined(__ppc__)
# define LFR_CPU_PPC 1
#endif

#define LFR_LITTLE_ENDIAN 1234
#define LFR_BIG_ENDIAN 4321

#if defined(__BYTE_ORDER__)
# if __BYTE_ORDER__ == __ORDER_BIG_ENDIAN__
#  define LFR_BYTE_ORDER LFR_BIG_ENDIAN
# elif __BYTE_ORDER__ == __ORDER_LITTLE_ENDIAN__
#  define LFR_BYTE_ORDER LFR_LITTLE_ENDIAN
# endif
#elif defined(__BIG_ENDIAN__)
# define LFR_BYTE_ORDER LFR_BIG_ENDIAN
#elif defined(__LITTLE_ENDIAN__)
# define LFR_BYTE_ORDER LFR_LITTLE_ENDIAN
#endif

#if !defined(LFR_BYTE_ORDER)
# if defined(LFR_CPU_X86)
#  define LFR_BYTE_ORDER LFR_LITTLE_ENDIAN
# elif defined(LFR_CPU_PPC)
#  define LFR_BYTE_ORDER LFR_BIG_ENDIAN
# else
#  error "cannot determine machine byte order"
# endif
#endif

/* ========================================
   CPU features
   ======================================== */

/*
  CPU features to use or disable.
*/
enum {
#if defined(LFR_CPU_X86)
    LFR_CPUF_MMX     = (1u << 0),
    LFR_CPUF_SSE     = (1u << 1),
    LFR_CPUF_SSE2    = (1u << 2),
    LFR_CPUF_SSE3    = (1u << 3),
    LFR_CPUF_SSSE3   = (1u << 4),
    LFR_CPUF_SSE4_1  = (1u << 5),
    LFR_CPUF_SSE4_2  = (1u << 6),
#else
    LFR_CPUF_MMX     = 0u,
    LFR_CPUF_SSE     = 0u,
    LFR_CPUF_SSE2    = 0u,
    LFR_CPUF_SSE3    = 0u,
    LFR_CPUF_SSSE3   = 0u,
    LFR_CPUF_SSE4_1  = 0u,
    LFR_CPUF_SSE4_2  = 0u,
#endif

#if defined(LFR_CPU_PPC)
    LFR_CPUF_ALTIVEC = (1u << 0),
#else
    LFR_CPUF_ALTIVEC = 0u,
#endif

    LFR_CPUF_NONE = 0u,
    LFR_CPUF_ALL = 0xffffffffu
};

/*
  Information about a CPU flag.
*/
struct lfr_cpuf {
    char name[8];
    unsigned flag;
};

/*
  Array of names for the CPU features this architecture supports.
  Names are the lower case version of the flag names above, e.g.,
  LFR_CPUF_MMX becomes "mmx".  Terminated by a zeroed entry.
*/
LFR_PUBLIC extern const struct lfr_cpuf LFR_CPUF[];

/*
  Set which CPU features are allowed or disallowed.  This is primarily
  used for comparing the performance and correctness of vector
  implementations and scalar implementations.  It can also be used to
  prohibit features that your CPU supports but which your OS does not.

  Returns the CPU flags actually enabled, which will be the
  intersection of the set of allowed flags (the argument) with the set
  of features that the current CPU actually supports.
*/
LFR_PUBLIC unsigned
lfr_setcpufeatures(unsigned flags);

/* ========================================
   Sample formats
   ======================================== */

/*
  Audio sample formats.
*/
typedef enum {
    LFR_FMT_U8,
    LFR_FMT_S16BE,
    LFR_FMT_S16LE,
    LFR_FMT_S24BE,
    LFR_FMT_S24LE,
    LFR_FMT_F32BE,
    LFR_FMT_F32LE
} lfr_fmt_t;

#define LFR_FMT_COUNT ((int) LFR_FMT_F32LE + 1)

#if LFR_BYTE_ORDER == LFR_BIG_ENDIAN
# define LFR_FMT_S16_NATIVE LFR_FMT_S16BE
# define LFR_FMT_S16_SWAPPED LFR_FMT_S16LE
# define LFR_FMT_F32_NATIVE LFR_FMT_F32BE
# define LFR_FMT_F32_SWAPPED LFR_FMT_F32LE
#else
# define LFR_FMT_S16_NATIVE LFR_FMT_S16LE
# define LFR_FMT_S16_SWAPPED LFR_FMT_S16BE
# define LFR_FMT_F32_NATIVE LFR_FMT_F32LE
# define LFR_FMT_F32_SWAPPED LFR_FMT_F32BE
#endif

/*
  Swap the byte order on 16-bit data.  The destination can either be
  the same buffer as the source, or it can be a non-overlapping
  buffer.  Behavior is undefined if the two buffers partially overlap.
*/
LFR_PUBLIC void
lfr_swap16(void *dest, const void *src, size_t count);

/* ========================================
   Resampling parameters
   ======================================== */

/*
  Names for filter quality presets.
*/
enum {
    /*
      Low quality: Currently, quality 0..3 are identical, since
      further reductions in quality do not increase performance.
    */
    LFR_QUALITY_LOW = 2,

    /*
      Medium quality: Transition band of 23%, nominal attenuation of
      60 dB.  Actual attenuation may be higher.
    */
    LFR_QUALITY_MEDIUM = 5,

    /*
      High quality: Transition band of 10%, nominal attenuation of 96
      dB.  It is not normally reasonable to increase quality beyond
      this level unless you are competing for the prettiest
      spectrogram.
    */
    LFR_QUALITY_HIGH = 8,

    /*
      Ultra quality: Transition band of 3%, nominal attenuation of 120
      dB.  Filter coefficients may not fit in L2 cache.  Impulse
      response may be several milliseconds long.
    */
    LFR_QUALITY_ULTRA = 10
};

/*
  Parameters for the filter generator.

  Filter generation goes through two stages.

  1. In the first stage, the resampling parameters are used to create
  a filter specification.  The filter specification consists of
  normalized frequencies for the pass band, stop band, and stop band
  attenuation.  This stage uses simple logic to create a filter
  specification that "makes sense" for any input.  It relaxes the
  filter specification for ultrasonic (inaudible) frequencies and
  ensures that enough of the input signal passes through.

  2. In the second stage, an FIR filter is generated that fits the
  filter specified by the first stage.

  Normally, for resampling, you will specify QUALITY, INRATE, and
  OUTRATE.  A filter specification for the conversion will be
  automatically generated with the given subjective quality level.

  If you are a signal-processing guru, you can create the filter
  specification directly by setting FPASS, FSTOP, and ATTEN.
*/
typedef enum {
    /*
      High level filter parameters.  These are typically the only
      parameters you will need to set.
    */

    /* Filter quality, default 8.  An integer between 0 and 10 which
       determines the default values for other parameters.  */
    LFR_PARAM_QUALITY,

    /* Input sample rate, in Hz.  The default is -1, which creates a
       generic filter.  */
    LFR_PARAM_INRATE,

    /* Output sample rate.  If the input sample rate is specified,
       then this is measured in Hz.  Otherwise, if the input rate is
       -1, then this value is relative to the input sample rate (so
       you would use 0.5 for downsampling by a factor of two).
       Defaults to the same value as the input sample rate, which
       creates a filter which can be used for upsampling at any ratio.
       Note that increasing the output rate above the input rate has
       no effect, all upsampling filters for a given input frequency
       are identical.  */
    LFR_PARAM_OUTRATE,

    /*
      Medium level filter parameters.  These parameters affect how the
      filter specification is generated from the input and output
      sample rates.  Most of these parameters have default values
      which depend on the QUALITY setting.
    */

    /* The width of the filter transition band, as a fraction of the
       input sample rate.  This value will be enlarged to extend the
       transition band to MAXFREQ and will be narrowed to extend the
       pass band to MINBW.  The default value depends on the QUALITY
       setting, and gets narrower as QUALITY increases.  */
    LFR_PARAM_FTRANSITION,

    /* Maximum audible frequency.  The pass band will be narrowed to
       fit within the range of audible frequencies.  Default value is
       20 kHz if the input frequency is set, otherwise this parameter
       is unused.  If you want to preserve ultrasonic frequencies,
       disable this parameter by setting it to -1.  This is disabled
       by default at absurd quality settings (9 and 10).  */
    LFR_PARAM_MAXFREQ,

    /* A flag which allows aliasing noise as long as it is above
       MAXFREQ.  This flag improves the subjective quality of
       low-quality filters by increasing their bandwidth, but causes
       problems for high-quality filters by increasing noise.  Default
       value depends on QUALITY setting, and is set for low QUALITY
       values.  Has no effect if MAXFREQ is disabled.  */
    LFR_PARAM_LOOSE,

    /* Minimum size of pass band, as a fraction of the output
       bandwidth.  This prevents the filter designer from filtering
       out the entire signal, which can happen when downsampling by a
       large enough ratio.  The default value is 0.5 at low quality
       settings, and higher at high quality settings.  The filter size
       can increase dramatically as this number approaches 1.0.  */
    LFR_PARAM_MINFPASS,

    /*
      Filter specification.  These parameters are normally generated
      from the higher level parameters.  If the filter specification
      is set, then the higher level parameters will all be ignored.
    */

    /* The end of the pass band, as a fraction of the input sample
       rate.  Normally, the filter designer chooses this value.  */
    LFR_PARAM_FPASS,

    /* The start of the stop band, as a fraction of the input sample
       rate.  Normally, the filter designer chooses this value.  */
    LFR_PARAM_FSTOP,

    /* Desired stop band attenuation, in dB.  Larger numbers are
       increase filter quality.  Default value depends on the QUALITY
       setting.  */
    LFR_PARAM_ATTEN
} lfr_param_t;

#define LFR_PARAM_COUNT ((int) LFR_PARAM_ATTEN + 1)

/*
  Get the name of a parameter, or return NULL if the paramater does
  not exist.
*/
LFR_PUBLIC const char *
lfr_param_name(lfr_param_t pname);

/*
  Get the index of a parameter by name, or return -1 if the parameter
  does not exist.
*/
LFR_PUBLIC int
lfr_param_lookup(const char *pname, size_t len);

/*
  A set of filter parameters.
*/
struct lfr_param;

/*
  Create a new filter parameter set.  Returns NULL if out of memory.
*/
LFR_PUBLIC struct lfr_param *
lfr_param_new(void);

/*
  Free a filter parameter set.
*/
LFR_PUBLIC void
lfr_param_free(struct lfr_param *param);

/*
  Duplicate a filter parameter set.  Returns NULL if out of memory.
*/
LFR_PUBLIC struct lfr_param *
lfr_param_copy(struct lfr_param *param);

/*
  Set an integer-valued parameter.
*/
LFR_PUBLIC void
lfr_param_seti(struct lfr_param *param, lfr_param_t pname, int value);

/*
  Set a float-valued parameter.
*/
LFR_PUBLIC void
lfr_param_setf(struct lfr_param *param, lfr_param_t pname, double value);

/*
  Get the value of a parameter as an integer.  This will compute the
  parameter value if necessary.
*/
LFR_PUBLIC void
lfr_param_geti(struct lfr_param *param, lfr_param_t pname, int *value);

/*
  Get the value of a parameter as a floating-point number, or -1 if
  the parameter is unset.  This will compute the parameter value if
  necessary.
*/
LFR_PUBLIC void
lfr_param_getf(struct lfr_param *param, lfr_param_t pname, double *value);

/* ========================================
   Resampling
   ======================================== */

/*
  Information queries that a filter responds to.  Each query gives an
  integer or floating point result, automatically cast to the type
  requested.
*/
enum {
    /*
      The size of the filter, also known as the filter's order.  This
      does not include oversampling.  The resampler will read this
      many samples, starting with the current position (rounded down),
      whenever it calculates a sample.

      In other words, this is the amount of overlap needed when
      resampling consecutive buffers.
    */
    LFR_INFO_SIZE,

    /*
      Filter delay.  Note that lfr_filter_delay returns a fixed point
      number and is usually preferable.
    */
    LFR_INFO_DELAY,

    /*
      The is the number of bytes used by filter coefficients.
    */
    LFR_INFO_MEMSIZE,

    /*
      The normalized pass frequency the filter was designed with.
    */
    LFR_INFO_FPASS,

    /*
      The normalized stop frequency the filter was designed with.
    */
    LFR_INFO_FSTOP,

    /*
      The stopband attenuation, in dB, that the filter was designed
      with.
    */
    LFR_INFO_ATTEN
};

#define LFR_INFO_COUNT (LFR_INFO_ATTEN + 1)

/*
  Get the name of a filter info query, or return NULL if the info query
  does not exist.
*/
LFR_PUBLIC const char *
lfr_info_name(int pname);

/*
  Get the index of a filter info query by name, or return -1 if the
  info query does not exist.
*/
LFR_PUBLIC int
lfr_info_lookup(const char *pname, size_t len);

/*
  A 32.32 fixed point number.  This is used for expressing fractional
  positions in a buffer.

  When used for converting from sample rate f_s to f_d, the timing
  error at time t is bounded by t * 2^-33 * r_d / r_s.  For a
  pessimistic conversion ratio, 8 kHz -> 192 kHz, this means that it
  will take at least five days to accumulate one millisecond of error.
*/
typedef LFR_INT64 lfr_fixed_t;

/*
  A filter for resampling audio.
*/
struct lfr_filter;

/*
  Create a low-pass filter with the given parameters.
*/
LFR_PUBLIC void
lfr_filter_new(struct lfr_filter **fpp, struct lfr_param *param);

/*
  Free a low-pass filter.
*/
LFR_PUBLIC void
lfr_filter_free(struct lfr_filter *fp);

/*
  Get the delay of a filter, in fixed point.  Filters are causal, so
  you can subtract the filter delay from the position to create a
  non-causal filter with zero delay.
*/
LFR_PUBLIC lfr_fixed_t
lfr_filter_delay(const struct lfr_filter *fp);

/*
  Query the filter and return an integer value.  This will cast if
  necessary.
*/
LFR_PUBLIC void
lfr_filter_geti(const struct lfr_filter *fp, int iname, int *value);

/*
  Query the filter and return a floating-point value.  This will cast
  if necessary.
*/
LFR_PUBLIC void
lfr_filter_getf(const struct lfr_filter *fp, int iname, double *value);

/*
  Resample an audio buffer.  Note that this function may need to
  create intermediate buffers if there is no function which can
  directly operate on the input and output formats.  No intermediate
  buffers will be necessary if the following conditions are met:

  - Input and output formats are identical.

  - Sample format is either S16_NATIVE or F32_NATIVE.

  - The number of channels is either 1 (mono) or 2 (stereo).

  pos: Current position relative to the start of the input buffer,
  expressed as a 32.32 fixed point number.  On return, this will
  contain the updated position.  Positions outside the input buffer
  are acceptable, it will be treated as if the input buffer were
  padded with an unlimited number of zeroes on either side.

  inv_ratio: Inverse of the resampling ratio, expressed as a 32.32
  fixed point number.  This number is equal to the input sample rate
  divided by the output sample rate.

  dither: State of the PRNG used for dithering.

  nchan: Number of interleaved channels.

  out, in: Input and output buffers.  The buffers are not permitted to
  alias each other.

  outlen, inlen: Length of buffers, in frames.  Note that the length
  type is 'int' instead of 'size_t'; this matches the precision of
  buffer positions.

  outfmt, infmt: Format of input and output buffers.

  filter: A suitable low-pass filter for resampling at the given
  ratio.
*/
LFR_PUBLIC void
lfr_resample(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio,
    unsigned *dither, int nchan,
    void *out, lfr_fmt_t outfmt, int outlen,
    const void *in, lfr_fmt_t infmt, int inlen,
    const struct lfr_filter *filter);

/*
  Specialized resampling function, designed to work with a specific
  sample format and filter type.  Do not attempt to reuse a function
  for a different filter.

  Note that lengths are still measured in frames, but since the
  function is specialized for a given format and number of channels,
  there is no need to specify the format or number of channels.
*/
typedef void
(*lfr_resample_func_t)(
    lfr_fixed_t *pos, lfr_fixed_t inv_ratio, unsigned *dither,
    void *out, int outlen, const void *in, int inlen,
    const struct lfr_filter *filter);

/*
  Get a function for resampling native 16-bit data with the given
  number of channels.  Returns NULL if no such function is available.
  This will always return non-NULL for mono and stereo data.
*/
LFR_PUBLIC lfr_resample_func_t
lfr_resample_s16func(int nchan, const struct lfr_filter *filter);

#ifdef __cplusplus
}
#endif
#endif
