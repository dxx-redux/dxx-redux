
#include "polyphase_resampler.h"

#include <algorithm>
#include <cmath>

//#include "alnumbers.h"
//#include "opthelpers.h"
#define UNLIKELY
#define LIKELY
#define M_PI 3.14159265f

namespace {

constexpr float Epsilon{1e-9};

using uint = unsigned int;

/* This is the normalized cardinal sine (sinc) function.
 *
 *   sinc(x) = { 1,                   x = 0
 *             { sin(pi x) / (pi x),  otherwise.
 */
float Sinc(const float x)
{
    if(std::abs(x) < Epsilon) UNLIKELY
        return 1.0;
    return std::sin(M_PI*x) / (M_PI*x);
}

/* The zero-order modified Bessel function of the first kind, used for the
 * Kaiser window.
 *
 *   I_0(x) = sum_{k=0}^inf (1 / k!)^2 (x / 2)^(2 k)
 *          = sum_{k=0}^inf ((x / 2)^k / k!)^2
 */
constexpr float BesselI_0(const float x)
{
    // Start at k=1 since k=0 is trivial.
    const float x2{x/2.0f};
    float term{1.0f};
    float sum{1.0f};
    int k{1};

    // Let the integration converge until the term of the sum is no longer
    // significant.
    float last_sum{};
    do {
        const float y{x2 / k};
        ++k;
        last_sum = sum;
        term *= y * y;
        sum += term;
    } while(sum != last_sum);
    return sum;
}

/* Calculate a Kaiser window from the given beta value and a normalized k
 * [-1, 1].
 *
 *   w(k) = { I_0(B sqrt(1 - k^2)) / I_0(B),  -1 <= k <= 1
 *          { 0,                              elsewhere.
 *
 * Where k can be calculated as:
 *
 *   k = i / l,         where -l <= i <= l.
 *
 * or:
 *
 *   k = 2 i / M - 1,   where 0 <= i <= M.
 */
float Kaiser(const float b, const float k)
{
    if(!(k >= -1.0 && k <= 1.0))
        return 0.0;
    return BesselI_0(b * std::sqrt(1.0 - k*k)) / BesselI_0(b);
}

// Calculates the greatest common divisor of a and b.
constexpr uint Gcd(uint x, uint y)
{
    while(y > 0)
    {
        const uint z{y};
        y = x % y;
        x = z;
    }
    return x;
}

/* Calculates the size (order) of the Kaiser window.  Rejection is in dB and
 * the transition width is normalized frequency (0.5 is nyquist).
 *
 *   M = { ceil((r - 7.95) / (2.285 2 pi f_t)),  r > 21
 *       { ceil(5.79 / 2 pi f_t),                r <= 21.
 *
 */
constexpr uint CalcKaiserOrder(const float rejection, const float transition)
{
    const float w_t{2.0f * M_PI * transition};
    if(rejection > 21.0f) LIKELY
        return static_cast<uint>(std::ceil((rejection - 7.95f) / (2.285f * w_t)));
    return static_cast<uint>(std::ceil(5.79f / w_t));
}

// Calculates the beta value of the Kaiser window.  Rejection is in dB.
constexpr float CalcKaiserBeta(const float rejection)
{
    if(rejection > 50.0f) LIKELY
        return 0.1102f * (rejection - 8.7f);
    if(rejection >= 21.0f)
        return (0.5842f * std::pow(rejection - 21.0f, 0.4f)) +
               (0.07886f * (rejection - 21.0f));
    return 0.0;
}

/* Calculates a point on the Kaiser-windowed sinc filter for the given half-
 * width, beta, gain, and cutoff.  The point is specified in non-normalized
 * samples, from 0 to M, where M = (2 l + 1).
 *
 *   w(k) 2 p f_t sinc(2 f_t x)
 *
 *   x    -- centered sample index (i - l)
 *   k    -- normalized and centered window index (x / l)
 *   w(k) -- window function (Kaiser)
 *   p    -- gain compensation factor when sampling
 *   f_t  -- normalized center frequency (or cutoff; 0.5 is nyquist)
 */
float SincFilter(const uint l, const float b, const float gain, const float cutoff,
    const uint i)
{
    const float x{static_cast<float>(i) - l};
    return Kaiser(b, x / l) * 2.0 * gain * cutoff * Sinc(2.0 * cutoff * x);
}

} // namespace

// Calculate the resampling metrics and build the Kaiser-windowed sinc filter
// that's used to cut frequencies above the destination nyquist.
void PPhaseResampler::init(const uint srcRate, const uint dstRate)
{
    const uint gcd{Gcd(srcRate, dstRate)};
    mP = dstRate / gcd;
    mQ = srcRate / gcd;

    /* The cutoff is adjusted by half the transition width, so the transition
     * ends before the nyquist (0.5).  Both are scaled by the downsampling
     * factor.
     */
    float cutoff, width;
    if(mP > mQ)
    {
        cutoff = 0.475 / mP;
        width = 0.05 / mP;
    }
    else
    {
        cutoff = 0.475 / mQ;
        width = 0.05 / mQ;
    }
    // A rejection of -180 dB is used for the stop band. Round up when
    // calculating the left offset to avoid increasing the transition width.
    const uint l{(CalcKaiserOrder(180.0, width)+1) / 2};
    const float beta{CalcKaiserBeta(180.0)};
    mM = l*2 + 1;
    mL = l;
    mF.resize(mM);
    for(uint i{0};i < mM;i++)
        mF[i] = SincFilter(l, beta, mP, cutoff, i);
}

// Perform the upsample-filter-downsample resampling operation using a
// polyphase filter implementation.
void PPhaseResampler::process(const uint inN, const float *in, const uint outN, float *out)
{
    if(outN == 0) UNLIKELY
        return;

    #if 0
    // Handle in-place operation.
    std::vector<float> workspace;
    float *work{out};
    if(work == in) UNLIKELY
    {
        workspace.resize(outN);
        work = workspace.data();
    }
    #endif

    // Resample the input.
    const uint p{4/*mP*/}, q{1/*mQ*/}, m{mM}, l{mL};
    const float *f{mF.data()};
    for(uint i{0};i < outN;i++)
    {
        // Input starts at l to compensate for the filter delay.  This will
        // drop any build-up from the first half of the filter.
        size_t j_f{(l + q*i) % p};
        size_t j_s{(l + q*i) / p};

        // Only take input when 0 <= j_s < inN.
        float r{0.0};
        if(j_f < m) LIKELY
        {
            size_t filt_len{(m-j_f+p-1) / p};
            if(j_s+1 > inN) LIKELY
            {
                size_t skip{std::min<size_t>(j_s+1 - inN, filt_len)};
                j_f += p*skip;
                j_s -= skip;
                filt_len -= skip;
            }
            if(size_t todo{std::min<size_t>(j_s+1, filt_len)}) LIKELY
            {
                if (todo >= 4) {
                    for (;todo >= 4; todo -= 4) {
                        float a = f[j_f] * in[j_s];
                        float b = f[j_f + p] * in[j_s - 1];
                        float c = f[j_f + p * 2] * in[j_s - 2];
                        float d = f[j_f + p * 3] * in[j_s - 3];
                        r += (a + b) + (c + d);
                        j_f += p * 4;
                        j_s -= 4;
                    }
                    if (!todo) {
                        out[i] = r;
                        continue;
                    }
                }
                do {
                    r += f[j_f] * in[j_s];
                    j_f += p;
                    --j_s;
                } while(--todo);
            }
        }
        out[i] = r;
    }
    #if 0
    // Clean up after in-place operation.
    if(work != out)
        std::copy_n(work, outN, out);
    #endif
}

extern "C" {
#include "resample_c.h"

void *resample_init(const unsigned int inRate, const unsigned int outRate) {
    PPhaseResampler *pp = new PPhaseResampler();
    pp->init(inRate, outRate);
    return (void *)pp;
}

void resample_process(void *ppobj, const unsigned int inN, const float *in, const unsigned int outN, float *out) {
    PPhaseResampler *pp = (PPhaseResampler *)ppobj;
    pp->process(inN, in, outN, out);
}

void resample_done(void *ppobj) {
    PPhaseResampler *pp = (PPhaseResampler *)ppobj;
    delete pp;
}
}
