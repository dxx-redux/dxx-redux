# LibFResample: Fast Audio Sample Rate Conversion

**WARNING!** LibFResample is not release quality yet:

1. LibFResample does not report errors yet!

2. LibFResample only works on mono or stereo 16-bit data! (Support for 32-bit floating point as well as automatic data conversions is in the works.)

3. LibFResample has an “in-progress” build system! (Autoconf and Xcode support is currently broken.)

4. LibFResample has not been ported to Windows!

LibFResample is a library for resampling audio with a permissive (FreeBSD style) license. Like other audio resampling libraries, it is based on the bandlimited interpolation algorithm described by Julius Orion Smith (see [Digital Audio Resampling Home Page][jos-resample]). LibFResample is designed to use SIMD operations where available. Currently, LibFResample supports SSE2 and AltiVec.

[jos-resample]: https://ccrma.stanford.edu/~jos/resample/

If you can hook me up with some hardware, I may be able to optimize this library for other architectures—ARM NEON, Cell SPE, et cetera.

## Resampling speed

LibFResample gets its speed by precalculating filter coefficients for the desired conversion ratio, reorganizing coefficients for maximum cache locality, and using SIMD operations as much as possible.

- System A: 1.6 GHz Intel Atom, GCC 4.4 (circa 2008)
- System B: 2.0 GHz IBM PowerPC G5, GCC 4.0 (circa 2005)
- System C: 3.2 GHz AMD Phenom II, GCC 4.7 (circa 2010)
- System D: 1.7 GHz Intel Core i5, Clang 4.0 (circa 2012)

- Task: Resample 16-bit stereo audio from 48 kHz to 44.1 kHz
- Speed: 1x is realtime, 2x is twice realtime, etc.
- SRC: Secret Rabbit Code 0.1.8, also known as libsamplerate

| Settings           | System A | System B | System C | System D |
| ------------------ | -------- | -------- | -------- | -------- |
| LibFResample       |          |          |          |          |
| Q=5 Medium         | 197x     | 545x     | 1276x    | 1247x    |
| Q=8 High           | 47x      | 141x     | 318x     | 303x     |
| Q=10 Ultra         | 13x      | 51x      | 107x     | 87x      |
| Secret Rabbit Code |          |          |          |
| Sinc Fast          | 13x      | 20x      | 68x      | 77x      |
| Sinc Med.          | 5.9x     | 9.3x     | 35x      | 39x      |
| Sinc Best          | 0.69x    | 1.9x     | 8.5x     | 11x      |

That’s fast! To put it in perspective, System D, an entry level MacBook Air from 2012, can resample an hour of audio in under 3 seconds at medium quality (Q=5). In fact, LibFResample is so fast, it even beats Secret Rabbit Code’s linear interpolator, which the docs note is “blindingly fast”…

Task: Resample 16-bit mono audio from 48 kHz to 44.1 kHz

| Settings           | System A | System B | System C | System D |
| ------------------ | -------- | -------- | -------- | -------- |
| LibFResample       |          |          |          |          |
| Q=2 Low            | 484x     | 887x     | 2759x    | 3195x    |
| Q=5 Medium         | 286x     | 721x     | 1875x    | 1811x    |
| Secret Rabbit Code |          |          |          |          |
| ZOH                | 119x     | 250x     | 986x     | 1326x    |
| Linear             | 113x     | 242x     | 895x     | 1112x    |

Notes: SRC throughput figures were calculated by dividing the output of SRC’s throughput tests by 44100. LibFResample throughput figures were calculated by resampling two minutes of pink noise twenty times after warming the cache.

The `benchmark.py` script in the tests folder will benchmark LibFResample, it requires SoX.

## Audio quality

LibFResample uses band-limited interpolation and dithering to achieve high-quality output. The filter is a simple windowed sinc filter with a Kaiser window, the window size and beta parameter are adjusted to achieve the desired SNR and transition band. There are better ways to design filters, but this works.

An included test script finds the bandwidth and signal to noise ratio of the resampler at various quality settings. The bandwidth and SNR will vary depending on the exact sample rates used. In particular, the bandwidth decreases if the input sample rate increases—but if you record 96 kHz or 192 kHz audio, you probably want to use higher quality settings anyway.

| Settings   | Bandwidth | SNR   |
| ---------- | --------- | ----- |
| Q=2 Low    | 13.3 kHz  | 29 dB |
| Q=5 Medium | 16.3 kHz  | 84 dB |
| Q=8 High   | 19.5 kHz  | 92 dB |
| Q=10 Ultra | 21.2 kHz  | 92 dB |

Proper dithering introduces a noise floor of 1 ULP peak-to-peak, which at 16 bit resolution puts the noise floor at –96. The test dithers both input and output, giving a noise floor of –93 dB. At high quality settings, the measured SNR of 92 dB is nearly perfect.

Bandwidth is measured by doing a binary search to find the 3 dB attenuation point. The attenuation is measured by resampling windowed sine waves.

The SNR is measured by repeatedly resampling sine waves and taking the FFT of the resampled result. The original sine wave is zeroed from the FFT bins and the signal power in the other bins is computed. Sine waves at 40 different frequencies are tested and the worst SNR is recorded.

The `quality.py` script in the tests folder will compute these quality figures, it requires SciPy.

## Theory of operation

Resampling audio takes two steps: first you design a low-pass filter, then you use the filter to resample the audio. In general, a new filter must be created for each combination of sample rates, filter parameters, and bit depth.

The low-pass filter is used to remove aliasing from the resampled output—aliasing is undesirable noise created during the resampling process. Adjusting the filter parameters is the only way to change the quality of the audio output. There are two major parameters to filter design:

1. Signal to noise ratio. A filter with a higher SNR more efficiently excludes aliasing noise, but increasing the SNR also makes the filter larger. Note that the noise is not pleasant noise like white noise; the noise has a gritty, low-fi sound.

2. Bandwidth. A filter with a higher bandwidth includes more of the original signal, but increasing the bandwidth also makes the filter larger. (The crucial parameter is actually transition bandwidth, between the filter’s pass band and stop band. Decreasing the transition bandwidth increases the filter size.)

LibFResample includes a function which creates a filter with the desired SNR and transition bandwidth, and there are also some preset SNR and transition bandwidth combinations. The function also performs a number of adjustments of the design parameters to ensure that the pass band is large enough and that the filter isn’t overdesigned to preserve ultrasonic frequencies.

## Internal details

The filter is a simple sinc filter windowed with the Kaiser window. Choosing the parameter for the Kaiser window allows us to adjust the level of the side lobes, and higher side lobes contribute to aliasing noise. Increasing the filter size decreases the width of the window’s main lobe, which decreases the transition bandwidth in the resulting filter.

Many copies of the filter are generated, each with a fractional (less than one sample) offset from each other. Choosing a copy with a given offset allows us to sample the original audio at that offset. Interpolating between copies allows us to sample the audio at finer intervals without a significant increase in memory usage.

Filters may be created with 16-bit integer or single precision floating point coefficients. The cutoff is typically above Q=5. As filter sizes increase, coefficient quantization begins to dominate stopband attenuation.

At low quality settings, the fractional copies are individually normalized. Otherwise, variations in DC gain will modulate the input signal.
