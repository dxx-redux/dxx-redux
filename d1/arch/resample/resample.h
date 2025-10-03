void *resample_init(const unsigned int inRate, const unsigned int outRate);
void resample_process(void *ppobj, const unsigned int inN, const float *in, const unsigned int outN, float *out);
void resample_done(void *ppobj);
