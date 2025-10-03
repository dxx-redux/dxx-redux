

import tempfile
import math
import sys
import re
import shutil
import atexit
import os
import subprocess
import time
import decimal

__all__ = [
    'to_dB',
    'temp_files', 'warning', 'error',
    'ParamSet',
    'param', 'param_format', 'param_quality', 'param_rate',
    'param_bool', 'param_range',
    'sox', 'resample_raw', 'resample_specs', 'resample_arr',
    'run_top',
]

def to_dB(x):
    return 20 * math.log10(x)

class TempGroup(object):
    def __init__(self, prefix):
        self._files = set()
        self._prefix = prefix
    def __enter__(self):
        return self
    def __exit__(self, exc_type, exc_value, traceback):
        for f in self._files:
            try:
                os.unlink(f)
            except OSError:
                pass
    def new(self, name):
        name = '%s_%s' % (self._prefix, name)
        self._files.add(name)
        return name

class TempFiles(object):
    def __init__(self):
        self._files = set()
        self._counter = 0
        self._dir = None

    def getdir(self):
        d = self._dir
        if d is None:
            atexit.register(self.destroy)
            d = tempfile.mkdtemp()
            self._dir = d
        return d

    def destroy(self):
        d = self._dir
        if d is not None:
            shutil.rmtree(d)
            self._dir = None

    def unique(self):
        c = self._counter
        self._counter = c + 1
        return os.path.join(self.getdir(), 'tmp%06x' % c)

    def group(self):
        return TempGroup(self.unique())

    def new(self, name):
        return '%s_%s' % (self.unique(), name)

temp_files = TempFiles()

def pexc(info):
    if info is not None:
        sys.stderr.write('%s: %s\n' % (info[0].__name__, str(info[1])))

def warning(why, info=None):
    sys.stderr.write('warning: ' + why + '\n')
    pexc(info)

def error(why, info=None):
    sys.stderr.write('\n\n' 'error: ' + why + '\n')
    pexc(info)
    sys.exit(1)

class Param(object):
    def __init__(self, name, doc, default, parse, check):
        self.name = name
        self.doc = doc
        self.default = default
        self.parse = parse
        self.check = check
        assert check(default)

    def __get__(self, instance, owner):
        if instance is None:
            return self
        obj = instance
        while obj is not None:
            try:
                value = obj._dict[self.name]
            except KeyError:
                pass
            else:
                obj._unread.discard(self.name)
                return value
            obj = obj._parent
        return self.default

    def __set__(self, instance, value):
        if not self.check(value):
            raise ValueError(
                'value for %s is out of range: %r' % (self.name, value))
        instance._dict[self.name] = value

def param_prog(name, doc, default):
    return Param(name, doc, default, lambda x: x, lambda x: True)

PARAM = re.compile('^[A-Za-z_][A-Za-z0-9_]*$')
class ParamSet(object):
    FR = param_prog('FR', 'path to fresample program',
                    os.path.join(sys.path[0], '../fresample'))

    SOX = param_prog('SOX', 'path to SoX program', 'sox')

    @classmethod
    def from_args(klass, args=None):
        if args is None:
            args = sys.argv[1:]
        d = {}
        for arg in args:
            idx = arg.find('=')
            if idx < 0:
                error('invalid argument, must be NAME=VALUE: %r' % arg)
            argname = arg[:idx]
            argval = arg[idx+1:]
            if not PARAM.match(argname):
                error('invalid argument name: %r' % argname)
            argname = argname.upper()
            try:
                parm = getattr(klass, argname)
            except AttributeError:
                parm = None
            if parm is None or not isinstance(parm, Param):
                warning('unknown parameter: %s' % argname)
                continue
            try:
                argval = parm.parse(argval)
            except ValueError:
                error('cannot parse value for %s: %r' % (argname, argval))
            d[argname] = argval
        return klass(**d)

    @classmethod
    def params(klass):
        for attr in dir(klass):
            if not PARAM.match(attr):
                continue
            param = getattr(klass, attr)
            if isinstance(param, Param):
                yield attr

    def __init__(self, **params):
        self._dict = {}
        self._parent = None
        klass = self.__class__
        for k, v in params.items():
            try:
                parm = getattr(klass, k)
            except AttributeError:
                parm = None
            if parm is None or not isinstance(parm, Param):
                raise ValueError('unknown parameter: %s' % k)
            parm.__set__(self, v)
        self._unread = set(self._dict)

    def warn_unread(self):
        if not self._unread:
            return
        warning('unused parameters: %s\n' % ', '.join(sorted(self._unread)))

    def override(self, **kw):
        obj = self.__class__(**kw)
        obj._parent = self
        return obj

    def __repr__(self):
        a = []
        obj = self
        while obj is not None:
            a.append(obj._dict)
            obj = obj._parent
        d = {}
        a.reverse()
        for x in a:
            d.update(x)
        return '%s(%s)' % (
            self.__class__.__name__,
            ', '.join('%s=%r' % x for x in sorted(d.items()))
        )

    def instances(self):
        d = []
        for param in self.params():
            val = getattr(self, param)
            if isinstance(val, list):
                d.append((param, val))
        versions = []
        if not d:
            versions.append(self)
        else:
            p = {}
            def spec(i):
                if i == len(d):
                    versions.append(self.override(**p))
                    return
                k, vv = d[i]
                for v in vv:
                    p[k] = v
                    spec(i+1)
            spec(0)
        return versions

    def str_vars(self, no_ranges=True, all_vars=False):
        """Get the variables a string in VAR=VALUE format."""
        vars = []
        if all_vars:
            items = []
            for param in self.params():
                items.append((param, getattr(self, param)))
        else:
            items = list(self._dict.items())
        for k, v in items:
            if v is None:
                continue
            if isinstance(v, list):
                if no_ranges:
                    continue
                if isinstance(v[0], int):
                    ranges = []
                    first = v[0]
                    last = first
                    for i in v[1:]:
                        if i == last + 1:
                            last = i
                        else:
                            ranges.append('%d..%d' % (first, last))
                            first = i
                            last = i
                    v = ranges.join(',')
                else:
                    v = ','.join([str(i) for i in v])
            else:
                v = str(v)
            vars.append('%s=%s' % (k, v))
        return ' '.join(vars)

param = Param

RATE = re.compile(r'^\s*([0-9.]+)\s*(k?)(?:hz)?\s*$', re.I)
def parse_rate(x):
    m = RATE.match(x)
    if not m:
        raise ValueError('invalid frequency: %r' % x)
    num, prefix = m.groups()
    if prefix.lower() == 'k':
        scale = 1000
    else:
        scale = 1
    return round(scale * float(num))
def valid_rate(x):
    return 8000 <= x <= 192000
def param_rate(name, doc, default):
    return Param(name, doc, default, parse_rate, valid_rate)

def parse_format(x):
    return x.lower()
FORMATS = set(['s16', 'f32'])
def valid_format(x):
    return x in FORMATS
def param_format(name, doc, default):
    return Param(name, doc, default, parse_format, valid_format)

def param_quality(name, doc, default):
    return Param(name, doc, default, int, lambda x: 0 <= x <= 10)

TRUE = frozenset(['1', 'on', 'true', 'yes'])
FALSE = frozenset(['0', 'off', 'false', 'no'])
def parse_bool(x):
    x = x.lower()
    if x in TRUE:
        return True
    if x in FALSE:
        return False
    raise ValueError('invalid boolean')
def param_bool(name, doc, default):
    return Param(name, doc, default, parse_bool,
                 lambda x: isinstance(x, bool))

def param_range(name, doc, default, minv, maxv):
    default = list(default)
    if len(default) == 1:
        default = default[0]
    def parse(x):
        vals = x.split(',')
        ivals = []
        for val in vals:
            val = val.strip()
            if not val:
                continue
            idx = val.find('..')
            if idx >= 0:
                first = val[:idx]
                if first:
                    first = int(first)
                else:
                    first = minv
                last = val[idx+2:]
                if last:
                    last = int(last)
                else:
                    last = maxv
                ivals.extend(list(range(first, last+1)))
            else:
                val = int(val)
                ivals.append(val)
        if len(ivals) == 1:
            return ivals[0]
        if not ivals:
            raise ValueError('empty range')
        return ivals
    def valid(x):
        if isinstance(x, list):
            if not x:
                return False
            for item in x:
                if not (minv <= item <= maxv):
                    return False
            return True
        else:
            return minv <= x <= maxv
    return Param(name, doc, default, parse, valid)

def proc_failed(proc, cmd):
    sys.stderr.write(
        '\n\n'
        'error: command failed with code %d\n'
        'command: %s\n' %
        (proc.returncode, ' '.join([mkparam(x) for x in cmd])))
    sys.exit(1)

def sox(param, args):
    cmd = [param.SOX]
    cmd.extend(args)
    try:
        proc = subprocess.Popen(cmd)
    except OSError:
        exc_type, exc_val, traceback = sys.exc_info()
        sys.stderr.write(
            '\n\n'
            'could not run SoX: %s: %s\n'
            'Is SoX installed?\n' %
            (exc_type.__name__, str(exc_val)))
        sys.exit(1)
    proc.wait()
    if proc.returncode != 0:
        proc_failed(proc, cmd)

def resample_raw(params, *args):
    cmd = [
        params.FR,
        '-q', str(params.QUALITY),
        '-r', str(params.RATE_OUT)]
    features = params.CPU_FEATURES
    if features is not None:
        cmd.append('--cpu-features=' + features)
    cmd.extend(args)
    try:
        proc = subprocess.Popen(cmd, stdout=subprocess.PIPE)
    except OSError:
        exc_type, exc_val, traceback = sys.exc_info()    
        sys.stderr.write(
            '\n\n'
            'error: could not run fresample (%s): %s: %s\n'
            'Did you remember to run "make"?\n' %
            (params.FR, exc_type.__name__, str(exc_val)))
        sys.exit(1)
    stdout, stderr = proc.communicate()
    if proc.returncode != 0:
        proc_failed(proc, cmd)
    return stdout.decode('ascii')

def fmt_size(size, digits=3):
    for k in range(2):
        s = decimal.Decimal(size)
        n = s.adjusted()
        i = n // 3 + k
        ii = i * 3
        s = s.scaleb(-ii)
        d = min((1 + n - ii) - digits, 0)
        s2 = s.quantize(decimal.Decimal(1).scaleb(d))
        if s2.adjusted() > s.adjusted() and d < 0:
            s2 = s.quantize(decimal.Decimal(1).scaleb(d+1))
        if s2 < 1000:
            break
    if i > 0:
        try:
            pfx = 'kMGTPEZY'[i-1]
        except IndexError:
            raise ValueError('size too large')
    else:
        pfx = ''
    return '%s %sB' % (s2, pfx)

def fmt_freq(x, nyquist):
    x = int(x)
    if x >= 1000:
        s = '%.1f kHz' % (x / 1000.0)
    else:
        s = '%d Hz' % x
    return s + ' (%.2f%% nyquist)' % (x * 100 / nyquist)

def resample_specs(param):
    out = resample_raw(param, '--inrate=%d' % param.RATE_IN,
                       '--dump-specs')
    data = {}
    for line in out.splitlines():
        idx = line.find(':')
        if not idx:
            error('invalid dump-specs format')
        varname = line[:idx].strip()
        varval = float(line[idx+1:].strip())
        data[varname] = varval
    nyquist = 0.5 * min(param.RATE_IN, param.RATE_OUT)
    rin = param.RATE_IN
    def pval(x, y):
        sys.stdout.write('    %s: %s\n' % (x, y))
    pval('size', int(round(data['size'])))
    pval('delay', str(data['delay']))
    pval('memsize', fmt_size(int(round(data['memsize']))))
    pval('fpass', fmt_freq(data['fpass'] * rin, nyquist))
    pval('fstop', fmt_freq(data['fstop'] * rin, nyquist))
    pval('atten', str(data['atten']) + ' dB')


def resample_arr(param, arr):
    with temp_files.group() as temp:
        inpath = temp.new('in.wav')
        outpath = temp.new('out.wav')
        write_wav(inpath, param.RATE_IN, arr, param.FORMAT)
        resample_raw(param, inpath, outpath)
        return read_wav(outpath)

NOESCAPE = re.compile('^[-A-Za-z0-9_./=:,+=]+$')
def mkparam(x):
    if NOESCAPE.match(x):
        return x
    return repr(x)

def run_top(pclass, func):
    t1 = time.time()
    try:
        param = pclass.from_args()
        func(param)
    finally:
        t2 = time.time()
        sys.stderr.write('Elapsed time: %f s\n' % (t2 - t1))

def write_wav(path, rate, data, format):
    """Write a signal as a wave file.

    The 'dtype' corresponds to LibFResample data types, either s16 for
    signed 16-bit integer or f32 for IEEE single precision floating
    point.  The input should be scaled so that 1.0 is 0 dBFS.
    """
    try:
        import numpy
        import scipy.io.wavfile
    except ImportError:
        error('SciPy is required to run this test')
    if format == 's16':
        scale = 2**15
        data = numpy.asarray(
            numpy.floor(scale * data +
                        numpy.random.random_sample(len(data))),
            dtype='int16')
    elif format == 'f32':
        data = numpy.asarray(data, dtype='float32')
    else:
        raise ValueError('unknown format')
    scipy.io.wavfile.write(path, rate, data)

def read_wav(path):
    try:
        import numpy
        import scipy.io.wavfile
    except ImportError:
        error('SciPy is required to run this test')
    rate, data = scipy.io.wavfile.read(path)
    n = data.dtype.name
    if n == 'int16':
        data = numpy.asarray(data, 'float32') * 2**-15
    return data
