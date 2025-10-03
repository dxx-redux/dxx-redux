/* Copyright 2012 Dietrich Epp <depp@zdome.net> */
#include "audio.h"
#include "common.h"
#include "file.h"
#include "fresample.h"

#include <assert.h>
#include <getopt.h>
#include <math.h>
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <time.h>
#include <unistd.h>

#define DITHER_SEED 0xc90fdaa2

static int verbose = 0;

enum {
    OPT_HELP = 'h',
    OPT_QUALITY = 'q',
    OPT_RATE = 'r',
    OPT_BENCH = 256,
    OPT_CPU_FEATURES,
    OPT_VERBOSE,
    OPT_VERSION,
    OPT_TEST_BUFSIZE,
    OPT_DUMP_SPECS,
    OPT_INRATE
};

static const struct option OPTIONS[] = {
    { "benchmark", required_argument, NULL, OPT_BENCH },
    { "cpu-features", required_argument, NULL, OPT_CPU_FEATURES },
    { "dump-specs", no_argument, NULL, OPT_DUMP_SPECS },
    { "help", no_argument, NULL, OPT_HELP },
    { "quality", required_argument, NULL, OPT_QUALITY },
    { "rate", required_argument, NULL, OPT_RATE },
    { "test-bufsize", no_argument, NULL, OPT_TEST_BUFSIZE },
    { "verbose", no_argument, NULL, OPT_VERBOSE },
    { "version", no_argument, NULL, OPT_VERSION },
    { "inrate", required_argument, NULL, OPT_INRATE },
    { NULL, 0, NULL, 0 }
};

static void
cpu_features_set(const char *str)
{
    char tmp[8];
    const char *p = str, *q;
    size_t n, i;
    int c;
    unsigned flags = 0, has_feature;

    while (1) {
        q = strchr(p, ',');
        n = q ? (size_t) (q - p) : strlen(p);
        if (n >= sizeof(tmp))
            goto unknown;
        if (!n)
            goto next;
        for (i = 0; i < n; ++i) {
            c = p[i];
            if (c >= 'A' && c <= 'Z')
                c += 'a' - 'A';
            tmp[i] = c;
        }
        tmp[n] = '\0';
        if (!strcmp(tmp, "all")) {
            flags |= LFR_CPUF_ALL;
            goto next;
        }
        if (!strcmp(tmp, "none"))
            goto next;
        for (i = 0; LFR_CPUF[i].name[0]; ++i) {
            if (!strcmp(tmp, LFR_CPUF[i].name)) {
                flags |= LFR_CPUF[i].flag;
                goto next;
            }
        }
        goto unknown;

    unknown:
        fprintf(stderr, "unknown CPU feature: %.*s\n", (int) n, tmp);
        goto next;

    next:
        p = p + n;
        if (!*p)
            break;
        p++;
    }

    flags = lfr_setcpufeatures(flags);
    if (verbose < 1)
        return;

    has_feature = 0;
    fputs("CPU features enabled: ", stderr);
    for (i = 0; LFR_CPUF[i].name[0]; ++i) {
        if ((flags & LFR_CPUF[i].flag) == 0)
            continue;
        if (has_feature)
            fputs(", ", stderr);
        fputs(LFR_CPUF[i].name, stderr);
        has_feature = 1;
    }
    if (!has_feature) {
        fputs("none (scalar implementations only)", stderr);
    }
    fputc('\n', stderr);
}

#define AUDIO_MINRATE 8000
#define AUDIO_MAXRATE 192000

static void
nchan_format(char *buf, size_t buflen, int nchan)
{
    if (nchan == 1)
        snprintf(buf, buflen, "mono");
    else if (nchan == 2)
        snprintf(buf, buflen, "stereo");
    else
        snprintf(buf, buflen, "%d channels", nchan);
}

static const char USAGE[] =
    "usage: fresample [OPTION..] IN OUT\n"
    "options:\n"
    "  --benchmark N        benchmark by converting N times, print speed\n"
    "  --cpu-features LIST  allow only CPU features in LIST\n"
    "  --dump-specs         dump filter specs and quit\n"
    "  -h, --help           show this help screen\n"
    "  -q, --quality Q      set conversion quality 0..10\n"
    "  -r, --rate R         set target sample rate\n"
    "  --verbose            print extra information\n"
    "  --version            print version number\n";

#define LCG_A  1103515245u
#define LCG_C       12345u

static void
test_bufsize(struct lfr_filter *fp, struct audio *ain, struct audio *aout,
             lfr_fixed_t inv_ratio, lfr_fixed_t pos0)
{
    int fsize, off, off0, len, minlen, i, j, nchan, ssize;
    unsigned char *buf, *ref;
    lfr_fixed_t pos;
    unsigned dither, dither0, dither1;
    char msgbuf[64];
    const char *what;
    short *px, *py;

    nchan = ain->nchan;
    ssize = (int) audio_format_size(aout->fmt) * nchan;
    buf = xmalloc(ssize * 32 + 32);
    ref = xmalloc(ssize * 32 + 32);
    lfr_filter_geti(fp, LFR_INFO_SIZE, &fsize);
    minlen = fsize + (int) (inv_ratio >> 26) + 1;

    if (ain->nframe < (size_t) minlen) {
        fprintf(stderr,
                "error: input too short for buffer size test\n"
                "  length: %zu, minimum length: %d\n",
                ain->nframe, minlen);
        exit(1);
    }

    off0 = 0;
    while (off0 * inv_ratio < pos0)
        off0 += 1;
    dither0 = DITHER_SEED;
    for (i = 0; i < off0 * nchan; ++i)
        dither0 = dither0 * LCG_A + LCG_C;

    for (len = 1; len <= 16; ++len) {
        dither1 = dither0;
        for (i = 0; i < len * nchan; ++i)
            dither1 = dither1 * LCG_A + LCG_C;
        for (off = 0; off < 16; ++off) {
            for (i = 0; i < 2; ++i) {
                memset(buf, i ? 0xff : 0x00, ssize * 32 + 32);
                pos = pos0 + off0 * inv_ratio;
                dither = dither0;
                lfr_resample(
                    &pos, inv_ratio, &dither, nchan,
                    buf + 16 + off, LFR_FMT_S16_NATIVE, len,
                    ain->data, LFR_FMT_S16_NATIVE, ain->nframe,
                    fp);

                memset(ref, i ? 0xff : 0x00, ssize * 32 + 32);
                memcpy(ref + 16 + off,
                       (const char *) aout->data + ssize * off0,
                       len * ssize);

                if (memcmp(ref, buf, ssize * 32 + 32)) {
                    for (j = 0; j < 16 + off; ++j) {
                        if (ref[j] != buf[j])
                            goto overrun;
                    }
                    for (j = 16 + off + ssize * len;
                         j < ssize * 32 + 32; ++j)
                    {
                        if (ref[j] != buf[j])
                            goto overrun;
                    }
                    px = xmalloc(ssize * len);
                    py = xmalloc(ssize * len);
                    memcpy(px, ref + 16 + off, ssize * len);
                    memcpy(py, buf + 16 + off, ssize * len);
                    if (verbose >= 1 || 1) {
                        fprintf(stderr, "data: [ref] [buf] [delta]\n");
                        for (j = 0; j < nchan * len; ++j) {
                            fprintf(stderr, "  %2d  %6d  %6d  %6d\n",
                                    j, px[j], py[j], py[j] - px[j]);
                        }
                    }
                    for (j = 0; j < nchan * len; ++j)
                        if (px[j] != py[j])
                            break;
                    assert(j < nchan * len);
                    snprintf(msgbuf, sizeof(msgbuf),
                             "invalid data at index %d", j);
                    what = msgbuf;
                    goto error;

                overrun:
                    snprintf(msgbuf, sizeof(msgbuf),
                             "buffer overrun at offset %d",
                             j - (16 + off));
                    what = msgbuf;
                    if (verbose >= 1 || 1) {
                        fprintf(stderr, "data: [ref] [buf]\n");
                        for (i = 0; i < ssize * 4 + 4; ++i) {
                            fprintf(stderr, "%4d ", i * 8);
                            for (j = 0; j < 8; ++j)
                                fprintf(stderr, " %02x", ref[i*8 + j]);
                            fputs("   ", stderr);
                            for (j = 0; j < 8; ++j)
                                fprintf(stderr, " %02x", buf[i*8 + j]);
                            fputc('\n', stderr);
                        }
                    }
                    goto error;
                }

                if (pos != pos0 + (off0 + len) * inv_ratio) {
                    what = "incorrect final position";
                    goto error;
                }

                if (dither1 != dither) {
                    what = "incorrect final dither";
                    goto error;
                }
            }
        }
    }

    return;

error:
    fprintf(
        stderr,
        "error: buffer test failed\n"
        "    len: %d, off: %d\n"
        "    %s\n",
        len, off, what);

    exit(1);
}

static void
dump_filter(struct lfr_filter *fp)
{
    int i;
    const char *name;
    double v;
    for (i = 0; ; ++i) {
        name = lfr_info_name(i);
        if (!name)
            break;
        lfr_filter_getf(fp, i, &v);
        printf("%s: %f\n", name, v);
    }
}

int
main(int argc, char *argv[])
{
    long v, benchmark, bi;
    int inrate, outrate, opt, nfiles, longindex = 0;
    size_t len;
    struct file_data din;
    struct audio ain, aout;
    char frate[AUDIO_RATE_FMTLEN], fnchan[32], *e, *files[2];
    struct lfr_filter *fp;
    struct lfr_param *param;
    FILE *file;
    clock_t t0, t1;
    lfr_fixed_t pos, pos0, inv_ratio;
    unsigned dither;
    double time, speed;
    int test_bufsize_flag = 0;
    int dump_specs = 0;

    param = lfr_param_new();
    if (!param)
        error("out of memory");
    benchmark = -1;
    nfiles = 0;
    inrate = -1;
    outrate = -1;
    while ((opt = getopt_long(argc, argv, ":hq:r:",
                              OPTIONS, &longindex)) != -1)
    {
        switch (opt) {
        case OPT_BENCH:
            benchmark = strtol(optarg, &e, 10);
            if (!*optarg || *e || benchmark < 1) {
                fprintf(stderr, "error: invalid benchmark count '%s'\n",
                        optarg);
                return 1;
            }
            break;

        case OPT_CPU_FEATURES:
            cpu_features_set(optarg);
            break;

        case OPT_HELP:
            fputs(USAGE, stderr);
            return 1;

        case OPT_QUALITY:
            v = strtol(optarg, &e, 10);
            if (!*optarg || *e) {
                fprintf(stderr, "error: invalid quality '%s'\n", optarg);
                return 1;
            }
            if (v > 10) v = 10;
            else if (v < 0) v = 0;
            lfr_param_seti(param, LFR_PARAM_QUALITY, (int) v);
            break;

        case OPT_RATE:
            outrate = audio_rate_parse(optarg);
            if (outrate < 0) {
                fprintf(stderr, "error: invalid sample rate '%s'\n", argv[1]);
                return 1;
            }
            break;

        case OPT_VERSION:
            fputs("FResample version 0.0\n", stdout);
            break;

        case OPT_VERBOSE:
            verbose += 1;
            break;

        case OPT_TEST_BUFSIZE:
            test_bufsize_flag += 1;
            break;

        case OPT_DUMP_SPECS:
            dump_specs = 1;
            break;

        case OPT_INRATE:
            inrate = audio_rate_parse(optarg);
            if (inrate < 0) {
                fprintf(stderr, "error: invalid sample rate '%s'\n", argv[1]);
                return 1;
            }
            break;

        case ':':
            fprintf(stderr, "error: option -%c requires an argument\n",
                    optopt);
            return 1;

        default:
        case '?':
            fprintf(stderr, "error: unrecognized option -%c\n", optopt);
            return 1;
        }
    }
    for (; optind < argc; ++optind) {
        if (nfiles >= 2) {
            fputs("error: too many files specified\n", stderr);
            return 1;
        }
        files[nfiles++] = argv[optind];
    }
    if (outrate < 0) {
        fputs("error: no rate specified\n", stderr);
        return 1;
    }

    if (dump_specs) {
        if (inrate < 0) {
            fputs("error: no input rate specified, "
                  "can't dump specs\n", stderr);
            return 1;
        }
        lfr_param_seti(param, LFR_PARAM_INRATE, inrate);
        lfr_param_seti(param, LFR_PARAM_OUTRATE, outrate);
        fp = NULL;
        lfr_filter_new(&fp, param);
        if (!fp)
            error("could not create filter");
        dump_filter(fp);
        return 0;
    }
    if (nfiles != 2) {
        fputs(USAGE, stderr);
        return 1;
    }

    file_read(&din, files[0]);

    audio_init(&ain);
    audio_init(&aout);
    audio_wav_load(&ain, din.data, din.length);
    if (inrate < 0)
        inrate = ain.rate;

    audio_rate_format(frate, sizeof(frate), inrate);
    nchan_format(fnchan, sizeof(fnchan), ain.nchan);
    if (verbose >= 1) {
        fprintf(stderr,
                "Input: %s, %s, %s, %zu samples\n",
                audio_format_name(ain.fmt), frate, fnchan, ain.nframe);
    }

    if (ain.nchan != 1 && ain.nchan != 2)
        error("unsupported number of channels "
              "(only mono and stereo supported)");

    if (inrate == outrate) {
        if (verbose >= 1) {
            fputs("No rate conversion necessary\n", stderr);
        }
        audio_alias(&aout, &ain);
    } else {
        audio_convert(&ain, LFR_FMT_S16_NATIVE);
        len = (size_t) floor(
            (double) ain.nframe * (double) outrate / (double) inrate + 0.5);

        audio_rate_format(frate, sizeof(frate), outrate);
        if (verbose >= 1) {
            fprintf(stderr,
                    "Output: %s, %s, %s, %zu samples\n",
                    audio_format_name(ain.fmt), frate, fnchan, len);
        }
        audio_alloc(&aout, len, ain.fmt, ain.nchan, outrate);

        inv_ratio =
            (((lfr_fixed_t) inrate << 32) + outrate / 2) / outrate;
        lfr_param_seti(param, LFR_PARAM_INRATE, inrate);
        lfr_param_seti(param, LFR_PARAM_OUTRATE, outrate);
        fp = NULL;
        lfr_filter_new(&fp, param);
        if (!fp)
            error("could not create filter");

        if (verbose >= 1)
            dump_filter(fp);

        pos0 = -lfr_filter_delay(fp);

        pos = pos0;
        dither = DITHER_SEED;
        lfr_resample(
            &pos, inv_ratio, &dither, ain.nchan,
            aout.alloc, LFR_FMT_S16_NATIVE, aout.nframe,
            ain.data, LFR_FMT_S16_NATIVE, ain.nframe,
            fp);

        if (benchmark > 0) {
            t0 = clock();
            for (bi = 0; bi < benchmark; ++bi) {
                pos = pos0;
                dither = DITHER_SEED;
                lfr_resample(
                    &pos, inv_ratio, &dither, ain.nchan,
                    aout.alloc, LFR_FMT_S16_NATIVE, aout.nframe,
                    ain.data, LFR_FMT_S16_NATIVE, ain.nframe,
                    fp);
            }
            t1 = clock();

            time = t1 - t0;
            speed = 
                ((double) CLOCKS_PER_SEC * benchmark * ain.nframe) /
                (time * inrate);
            if (verbose >= 1) {
                fprintf(
                    stderr,
                    "Average time: %g s\n"
                    "Speed: %g\n",
                    (t1 - t0) / ((double) CLOCKS_PER_SEC * benchmark),
                    speed);
            }
            printf("%.3f\n", speed);
        }

        if (test_bufsize_flag)
            test_bufsize(fp, &ain, &aout, inv_ratio, pos0);

        lfr_filter_free(fp);

        file_destroy(&din);
        audio_destroy(&ain);
        audio_convert(&aout, LFR_FMT_S16LE);
    }

    file = fopen(files[1], "wb");
    if (!file)
        error("error opening output file");
    audio_wav_save(file, &aout);
    fclose(file);

    file_destroy(&din);
    audio_destroy(&ain);
    audio_destroy(&aout);
    lfr_param_free(param);

    return 0;
}
