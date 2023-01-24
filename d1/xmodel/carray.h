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

#ifndef _CARRAY_H
#define _CARRAY_H

//#ifdef HAVE_CONFIG_H
//#include <conf.h>
//#endif
#include "xdescent.h"

#include <stdint.h>
#include <string.h>
#include <stdlib.h>

#ifndef DBG
#	ifdef _DEBUG
#		define DBG 1
#	else
#		define DBG 0
#	endif
#endif

#define DBG_ARRAYS	0 //DBG

#include "xpstypes.h"
//#include "cquicksort.h"
#include "xcfile.h"
//#include "u_mem.h"

void ArrayError (const char* pszMsg);

//-----------------------------------------------------------------------------

template < class _T >
class CArray /*: public CQuickSort < _T >*/ {

	template < class _U >
	class CArrayData {
		public:
			char			szName [256];
			_U	*			buffer;
			//_U				null;
			uint32_t		length;
			uint32_t		pos;
			int32_t		nMode;
			bool			bWrap;

		public:
			inline uint32_t Length (void) { return length; }
		};

	protected:
		CArrayData<_T>	m_data;

	public:
		template < class _V >
		class Iterator {
			private:
				_V*			m_start;
				_V*			m_end;
				_V*			m_p;
				CArray<_V>&	m_a;
			public:
				Iterator () : m_start (NULL), m_end (NULL), m_p (NULL) {}
				Iterator (CArray<_V>& a) : m_start (NULL), m_end (NULL), m_p (NULL), m_a (a) {}
				operator bool() const { return m_p != NULL; }
				_V* operator*() const { return m_p; }
				Iterator& operator++() {
					if (m_p) {
						if (m_p < m_end)
							m_p++;
						else
							m_p = NULL;
						}
					return *this;
					}
				Iterator& operator--() {
					if (m_p) {
						if (m_p > m_end)
							m_p--;
						else
							m_p = NULL;
						}
					return *this;
					}
				_V* Start (void) {
					m_p = m_start = m_a.Start (); m_end = m_a.End ();
					return m_p;
					}
				_V* End (void) {
					m_p = m_start = m_a.End (); m_end = m_a.Start ();
					return m_p;
					}
			};

		// ----------------------------------------

		explicit CArray<_T> () {
			Init ();
			}

		explicit CArray<_T> (const uint32_t nLength) {
			Init ();
			Create (nLength);
			}

		explicit CArray<_T> (const CArray& other) {
			Init ();
			Copy (other);
			}

		~CArray<_T>() { Destroy (); }

		// ----------------------------------------

		void Init (void) {
			*m_data.szName = 0;
			m_data.buffer = reinterpret_cast<_T *> (NULL);
			m_data.length = 0;
			m_data.pos = 0;
			m_data.nMode = 0;
			m_data.bWrap = false;
			//memset (&m_data.null, 0, sizeof (m_data.null));
			}

		// ----------------------------------------

		void Clear (uint8_t filler = 0, uint32_t count = 0xffffffff) {
#if DBG_ARRAYS
			if ((count != 0xffffffff) && (count > 1000000)) {
				count = count;
				ArrayError ("array overflow\n");
				}
			if ((count == 0xffffffff) && (m_data.length > 512 * 512 * 16 * 4)) {
				count = count;
				ArrayError ("array overflow\n");
				}
#endif
			if (m_data.buffer)
				memset (m_data.buffer, filler, sizeof (_T) * ((count < m_data.length) ? count : m_data.length));
			}

		// ----------------------------------------

		inline bool IsIndex (uint32_t i) { return (m_data.buffer != NULL) && (i < m_data.length); }

		// ----------------------------------------

		inline bool IsElement (_T* elem, bool bDiligent = false) {
			if (!m_data.buffer || (elem < m_data.buffer) || (elem >= m_data.buffer + m_data.length))
				return false;	// no buffer or element out of buffer
			if (bDiligent) {
				uint32_t i = static_cast<uint32_t> (reinterpret_cast<uint8_t*> (elem) - reinterpret_cast<uint8_t*> (m_data.buffer));
				if (i % sizeof (_T))
					return false;	// elem in the buffer, but not properly aligned
				}
			return true;
			}

		// ----------------------------------------

#if DBG_ARRAYS
		inline int32_t Index (_T* elem) {
			if (IsElement (elem))
				return static_cast<int32_t> (elem - m_data.buffer);
			ArrayError ("invalid array index\n");
			return -1;
			}
#else
		inline uint32_t Index (_T* elem) { return uint32_t (elem - m_data.buffer); }
#endif

		// ----------------------------------------

#if DBG_ARRAYS
		inline _T* Pointer (uint32_t i) {
			if (!m_data.buffer || (i >= m_data.length)) {
				ArrayError ("invalid array handle or index\n");
				return NULL;
				}
			return m_data.buffer + i;
			}
#else
		inline _T* Pointer (uint32_t i) { return m_data.buffer + i; }
#endif

		// ----------------------------------------

		inline void SetName (const char* pszName) {
#if DBG_ARRAYS
			if (strlen (pszName) > 255)
				ArrayError ("invalid array name\n");
#endif
			strncpy (m_data.szName, pszName, 256);
			m_data.szName [sizeof (m_data.szName) - 1] = '\0';
			}

		// ----------------------------------------

		inline const char* GetName (void) { return m_data.szName; }

		// ----------------------------------------

		void Destroy (void) {
			if (m_data.buffer) {
				if (!m_data.nMode) {
#if DBG_MALLOC
					UnregisterMemBlock (m_data.buffer);
					bool b = TrackMemory (false);
#endif
					try {
						delete[] m_data.buffer;
						}
					catch(...) {
#if DBG_ARRAYS
						ArrayError ("invalid buffer pointer\n");
#endif
						}
#if DBG_MALLOC
					TrackMemory (b);
#endif
#if DBG_ARRAYS
					m_data.buffer = reinterpret_cast<_T *> (NULL);
#endif
					}
				Init ();
				}
			}

		// ----------------------------------------

		_T *Create (uint32_t length, const char* pszName = NULL) {
			if (pszName)
				SetName (pszName);
			if (m_data.length != length) {
				Destroy ();
#if DBG_MALLOC
				bool b = TrackMemory (false);
#endif
				try {
					if ((m_data.buffer = NEW _T [length]))
						m_data.length = length;
					}
				catch(...) {
#if DBG_ARRAYS
					ArrayError ("invalid buffer size\n");
#endif
					m_data.buffer = NULL;
					}
#if DBG_MALLOC
				TrackMemory (b);
				RegisterMemBlock (m_data.buffer, length * sizeof (_T), m_data.szName, 0);
#endif
				}
			return m_data.buffer;
			}

		// ----------------------------------------

		inline _T* Buffer (uint32_t i = 0) const { return m_data.buffer + i; }

		// ----------------------------------------

		void SetBuffer (_T *buffer, int32_t nMode = 0, uint32_t length = 0xffffffff) {
			if (m_data.buffer != buffer) {
				if (!(m_data.buffer = buffer))
					Init ();
				else {
					m_data.length = length;
					m_data.nMode = nMode;
					}
				}
			}

		// ----------------------------------------

		_T* Resize (uint32_t length, bool bCopy = true) {
			if (m_data.nMode == 2)
				return m_data.buffer;
			if (!m_data.buffer)
				return Create (length);
			_T* p;
#if DBG_MALLOC
			bool b = TrackMemory (false);
#endif
			try {
				p = NEW _T [length];
				}
			catch(...) {
#if DBG_ARRAYS
				ArrayError ("invalid buffer size\n");
#endif
				p = NULL;
				}
			if (!p) {
#if DBG_MALLOC
				TrackMemory (b);
#endif
				return m_data.buffer;
				}
#if DBG_MALLOC
				TrackMemory (true);
				RegisterMemBlock (p, length * sizeof (_T), m_data.szName, 0);
#endif
			if (bCopy) {
				memcpy (p, m_data.buffer, ((length > m_data.length) ? m_data.length : length) * sizeof (_T));
				Clear (); // hack to avoid d'tors
				}
			m_data.length = length;
			m_data.pos %= length;
#if DBG_MALLOC
			if (!UnregisterMemBlock (m_data.buffer))
				ArrayError ("invalid buffer pointer\n");
			TrackMemory (false);
#endif
			try {
				delete[] m_data.buffer;
				}
			catch (...) {
#if DBG_ARRAYS
				ArrayError ("invalid buffer pointer\n");
#endif
				}
#if DBG_MALLOC
			TrackMemory (b);
#endif
			return m_data.buffer = p;
			}

		// ----------------------------------------

		inline uint32_t Length (void) { return m_data.length; }

		// ----------------------------------------

		inline _T* Current (void) { return m_data.buffer ? m_data.buffer + m_data.pos : NULL; }

		// ----------------------------------------

		inline size_t Size (void) { return m_data.length * sizeof (_T); }
#if DBG_ARRAYS
		inline _T& operator[] (uint32_t i) {
			if (m_data.buffer && (i < m_data.length))
				return m_data.buffer [i];
			if (i == m_data.length)
				return m_data.null;
			else {
				ArrayError ("invalid array handle or index\n");
				return m_data.null;
				}
			}
#else
		inline _T& operator[] (const uint32_t i) { return m_data.buffer [i]; }
#endif

		// ----------------------------------------

		inline _T& operator* () const { return m_data.buffer; }

		// ----------------------------------------

		inline _T& operator= (CArray<_T>& source) { return Copy (source); }

		// ----------------------------------------

		inline _T& operator= (_T* source) {
#if DBG_ARRAYS
			if (!m_data.buffer)
				return m_data.null;
#endif
			memcpy (m_data.buffer, source, m_data.length * sizeof (_T));
			return m_data.buffer [0];
			}

		// ----------------------------------------

		_T& Copy (CArray<_T> const & source, uint32_t offset = 0) {
#if DBG_ARRAYS
			if (!source.m_data.buffer)
				return m_data.null;
#endif
			if (((static_cast<int32_t> (m_data.length)) >= 0) && (static_cast<int32_t> (source.m_data.length) > 0)) {
				if (!*GetName ())
					SetName (source.m_data.szName);
				if ((m_data.buffer && (m_data.length >= source.m_data.length + offset)) || Resize (source.m_data.length + offset, false)) {
					memcpy (m_data.buffer + offset, source.m_data.buffer, ((m_data.length - offset < source.m_data.length) ? m_data.length - offset : source.m_data.length) * sizeof (_T));
					}
				}
			return m_data.buffer [0];
			}

		// ----------------------------------------

		inline _T operator+ (CArray<_T>& source) {
			CArray<_T> a (*this);
			a += source;
			return a;
			}

		// ----------------------------------------

		inline _T& operator+= (CArray<_T>& source) {
			uint32_t offset = m_data.length;
			if (m_data.buffer)
				Resize (m_data.length + source.m_data.length);
			return Copy (source, offset);
			}

		// ----------------------------------------

		inline bool operator== (CArray<_T>& other) {
			return (m_data.length == other.m_data.length) && !(m_data.length && memcmp (m_data.buffer, other.m_data.buffer));
			}

		// ----------------------------------------

		inline bool operator!= (CArray<_T>& other) {
			return (m_data.length != other.m_data.length) || (m_data.length && memcmp (m_data.buffer, other.m_data.buffer));
			}

		// ----------------------------------------

		inline _T* Start (void) { return m_data.buffer; }

		// ----------------------------------------

		inline _T* End (void) { return (m_data.buffer && m_data.length) ? m_data.buffer + m_data.length - 1 : NULL; }

		// ----------------------------------------

		inline _T* operator++ (void) {
			if (!m_data.buffer)
				return NULL;
			if (m_data.pos < m_data.length - 1)
				m_data.pos++;
			else if (m_data.bWrap)
				m_data.pos = 0;
			else
				return NULL;
			return m_data.buffer + m_data.pos;
			}

		// ----------------------------------------

		inline _T* operator-- (void) {
			if (!m_data.buffer)
				return NULL;
			if (m_data.pos > 0)
				m_data.pos--;
			else if (m_data.bWrap)
				m_data.pos = m_data.length - 1;
			else
				return NULL;
			return m_data.buffer + m_data.pos;
			}

#if DBG_ARRAYS

		// ----------------------------------------

		inline _T* operator+ (uint32_t i) {
			if (m_data.buffer && (i < m_data.length))
				return m_data.buffer + i;
			if (i == m_data.length)
				return NULL;
			else {
				ArrayError ("invalid array handle or index\n");
				return  NULL;
				}
			}

#else

		inline _T* operator+ (uint32_t i) { return m_data.buffer ? m_data.buffer + i : NULL; }

#endif

		// ----------------------------------------

		inline _T* operator- (uint32_t i) { return m_data.buffer ? m_data.buffer - i : NULL; }

		// ----------------------------------------

		CArray<_T>& ShareBuffer (CArray<_T>& child) {
			memcpy (&child.m_data, &m_data, sizeof (m_data));
			if (!child.m_data.nMode)
				child.m_data.nMode = 1;
			return child;
			}

		// ----------------------------------------

		inline bool operator! () { return m_data.buffer == NULL; }

		// ----------------------------------------

		inline uint32_t Pos (void) { return m_data.pos; }

		// ----------------------------------------

		inline void Pos (uint32_t pos) { m_data.pos = pos % m_data.length; }

		// ----------------------------------------

		size_t Read (CFile& cf, uint32_t nCount = 0, uint32_t nOffset = 0, int32_t bCompressed = 0) {
			if (!m_data.buffer)
				return -1;
			if (nOffset >= m_data.length)
				return -1;
			if (!nCount)
				nCount = m_data.length - nOffset;
			else if (nCount > m_data.length - nOffset)
				nCount = m_data.length - nOffset;
			return cf.Read (m_data.buffer + nOffset, sizeof (_T), nCount, bCompressed);
			}

		// ----------------------------------------

		size_t Write (CFile& cf, uint32_t nCount = 0, uint32_t nOffset = 0, int32_t bCompressed = 0) {
			if (!m_data.buffer)
				return -1;
			if (nOffset >= m_data.length)
				return -1;
			if (!nCount)
				nCount = m_data.length - nOffset;
			else if (nCount > m_data.length - nOffset)
				nCount = m_data.length - nOffset;
			return cf.Write (m_data.buffer + nOffset, sizeof (_T), nCount, bCompressed);
			}

		// ----------------------------------------

		inline void SetWrap (bool bWrap) { m_data.bWrap = bWrap; }

		// ----------------------------------------

		/*
		inline void SortAscending (int32_t left = 0, int32_t right = -1) {
			if (m_data.buffer)
				CQuickSort<_T>::SortAscending (m_data.buffer, left, (right >= 0) ? right : m_data.length - 1);
				}

		// ----------------------------------------

		inline void SortDescending (int32_t left = 0, int32_t right = -1) {
			if (m_data.buffer)
				CQuickSort<_T>::SortDescending (m_data.buffer, left, (right >= 0) ? right : m_data.length - 1);
			}

		// ----------------------------------------

#ifdef _WIN32
		typedef typename CQuickSort<_T>::comparator comparator;

		inline void SortAscending (comparator compare, int32_t left = 0, int32_t right = -1) {
			if (m_data.buffer)
				CQuickSort<_T>::SortAscending (m_data.buffer, left, (right >= 0) ? right : m_data.length - 1, compare);
			}

		// ----------------------------------------

		inline void SortDescending (comparator compare, int32_t left = 0, int32_t right = -1) {
			if (m_data.buffer)
				CQuickSort<_T>::SortDescending (m_data.buffer, left, (right >= 0) ? right : m_data.length - 1, compare);
			}
#endif

		// ----------------------------------------

		inline int32_t BinSearch (_T key, int32_t left = 0, int32_t right = -1) {
			return m_data.buffer ? CQuickSort<_T>::BinSearch (m_data.buffer, left, (right >= 0) ? right : m_data.length - 1, key) : -1;
			}
*/
	};

//-----------------------------------------------------------------------------

inline int32_t operator- (char* v, CArray<char>& a) { return a.Index (v); }
inline int32_t operator- (uint8_t* v, CArray<uint8_t>& a) { return a.Index (v); }
inline int32_t operator- (int16_t* v, CArray<int16_t>& a) { return a.Index (v); }
inline int32_t operator- (uint16_t* v, CArray<uint16_t>& a) { return a.Index (v); }
inline int32_t operator- (int32_t* v, CArray<int32_t>& a) { return a.Index (v); }
inline int32_t operator- (uint32_t* v, CArray<uint32_t>& a) { return a.Index (v); }

//-----------------------------------------------------------------------------

class CCharArray : public CArray<char> {
	public:
		inline char* operator= (const char* source) {
			uint32_t l = uint32_t (strlen (source) + 1);
			if ((l > this->m_data.length) && !this->Resize (this->m_data.length + l))
				return NULL;
			memcpy (this->m_data.buffer, source, l);
			return this->m_data.buffer;
		}
};

//-----------------------------------------------------------------------------

class CByteArray : public CArray<uint8_t> {};
class CShortArray : public CArray<int16_t> {};
class CUShortArray : public CArray<uint16_t> {};
class CIntArray : public CArray<int32_t> {};
class CUIntArray : public CArray<uint32_t> {};
class CFloatArray : public CArray<float> {};

//-----------------------------------------------------------------------------

template < class _T, uint32_t length >
class CStaticArray : public CArray < _T > {

	template < class _U, uint32_t _length >
	class CStaticArrayData {
		public:
			_U		buffer [_length];
			};

	protected:
		CStaticArrayData< _T, length > m_data;

	public:
		CStaticArray () { Create (length); }

		_T *Create (uint32_t _length) {
			this->SetBuffer (m_data.buffer, 2, _length);
			return m_data.buffer;
			}
		void Destroy (void) { }
	};

//-----------------------------------------------------------------------------


#endif //_CARRAY_H
