#!/usr/bin/env python
import math
import sys
from common import *

try:
    import numpy
    import scipy.io.wavfile
except ImportError:
    error('SciPy is required to run this test.')

class TestParamSet(ParamSet):
    RATE_IN = param_rate('RATE_IN', 'input sampling rate', 48000)
    RATE_OUT = param_rate('RATE_OUT', 'output sample rate', 44100)
    QUALITY = param_range(
        'QUALITY', 'resampler quality setting', list(range(11)), 0, 10)
    FORMAT = param_format('FORMAT', 'sample format', 's16')

    TEST_BETA = param(
        'TEST_BETA', 'beta parameter for windowed sine waves',
        14, float, lambda x: 0 <= x <= 50)
    TEST_LENGTH = param(
        'TEST_LENGTH', 'length of test input files',
        2**16, int, lambda x: 10**2 <= x <= 10**6)

    CPU_FEATURES = None

    DUMP_SPECS = param_bool(
        'DUMP_SPECS', 'dump specs for each setting', False)

NBINS = 10
SNR_NTEST = 40
BW_EPSILON = 0.1

def test_atten(param, freq):
    """Test the attenuation of the resampler at the given frequency.

    Produces a windowed sine wave, resamples it, and compares the
    signal power of the output to the signal power of the input.
    Returns the ratio.
    """
    # Note that using float32 for generating sine waves will result in
    # noticeable harmonics as the input to the sine function is
    # subject to rounding, harmonics that can clearly be heard if a
    # 'pure' sine wave is notch filtered out.
    #
    # We also use a -6 dBFS signal so we don't get bothered by
    # clipping.
    length = param.TEST_LENGTH
    w = 2 * math.pi * freq / param.RATE_IN
    beta = param.TEST_BETA
    indata = (
        0.5 *
        numpy.sin(w * numpy.arange(length, dtype='float64')) *
        numpy.kaiser(length, beta))
    outdata = resample_arr(param, indata)
    return numpy.std(outdata) / numpy.std(indata)

def test_snr(param, freq):
    """Test the SNR using a test tone at the given frequency.

    This works by computing the FFT of the result, zeroing the FFT
    corresponding to the test tone, and comparing the signal power
    with the signal power of the output signal.  Returns the SNR ratio
    (as a ratio, not dB).
    """
    w = 2 * math.pi * freq / param.RATE_IN
    length = param.TEST_LENGTH
    beta = param.TEST_BETA
    indata = (
        0.5 * numpy.sin(w * numpy.arange(length, dtype='float64')))
    outdata = resample_arr(param, indata)
    window = numpy.kaiser(len(outdata), beta)
    outdata *= window
    fft = numpy.fft.rfft(outdata)

    nbins = NBINS
    fbin = round(freq * len(outdata) / param.RATE_OUT)
    bin0 = min(max(fbin-nbins//2, 0), len(fft))
    bin1 = min(max(fbin-nbins//2 + nbins, 0), len(fft))
    fft[bin0:bin1] = 0
    noise = numpy.std(fft) / math.sqrt(len(outdata))
    signal = numpy.average(window)

    return signal / noise

def test_bw(param, target):
    rate = min(param.RATE_IN, param.RATE_OUT)
    f1 = rate * 0.1
    f2 = rate * 0.499
    base_atten = test_atten(param, f1)
    rel_target = max(base_atten, 1) * target
    epsilon = BW_EPSILON
    f3 = (f1 + f2) * 0.5
    while f2 - f1 > epsilon:
        f3 = (f1 + f2) * 0.5
        atten = test_atten(param, f3)
        if atten < rel_target:
            f2 = f3
        else:
            f1 = f3
    return f3

def test_snr_bw(param):
    sys.stdout.write(param.str_vars() + '\n')
    if param.DUMP_SPECS:
        resample_specs(param)
    freq = test_bw(param, math.sqrt(0.5))
    minrate = min(param.RATE_IN, param.RATE_OUT)
    sys.stdout.write(
        '    Bandwidth: %.2f Hz (%.2f%% of Nyquist frequency)\n' %
        (freq, 100 * 2 * freq / minrate))

    maxfreq = param.RATE_IN * 0.5
    test_freqs = numpy.linspace(maxfreq*0.05, maxfreq*0.95, SNR_NTEST)
    test_freqs += numpy.random.uniform(
        -maxfreq*0.04, maxfreq*0.04, len(test_freqs))

    test_snr_vec = numpy.vectorize(
        lambda x: test_snr(param, x), otypes=['float32'])
    snrs = test_snr_vec(test_freqs)
    snr_avg = numpy.average(snrs)
    snr_min = numpy.amin(snrs)
    sys.stdout.write(
        '    Worst-case SNR: %.2f dB (avg %.2f dB)\n' %
        (to_dB(snr_min), to_dB(snr_avg)))

def run(param):
    sys.stdout.write(param.str_vars(all_vars=True) + '\n')
    for iparam in param.instances():
        test_snr_bw(iparam)

run_top(TestParamSet, run)
