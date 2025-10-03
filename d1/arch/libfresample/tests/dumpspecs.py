#!/usr/bin/env python
from common import *
import sys

class TestParamSet(ParamSet):
    RATE_IN = param_rate('RATE_IN', 'input sampling rate', 48000)
    RATE_OUT = param_rate('RATE_OUT', 'output sample rate', 44100)
    QUALITY = param_range(
        'QUALITY', 'resampler quality setting', list(range(11)), 0, 10)
    CPU_FEATURES = None

def run(param):
    sys.stdout.write(param.str_vars(all_vars=True) + '\n')
    for iparam in param.instances():
        sys.stdout.write('\n')
        sys.stdout.write(iparam.str_vars() + '\n')
        resample_specs(iparam)

run_top(TestParamSet, run)
