#!/usr/bin/env python
from common import *
import sys

class TestParamSet(ParamSet):
    RATE_IN = param_rate('RATE_IN', 'input sampling rate', 48000)
    RATE_OUT = param_rate('RATE_OUT', 'output sample rate', 44100)
    QUALITY = param_range(
        'QUALITY', 'resampler quality setting', list(range(11)), 0, 10)
    NCHAN = param_range('NCHAN', 'number of channels', [1, 2], 1, 2)
    FORMAT = param_format('FORMAT', 'sample format', 's16')

    ITERCOUNT = param(
        'ITERCOUNT', 'benchmark iteration count', 20,
        int, lambda x: 1 <= x <= 1000)

    TEST_LENGTH = param(
        'TEST_LENGTH', 'length of test input files',
        5760000, int, lambda x: 10**3 <= x <= 10**7)

    CPU_FEATURES = param(
        'CPU_FEATURES', 'allowed CPU features', None,
        lambda x: x, lambda x: True)

    DUMP_SPECS = param_bool(
        'DUMP_SPECS', 'dump specs for each setting', False)

FORMATS = { 's16': '16', 'f32': '32' }

class InputGenerator(object):
    def __init__(self, param):
        self.inputs = {}

    def gen_input(self, param):
        k = param.FORMAT, param.NCHAN, param.RATE_IN
        try:
            return self.inputs[k]
        except KeyError:
            pass
        path = temp_files.new('bench_in.wav')
        cmd = [
            '-b', FORMATS[param.FORMAT],
            '-r', str(param.RATE_IN),
            '-n', path,
            'synth', str(round(param.TEST_LENGTH / param.RATE_IN))]
        cmd.extend(['pinknoise'] * param.NCHAN)
        sox(param, cmd)
        self.inputs[k] = path
        return path

def benchmark(inputgen, param):
    def benchmark():
        out = resample_raw(param, '--benchmark=' + str(param.ITERCOUNT),
                           infile, '/dev/null')
        return out.strip()
    infile = inputgen.gen_input(param)
    if param.DUMP_SPECS:
        sys.stdout.write(param.str_vars() + '\n')
        resample_specs(param)
        out = benchmark()
        sys.stdout.write('    speed: %s\n' % out)
    else:
        sys.stdout.write(param.str_vars())
        sys.stdout.flush()
        out = benchmark()
        sys.stdout.write('; speed: %s\n' % out)

def run(param):
    inputgen = InputGenerator(param)
    sys.stdout.write(param.str_vars(all_vars=True) + '\n')
    for iparam in param.instances():
        benchmark(inputgen, iparam)

run_top(TestParamSet, run)

