/*
The computer code contained herein is the sole property of Dietfrid Mali.
I (Dietfrid Mali), in distributing the code to end users, and subject to all
terms and conditions herein, grant a royalty free, perpetual license, to such
end users for use by such end users in using, displaying, and creating derivative
works thereof, so long as such use, display or creation is for non-commercial,
royalty or revenue free purposes. In no event shall the end user use the computer
code described above for revenue bearing purposes. The end user understands and
agrees to the terms herein an accepts the same by the use of this file.
*/

#ifndef _TGA_H
#define _TGA_H

#ifdef HAVE_CONFIG_H
#	include <conf.h>
#endif

typedef struct {
    char  identSize;         // size of ID field that follows 18 char header (0 usually)
    char  colorMapType;      // nType of colour map 0=none, 1=has palette
    char  imageType;         // nType of image 0=none,1=indexed,2=rgb,3=grey,+8=rle packed

    int16_t colorMapStart;     // first colour map entry in palette
    int16_t colorMapLength;    // number of colours in palette
    char  colorMapBits;      // number of bits per palette entry 15,16,24,32

    uint16_t xStart;            // image x origin
    uint16_t yStart;            // image y origin
    uint16_t width;             // image width in pixels
    uint16_t height;            // image height in pixels
    char   bits;              // image bits per pixel 8,16,24,32
    char   descriptor;        // image descriptor bits (vh flip bits)
} __xpack__ tTGAHeader;


class CTGAHeader {
	public:
		tTGAHeader	m_data;

	public:
		CTGAHeader () { Reset (); }
		void Reset (void) { memset (&m_data, 0, sizeof (m_data)); }
		void Setup (const tTGAHeader* headerP) {
			if (headerP)
				m_data = *headerP;
			else
				Reset ();
			}

		int32_t Read (CFile& cf, CBitmap* pBm);
		int32_t Write (CFile& cf, CBitmap *pBm);

		inline tTGAHeader& Data (void) { return m_data; }
		inline uint16_t Width (void) { return m_data.width; }
		inline uint16_t Height (void) { return m_data.height; }
		inline char Bits (void) { return m_data.bits; }
	};

class CModelTextures {
	public:
		int32_t					m_nBitmaps;
		CArray<CCharArray>	m_names;
		CArray<CBitmap>		m_bitmaps;
		CArray<uint8_t>		m_nTeam;

	public:
		CModelTextures () { Init (); }
		void Init (void) { m_nBitmaps = 0; }
		int32_t Bind (int32_t bCustom);
		void Release (void);
		int32_t Read (int32_t bCustom);
		int32_t ReadBitmap (int32_t i, int32_t bCustom);
		bool Create (int32_t nBitmaps);
		void Destroy (void);
};


class CTGA {
	protected:
		CFile			m_cf;
		CTGAHeader	m_header;
		CBitmap*		m_pBm;

	public:
		CTGA (CBitmap* pBm = NULL)
			: m_pBm (pBm)
			{
			}

		~CTGA () { m_pBm = NULL; }

		void Setup (CBitmap* pBm = NULL, const tTGAHeader* headerP = NULL)
			{
			if (pBm)
				m_pBm = pBm;
			m_header.Setup (headerP);
			}

		int32_t Shrink (int32_t xFactor, int32_t yFactor, int32_t bRealloc);
		int32_t ReadData (CFile& cf, int32_t alpha, double brightness, int32_t bGrayScale, int32_t bReverse);
		int32_t WriteData (void);
		int32_t Load (int32_t alpha, double brightness, int32_t bGrayScale);
		int32_t Read (const char* pszFile, const char* pszFolder, int32_t alpha = -1, double brightness = 1.0, int32_t bGrayScale = 0, bool bAutoComplete = true);
		int32_t Write (void);
		CBitmap* CreateAndRead (char* pszFile);
		int32_t Save (const char *pszFile, const char *pszFolder);
		double Brightness (void);
		void ChangeBrightness (double dScale, int32_t bInverse, int32_t nOffset, int32_t bSkipAlpha);
		int32_t Interpolate (int32_t nScale);
		int32_t MakeSquare (void);
		int32_t Compress (void);
		void ConvertToRGB (void);
		void PreMultiplyAlpha (float fScale = 1.0f);
		CBitmap* ReadModelTexture (const char *pszFile, int32_t bCustom);

		inline tTGAHeader& Header (void) { return m_header.Data (); }

	private:
		void SetProperties (int32_t alpha, int32_t bGrayScale, double brightness, bool bSwapRB = true);
		int32_t ReadImage (const char* pszFile, const char* pszFolder, int32_t alpha, double brightness, int32_t bGrayScale);

	};


#endif //_TGA_H
