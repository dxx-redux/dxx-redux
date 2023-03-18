/*
   THE COMPUTER CODE CONTAINED HEREIN IS THE SOLE PROPERTY OF PARALLAX
   SOFTWARE CORPORATION ("PARALLAX").  PARALLAX, IN DISTRIBUTING THE CODE TO
   END-USERS, AND SUBJECT TO ALL OF THE TERMS AND CONDITIONS HEREIN, GRANTS A
   ROYALTY-FREE, PERPETUAL LICENSE TO SUCH END-USERS FOR USE BY SUCH END-USERS
   IN USING, DISPLAYING,  AND CREATING DERIVATIVE WORKS THEREOF, SO LONG AS
   SUCH USE, DISPLAY OR CREATION IS FOR NON-COMMERCIAL, ROYALTY OR REVENUE
   FREE PURPOSES.  IN NO EVENT SHALL THE END-USER USE THE COMPUTER CODE
   CONTAINED HEREIN FOR REVENUE-BEARING PURPOSES.  THE END-USER UNDERSTANDS
   AND AGREES TO THE TERMS HEREIN AND ACCEPTS THE SAME BY USE OF THIS FILE.
   COPYRIGHT 1993-1998 PARALLAX SOFTWARE CORPORATION.  ALL RIGHTS RESERVED.
 */

#ifndef _XVECMAT_H
#define _XVECMAT_H

#include "xfix.h"
#include <cstdlib>
#include <string.h>

//namespace VecMat {

const size_t R = 0;
const size_t G = 1;
const size_t B = 2;
const size_t A = 3;

#if 0
const size_t X = 0;
const size_t Y = 1;
const size_t Z = 2;
const size_t W = 3;

const size_t PA = 0;
const size_t BA = 1;
const size_t HA = 2;

const size_t RVEC = 0;
const size_t UVEC = 1;
const size_t FVEC = 2;
const size_t HVEC = 3;
#endif

class CFixVector;
class CFloatVector;
class CFloatVector3;
class CAngleVector;

class CFixMatrix;
class CFloatMatrix;

//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
/**
 * \class __pack__ CFixVector
 * A 3 element fixed-point vector.
 */
class __pack__ CFixVector {
#if 1
	public:
		union {
			struct {
				fix	x, y, z;
			} coord;
			fix	vec [3];
		} v;
#else
	private:
		fix dir [3];
#endif

	public:
		static const CFixVector ZERO;
		static const CFixVector XVEC;
		static const CFixVector YVEC;
		static const CFixVector ZVEC;

		static const CFixVector Create (fix f0, fix f1, fix f2);
		static const CFixVector Avg (const CFixVector& src0, const CFixVector& src1);
		static const CFixVector Avg (CFixVector& src0, CFixVector& src1, CFixVector& src2, CFixVector& src3);
		void Check (void);
		static const CFixVector Cross (const CFixVector& v0, const CFixVector& v1);
		static CFixVector& Cross (CFixVector& dest, const CFixVector& v0, const CFixVector& v1);
		// computes the delta angle between two vectors.
		// vectors need not be normalized. if they are, call CFixVector::DeltaAngleNorm ()
		// the forward vector (third parameter) can be NULL, in which case the absolute
		// value of the angle in returned.  Otherwise the angle around that vector is
		// returned.
		static const fixang DeltaAngle (const CFixVector& v0, const CFixVector& v1, CFixVector *fVec);		//computes the delta angle between two normalized vectors.
		static const fixang DeltaAngleNorm (const CFixVector& v0, const CFixVector& v1, CFixVector *fVec);
		static const fix Dist (const CFixVector& vec0, const CFixVector& vec1);
		static const fix DistQuick (const CFixVector& vec0, const CFixVector& vec1);
		static const fix Dot (const CFixVector& v0, const CFixVector& v1);
		static const fix Dot (const fix x, const fix y, const fix z, const CFixVector& v);
		static const fix Normalize (CFixVector& v);
		static const fix NormalizeQuick (CFixVector& v);
		static const CFixVector Perp (const CFixVector& p0, const CFixVector& p1, const CFixVector& p2);
		static CFixVector& Perp (CFixVector& dest, const CFixVector& p0, const CFixVector& p1, const CFixVector& p2);
		static const CFixVector Normal (const CFixVector& p0, const CFixVector& p1, const CFixVector& p2);
		static const CFixVector Random (void);
		static const CFixVector Reflect (const CFixVector& d, const CFixVector& n);
		// return the normalized direction vector between two points
		// dest = normalized (end - start).  Returns Mag of direction vector
		// NOTE: the order of the parameters m.matches the vector subtraction
		static const fix NormalizedDir (CFixVector& dest, const CFixVector& end, const CFixVector& start);
		static const fix NormalizedDirQuick (CFixVector& dest, const CFixVector& end, const CFixVector& start);

		bool operator== (const CFixVector& rhs) const;

		const CFixVector& Set (fix x, fix y, fix z);
		void Set (const fix *vec);

		bool IsZero (void) const;
		void SetZero (void);
		const int32_t Sign (void) const;

		fix SqrMag (void) const;
		float Sqr (float f) const;
		fix Mag (void) const;
		fix MagQuick (void) const;
		CFixVector& Scale (CFixVector& scale);
		const void Scale2 (const fix num, const fix den);
		const CFixVector MulInt (const int n) const;
		const CFixVector DivInt (const int n) const;
		CFixVector& Neg (void);
		const CFixVector operator- (void) const;
		const bool operator== (const CFixVector& other);
		const bool operator!= (const CFixVector& other);
		const bool operator< (const CFixVector& other);
		const bool operator<= (const CFixVector& other);
		const bool operator> (const CFixVector& other);
		const bool operator>= (const CFixVector& other);
		const CFixVector& operator+= (const CFixVector& other);
		const CFixVector& operator+= (const CFloatVector& other);
		const CFixVector& operator-= (const CFloatVector& other);
		const CFixVector& operator-= (const CFixVector& other);
		const CFixVector& operator*= (const CFixVector& other);
		const CFixVector& operator*= (const fix s);
		const CFixVector& operator/= (const fix s);
		const CFixVector& operator*= (const float s);
		const CFixVector operator+ (const CFixVector& other) const;
		const CFixVector operator+ (const CFloatVector& other) const;
		const CFixVector operator- (const CFixVector& other) const;
		const CFixVector operator- (const CFloatVector& other) const;
		const fix operator* (const CFixVector& other) const;
		const CFixVector operator* (const fix s) const;
		const CFixVector operator/ (const fix s) const;
		CFixVector& Assign (const CFloatVector3& other);
		CFixVector& Assign (const CFloatVector& other);
		CFixVector& Assign (const CFixVector& other);
#if 0
		// access op for assignment
		fix& operator[] (size_t i);
		// read-only access op
		const fix operator[] (size_t i) const;

		inline fix& X (void) { return vec.coord.x; }
		inline fix& Y (void) { return vec.coord.y; }
		inline fix& Z (void) { return vec.coord.z; }
#endif
		// compute intersection of a line through a point a, with the line being orthogonal relative
		// to the plane given by the Normal n and a point p lieing in the plane, and store it in i.
		const CFixVector PlaneProjection (const CFixVector& n, const CFixVector& p) const;
		//compute the distance from a point to a plane.  takes the normalized Normal
		//of the plane (ebx), a point on the plane (edi), and the point to check (esi).
		//returns distance in eax
		//distance is signed, so Negative Dist is on the back of the plane
		const fix DistToPlane (const CFixVector& n, const CFixVector& p) const;
		//extract heading and pitch from a vector, assuming bank==0
		const CAngleVector ToAnglesVecNorm (void) const;
		//extract heading and pitch from a vector, assuming bank==0
		const CAngleVector ToAnglesVec (void) const;
};

//inline const fix operator* (const CFixVector& v0, const CFixVector& v1);
//inline const CFixVector operator* (const CFixVector& v, const fix s);
//inline const CFixVector operator* (const fix s, const CFixVector& v);
//inline const CFixVector operator/ (const CFixVector& v, const fix d);

//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
/**
 * \class __pack__ CFloatVector
 * A 4 element floating point vector class
 */
class CFloatVector {
#if 1
	public:
		union {
			struct {
				float x, y, z, w;
			} coord;
			struct {
				float r, g, b, a;
			} color;
			float vec [4];
		} v;
#else
	private:
		float dir [4];
#endif

	public:
		static const CFloatVector ZERO;
		static const CFloatVector ZERO4;
		static const CFloatVector XVEC;
		static const CFloatVector YVEC;
		static const CFloatVector ZVEC;

		static const CFloatVector Create (float f0, float f1, float f2, float f3 = 1.0f);

		static const CFloatVector Avg (const CFloatVector& src0, const CFloatVector& src1);
		static const CFloatVector Cross (const CFloatVector& v0, const CFloatVector& v1);
		static CFloatVector& Cross (CFloatVector& dest, const CFloatVector& v0, const CFloatVector& v1);

		static const float Dist (const CFloatVector& v0, const CFloatVector& v1);
		static const float Dot (const CFloatVector& v0, const CFloatVector& v1);
		static const float Dot (const float x, const float y, const float z, const CFloatVector& v);
		static const float Normalize (CFloatVector& vec);
		static const CFloatVector Min (CFloatVector& v, const float l);
		static const CFloatVector Max (CFloatVector& v, const float l);
		static const CFloatVector Min (CFloatVector& v, const CFloatVector l);
		static const CFloatVector Max (CFloatVector& v, const CFloatVector l);
		static const CFloatVector Perp (const CFloatVector& p0, const CFloatVector& p1, const CFloatVector& p2);
		static CFloatVector& Perp (CFloatVector& dest, const CFloatVector& p0, const CFloatVector& p1, const CFloatVector& p2);
		static const CFloatVector Normal (const CFloatVector& p0, const CFloatVector& p1, const CFloatVector& p2);
		static const CFloatVector Reflect (const CFloatVector& d, const CFloatVector& n);

#if 0
		// access op for assignment
		float& operator[] (size_t i);
		// read-only access op
		const float operator[] (size_t i) const;
#endif

		bool IsZero (void) const;

		void SetZero (void);
		void Set (const float f0, const float f1, const float f2, const float f3=1.0f);
		void Set (const float *vec);
		const float SqrMag (void) const;
		const float Mag (void) const;
		CFloatVector& Neg (void);
		CFloatVector& Scale (CFloatVector& scale);
		CFloatVector3* XYZ (void);

		const CFloatVector operator- (void) const;
		const bool operator== (const CFloatVector& other);
		const bool operator!= (const CFloatVector& other);
		const bool operator< (const CFloatVector& other);
		const bool operator<= (const CFloatVector& other);
		const bool operator> (const CFloatVector& other);
		const bool operator>= (const CFloatVector& other);
		const CFloatVector& operator+= (const CFloatVector& other);
		const CFloatVector& operator+= (const CFixVector& other);
		const CFloatVector& operator-= (const CFixVector& other);
		const CFloatVector& operator-= (const CFloatVector& other);
		const CFloatVector& operator*= (const CFloatVector& other);
		const CFloatVector& operator*= (const float s);
		const CFloatVector& operator/= (const float s);
		const CFloatVector  operator+ (const CFloatVector& other) const;
		const CFloatVector  operator+ (const CFixVector& other) const;
		const CFloatVector  operator- (const CFloatVector& other) const;
		const CFloatVector  operator- (const CFixVector& other) const;
		const float operator* (const CFloatVector& other) const;
		const CFloatVector operator* (const float s) const;
		const CFloatVector operator/ (const float s) const;
		CFloatVector& Assign (const CFloatVector3& other);
		CFloatVector& Assign (const CFloatVector& other);
		CFloatVector& Assign (const CFixVector& other);
		const float DistToPlane (const CFloatVector& n, const CFloatVector& p) const;
		inline float& X (void) { return v.coord.x; }
		inline float& Y (void) { return v.coord.y; }
		inline float& Z (void) { return v.coord.z; }
		inline float& W (void) { return v.coord.w; }
		inline float& Red (void) { return v.color.r; }
		inline float& Green (void) { return v.color.g; }
		inline float& Blue (void) { return v.color.b; }
		inline float& Alpha (void) { return v.color.a; }
		inline float Max (void) { return (v.coord.x > v.coord.y) ? (v.coord.x > v.coord.z) ? v.coord.x : v.coord.z : (v.coord.y > v.coord.z) ? v.coord.y : v.coord.z; }
		inline float Sum (void) { return v.coord.x + v.coord.y + v.coord.z; }

		inline float& operator[] (size_t i) { return v.vec [i]; }
		};

//const float operator* (const CFloatVector& v0, const CFloatVector& v1);
//const CFloatVector operator* (const CFloatVector& v, const float s);
//const CFloatVector operator* (const float s, const CFloatVector& v);
//const CFloatVector operator/ (const CFloatVector& v, const float s);

//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------

/**
 * \class __pack__ CFloatVector3
 * A 3 element floating point vector class
 */
class CFloatVector3 {
#if 1
	public:
		union {
			struct {
				float x, y, z;
			} coord;
			struct {
				float r, g, b;
			} color;
			float vec [3];
		} v;
#else
	private:
		float dir [3];
#endif
	public:
		static const CFloatVector3 ZERO;
		static const CFloatVector3 XVEC;
		static const CFloatVector3 YVEC;
		static const CFloatVector3 ZVEC;

		static const CFloatVector3 Create (float f0, float f1, float f2);
		static const CFloatVector3 Avg (const CFloatVector3& src0, const CFloatVector3& src1);
		static const CFloatVector3 Cross (const CFloatVector3& v0, const CFloatVector3& v1);
		static CFloatVector3& Cross (CFloatVector3& dest, const CFloatVector3& v0, const CFloatVector3& v1);

		static const float Dist (const CFloatVector3& v0, const CFloatVector3& v1);
		static const float Dot (const CFloatVector3& v0, const CFloatVector3& v1);
		static const float Normalize (CFloatVector3& vec);
		static const CFloatVector3 Perp (const CFloatVector3& p0, const CFloatVector3& p1, const CFloatVector3& p2);
		static CFloatVector3& Perp (CFloatVector3& dest, const CFloatVector3& p0, const CFloatVector3& p1, const CFloatVector3& p2);
		static const CFloatVector3 Normal (const CFloatVector3& p0, const CFloatVector3& p1, const CFloatVector3& p2);
		static const CFloatVector3 Reflect (const CFloatVector3& d, const CFloatVector3& n);
#if 0
		// access op for assignment
		float& operator[] (size_t i);
		// read-only access op
		const float operator[] (size_t i) const;
#endif
		bool IsZero (void) const;
		void SetZero (void);
		void Set (const float f0, const float f1, const float f2);
		void Set (const float *vec);

		CFloatVector3& Neg (void);
		CFloatVector3& Scale (CFloatVector3& scale);
		const float Mag (void) const;
		const float SqrMag (void) const;

		const CFloatVector3 operator- (void) const;
		const bool operator== (const CFloatVector3& other);
		const bool operator!= (const CFloatVector3& other);
		const CFloatVector3& operator+= (const CFloatVector3& other);
		const CFloatVector3& operator-= (const CFloatVector3& other);
		const CFloatVector3& operator*= (const CFloatVector3& other);
		const CFloatVector3& operator*= (const float s);
		const CFloatVector3& operator/= (const float s);
		const CFloatVector3 operator+ (const CFloatVector3& other) const;
		const CFloatVector3 operator- (const CFloatVector3& other) const;
		const float operator* (const CFloatVector3& other) const;
		const CFloatVector3 operator* (const float s) const;
		const CFloatVector3 operator/ (const float s) const;
		CFloatVector3& Assign (const CFloatVector3& other);
		CFloatVector3& Assign (const CFloatVector& other);
		CFloatVector3& Assign (const CFixVector& other);
		inline float& X (void) { return v.coord.x; }
		inline float& Y (void) { return v.coord.y; }
		inline float& Z (void) { return v.coord.z; }
		inline float& Red (void) { return v.color.r; }
		inline float& Green (void) { return v.color.g; }
		inline float& Blue (void) { return v.color.b; }
		inline float Max (void) { return (v.coord.x > v.coord.y) ? (v.coord.x > v.coord.z) ? v.coord.x : v.coord.z : (v.coord.y > v.coord.z) ? v.coord.y : v.coord.z; }
		inline float Sum (void) { return v.coord.x + v.coord.y + v.coord.z; }
};

//const float operator* (const CFloatVector3& v0, const CFloatVector3& v1);
//const CFloatVector3 operator* (const CFloatVector3& v, float s);
//const CFloatVector3 operator* (float s, const CFloatVector3& v);
//const CFloatVector3 operator/ (const CFloatVector3& v, float s);

// -----------------------------------------------------------------------------
// -----------------------------------------------------------------------------
// -----------------------------------------------------------------------------

//Angle vector.  Used to store orientations

class __pack__ CAngleVector {
#if 1
	public:
		union {
			struct {
				fixang p, b, h; // pitch, roll, yaw
			} coord;
			fixang vec [3];
		} v;
#else
	private:
		 fixang vec [3];
#endif
public:
	static const CAngleVector ZERO;
	static const CAngleVector Create (const fixang p, const fixang b, const fixang h) {
		CAngleVector a;
		a.v.coord.p = p; a.v.coord.b = b; a.v.coord.h = h;
		return a;
		}
#if 0
	// access op for assignment
	fixang& operator[] (size_t i) { return vec.vec [i]; }
	// read-only access op
	const fixang operator[] (size_t i) const { return vec.vec [i]; }
#endif
	bool IsZero (void) const { return !(v.coord.p || v.coord.h || v.coord.b); }
	void SetZero (void) { memset (&v, 0, sizeof (v)); }
	void Set (const fixang p, const fixang b, const fixang h) { v.coord.p = p; v.coord.b = b; v.coord.h = h; }
	void Set (const CAngleVector& other) { v.coord.p = other.v.coord.p; v.coord.b = other.v.coord.b; v.coord.h = other.v.coord.h; }
#if 0
	inline CAngleVector& operator+= (const CAngleVector& other) {
		v.coord.p += other.v.coord.p;
		v.coord.b += other.v.coord.b;
		v.coord.h += other.v.coord.h;
		return *this;
		}

	inline CAngleVector& operator-= (const CAngleVector& other) {
		v.coord.p -= other.v.coord.p;
		v.coord.b -= other.v.coord.b;
		v.coord.h -= other.v.coord.h;
		return *this;
		}
#endif
	inline CAngleVector& operator+= (const CAngleVector other) {
		v.coord.p += other.v.coord.p;
		v.coord.b += other.v.coord.b;
		v.coord.h += other.v.coord.h;
		return *this;
		}

	inline CAngleVector& operator-= (const CAngleVector other) {
		v.coord.p -= other.v.coord.p;
		v.coord.b -= other.v.coord.b;
		v.coord.h -= other.v.coord.h;
		return *this;
		}

	inline CAngleVector& operator*= (int32_t nScale) {
		v.coord.p *= nScale;
		v.coord.b *= nScale;
		v.coord.h *= nScale;
		return *this;
		}

	inline CAngleVector& operator/= (int32_t nScale) {
		v.coord.p /= nScale;
		v.coord.b /= nScale;
		v.coord.h /= nScale;
		return *this;
		}

	inline CAngleVector operator+ (const CAngleVector other) {
		CAngleVector a;
		a.Set (v.coord.p + other.v.coord.p, v.coord.b + other.v.coord.b, v.coord.h + other.v.coord.h);
		return a;
		}

	inline CAngleVector operator- (const CAngleVector other) {
		CAngleVector a;
		a.Set (v.coord.p - other.v.coord.p, v.coord.b - other.v.coord.b, v.coord.h - other.v.coord.h);
		return a;
		}

	inline CAngleVector operator* (const int32_t nScale) {
		CAngleVector a;
		a.Set (v.coord.p * nScale, v.coord.b * nScale, v.coord.h * nScale);
		return a;
		}

	inline CAngleVector operator/ (const int32_t nScale) {
		CAngleVector a;
		a.Set (v.coord.p / nScale, v.coord.b / nScale, v.coord.h / nScale);
		return a;
		}

	inline const float SqrMag (void) const {
		return X2F (v.coord.p) * X2F (v.coord.p) + X2F (v.coord.b) * X2F (v.coord.b) + X2F (v.coord.h) * X2F (v.coord.h);
		}

	inline const float Mag (void) const {
		return (const float) sqrt (SqrMag ());
		}

};


//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
// -----------------------------------------------------------------------------
// CFloatVector static inlines

inline const CFloatVector CFloatVector::Create (float f0, float f1, float f2, float f3) {
	CFloatVector vec;
	vec.Set (f0, f1, f2, f3);
	return vec;
}

inline const CFloatVector CFloatVector::Avg (const CFloatVector& src0, const CFloatVector& src1) {
	CFloatVector vec;
	vec.Set ((src0.v.coord.x + src1.v.coord.x) / 2, (src0.v.coord.y + src1.v.coord.y) / 2, (src0.v.coord.z + src1.v.coord.z) / 2, 1.0);
	return vec;
}

inline CFloatVector& CFloatVector::Cross (CFloatVector& dest, const CFloatVector& v0, const CFloatVector& v1) {
	dest.Set (v0.v.coord.y * v1.v.coord.z - v0.v.coord.z * v1.v.coord.y,
	          v0.v.coord.z * v1.v.coord.x - v0.v.coord.x * v1.v.coord.z,
	          v0.v.coord.x * v1.v.coord.y - v0.v.coord.y * v1.v.coord.x);
	return dest;
}

inline const CFloatVector CFloatVector::Cross (const CFloatVector& v0, const CFloatVector& v1) {
	CFloatVector vec;
	vec.Set (v0.v.coord.y * v1.v.coord.z - v0.v.coord.z * v1.v.coord.y,
	         v0.v.coord.z * v1.v.coord.x - v0.v.coord.x * v1.v.coord.z,
	         v0.v.coord.x * v1.v.coord.y - v0.v.coord.y * v1.v.coord.x);
	return vec;
}

inline const float CFloatVector::Dist (const CFloatVector& v0, const CFloatVector& v1) {
	return (v0 - v1).Mag ();
}

inline const float CFloatVector::Dot (const CFloatVector& v0, const CFloatVector& v1) {
	return v0.v.coord.x * v1.v.coord.x + v0.v.coord.y * v1.v.coord.y + v0.v.coord.z * v1.v.coord.z;
}

inline const float CFloatVector::Dot (const float x, const float y, const float z, const CFloatVector& v) {
	return x * v.v.coord.x + y * v.v.coord.y + z * v.v.coord.z;
}

inline const float CFloatVector::Normalize (CFloatVector& vec) {
	float m = vec.Mag ();
	if (m)
		vec /= m;
	return m;
}

inline CFloatVector& CFloatVector::Perp (CFloatVector& dest, const CFloatVector& p0, const CFloatVector& p1, const CFloatVector& p2) {
	return Cross (dest, p1 - p0, p2 - p1);
}

inline const CFloatVector CFloatVector::Perp (const CFloatVector& p0, const CFloatVector& p1, const CFloatVector& p2) {
	return Cross (p1 - p0, p2 - p1);
}

inline const CFloatVector CFloatVector::Normal (const CFloatVector& p0, const CFloatVector& p1, const CFloatVector& p2) {
	CFloatVector vec = Perp (p0, p1, p2);
	Normalize (vec);
	return vec;
}

inline const CFloatVector CFloatVector::Reflect (const CFloatVector& d, const CFloatVector& n) {
	return n * (Dot (d, n) * -2.0f) + d;
}

inline const CFloatVector CFloatVector::Min (CFloatVector& v, const float l) {
	v.v.vec [0] = ::Min (v.v.vec [0], l);
	v.v.vec [1] = ::Min (v.v.vec [1], l);
	v.v.vec [2] = ::Min (v.v.vec [2], l);
	return v;
}

inline const CFloatVector CFloatVector::Max (CFloatVector& v, const float l) {
	v.v.vec [0] = ::Max (v.v.vec [0], l);
	v.v.vec [1] = ::Max (v.v.vec [1], l);
	v.v.vec [2] = ::Max (v.v.vec [2], l);
	return v;
}

inline const CFloatVector CFloatVector::Min (CFloatVector& v, const CFloatVector l) {
	v.v.vec [0] = ::Min (v.v.vec [0], l.v.vec [0]);
	v.v.vec [1] = ::Min (v.v.vec [1], l.v.vec [1]);
	v.v.vec [2] = ::Min (v.v.vec [2], l.v.vec [2]);
	return v;
}

inline const CFloatVector CFloatVector::Max (CFloatVector& v, const CFloatVector l) {
	v.v.vec [0] = ::Max (v.v.vec [0], l.v.vec [0]);
	v.v.vec [1] = ::Max (v.v.vec [1], l.v.vec [1]);
	v.v.vec [2] = ::Max (v.v.vec [2], l.v.vec [2]);
	return v;
}

// -----------------------------------------------------------------------------
// CFloatVector member inlines

//inline float& CFloatVector::operator[] (size_t i) { return vec [i]; }

//inline const float CFloatVector::operator[] (size_t i) const { return vec [i]; }

inline bool CFloatVector::IsZero (void) const { return (v.coord.x == 0.0f) && (v.coord.y == 0.0f) && (v.coord.z == 0.0f) && (v.coord.w == 0.0f); }

inline void CFloatVector::SetZero (void) { memset (&v, 0, sizeof (v)); }

inline void CFloatVector::Set (const float f0, const float f1, const float f2, const float f3) {
	v.coord.x = f0; v.coord.y = f1; v.coord.z = f2; v.coord.w = f3;
}

inline void CFloatVector::Set (const float *vec) {
	v.coord.x = vec [0], v.coord.y = vec [1], v.coord.z = vec [2], v.coord.w = 1.0f;
}

inline const float CFloatVector::SqrMag (void) const {
	return v.coord.x * v.coord.x + v.coord.y * v.coord.y + v.coord.z * v.coord.z;
}

inline const float CFloatVector::Mag (void) const {
	return (const float) sqrt (SqrMag ());
}

inline CFloatVector& CFloatVector::Scale (CFloatVector& scale) {
	v.coord.x *= scale.v.coord.x, v.coord.y *= scale.v.coord.y, v.coord.z *= scale.v.coord.z;
	return *this;
	}

inline CFloatVector& CFloatVector::Neg (void) {
	v.coord.x = -v.coord.x, v.coord.y = -v.coord.y, v.coord.z = -v.coord.z;
	return *this;
	}

inline CFloatVector3* CFloatVector::XYZ (void) { return reinterpret_cast<CFloatVector3*> (&v.coord.x); }

inline const CFloatVector CFloatVector::operator- (void) const {
	CFloatVector vec;
	vec.Set (-v.coord.x, -v.coord.y, -v.coord.z);
	return vec;
}

inline CFloatVector& CFloatVector::Assign (const CFloatVector3& other) {
	v.coord.x = other.v.coord.x, v.coord.y = other.v.coord.y, v.coord.z = other.v.coord.z;
	return *this;
}

inline CFloatVector& CFloatVector::Assign (const CFloatVector& other) {
	v.coord.x = other.v.coord.x, v.coord.y = other.v.coord.y, v.coord.z = other.v.coord.z, v.coord.w = other.v.coord.w;
	return *this;
}

inline CFloatVector& CFloatVector::Assign (const CFixVector& other) {
	v.coord.x = X2F (other.v.coord.x), v.coord.y = X2F (other.v.coord.y), v.coord.z = X2F (other.v.coord.z);
	return *this;
}

inline const bool CFloatVector::operator== (const CFloatVector& other) {
	return (v.coord.x == other.v.coord.x) && (v.coord.y == other.v.coord.y) && (v.coord.z == other.v.coord.z);
}

inline const bool CFloatVector::operator!= (const CFloatVector& other) {
	return (v.coord.x != other.v.coord.x) || (v.coord.y != other.v.coord.y) || (v.coord.z != other.v.coord.z);
}

inline const bool CFloatVector::operator< (const CFloatVector& other)
{
return (v.coord.x < other.v.coord.x) && (v.coord.y < other.v.coord.y) && (v.coord.z < other.v.coord.z);
}

inline const bool CFloatVector::operator<= (const CFloatVector& other)
{
return (v.coord.x <= other.v.coord.x) && (v.coord.y <= other.v.coord.y) && (v.coord.z <= other.v.coord.z);
}

inline const bool CFloatVector::operator> (const CFloatVector& other)
{
return (v.coord.x > other.v.coord.x) && (v.coord.y > other.v.coord.y) && (v.coord.z > other.v.coord.z);
}

inline const bool CFloatVector::operator>= (const CFloatVector& other)
{
return (v.coord.x >= other.v.coord.x) && (v.coord.y >= other.v.coord.y) && (v.coord.z >= other.v.coord.z);
}

inline const CFloatVector& CFloatVector::operator+= (const CFloatVector& other) {
	v.coord.x += other.v.coord.x; v.coord.y += other.v.coord.y; v.coord.z += other.v.coord.z;
	return *this;
}

inline const CFloatVector& CFloatVector::operator*= (const CFloatVector& other) {
	v.coord.x *= other.v.coord.x; v.coord.y *= other.v.coord.y; v.coord.z *= other.v.coord.z;
	return *this;
}

inline const CFloatVector& CFloatVector::operator+= (const CFixVector& other) {
	v.coord.x += X2F (other.v.coord.x); v.coord.y += X2F (other.v.coord.y); v.coord.z += X2F (other.v.coord.z);
	return *this;
}

inline const CFloatVector& CFloatVector::operator-= (const CFloatVector& other) {
	v.coord.x -= other.v.coord.x; v.coord.y -= other.v.coord.y; v.coord.z -= other.v.coord.z;
	return *this;
}

inline const CFloatVector& CFloatVector::operator-= (const CFixVector& other)
{
v.coord.x -= X2F (other.v.coord.x);
v.coord.y -= X2F (other.v.coord.y);
v.coord.z -= X2F (other.v.coord.z);
return *this;
}

inline const CFloatVector& CFloatVector::operator*= (const float s) {
	v.coord.x *= s; v.coord.y *= s; v.coord.z *= s;
	return *this;
}

inline const CFloatVector& CFloatVector::operator/= (const float s) {
	v.coord.x /= s; v.coord.y /= s; v.coord.z /= s;
	return *this;
}

inline const CFloatVector CFloatVector::operator+ (const CFloatVector& other) const {
	CFloatVector vec;
	vec.Set (v.coord.x + other.v.coord.x, v.coord.y + other.v.coord.y, v.coord.z + other.v.coord.z, 1);
	return vec;
}

inline const CFloatVector CFloatVector::operator+ (const CFixVector& other) const {
	CFloatVector vec;
	vec.Set (v.coord.x + X2F (other.v.coord.x), v.coord.y + X2F (other.v.coord.y), v.coord.z + X2F (other.v.coord.z), 1);
	return vec;
}

inline const CFloatVector CFloatVector::operator- (const CFloatVector& other) const {
	CFloatVector vec;
	vec.Set (v.coord.x - other.v.coord.x, v.coord.y - other.v.coord.y, v.coord.z - other.v.coord.z, 1);
	return vec;
}

inline const CFloatVector CFloatVector::operator- (const CFixVector& other) const {
	CFloatVector vec;
	vec.Set (v.coord.x - X2F (other.v.coord.x), v.coord.y - X2F (other.v.coord.y), v.coord.z - X2F (other.v.coord.z), 1);
	return vec;
}

inline const float CFloatVector::DistToPlane (const CFloatVector& n, const CFloatVector& p) const
{
#if 0
CFloatVector t = *this;
t -= p;
return CFloatVector::Dot (t, n);
#else
return CFloatVector::Dot (v.coord.x - p.v.coord.x, v.coord.y - p.v.coord.y, v.coord.z - p.v.coord.z, n);
#endif
}

// -----------------------------------------------------------------------------
// CFloatVector-related non-member ops

inline const float CFloatVector::operator* (const CFloatVector& other) const {
	return v.coord.x * other.v.coord.x + v.coord.y * other.v.coord.y + v.coord.z * other.v.coord.z;
}

inline const CFloatVector CFloatVector::operator* (const float s) const {
	CFloatVector vec;
	vec.Set (v.coord.x * s, v.coord.y * s, v.coord.z * s, 1.0f);
	return vec;
}

//inline const CFloatVector operator* (const float s, const CFloatVector& v) {
//	CFloatVector vec;
//	vec.Set (v.v.coord.x * s, v.v.coord.y * s, v.v.coord.z * s, 1.0f);
//	return vec;
//}

inline const CFloatVector CFloatVector::operator/ (const float s) const {
	CFloatVector vec;
	vec.Set (v.coord.x / s, v.coord.y / s, v.coord.z / s, 1.0);
	return vec;
}


//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
// CFloatVector3 static inlines

inline const CFloatVector3 CFloatVector3::Create (float f0, float f1, float f2) {
	CFloatVector3 vec;
	vec.Set (f0, f1, f2);
	return vec;
}

inline const CFloatVector3 CFloatVector3::Avg (const CFloatVector3& src0, const CFloatVector3& src1) {
	CFloatVector3 vec;
	vec.Set ((src0.v.coord.x + src1.v.coord.x) / 2, (src0.v.coord.y + src1.v.coord.y) / 2, (src0.v.coord.z + src1.v.coord.z) / 2);
	return vec;
}

inline CFloatVector3& CFloatVector3::Cross (CFloatVector3& dest, const CFloatVector3& v0, const CFloatVector3& v1) {
	dest.Set (v0.v.coord.y * v1.v.coord.z - v0.v.coord.z * v1.v.coord.y,
	          v0.v.coord.z * v1.v.coord.x - v0.v.coord.x * v1.v.coord.z,
	          v0.v.coord.x * v1.v.coord.y - v0.v.coord.y * v1.v.coord.x);
	return dest;
}

inline const CFloatVector3 CFloatVector3::Cross (const CFloatVector3& v0, const CFloatVector3& v1) {
	CFloatVector3 vec;
	vec.Set (v0.v.coord.y * v1.v.coord.z - v0.v.coord.z * v1.v.coord.y,
	       v0.v.coord.z * v1.v.coord.x - v0.v.coord.x * v1.v.coord.z,
	       v0.v.coord.x * v1.v.coord.y - v0.v.coord.y * v1.v.coord.x);
	return vec;
}

inline const float CFloatVector3::Dist (const CFloatVector3& v0, const CFloatVector3& v1) {
	return (v0-v1).Mag ();
}

inline const float CFloatVector3::Dot (const CFloatVector3& v0, const CFloatVector3& v1) {
	return v0.v.coord.x * v1.v.coord.x + v0.v.coord.y * v1.v.coord.y + v0.v.coord.z * v1.v.coord.z;
}

inline const float CFloatVector3::Normalize (CFloatVector3& vec) {
	float m = vec.Mag ();
	if (m)
		vec /= m;
	return m;
}

inline CFloatVector3& CFloatVector3::Perp (CFloatVector3& dest, const CFloatVector3& p0, const CFloatVector3& p1, const CFloatVector3& p2) {
	return Cross (dest, p1 - p0, p2 - p1);
}

inline const CFloatVector3 CFloatVector3::Perp (const CFloatVector3& p0, const CFloatVector3& p1, const CFloatVector3& p2) {
	return Cross (p1 - p0, p2 - p1);
}

inline const CFloatVector3 CFloatVector3::Normal (const CFloatVector3& p0, const CFloatVector3& p1, const CFloatVector3& p2) {
	CFloatVector3 vec = Perp (p0, p1, p2);
	Normalize (vec);
	return vec;
}

inline const CFloatVector3 CFloatVector3::Reflect (const CFloatVector3& d, const CFloatVector3& n) {
	return n * (Dot (d, n) * -2.0f) + d;
}

// -----------------------------------------------------------------------------
// CFloatVector3 member inlines

//inline float& CFloatVector3::operator[] (size_t i) { return vec [i]; }

//inline const float CFloatVector3::operator[] (size_t i) const { return vec [i]; }

inline bool CFloatVector3::IsZero (void) const { return (v.coord.x == 0.0f) && (v.coord.y == 0.0f) && (v.coord.z == 0.0f); }

inline void CFloatVector3::SetZero (void) { memset (&v, 0, sizeof (v)); }

inline void CFloatVector3::Set (const float f0, const float f1, const float f2) {
	v.coord.x = f0; v.coord.y = f1; v.coord.z = f2;
}

inline void CFloatVector3::Set (const float *vec) {
	v.coord.x = vec [0]; v.coord.y = vec [1]; v.coord.z = vec [2];
}

inline CFloatVector3& CFloatVector3::Scale (CFloatVector3& scale) {
	v.coord.x *= scale.v.coord.x, v.coord.y *= scale.v.coord.y, v.coord.z *= scale.v.coord.z;
	return *this;
	}

inline CFloatVector3& CFloatVector3::Neg (void) {
	v.coord.x = -v.coord.x, v.coord.y = -v.coord.y, v.coord.z = -v.coord.z;
	return *this;
	}

inline const float CFloatVector3::SqrMag (void) const {
	return v.coord.x * v.coord.x + v.coord.y * v.coord.y + v.coord.z * v.coord.z;
}

inline const float CFloatVector3::Mag (void) const {
	return (const float) sqrt (SqrMag ());
}

inline CFloatVector3& CFloatVector3::Assign (const CFloatVector3& other) {
	v = other.v;
	return *this;
}

inline CFloatVector3& CFloatVector3::Assign (const CFloatVector& other) {
	v.coord.x = other.v.coord.x, v.coord.y = other.v.coord.y, v.coord.z = other.v.coord.z;
	return *this;
}

inline CFloatVector3& CFloatVector3::Assign (const CFixVector& other) {
	v.coord.x = X2F (other.v.coord.x), v.coord.y = X2F (other.v.coord.y), v.coord.z = X2F (other.v.coord.z);
	return *this;
}

inline const bool CFloatVector3::operator== (const CFloatVector3& other) {
	return v.coord.x == other.v.coord.x && v.coord.y == other.v.coord.y && v.coord.z == other.v.coord.z;
}

inline const bool CFloatVector3::operator!= (const CFloatVector3& other) {
	return v.coord.x != other.v.coord.x || v.coord.y != other.v.coord.y || v.coord.z != other.v.coord.z;
}

inline const CFloatVector3 CFloatVector3::operator- (void) const {
	CFloatVector3 vec;
	vec.Set (-v.coord.x, -v.coord.y, -v.coord.z);
	return vec;
}

inline const CFloatVector3& CFloatVector3::operator+= (const CFloatVector3& other) {
	v.coord.x += other.v.coord.x; v.coord.y += other.v.coord.y; v.coord.z += other.v.coord.z;
	return *this;
}

inline const CFloatVector3& CFloatVector3::operator-= (const CFloatVector3& other) {
	v.coord.x -= other.v.coord.x; v.coord.y -= other.v.coord.y; v.coord.z -= other.v.coord.z;
	return *this;
}

inline const CFloatVector3& CFloatVector3::operator*= (const CFloatVector3& other) {
	v.coord.x *= other.v.coord.x; v.coord.y *= other.v.coord.y; v.coord.z *= other.v.coord.z;
	return *this;
}

inline const CFloatVector3& CFloatVector3::operator*= (const float s) {
	v.coord.x *= s; v.coord.y *= s; v.coord.z *= s;
	return *this;
}

inline const CFloatVector3& CFloatVector3::operator/= (const float s) {
	v.coord.x /= s; v.coord.y /= s; v.coord.z /= s;
	return *this;
}

inline const CFloatVector3 CFloatVector3::operator+ (const CFloatVector3& other) const {
	CFloatVector3 vec;
	vec.Set (v.coord.x + other.v.coord.x, v.coord.y + other.v.coord.y, v.coord.z + other.v.coord.z);
	return vec;
}

inline const CFloatVector3 CFloatVector3::operator- (const CFloatVector3& other) const {
	CFloatVector3 vec;
	vec.Set (v.coord.x - other.v.coord.x, v.coord.y - other.v.coord.y, v.coord.z - other.v.coord.z);
	return vec;
}


// -----------------------------------------------------------------------------
// CFloatVector3-related non-member ops

inline const float CFloatVector3::operator* (const CFloatVector3& other) const {
	return v.coord.x * other.v.coord.x + v.coord.y * other.v.coord.y + v.coord.z * other.v.coord.z;
}

inline const CFloatVector3 CFloatVector3::operator* (float s) const {
	CFloatVector3 vec;
	vec.Set (v.coord.x * s, v.coord.y * s, v.coord.z * s);
	return vec;
}

//inline const CFloatVector3 operator* (float s, const CFloatVector3& v) {
//	CFloatVector3 vec;
//	vec.Set (v.v.coord.x * s, v.v.coord.y * s, v.v.coord.z * s);
//	return vec;
//}

inline const CFloatVector3 CFloatVector3::operator/ (float s) const {
	CFloatVector3 vec;
	vec.Set (v.coord.x / s, v.coord.y / s, v.coord.z / s);
	return vec;
}

// -----------------------------------------------------------------------------
// -----------------------------------------------------------------------------
// -----------------------------------------------------------------------------
// CFixVector static inlines

inline const CFixVector CFixVector::Create (fix f0, fix f1, fix f2) {
	CFixVector vec;
	vec.Set (f0, f1, f2);
	return vec;
}

inline const CFixVector CFixVector::Avg (const CFixVector& src0, const CFixVector& src1) {
	CFixVector vec;
	vec.Set ((src0.v.coord.x + src1.v.coord.x) / 2, (src0.v.coord.y + src1.v.coord.y) / 2, (src0.v.coord.z + src1.v.coord.z) / 2);
	return vec;
}

inline const CFixVector CFixVector::Avg (CFixVector& src0, CFixVector& src1, CFixVector& src2, CFixVector& src3) {
	CFixVector vec;
	vec.Set ((src0.v.coord.x + src1.v.coord.x + src2.v.coord.x + src3.v.coord.x) / 4,
			   (src0.v.coord.y + src1.v.coord.y + src2.v.coord.y + src3.v.coord.y) / 4,
				(src0.v.coord.z + src1.v.coord.z + src2.v.coord.z + src3.v.coord.z) / 4);
	return vec;
}

//computes the delta angle between two vectors.
//vectors need not be normalized. if they are, call CFixVector::DeltaAngleNorm ()
//the forward vector (third parameter) can be NULL, in which case the absolute
//value of the angle in returned.  Otherwise the angle around that vector is
//returned.
inline const fixang CFixVector::DeltaAngle (const CFixVector& v0, const CFixVector& v1, CFixVector *fVec) {
	CFixVector t0 = v0, t1 = v1;

CFixVector::Normalize (t0);
CFixVector::Normalize (t1);
return DeltaAngleNorm (t0, t1, fVec);
}

//computes the delta angle between two normalized vectors.
inline const fixang CFixVector::DeltaAngleNorm (const CFixVector& v0, const CFixVector& v1, CFixVector *fVec) {
	fixang a = FixACos (CFixVector::Dot (v0, v1));

if (fVec) {
	CFixVector t = CFixVector::Cross (v0, v1);
	if (CFixVector::Dot (t, *fVec) < 0)
		a = -a;
	}
return a;
}

inline const fix CFixVector::Dist (const CFixVector& vec0, const CFixVector& vec1) {
	return (vec0-vec1).Mag ();
}

inline const fix CFixVector::DistQuick (const CFixVector& vec0, const CFixVector& vec1) {
	return (vec0-vec1).MagQuick ();
}

inline const fix CFixVector::Dot (const CFixVector& v0, const CFixVector& v1) {
	return v0 * v1;
	//return fix ((double (v0.v.coord.x) * double (v1.v.coord.x) + double (v0.v.coord.y) * double (v1.v.coord.y) + double (v0.v.coord.z) * double (v1.v.coord.z)) / 65536.0);
}

inline const fix CFixVector::Dot (const fix x, const fix y, const fix z, const CFixVector& v) {
	#if 1
	return CFixVector::Dot (CFixVector::Create(x, y, z), v);
	#else
	return fix ((double (x) * double (v.v.coord.x) + double (y) * double (v.v.coord.y) + double (z) * double (v.v.coord.z)) / 65536.0);
	#endif
}

inline const fix CFixVector::Normalize (CFixVector& v) {
fix m = v.Mag ();
if (!m)
	v.v.coord.x = v.v.coord.y = v.v.coord.z = 0;
else {
	v.v.coord.x = FixDiv (v.v.coord.x, m);
	v.v.coord.y = FixDiv (v.v.coord.y, m);
	v.v.coord.z = FixDiv (v.v.coord.z, m);
	}
return m;
}

inline const fix CFixVector::NormalizeQuick (CFixVector& v) {
fix m = v.MagQuick ();
if (!m)
	v.v.coord.x = v.v.coord.y = v.v.coord.z = 0;
else {
	v.v.coord.x = FixDiv (v.v.coord.x, m);
	v.v.coord.y = FixDiv (v.v.coord.y, m);
	v.v.coord.z = FixDiv (v.v.coord.z, m);
	}
return m;
}

inline CFixVector& CFixVector::Perp (CFixVector& dest, const CFixVector& p0, const CFixVector& p1, const CFixVector& p2) {
	CFixVector t0 = p1 - p0, t1 = p2 - p1;
#if 0
	Normalize (t0);
	Normalize (t1);
#else
	t0.Check ();
	t1.Check ();
#endif
	return Cross (dest, t0, t1);
}

inline const CFixVector CFixVector::Perp (const CFixVector& p0, const CFixVector& p1, const CFixVector& p2) {
	CFixVector t0 = p1 - p0, t1 = p2 - p1;
#if 0
	Normalize (t0);
	Normalize (t1);
#else
	t0.Check ();
	t1.Check ();
#endif
	return Cross (t0, t1);
}

inline const CFixVector CFixVector::Normal (const CFixVector& p0, const CFixVector& p1, const CFixVector& p2) {
	CFixVector vec = Perp (p0, p1, p2);
	Normalize (vec);
	return vec;
}

inline const CFixVector CFixVector::Reflect (const CFixVector& d, const CFixVector& n) {
	fix k = Dot (d, n) * 2;
	CFixVector r = n;
	r *= k;
	r -= d;
	r.Neg ();
	return r;
}

//return the normalized direction vector between two points
//dest = normalized (end - start).  Returns Mag of direction vector
//NOTE: the order of the parameters m.matches the vector subtraction
inline const fix CFixVector::NormalizedDir (CFixVector& dest, const CFixVector& end, const CFixVector& start) {
	dest = end - start;
	return CFixVector::Normalize (dest);
}

inline const fix CFixVector::NormalizedDirQuick (CFixVector& dest, const CFixVector& end, const CFixVector& start) {
	dest = end - start;
	return CFixVector::NormalizeQuick (dest);
}

// -----------------------------------------------------------------------------
// CFixVector member inlines

//inline fix& CFixVector::operator[] (size_t i) { return vec [i]; }

//inline const fix CFixVector::operator[] (size_t i) const { return vec [i]; }

inline CFixVector& CFixVector::Assign (const CFloatVector3& other)
{
v.coord.x = F2X (other.v.coord.x), v.coord.y = F2X (other.v.coord.y), v.coord.z = F2X (other.v.coord.z);
return *this;
}

inline CFixVector& CFixVector::Assign (const CFloatVector& other)
{
v.coord.x = F2X (other.v.coord.x), v.coord.y = F2X (other.v.coord.y), v.coord.z = F2X (other.v.coord.z);
return *this;
}

inline CFixVector& CFixVector::Assign (const CFixVector& other)
{
v.coord.x = other.v.coord.x, v.coord.y = other.v.coord.y, v.coord.z = other.v.coord.z;
return *this;
}

inline bool CFixVector::operator== (const CFixVector& other) const
{
return (v.coord.x == other.v.coord.x) && (v.coord.y == other.v.coord.y) && (v.coord.z == other.v.coord.z);
}

__attribute__((always_inline)) inline const CFixVector& CFixVector::Set (fix x, fix y, fix z)
{
v.coord.x = x; v.coord.y = y; v.coord.z = z;
return *this;
}

inline void CFixVector::Set (const fix *vec)
{
v.coord.x = vec [0]; v.coord.y = vec [1]; v.coord.z = vec [2];
}

inline bool CFixVector::IsZero (void) const
{
return !(v.coord.x || v.coord.y || v.coord.z);
}

inline void CFixVector::SetZero (void)
{
memset (&v, 0, sizeof (v));
}

inline const int32_t CFixVector::Sign (void) const
{
return (v.coord.x * v.coord.y * v.coord.z < 0) ? -1 : 1;
}

inline fix CFixVector::SqrMag (void) const
{
return FixMul (v.coord.x, v.coord.x) + FixMul (v.coord.y, v.coord.y) + FixMul (v.coord.z, v.coord.z);
}

inline float CFixVector::Sqr (float f) const { return f * f; }

inline CFixVector& CFixVector::Scale (CFixVector& scale)
{
v.coord.x = FixMul (v.coord.x, scale.v.coord.x);
v.coord.y = FixMul (v.coord.y, scale.v.coord.y);
v.coord.z = FixMul (v.coord.z, scale.v.coord.z);
return *this;
}

inline const CFixVector CFixVector::MulInt (const int n) const
{
	CFixVector vec;
	vec.Set (v.coord.x * n, v.coord.y * n, v.coord.z * n);
	return vec;
}

inline const CFixVector CFixVector::DivInt (const int n) const
{
	CFixVector vec;
	vec.Set (v.coord.x / n, v.coord.y / n, v.coord.z / n);
	return vec;
}

inline CFixVector& CFixVector::Neg (void)
{
v.coord.x = -v.coord.x, v.coord.y = -v.coord.y, v.coord.z = -v.coord.z;
return *this;
}

inline const CFixVector CFixVector::operator- (void) const
{
	CFixVector vec;
	vec.Set (-v.coord.x, -v.coord.y, -v.coord.z);
	return vec;
}

inline const bool CFixVector::operator== (const CFixVector& other)
{
return (v.coord.x == other.v.coord.x) && (v.coord.y == other.v.coord.y) && (v.coord.z == other.v.coord.z);
}

inline const bool CFixVector::operator!= (const CFixVector& other)
{
return (v.coord.x != other.v.coord.x) || (v.coord.y != other.v.coord.y) || (v.coord.z != other.v.coord.z);
}

inline const bool CFixVector::operator< (const CFixVector& other)
{
return (v.coord.x < other.v.coord.x) && (v.coord.y < other.v.coord.y) && (v.coord.z < other.v.coord.z);
}

inline const bool CFixVector::operator<= (const CFixVector& other)
{
return (v.coord.x <= other.v.coord.x) && (v.coord.y <= other.v.coord.y) && (v.coord.z <= other.v.coord.z);
}

inline const bool CFixVector::operator> (const CFixVector& other)
{
return (v.coord.x > other.v.coord.x) && (v.coord.y > other.v.coord.y) && (v.coord.z > other.v.coord.z);
}

inline const bool CFixVector::operator>= (const CFixVector& other)
{
return (v.coord.x >= other.v.coord.x) && (v.coord.y >= other.v.coord.y) && (v.coord.z >= other.v.coord.z);
}

inline const CFixVector& CFixVector::operator+= (const CFixVector& other)
{
v.coord.x += other.v.coord.x;
v.coord.y += other.v.coord.y;
v.coord.z += other.v.coord.z;
return *this;
}

inline const CFixVector& CFixVector::operator+= (const CFloatVector& other)
{
v.coord.x += F2X (other.v.coord.x);
v.coord.y += F2X (other.v.coord.y);
v.coord.z += F2X (other.v.coord.z);
return *this;
}

__attribute__((always_inline)) inline const CFixVector& CFixVector::operator-= (const CFixVector& other) {
	v.coord.x -= other.v.coord.x;
	v.coord.y -= other.v.coord.y;
	v.coord.z -= other.v.coord.z;
	return *this;
}

inline const CFixVector& CFixVector::operator-= (const CFloatVector& other)
{
v.coord.x -= F2X (other.v.coord.x);
v.coord.y -= F2X (other.v.coord.y);
v.coord.z -= F2X (other.v.coord.z);
return *this;
}

inline const CFixVector& CFixVector::operator*= (const fix s)
{
v.coord.x = FixMul (v.coord.x, s);
v.coord.y = FixMul (v.coord.y, s);
v.coord.z = FixMul (v.coord.z, s);
return *this;
}

inline const CFixVector& CFixVector::operator*= (const float s)
{
v.coord.x = fix (v.coord.x * s);
v.coord.y = fix (v.coord.y * s);
v.coord.z = fix (v.coord.z * s);
return *this;
}

inline const CFixVector& CFixVector::operator*= (const CFixVector& other)
{
v.coord.x = FixMul (v.coord.x, other.v.coord.x);
v.coord.y = FixMul (v.coord.y, other.v.coord.y);
v.coord.z = FixMul (v.coord.z, other.v.coord.z);
return *this;
}

inline const CFixVector& CFixVector::operator/= (const fix s)
{
v.coord.x = FixDiv (v.coord.x, s);
v.coord.y = FixDiv (v.coord.y, s);
v.coord.z = FixDiv (v.coord.z, s);
return *this;
}

inline const CFixVector CFixVector::operator+ (const CFixVector& other) const {
	CFixVector vec;
	vec.Set (v.coord.x + other.v.coord.x, v.coord.y + other.v.coord.y, v.coord.z + other.v.coord.z);
	return vec;
}

inline const CFixVector CFixVector::operator+ (const CFloatVector& other) const
{
	CFixVector vec;
	vec.Set (v.coord.x + F2X (other.v.coord.x), v.coord.y + F2X (other.v.coord.y), v.coord.z + F2X (other.v.coord.z));
	return vec;
}

inline const CFixVector CFixVector::operator- (const CFixVector& other) const
{
	CFixVector vec;
	vec.Set (v.coord.x - other.v.coord.x, v.coord.y - other.v.coord.y, v.coord.z - other.v.coord.z);
	return vec;
}

inline const CFixVector CFixVector::operator- (const CFloatVector& other) const
{
	CFixVector vec;
	vec.Set (v.coord.x - F2X (other.v.coord.x), v.coord.y - F2X (other.v.coord.y), v.coord.z - F2X (other.v.coord.z));
	return vec;
}


// compute intersection of a line through a point a, with the line being orthogonal relative
// to the plane given by the Normal n and a point p lieing in the plane, and store it in i.
inline const CFixVector CFixVector::PlaneProjection (const CFixVector& n, const CFixVector& p) const
{
	CFixVector i;

double l = double (-CFixVector::Dot (n, p)) / double (CFixVector::Dot (n, *this));
i.v.coord.x = fix (l * double (v.coord.x));
i.v.coord.y = fix (l * double (v.coord.y));
i.v.coord.z = fix (l * double (v.coord.z));
return i;
}

//compute the distance from a point to a plane.  takes the normalized Normal
//of the plane (ebx), a point on the plane (edi), and the point to check (esi).
//returns distance in eax
//distance is signed, so Negative Dist is on the back of the plane
inline const fix CFixVector::DistToPlane (const CFixVector& n, const CFixVector& p) const
{
#if 0
CFixVector t = *this;
t -= p;
return CFixVector::Dot (t, n);
#else
return CFixVector::Dot (v.coord.x - p.v.coord.x, v.coord.y - p.v.coord.y, v.coord.z - p.v.coord.z, n);
#endif
}

inline const void CFixVector::Scale2 (const fix num, const fix den)
{
v.coord.x = FixMulDiv (v.coord.x, num, den);
v.coord.y = FixMulDiv (v.coord.y, num, den);
v.coord.z = FixMulDiv (v.coord.z, num, den);
}

//extract heading and pitch from a vector, assuming bank==0
inline const CAngleVector CFixVector::ToAnglesVecNorm (void) const
{
	CAngleVector a;

a.v.coord.b = 0;		//always zero bank
a.v.coord.p = FixASin (-v.coord.y);
a.v.coord.h = (v.coord.x || v.coord.z) ? FixAtan2 (v.coord.z, v.coord.x) : 0;
return a;
}

//extract heading and pitch from a vector, assuming bank==0
inline const CAngleVector CFixVector::ToAnglesVec (void) const
{
	CFixVector t = *this;

CFixVector::Normalize (t);
return t.ToAnglesVecNorm ();
}


// -----------------------------------------------------------------------------
// CFixVector-related non-member ops

inline const fix CFixVector::operator* (const CFixVector& other) const {
	//return fix ((double (v.coord.x) * double (other.v.coord.x) + double (v.coord.y) * double (other.v.coord.y) + double (v.coord.z) * double (other.v.coord.z)) / 65536.0);
	#ifdef MATH64
	tQuadInt q = {0};
	#else
	tQuadInt q = {0, 0};
	#endif
	FixMulAccum (&q, v.coord.x, other.v.coord.x);
	FixMulAccum (&q, v.coord.y, other.v.coord.y);
	FixMulAccum (&q, v.coord.z, other.v.coord.z);
	return FixQuadAdjust(&q);
}

inline const CFixVector CFixVector::operator* (const fix s) const {
	CFixVector vec;
	vec.Set (FixMul (v.coord.x, s), FixMul (v.coord.y, s), FixMul (v.coord.z, s));
	return vec;
}

//static inline const CFixVector operator* (const fix s, const CFixVector& v) {
//	CFixVector vec;
//	vec.Set (FixMul (v.v.coord.x, s), FixMul (v.v.coord.y, s), FixMul (v.v.coord.z, s));
//	return vec;
//}

inline const CFixVector CFixVector::operator/ (const fix d) const {
	CFixVector vec;
	vec.Set (FixDiv (v.coord.x, d), FixDiv (v.coord.y, d), FixDiv (v.coord.z, d));
	return vec;
}

// -----------------------------------------------------------------------------
// -----------------------------------------------------------------------------
// -----------------------------------------------------------------------------

/**
 * \class __pack__ CFixMatrix
 *
 * A 3x3 rotation m.matrix.  Sorry about the numbering starting with one. Ordering
 * is across then down, so <m1,m2,m3> is the first row.
 */
typedef union tFixMatrixData {
	struct {
		CFixVector	r, u, f;
	} dir;
	CFixVector	mat [3];
	fix			vec [9];
} __pack__ tFixMatrixData;


class CFixMatrix {
	friend class CFloatMatrix;
#if 1
	public:
		tFixMatrixData	m;
#else
	private:
		tFixMatrixData	mat;
#endif
	public:
		static const CFixMatrix IDENTITY;
		static const CFixMatrix Create (const CFixVector& r, const CFixVector& u, const CFixVector& f);
		static const CFixMatrix Create (fix sinp, fix cosp, fix sinb, fix cosb, fix sinh, fix cosh);
		//computes a m.matrix from a Set of three angles.  returns ptr to m.matrix
		static const CFixMatrix Create (const CAngleVector& a);
		//computes a m.matrix from a forward vector and an angle
		static const CFixMatrix Create (CFixVector *v, fixang a);
		static const CFixMatrix CreateF (const CFixVector& fVec);
		static const CFixMatrix CreateFU (const CFixVector& fVec, const CFixVector& uVec);
		static const CFixMatrix CreateFR (const CFixVector& fVec, const CFixVector& rVec);

		static CFixMatrix& Invert (CFixMatrix& m);
		static CFixMatrix& Transpose (CFixMatrix& m);
		static CFixMatrix& Transpose (CFixMatrix& dest, CFixMatrix& source);
		static CFloatMatrix& Transpose (CFloatMatrix& dest, CFixMatrix& src);

		const CFixMatrix Mul (const CFixMatrix& m) const;
		CFixMatrix& Scale (CFixVector& scale);
		const CFixVector operator* (const CFixVector& v) const;
		const CFixMatrix operator* (const CFixMatrix& m) const;

		const fix Det (void);
		const CFixMatrix Inverse (void);
		const CFixMatrix Transpose (void);

		//make sure m.matrix is orthogonal
		void CheckAndFix (void);

		//extract angles from a m.matrix
		const CAngleVector ComputeAngles (void) const;

		const CFixMatrix& Assign (CFixMatrix& other);
		const CFixMatrix& Assign (CFloatMatrix& other);
#if 0
		fix& operator[] (size_t i);
		const fix operator[] (size_t i) const;

		inline const CFixVector Mat (size_t i) const { return m.mat [i]; }
		inline const CFixVector R (void) const { return m.dir.r; }
		inline const CFixVector U (void) const { return m.dir.u; }
		inline const CFixVector F (void) const { return m.dir.f; }

		inline CFixVector& Mat (size_t i) { return m.mat [i]; }
		inline fix* Vec (void) { return m.vec; }
		inline CFixVector& R (void) { return m.dir.r; }
		inline CFixVector& U (void) { return m.dir.u; }
		inline CFixVector& F (void) { return m.dir.f; }
#endif

	private:
		static inline void Swap (fix& l, fix& r) {
			fix t = l;
			l = r;
			r = t;
			}
};



// -----------------------------------------------------------------------------
// CFixMatrix static inlines

inline const CFixMatrix CFixMatrix::Create (const CFixVector& r, const CFixVector& u, const CFixVector& f)
{
	CFixMatrix m;

m.m.dir.r = r;
m.m.dir.u = u;
m.m.dir.f = f;
return m;
}

inline const CFixMatrix CFixMatrix::Create (fix sinp, fix cosp, fix sinb, fix cosb, fix sinh, fix cosh)
{
	CFixMatrix m;
	fix sbsh, cbch, cbsh, sbch;

sbsh = FixMul (sinb, sinh);
cbch = FixMul (cosb, cosh);
cbsh = FixMul (cosb, sinh);
sbch = FixMul (sinb, cosh);
m.m.dir.r.v.coord.x = cbch + FixMul (sinp, sbsh);		//m1
m.m.dir.u.v.coord.z = sbsh + FixMul (sinp, cbch);		//m8
m.m.dir.u.v.coord.x = FixMul (sinp, cbsh) - sbch;		//m2
m.m.dir.r.v.coord.z = FixMul (sinp, sbch) - cbsh;		//m7
m.m.dir.f.v.coord.x = FixMul (sinh, cosp);			//m3
m.m.dir.r.v.coord.y = FixMul (sinb, cosp);			//m4
m.m.dir.u.v.coord.y = FixMul (cosb, cosp);			//m5
m.m.dir.f.v.coord.z = FixMul (cosh, cosp);			//m9
m.m.dir.f.v.coord.y = -sinp;							//m6
return m;
}

//computes a m.matrix from a Set of three angles.  returns ptr to m.matrix
inline const CFixMatrix CFixMatrix::Create (const CAngleVector& a)
{
	fix sinp, cosp, sinb, cosb, sinh, cosh;

FixSinCos (a.v.coord.p, &sinp, &cosp);
FixSinCos (a.v.coord.b, &sinb, &cosb);
FixSinCos (a.v.coord.h, &sinh, &cosh);
return Create (sinp, cosp, sinb, cosb, sinh, cosh);
}

//computes a m.matrix from a forward vector and an angle
inline const CFixMatrix CFixMatrix::Create (CFixVector *v, fixang a)
{
	fix sinb, cosb, sinp, cosp;

FixSinCos (a, &sinb, &cosb);
sinp = - v->v.coord.x;
cosp = FixSqrt (I2X (1) - FixMul (sinp, sinp));
return Create (sinp, cosp, sinb, cosb, FixDiv (v->v.coord.x, cosp), FixDiv (v->v.coord.z, cosp));
}


inline CFixMatrix& CFixMatrix::Invert (CFixMatrix& m)
{
	// TODO implement?
	return m;
}

inline CFixMatrix& CFixMatrix::Transpose (CFixMatrix& m)
{
Swap (m.m.vec [1], m.m.vec [3]);
Swap (m.m.vec [2], m.m.vec [6]);
Swap (m.m.vec [5], m.m.vec [7]);
return m;
}

// -----------------------------------------------------------------------------
// CFixMatrix member ops

//inline fix& CFixMatrix::operator[] (size_t i) { return m.vec [i]; }

//inline const fix CFixMatrix::operator[] (size_t i) const { return m.vec [i]; }

inline const CFixVector CFixMatrix::operator* (const CFixVector& v) const
{
	CFixVector vec;
	vec.Set (CFixVector::Dot (v, m.dir.r), CFixVector::Dot (v, m.dir.u), CFixVector::Dot (v, m.dir.f));
	return vec;
}

inline const CFixMatrix CFixMatrix::operator* (const CFixMatrix& other) const { return Mul (other); }

inline CFixMatrix& CFixMatrix::Scale (CFixVector& scale)
{
m.dir.r *= scale.v.coord.x;
m.dir.u *= scale.v.coord.y;
m.dir.f *= scale.v.coord.z;
return *this;
};

const inline CFixMatrix CFixMatrix::Transpose (void)
{
CFixMatrix dest;
Transpose (dest, *this);
return dest;
}

//make sure this m.matrix is orthogonal
inline void CFixMatrix::CheckAndFix (void)
{
*this = CreateFU (m.dir.f, m.dir.u);
}


/**
 * \class __pack__ CFloatMatrix
 *
 * A 4x4 floating point transformation m.matrix
 */

typedef union tFloatMatrixData {
	struct {
		CFloatVector	r, u, f, h;
	} dir;
	CFloatVector	mat [4];
	float				vec [16];
} tFloatMatrixData;

class CFloatMatrix {
	friend class CFixMatrix;

#if 1
	public:
		tFloatMatrixData	m;
#else
	private:
		tFloatMatrixData	mat;
#endif
	public:
		static const CFloatMatrix IDENTITY;
		static const CFloatMatrix Create (const CFloatVector& r, const CFloatVector& u, const CFloatVector& f, const CFloatVector& w);
		static const CFloatMatrix Create (float sinp, float cosp, float sinb, float cosb, float sinh, float cosh);
		static const CFloatMatrix CreateFU (const CFloatVector& fVec, const CFloatVector& uVec);
		static const CFloatMatrix CreateFR (const CFloatVector& fVec, const CFloatVector& rVec);

		static CFloatMatrix& Invert (CFloatMatrix& m);
		static CFloatMatrix& Transpose (CFloatMatrix& m);
		static CFloatMatrix& Transpose (CFloatMatrix& dest, CFloatMatrix& source);


		const CFloatVector operator* (const CFloatVector& v);
		const CFloatVector3 operator* (const CFloatVector3& v);
		const CFloatMatrix operator* (CFloatMatrix& m);

		const CFloatMatrix Mul (CFloatMatrix& other);
		const CFloatMatrix& Scale (CFloatVector& scale);
		const float Det (void);
		const CFloatMatrix Inverse (void);
		const CFloatMatrix Transpose (void);

		void CheckAndFix (void);

		const CFloatVector ComputeAngles (void) const;

		const CFloatMatrix& Assign (CFixMatrix& other);
		const CFloatMatrix& Assign (CFloatMatrix& other);

		void Flip (void);

		static float* Transpose (float* dest, const CFloatMatrix& src);

		float& operator[] (size_t i);
#if 0
		inline const CFloatVector Mat (size_t i) const { return m.mat [i]; }
		inline const CFloatVector R (void) const { return m.dir.r; }
		inline const CFloatVector U (void) const { return m.dir.u; }
		inline const CFloatVector F (void) const { return m.dir.f; }
		inline float* Vec (void) { return m.vec; }
		inline CFloatVector& R (void) { return m.dir.r; }
		inline CFloatVector& U (void) { return m.dir.u; }
		inline CFloatVector& F (void) { return m.dir.f; }
		inline CFloatVector& H (void) { return m.dir.h; }
#endif

	private:
		static inline void Swap (float& l, float& r) {
			float t = l;
			l = r;
			r = t;
			}
};


// -----------------------------------------------------------------------------
// CFloatMatrix static inlines

inline CFloatMatrix& CFloatMatrix::Invert (CFloatMatrix& m) {
	//TODO: implement?
	return m;
}

inline CFloatMatrix& CFloatMatrix::Transpose (CFloatMatrix& m) {
	Swap (m.m.vec [1], m.m.vec [3]);
	Swap (m.m.vec [2], m.m.vec [6]);
	Swap (m.m.vec [5], m.m.vec [7]);
	return m;
}

// -----------------------------------------------------------------------------
// CFloatMatrix member ops

inline float& CFloatMatrix::operator[] (size_t i) { return m.vec [i]; }

inline const CFloatVector CFloatMatrix::operator* (const CFloatVector& v)
{
	CFloatVector vec;
	vec.Set (CFloatVector::Dot (v, m.dir.r), CFloatVector::Dot (v, m.dir.u), CFloatVector::Dot (v, m.dir.f));
	return vec;
}

inline const CFloatVector3 CFloatMatrix::operator* (const CFloatVector3& v)
{
	CFloatVector3 vec;
	vec.Set (CFloatVector3::Dot (v, *m.dir.r.XYZ ()), CFloatVector3::Dot (v, *m.dir.u.XYZ ()), CFloatVector3::Dot (v, *m.dir.f.XYZ ()));
	return vec;
}

inline const CFloatMatrix CFloatMatrix::Transpose (void)
{
CFloatMatrix dest;
Transpose (dest, *this);
return dest;
}

inline const CFloatMatrix CFloatMatrix::operator* (CFloatMatrix& other) { return Mul (other); }

inline const CFloatMatrix& CFloatMatrix::Scale (CFloatVector& scale)
{
m.dir.r *= scale.v.coord.x;
m.dir.u *= scale.v.coord.y;
m.dir.f *= scale.v.coord.z;
return *this;
};

// -----------------------------------------------------------------------------
// misc conversion member ops

inline const CFloatMatrix& CFloatMatrix::Assign (CFloatMatrix& other)
{
*this = other;
return *this;
}

inline const CFloatMatrix& CFloatMatrix::Assign (CFixMatrix& other)
{
*this = CFloatMatrix::IDENTITY;
m.dir.r.Assign (other.m.dir.r);
m.dir.u.Assign (other.m.dir.u);
m.dir.f.Assign (other.m.dir.f);
return *this;
}

inline const CFixMatrix& CFixMatrix::Assign (CFixMatrix& other)
{
*this = other;
return *this;
}

inline const CFixMatrix& CFixMatrix::Assign (CFloatMatrix& other)
{
m.dir.r.Assign (other.m.dir.r);
m.dir.u.Assign (other.m.dir.u);
m.dir.f.Assign (other.m.dir.f);
return *this;
}

//make sure this m.matrix is orthogonal
inline void CFloatMatrix::CheckAndFix (void) {
	*this = CreateFU (m.dir.f, m.dir.u);
}

//} // VecMat

// -----------------------------------------------------------------------------
// misc remaining C-style funcs

const int32_t FindPointLineIntersection (CFixVector& hitP, const CFixVector& p1, const CFixVector& p2, const CFixVector& p3, int32_t bClampToFarthest);
//const int32_t FindPointLineIntersection (CFixVector& hitP, const CFixVector& p1, const CFixVector& p2, const CFixVector& p3, const CFixVector& vPos, int32_t bClampToFarthest);
const fix VmLinePointDist (const CFixVector& a, const CFixVector& b, const CFixVector& p);
const int32_t FindPointLineIntersection (CFloatVector& hitP, const CFloatVector& p1, const CFloatVector& p2, const CFloatVector& p3, const CFloatVector& vPos, int32_t bClamp);
const int32_t FindPointLineIntersection (CFloatVector& hitP, const CFloatVector& p1, const CFloatVector& p2, const CFloatVector& p3, int32_t bClamp);
const int32_t FindPointLineIntersection (CFloatVector3& hitP, const CFloatVector3& p1, const CFloatVector3& p2, const CFloatVector3& p3, CFloatVector3 *vPos, int32_t bClamp);
const float VmLinePointDist (const CFloatVector& a, const CFloatVector& b, const CFloatVector& p, int32_t bClamp);
const float VmLinePointDist (const CFloatVector3& a, const CFloatVector3& b, const CFloatVector3& p, int32_t bClamp);
const float VmLineLineIntersection (const CFloatVector3& v1, const CFloatVector3& v2, const CFloatVector3& v3, const CFloatVector3& v4, CFloatVector3& va, CFloatVector3& vb);
const float VmLineLineIntersection (const CFloatVector& v1, const CFloatVector& v2, const CFloatVector& v3, const CFloatVector& v4, CFloatVector& va, CFloatVector& vb);

CFloatVector* VmsReflect (CFloatVector *vReflect, CFloatVector *vLight, CFloatVector *vNormal);

float TriangleSize (const CFixVector& p0, const CFixVector& p1, const CFixVector& p2);

// ------------------------------------------------------------------------

#endif //_VECMAT_H
