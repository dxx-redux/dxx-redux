#if 0
__attribute__((optimize("-O3")))
static char *strupr(char *s) {
	for (char *p = s, c; c = *p; p++)
		if (c >= 'a' && c <= 'z')
			*p = c - 'a' + 'A';
	return s;
}
#endif

#if 0
__attribute__((optimize("-O3")))
static inline void strupr4(char *s) {
	uint32_t *p = (uint32_t *)s;
	for (int i, l = strlen(s); l >= 4; l -= 4, p++) {
		uint32_t v = *p;
		uint32_t v1 = v & 0x40404040;
		if (!v1)
			continue;
		if (!(v & (v1 >> 1)))
			continue;
		for (i = 0, s = (char *)p; i < 4; i++, s++)
			if (*s >= 'a' && *s <= 'z')
				*s -= 'a' - 'A';
	}
	for (s = (char *)p; *s; s++)
		if (*s >= 'a' && *s <= 'z')
			*s -= 'a' - 'A';
}
#endif

__attribute__((optimize("-O3")))
static inline void strupr8(char *s) {
	uint64_t *p = (uint64_t *)s;
	for (int i, l = strlen(s); l >= 8; l -= 8, p++) {
		uint64_t v = *p;
		uint64_t v1 = v & 0x4040404040404040;
		if (!v1)
			continue;
		if (!(v & (v1 >> 1)))
			continue;
		for (i = 0, s = (char *)p; i < 8; i++, s++)
			if (*s >= 'a' && *s <= 'z')
				*s -= 'a' - 'A';
	}
	for (s = (char *)p; *s; s++)
		if (*s >= 'a' && *s <= 'z')
			*s -= 'a' - 'A';
}

#if 0
#define UINT64_C(x) ((uint64_t)x)

//bool contains_zero_byte(uint64_t v) {
//  return (v - UINT64_C(0x0101010101010101)) & ~(v) & UINT64_C(0x8080808080808080);
//}

static void strupr8(char *s) {
	int l = strlen(s);
	uint64_t *p = (uint64_t *)s;
	for (; l >= 8; p++, l -= 8) {
		uint64_t v = *p;
		uint64_t v1 = v & 0x4040404040404040;
		if (!v1) // any alpha
			continue;
		uint64_t v2 = v & (v << 1);
		if (!v2) // any lower
			continue;
		uint64_t off = (v & 0x8080808080808080) |
			(v - 0x6161616161616161)

	for (char *p = s, c; c = *p; p++)
		if (c >= 'a' && c <= 'z')
			*p = c - 'a' + 'A';
	return s;
}
#endif

#ifndef WIN32
static char *strlwr(char *s) {
	for (char *p = s, c; (c = *p); p++)
		if (c >= 'A' && c <= 'Z')
			*p = c - 'A' + 'a';
	return s;
}
#endif
