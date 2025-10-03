/*
 * This is an alternate backend for the sound effect system.
 * It uses SDL_mixer to provide a more reliable playback,
 * and allow processing of multiple audio formats.
 *
 * This file is based on the original D1X arch/sdl/digi.c
 *
 *  -- MD2211 (2006-10-12)
 */

#include <stdlib.h>
#include <stdio.h>
#include <string.h>

#include <SDL.h>
#include <SDL_audio.h>
#include <SDL_mixer.h>

#include <samplerate.h>

#include "pstypes.h"
#include "dxxerror.h"
#include "sounds.h"
#include "digi.h"
#include "digi_mixer.h"
#include "digi_mixer_music.h"
#include "console.h"
#include "config.h"
#include "args.h"

#include "fix.h"
#include "gr.h" // needed for piggy.h
#include "piggy.h"

#define MIX_DIGI_DEBUG 0
#define MIX_OUTPUT_FORMAT	AUDIO_S16
#define MIX_OUTPUT_CHANNELS	2

#define MAX_SOUND_SLOTS 64
#define SOUND_BUFFER_SIZE 512
#define MIN_VOLUME 10

static int digi_initialised = 0;
static int digi_max_channels = MAX_SOUND_SLOTS;
static inline int fix2byte(fix f) { return f < 0 ? 0 : f >= 65536 ? 255 : f / 256; }
Mix_Chunk SoundChunks[MAX_SOUNDS];
ubyte channels[MAX_SOUND_SLOTS];

#ifdef __linux__
static int digi_mixer_check_soundfont(const char *path, void *data)
{
	FILE *file = fopen(path, "r");
	if (!file)
		return 0;
	fclose(file);
	return 1;
}
#endif

/* Initialise audio */
int digi_mixer_init()
{
	digi_sample_rate = SAMPLE_RATE_44K;

	if (MIX_DIGI_DEBUG) con_printf(CON_DEBUG,"digi_init %d (SDL_Mixer)\n", MAX_SOUNDS);
	if (SDL_InitSubSystem(SDL_INIT_AUDIO) < 0) Error("SDL audio initialisation failed: %s.", SDL_GetError());

	#ifdef __linux__
	// Use the soundfont in the AppImage if no other sound font specified
	Mix_Init(0); // hack to set soundfont_paths on Debian patched SDL-mixer
	if (!Mix_EachSoundFont(digi_mixer_check_soundfont, NULL) && getenv("APPDIR"))
	{
		char soundfonts[PATH_MAX];
		snprintf(soundfonts, sizeof(soundfonts),
			"%s/usr/share/sounds/sf3/default-GM.sf3", getenv("APPDIR"));
		Mix_SetSoundFonts(soundfonts);
	}
	#endif

	if (Mix_OpenAudio(digi_sample_rate, MIX_OUTPUT_FORMAT, MIX_OUTPUT_CHANNELS, SOUND_BUFFER_SIZE))
	{
		//edited on 10/05/98 by Matt Mueller - should keep running, just with no sound.
		con_printf(CON_URGENT,"\nError: Couldn't open audio: %s\n", SDL_GetError());
		GameArg.SndNoSound = 1;
		return 1;
	}

	digi_max_channels = Mix_AllocateChannels(digi_max_channels);
	memset(channels, 0, MAX_SOUND_SLOTS);
	Mix_Pause(0);

	digi_initialised = 1;

	digi_mixer_set_digi_volume( (GameCfg.DigiVolume*32768)/8 );

	return 0;
}

/* Shut down audio */
void digi_mixer_close() {
	if (MIX_DIGI_DEBUG) con_printf(CON_DEBUG,"digi_close (SDL_Mixer)\n");
	if (!digi_initialised) return;
	digi_initialised = 0;
	Mix_CloseAudio();
}

/* channel management */
int digi_mixer_find_channel()
{
	int i;
	for (i = 0; i < digi_max_channels; i++)
		if (channels[i] == 0)
			return i;
	return -1;
}

void digi_mixer_free_channel(int channel_num)
{
	channels[channel_num] = 0;
}

#if 0
void *resampler;
#include "resample_c.h"
#endif

#include "fresample.h"
#define DITHER_SEED 0xc90fdaa2
struct resample {
	struct lfr_filter *filter;
	lfr_fixed_t inv_ratio;
};
struct resample *resampler;
struct resample *resample_init(int inrate, int outrate) {
	struct lfr_filter *fp;
	struct lfr_param *param;

	param = lfr_param_new();
	lfr_param_seti(param, LFR_PARAM_INRATE, inrate);
	lfr_param_seti(param, LFR_PARAM_OUTRATE, outrate);
	lfr_param_seti(param, LFR_PARAM_QUALITY, 5); // medium
	fp = NULL;
	lfr_filter_new(&fp, param);
	lfr_param_free(param);

	struct resample *ret = malloc(sizeof(struct resample));
	ret->filter = fp;
	ret->inv_ratio = (((lfr_fixed_t) inrate << 32) + outrate / 2) / outrate;

	return ret;
}

void resample_process(struct resample *resample, int srclen, void *srcbuf, int dstlen, void *dstbuf) {
	lfr_fixed_t pos, inv_ratio;
	unsigned dither;

	pos = -lfr_filter_delay(resample->filter);
	dither = DITHER_SEED;
	lfr_resample(
		&pos, resample->inv_ratio, &dither, 1,
		dstbuf, LFR_FMT_S16_NATIVE, dstlen,
		srcbuf, LFR_FMT_S16_NATIVE, srclen,
		resample->filter);
}

void resample_done(struct resample *resample) {
	lfr_filter_free(resample->filter);
	free(resample);
}

#if 0
#define SKP_Silk_RESAMPLER_MAX_IIR_ORDER 6
#define SKP_assert(x)

#define SKP_int32       int
#define SKP_int16       short
#define SKP_int16_MAX   0x7FFF                              //  2^15 - 1 =  32767
#define SKP_int16_MIN   ((SKP_int16)0x8000)                 // -2^15     = -32768

#define SKP_ADD32(a, b)                    ((a) + (b))
#define SKP_SUB32(a, b)                    ((a) - (b))
#define SKP_LSHIFT32(a, shift)             ((a)<<(shift))                // shift >= 0, shift < 32
#define SKP_RSHIFT32(a, shift)             ((a)>>(shift))                // shift >= 0, shift < 32
#define SKP_LSHIFT(a, shift)               SKP_LSHIFT32(a, shift)        // shift >= 0, shift < 32
#define SKP_SAT16(a)                       ((a) > SKP_int16_MAX ? SKP_int16_MAX : \
                                           ((a) < SKP_int16_MIN ? SKP_int16_MIN : (a)))
#define SKP_RSHIFT_ROUND(a, shift)        ((shift) == 1 ? ((a) >> 1) + ((a) & 1) : (((a) >> ((shift) - 1)) + 1) >> 1)

// (a32 * (SKP_int32)((SKP_int16)(b32))) >> 16 output have to be 32bit int
#define SKP_SMULWB(a32, b32)            ((((a32) >> 16) * (SKP_int32)((SKP_int16)(b32))) + ((((a32) & 0x0000FFFF) * (SKP_int32)((SKP_int16)(b32))) >> 16))

// a32 + (b32 * (SKP_int32)((SKP_int16)(c32))) >> 16 output have to be 32bit int
#define SKP_SMLAWB(a32, b32, c32)       ((a32) + ((((b32) >> 16) * (SKP_int32)((SKP_int16)(c32))) + ((((b32) & 0x0000FFFF) * (SKP_int32)((SKP_int16)(c32))) >> 16)))

/* Tables for 2x upsampler, high quality */
const SKP_int16 SKP_Silk_resampler_up2_hq_0[ 2 ] = {  4280, 33727 - 65536 };
const SKP_int16 SKP_Silk_resampler_up2_hq_1[ 2 ] = { 16295, 54015 - 65536 };
/* Matlab code for the notch filter coefficients: */
/* B = [1, 0.12, 1];  A = [1, 0.055, 0.8]; G = 0.87; freqz(G * B, A, 2^14, 16e3); axis([0, 8000, -10, 1]);  */
/* fprintf('\t%6d, %6d, %6d, %6d\n', round(B(2)*2^16), round(-A(2)*2^16), round((1-A(3))*2^16), round(G*2^15)) */
const SKP_int16 SKP_Silk_resampler_up2_hq_notch[ 4 ] = { 7864,  -3604,  13107,  28508 };

void SKP_Silk_resampler_private_up2_HQ(
	//SKP_int32	                    *S,			    /* I/O: Resampler state [ 6 ]					*/
	SKP_int16                       *out,           /* O:   Output signal [ 2 * len ]               */
	const SKP_int16                 *in,            /* I:   Input signal [ len ]                    */
	SKP_int32                       len             /* I:   Number of INPUT samples                 */
)
{
	SKP_int32 k;
	SKP_int32 in32, out32_1, out32_2, Y, X;


	SKP_int32       S[ SKP_Silk_RESAMPLER_MAX_IIR_ORDER ] = { 0, 0, 0, 0, 0, 0 };

	SKP_assert( SKP_Silk_resampler_up2_hq_0[ 0 ] > 0 );
	SKP_assert( SKP_Silk_resampler_up2_hq_0[ 1 ] < 0 );
	SKP_assert( SKP_Silk_resampler_up2_hq_1[ 0 ] > 0 );
	SKP_assert( SKP_Silk_resampler_up2_hq_1[ 1 ] < 0 );

	/* Internal variables and state are in Q10 format */
	for( k = 0; k < len; k++ ) {
		/* Convert to Q10 */
		in32 = SKP_LSHIFT( (SKP_int32)in[ k ], 10 );

		/* First all-pass section for even output sample */
		Y       = SKP_SUB32( in32, S[ 0 ] );
		X       = SKP_SMULWB( Y, SKP_Silk_resampler_up2_hq_0[ 0 ] );
		out32_1 = SKP_ADD32( S[ 0 ], X );
		S[ 0 ]  = SKP_ADD32( in32, X );

		/* Second all-pass section for even output sample */
		Y       = SKP_SUB32( out32_1, S[ 1 ] );
		X       = SKP_SMLAWB( Y, Y, SKP_Silk_resampler_up2_hq_0[ 1 ] );
		out32_2 = SKP_ADD32( S[ 1 ], X );
		S[ 1 ]  = SKP_ADD32( out32_1, X );

		/* Biquad notch filter */
		out32_2 = SKP_SMLAWB( out32_2, S[ 5 ], SKP_Silk_resampler_up2_hq_notch[ 2 ] );
		out32_2 = SKP_SMLAWB( out32_2, S[ 4 ], SKP_Silk_resampler_up2_hq_notch[ 1 ] );
		out32_1 = SKP_SMLAWB( out32_2, S[ 4 ], SKP_Silk_resampler_up2_hq_notch[ 0 ] );
		S[ 5 ]  = SKP_SUB32(  out32_2, S[ 5 ] );

		/* Apply gain in Q15, convert back to int16 and store to output */
		out[ 2 * k ] = (SKP_int16)SKP_SAT16( SKP_RSHIFT32( 
			SKP_SMLAWB( 256, out32_1, SKP_Silk_resampler_up2_hq_notch[ 3 ] ), 9 ) );

		/* First all-pass section for odd output sample */
		Y       = SKP_SUB32( in32, S[ 2 ] );
		X       = SKP_SMULWB( Y, SKP_Silk_resampler_up2_hq_1[ 0 ] );
		out32_1 = SKP_ADD32( S[ 2 ], X );
		S[ 2 ]  = SKP_ADD32( in32, X );

		/* Second all-pass section for odd output sample */
		Y       = SKP_SUB32( out32_1, S[ 3 ] );
		X       = SKP_SMLAWB( Y, Y, SKP_Silk_resampler_up2_hq_1[ 1 ] );
		out32_2 = SKP_ADD32( S[ 3 ], X );
		S[ 3 ]  = SKP_ADD32( out32_1, X );

		/* Biquad notch filter */
		out32_2 = SKP_SMLAWB( out32_2, S[ 4 ], SKP_Silk_resampler_up2_hq_notch[ 2 ] );
		out32_2 = SKP_SMLAWB( out32_2, S[ 5 ], SKP_Silk_resampler_up2_hq_notch[ 1 ] );
		out32_1 = SKP_SMLAWB( out32_2, S[ 5 ], SKP_Silk_resampler_up2_hq_notch[ 0 ] );
		S[ 4 ]  = SKP_SUB32(  out32_2, S[ 4 ] );

		/* Apply gain in Q15, convert back to int16 and store to output */
		out[ 2 * k + 1 ] = (SKP_int16)SKP_SAT16( SKP_RSHIFT32( 
			SKP_SMLAWB( 256, out32_1, SKP_Silk_resampler_up2_hq_notch[ 3 ] ), 9 ) );
	}
}
#endif


/*
 * Play-time conversion. Performs output conversion only once per sound effect used.
 * Once the sound sample has been converted, it is cached in SoundChunks[]
 */
void mixdigi_convert_sound(int i)
{
	//SDL_AudioCVT cvt;
	Uint8 *data = GameSounds[i].data;
	Uint32 dlen = GameSounds[i].length;
	int freq = GameSounds[i].freq;
	//int bits = GameSounds[i].bits;

	if (SoundChunks[i].abuf) return; //proceed only if not converted yet

	if (data)
	{
#if 1
		if (!resampler)
			resampler = resample_init(freq, digi_sample_rate);

		if (dlen >= 3 && data[0] == 0 && data[1] == 0 && data[2] == 0x80) {
			dlen -= 2;
			data += 2;
		}
		
		int outlen = dlen * (digi_sample_rate + freq - 1) / freq;
		Sint16 *srcbuf = malloc(dlen * sizeof(Sint16));
		Sint16 *dstbuf = malloc(outlen * sizeof(Sint16));
		Sint16 *out = malloc(outlen * sizeof(Sint16) * 2);

		for (int i = 0; i < dlen; i++)
			srcbuf[i] = (data[i] - 128) * 232;
		resample_process(resampler, dlen, srcbuf, outlen, dstbuf);

		for (int i = 0; i < outlen; i++)
			out[i * 2] = out[i * 2 + 1] = dstbuf[i];

		free(dstbuf);
		free(srcbuf);

		SoundChunks[i].abuf = out;
		SoundChunks[i].alen = outlen * sizeof(Sint16) * 2;
		SoundChunks[i].allocated = 1;
		SoundChunks[i].volume = 128; // Max volume = 128
#elif 0
		int outlen = dlen * 4;
		SKP_int16 *srcbuf = malloc(outlen * sizeof(SKP_int16));
		SKP_int16 *dstbuf = malloc(dlen * 2 * sizeof(SKP_int16));
		Sint16 *out = malloc(outlen * sizeof(Sint16) * 2);

		for (int i = 0; i < dlen; i++)
			srcbuf[i] = (data[i] - 128) * 256;

		SKP_Silk_resampler_private_up2_HQ(dstbuf, srcbuf, dlen);
		SKP_Silk_resampler_private_up2_HQ(srcbuf, dstbuf, dlen * 2);

		for (int i = 0; i < outlen; i++)
			out[i * 2] = out[i * 2 + 1] = srcbuf[i];
		free(dstbuf);
		free(srcbuf);

		SoundChunks[i].abuf = out;
		SoundChunks[i].alen = outlen * sizeof(Sint16) * 2;
		SoundChunks[i].allocated = 1;
		SoundChunks[i].volume = 128; // Max volume = 128
#elif 1
		if (!resampler)
			resampler = resample_init(freq, digi_sample_rate);
		
		int outlen = dlen * (digi_sample_rate + freq - 1) / freq;
		float *srcbuf = malloc(dlen * sizeof(float));
		float *dstbuf = malloc(outlen * sizeof(float));
		Sint16 *out = malloc(outlen * sizeof(Sint16) * 2);

		for (int i = 0; i < dlen; i++)
			srcbuf[i] = (data[i] - 128) / 128.0f;
		resample_process(resampler, dlen, srcbuf, outlen, dstbuf);
		/*
		SRC_DATA conv;
		int ret;
		conv.data_in = srcbuf;
		conv.data_out = dstbuf;
		conv.input_frames = dlen;
		conv.output_frames = outlen;
		conv.src_ratio = (double)digi_sample_rate / (double)freq;
		ret = src_simple(&conv, SRC_SINC_MEDIUM_QUALITY, 1);
		outlen = conv.output_frames_gen;
		*/

		for (int i = 0; i < outlen; i++)
			out[i * 2] = out[i * 2 + 1] = dstbuf[i] * 30000;

		free(dstbuf);
		free(srcbuf);

		SoundChunks[i].abuf = out;
		SoundChunks[i].alen = outlen * sizeof(Sint16) * 2;
		SoundChunks[i].allocated = 1;
		SoundChunks[i].volume = 128; // Max volume = 128
#else
		if (MIX_DIGI_DEBUG) con_printf(CON_DEBUG,"converting %d (%d)\n", i, dlen);
		SDL_BuildAudioCVT(&cvt, AUDIO_U8, 1, freq, MIX_OUTPUT_FORMAT, MIX_OUTPUT_CHANNELS, digi_sample_rate);

		cvt.buf = malloc(dlen * cvt.len_mult);
		cvt.len = dlen;
		memcpy(cvt.buf, data, dlen);
		if (SDL_ConvertAudio(&cvt)) con_printf(CON_DEBUG,"conversion of %d failed\n", i);

		SoundChunks[i].abuf = cvt.buf;
		SoundChunks[i].alen = cvt.len_cvt;
		SoundChunks[i].allocated = 1;
		SoundChunks[i].volume = 128; // Max volume = 128
#endif
	}
}

// Volume 0-F1_0
int digi_mixer_start_sound(short soundnum, fix volume, int pan, int looping, int loop_start, int loop_end, int soundobj)
{
	int mix_vol = fix2byte(fixmul(digi_volume, volume));
	int mix_pan = fix2byte(pan);
	int mix_loop = looping * -1;
	int channel;

	if (!digi_initialised) return -1;
	Assert(GameSounds[soundnum].data != (void *)-1);

	mixdigi_convert_sound(soundnum);

	if (MIX_DIGI_DEBUG) con_printf(CON_DEBUG,"digi_start_sound %d, volume %d, pan %d (start=%d, end=%d)\n", soundnum, mix_vol, mix_pan, loop_start, loop_end);

	channel = digi_mixer_find_channel();
	if (channel == -1)
		return -1;

	Mix_PlayChannel(channel, &(SoundChunks[soundnum]), mix_loop);
	Mix_SetPanning(channel, 255-mix_pan, mix_pan);
	if (volume > F1_0)
		Mix_SetDistance(channel, 0);
	else
		Mix_SetDistance(channel, 255-mix_vol);
	channels[channel] = 1;
	Mix_ChannelFinished(digi_mixer_free_channel);

	return channel;
}

void digi_mixer_set_channel_volume(int channel, int volume)
{
	int mix_vol = fix2byte(volume);
	if (!digi_initialised) return;
	Mix_SetDistance(channel, 255-mix_vol);
}

void digi_mixer_set_channel_pan(int channel, int pan)
{
	int mix_pan = fix2byte(pan);
	Mix_SetPanning(channel, 255-mix_pan, mix_pan);
}

void digi_mixer_stop_sound(int channel)
{
	if (!digi_initialised) return;
	if (MIX_DIGI_DEBUG) con_printf(CON_DEBUG,"digi_stop_sound %d\n", channel);
	Mix_HaltChannel(channel);
	channels[channel] = 0;
}

void digi_mixer_end_sound(int channel)
{
	digi_mixer_stop_sound(channel);
	channels[channel] = 0;
}

void digi_mixer_set_digi_volume( int dvolume )
{
	digi_volume = dvolume;
	if (!digi_initialised) return;
	Mix_Volume(-1, fix2byte(dvolume));
}

int digi_mixer_is_sound_playing(int soundno) { return 0; }
int digi_mixer_is_channel_playing(int channel) { return 0; }

void digi_mixer_reset() {}
void digi_mixer_stop_all_channels()
{
	Mix_HaltChannel(-1);
	memset(channels, 0, MAX_SOUND_SLOTS);
}

extern void digi_end_soundobj(int channel);

 //added on 980905 by adb to make sound channel setting work
void digi_mixer_set_max_channels(int n) { }
int digi_mixer_get_max_channels() { return digi_max_channels; }
// end edit by adb


#ifndef NDEBUG
void digi_mixer_debug() {}
#endif
