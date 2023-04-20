#ifndef XCFILE_H
#define XCFILE_H

#include <stdio.h>
#include <stdint.h>
#include "xvecmat.h"
extern "C" {
#include "physfs.h"
#include "physfsx.h"
}

class CFile {
	char *name;
	PHYSFS_file *f;
	inline void ReadBytes(void*buf, int n) { if (PHYSFS_readBytes(f, buf, n) != n) abort(); }
	char *sbuf;
	char *sbufp;
	int sbufrest;
	int sbufeof;
public:
CFile() : name(NULL), f(NULL), sbuf(NULL), sbufrest(0), sbufeof(0) {}
~CFile() { Close(); if (sbuf) free(sbuf); }
inline const char *Name() { return name; }
inline void Close() { if (!f) return; PHYSFS_close(f); f = NULL; }
inline time_t Date(char const*, char const*, int){ return 0;}
inline bool EoF() { return sbuf ? sbufeof : PHYSFS_eof(f) != 0; }
inline char *GetS(char*buf, size_t size) {
	char *p = buf;
	if (!sbuf)
		sbuf = sbufp = (char *)malloc(65536);
	if (!size)
		return buf;
	size--;
	while (size) {
		if (!sbufrest && !(sbufrest = PHYSFS_readBytes(f, sbufp = sbuf, 65536))) {
			sbufeof = 1;
			break;
		}
		char *nl = (char *)memchr(sbufp, '\n', sbufrest);
		if (nl && (size_t)(nl - sbufp + 1) < size)
			size = nl - sbufp + 1;
		size_t len = size < (size_t)sbufrest ? size : sbufrest;
		memcpy(p, sbufp, len);
		p += len;
		sbufp += len;
		size -= len;
		sbufrest -= len;
		if (p - buf >= 2 && p[-1] == '\n' && p[-2] == '\r')
			(--p)[-1] = '\n';
	}
	*p = 0;
	return p == buf ? NULL : buf;
	//return PHYSFSX_fgets(buf, size, f);
}
inline void Init() { f = NULL; }
inline static void MkDir(char const*path) {}
inline bool Open(char const*file, char const*dir, char const*mode, int z) {
	if (f)
		PHYSFS_close(f);
	f = PHYSFSX_openReadBuffered(file);
	return f;
}
inline int8_t ReadByte() { int8_t x; ReadBytes(&x, sizeof(x)); return x; }
inline int ReadInt() { int x; ReadBytes(&x, sizeof(x)); return x; }
inline short ReadShort() { short x; ReadBytes(&x, sizeof(x)); return x; }
inline void ReadVector(CFloatVector3&v) { ReadBytes(&v, sizeof(v)); }
inline size_t Read(void*buf, size_t size, size_t nelem, int c=0) {
	return PHYSFS_readBytes(f, buf, size * nelem) / size;
}
inline static void SplitPath(char const*path, char*dir, char*fn, char*ext) {
	const char *p1 = strrchr(path, '/');
	const char *p2 = strrchr(path, '\\');
	const char *p = !p2 || (p1 && p1 > p2) ? p1 : p2;
	p = p ? p + 1 : path;
	const char *pe = NULL; //ext ? strrchr(p, '.') : NULL;
	if (!pe)
		pe = path + strlen(path);
	if (dir) {
		memcpy(dir, path, p - path);
		dir[p - path] = 0;
	}
	if (fn) {
		memcpy(fn, p, pe - p);
		fn[pe - p] = 0;
	}
	if (ext)
		strcpy(ext, pe);
}
inline void WriteByte(signed char b) {}
inline void WriteInt(int) { }
inline void WriteShort(short) {}
inline void WriteVector(CFloatVector3 const&) { }
inline int Write(void const*buf, int a, int b, int c=0) { return 0;}
inline size_t Seek(PHYSFS_sint64 ofs, int method) {
	switch (method) {
		case 0: PHYSFS_seek(f, ofs); break;
		case 1: PHYSFS_seek(f, PHYSFS_tell(f) + ofs); break;
		case 2: PHYSFS_seek(f, PHYSFS_fileLength(f) + ofs); break;
	}
	return PHYSFS_tell(f);
}
inline bool File() { return f; }
};

#endif
