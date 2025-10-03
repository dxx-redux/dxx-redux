#ifndef RESAMPLE_H
#define RESAMPLE_H

#include "fresample.h"
#define DITHER_SEED 0xc90fdaa2

struct resample {
	struct lfr_filter *filter;
	lfr_fixed_t inv_ratio;
};

static struct resample *resample_init(int inrate, int outrate) {
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

static void resample_process(struct resample *resample, int srclen, void *srcbuf, int dstlen, void *dstbuf) {
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

static void resample_done(struct resample *resample) {
	lfr_filter_free(resample->filter);
	free(resample);
}

#endif
