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

#include <stdio.h>
#include <stdarg.h>
#include <string.h>
#include <math.h>


#include "xmaths.h"
#include "carray.h"
#include "xdescent.h"
#include "strfunc.h"

#include "tga.h"
#include "ase.h"

static int bCacheModelData = 0;
int nHiresModels;
CArray<ASE::CModel> aseModels[2];

#undef DBG

int IsPlayerShip(int model) {
	(void)model;
	return 0;
}

static char	szLine [1024];
static char	szLineBackup [1024];
static int32_t nLine = 0;
static CFile *aseFile = NULL;
static char *pszToken = NULL;
static int32_t bErrMsg = 0;

#define ASE_ROTATE_MODEL	1
#define ASE_FLIP_TEXCOORD	1

using namespace ASE;

#define MODEL_DATA_VERSION 1010	//must start with something bigger than the biggest model number

//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------

int32_t CModel::Error (const char *pszMsg, ...)
{
char buf[1024];
va_list vp;

if (!bErrMsg) {
	if (pszMsg) {
		snprintf (buf, sizeof(buf) - 1, "%s: error in line %d: ", aseFile->Name (), nLine);
		va_start(vp, pszMsg);
		vsnprintf(buf + strlen(buf), sizeof(buf) - strlen(buf) - 1, pszMsg, vp);
		va_end(vp);
		strcat(buf, "\n");
	} else
		snprintf (buf, sizeof(buf), "%s: error in line %d\n", aseFile->Name (), nLine);
	PrintLog(0, "%s", buf);
	bErrMsg = 1;
	}
return 0;
}

//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------

static float FloatTok (const char *delims)
{
pszToken = strtok (NULL, delims);
if (!(pszToken && *pszToken))
	CModel::Error ("missing data");
return pszToken ? (float) atof (pszToken) : 0;
}

//------------------------------------------------------------------------------

static int32_t IntTok (const char *delims)
{
pszToken = strtok (NULL, delims);
if (!(pszToken && *pszToken))
	CModel::Error ("missing data");
return pszToken ? atoi (pszToken) : 0;
}

//------------------------------------------------------------------------------

static char CharTok (const char *delims)
{
pszToken = strtok (NULL, delims);
if (!(pszToken && *pszToken))
	CModel::Error ("missing data");
return pszToken ? *pszToken : '\0';
}

//------------------------------------------------------------------------------

static char szEmpty [1] = "";

static char *StrTok (const char *delims)
{
pszToken = strtok (NULL, delims);
if (!(pszToken && *pszToken))
	CModel::Error ("missing data");
return pszToken ? pszToken : szEmpty;
}

//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------

static void ReadVector (CFile& cf, CFloatVector3 *pv)
{
#if ASE_ROTATE_MODEL
	float x = FloatTok (" \t");
	float z = -FloatTok (" \t");
	float y = FloatTok (" \t");
	*pv = CFloatVector3::Create(x, y, z);
#else	// need to rotate model for Descent
	int32_t	i;

for (i = 0; i < 3; i++)
	pv [i] = FloatTok (" \t");
#endif
}

//------------------------------------------------------------------------------

#ifndef _MSC_VER
__attribute__((optimize("-O3")))
#endif
static char* ReadLine (CFile& cf)
{
while (!cf.EoF ()) {
	cf.GetS (szLine, sizeof (szLine));
	//fputs(szLine, stdout);
	nLine++;
	strcpy (szLineBackup, szLine);
	strupr8 (szLine);
	if ((pszToken = strtok (szLine, " \t")))
		return pszToken;
	}
return NULL;
}

//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------

int32_t ASE_ReleaseTextures (void)
{
	//CModel*	pModel;
	//int32_t		bCustom, i;

PrintLog (1, "releasing ASE model textures\n");
//for (bCustom = 0; bCustom < 2; bCustom++)
//	for (i = gameData.modelData.nHiresModels, pModel = gameData.modelData.aseModels [bCustom].Buffer (); i; i--, pModel++)
//		pModel->ReleaseTextures ();
PrintLog (-1);
return 0;
}

//------------------------------------------------------------------------------

int32_t ASE_ReloadTextures (void)
{
	CModel*	pModel;
	int32_t		bCustom, i;

PrintLog (1, "reloading ASE model textures\n");
for (bCustom = 0; bCustom < 2; bCustom++)
	for (i = nHiresModels, pModel = aseModels [bCustom].Buffer (); i; i--, pModel++)
		if (!pModel->ReloadTextures ()) {
			PrintLog (-1);
			return 0;
			}
PrintLog (-1);
return 1;
}

//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------

void CSubModel::Init (void)
{
m_next = NULL;
memset (m_szName, 0, sizeof (m_szName));
memset (m_szParent, 0, sizeof (m_szParent));
m_nSubModel = 0;
m_nParent = 0;
m_nBitmap = 0;
m_nFaces = 0;
m_nVerts = 0;
m_nTexCoord = 0;
m_nIndex = 0;
m_bRender = 1;
m_bGlow = 0;
m_bFlare = 0;
m_bBillboard = 0;
m_bThruster = 0;
m_bWeapon = 0;
m_bHeadlight = 0;
m_bBombMount = 0;
m_nGun = -1;
m_nBomb = -1;
m_nMissile = -1;
m_nType = 0;
m_nWeaponPos = 0;
m_nGunPoint = -1;
m_nBullets = -1;
m_bBarrel = 0;
m_vOffset.SetZero ();
}

//------------------------------------------------------------------------------

void CSubModel::Destroy (void)
{
m_faces.Destroy ();
m_vertices.Destroy ();
m_texCoord.Destroy ();
Init ();
}

//------------------------------------------------------------------------------

int32_t CSubModel::ReadNode (CFile& cf)
{
	int32_t	i;

if (CharTok (" \t") != '{')
	return CModel::Error ("syntax error");
while ((pszToken = ReadLine (cf))) {
	if (*pszToken == '}')
		return 1;
	if (!strcmp (pszToken, "*TM_POS")) {
		for (i = 0; i < 3; i++)
			m_vOffset.v.vec [i] = 0; //FloatTok (" \t");
		}
	}
return CModel::Error ("unexpected end of file");
}

//------------------------------------------------------------------------------

int32_t CSubModel::ReadMeshVertexList (CFile& cf)
{
	CVertex*	pv;
	int32_t		i;

if (CharTok (" \t") != '{')
	return CModel::Error ("syntax error");
while ((pszToken = ReadLine (cf))) {
	if (*pszToken == '}') {
		if (m_bBillboard) {
			m_vOffset /= m_nVerts;
			for (i = 0; i < m_nVerts; i++)
				m_vertices [i].m_vertex -= m_vOffset;
			}
		return 1;
		}
	if (!strcmp (pszToken, "*MESH_VERTEX")) {
		if (!m_vertices)
			return CModel::Error ("no vertices found");
		i = IntTok (" \t");
		if ((i < 0) || (i >= m_nVerts))
			return CModel::Error ("invalid vertex number");
		pv = m_vertices + i;
		ReadVector (cf, &pv->m_vertex);
		if (m_bBillboard)
			m_vOffset += pv->m_vertex;
		//pv->m_vertex -= m_vOffset;
		}
	}
return CModel::Error ("unexpected end of file");
}

//------------------------------------------------------------------------------

int32_t CSubModel::ReadMeshFaceList (CFile& cf)
{
	CFace*	pf;
	int32_t	i;

if (CharTok (" \t") != '{')
	return CModel::Error ("syntax error");
while ((pszToken = ReadLine (cf))) {
	if (*pszToken == '}')
		return 1;
	if (!strcmp (pszToken, "*MESH_FACE")) {
		if (!m_faces)
			return CModel::Error ("no faces found");
		i = IntTok (" \t");
		if ((i < 0) || (i >= m_nFaces))
			return CModel::Error ("invalid face number");
		pf = m_faces + i;
		for (i = 0; i < 3; i++) {
			strtok (NULL, " :\t");
			pf->m_nVerts [i] = IntTok (" :\t");
			}
		#if 0
		do {
			pszToken = StrTok (" :\t");
			if (!*pszToken)
				return CModel::Error ("unexpected end of file");
			} while (strcmp (pszToken, "*MESH_MTLID"));
		pf->m_nBitmap = IntTok (" ");
		#endif
		}
	}
return CModel::Error ("unexpected end of file");
}

//------------------------------------------------------------------------------

int32_t CSubModel::ReadVertexTexCoord (CFile& cf)
{
	tTexCoord2f*	pt;
	int32_t			i;

if (CharTok (" \t") != '{')
	return CModel::Error ("syntax error");
while ((pszToken = ReadLine (cf))) {
	if (*pszToken == '}')
		return 1;
	if (!strcmp (pszToken, "*MESH_TVERT")) {
		if (!m_texCoord)
			return CModel::Error ("no texture coordinates found");
		i = IntTok (" \t");
		if ((i < 0) || (i >= m_nTexCoord))
			return CModel::Error ("invalid texture coordinate number");
		pt = m_texCoord + i;
#if ASE_FLIP_TEXCOORD
		pt->v.u = FloatTok (" \t");
		pt->v.v = -FloatTok (" \t");
#else
		for (i = 0; i < 2; i++)
			pt->vec [i] = FloatTok (" \t");
#endif
		}
	}
return CModel::Error ("unexpected end of file");
}

//------------------------------------------------------------------------------

int32_t CSubModel::ReadFaceTexCoord (CFile& cf)
{
	CFace*	pf;
	int32_t	i;

if (CharTok (" \t") != '{')
	return CModel::Error ("syntax error");
while ((pszToken = ReadLine (cf))) {
	if (*pszToken == '}')
		return 1;
	if (!strcmp (pszToken, "*MESH_TFACE")) {
		if (!m_faces)
			return CModel::Error ("no faces found");
		i = IntTok (" \t");
		if ((i < 0) || (i >= m_nFaces))
			return CModel::Error ("invalid face number");
		pf = m_faces + i;
		for (i = 0; i < 3; i++)
			pf->m_nTexCoord [i] = IntTok (" \t");
		}
	}
return CModel::Error ("unexpected end of file");
}

//------------------------------------------------------------------------------

int32_t CSubModel::ReadMeshNormals (CFile& cf)
{
	#if 0
	CFace*	pf;
	CVertex*	pv;
	int32_t		i;
	#endif

if (CharTok (" \t") != '{')
	return CModel::Error ("syntax error");
while ((pszToken = ReadLine (cf))) {
	if (*pszToken == '}')
		return 1;
	#if 0
	if (!strcmp (pszToken, "*MESH_FACENORMAL")) {
		if (!m_faces)
			return CModel::Error ("no faces found");
		i = IntTok (" \t");
		if ((i < 0) || (i >= m_nFaces))
			return CModel::Error ("invalid face number");
		pf = m_faces + i;
		ReadVector (cf, &pf->m_vNormal);
		}
	else if (!strcmp (pszToken, "*MESH_VERTEXNORMAL")) {
		if (!m_vertices)
			return CModel::Error ("no vertices found");
		i = IntTok (" \t");
		if ((i < 0) || (i >= m_nVerts))
			return CModel::Error ("invalid vertex number");
		pv = m_vertices + i;
		ReadVector (cf, &pv->m_normal);
		}
	#endif
	}
return CModel::Error ("unexpected end of file");
}

//------------------------------------------------------------------------------

int32_t CSubModel::ReadMesh (CFile& cf, int32_t& nFaces, int32_t& nVerts)
{
if (CharTok (" \t") != '{')
	return CModel::Error ("syntax error");
while ((pszToken = ReadLine (cf))) {
	if (*pszToken == '}')
		return 1;
	if (!strcmp (pszToken, "*MESH_NUMVERTEX")) {
		if (m_vertices.Buffer ())
			return CModel::Error ("duplicate vertex list");
		m_nVerts = IntTok (" \t");
		if (!m_nVerts)
			return CModel::Error ("no vertices found");
		nVerts += m_nVerts;
		if (!(m_vertices.Create (m_nVerts, "ASE::CSubModel::m_vertices")))
			return CModel::Error ("out of memory");
		m_vertices.Clear ();
		}
	else if (!strcmp (pszToken, "*MESH_NUMTVERTEX")) {
		if (m_texCoord.Buffer ())
			return CModel::Error ("no texture coordinates found");
		m_nTexCoord = IntTok (" \t");
		if (m_nTexCoord) {
			if (!(m_texCoord.Create (m_nTexCoord, "ASE::CSubModel::m_texCoord")))
				return CModel::Error ("out of memory");
			}
		}
	else if (!strcmp (pszToken, "*MESH_NUMFACES")) {
		if (m_faces.Buffer ())
			return CModel::Error ("no faces found");
		m_nFaces = IntTok (" \t");
		if (!m_nFaces)
			return CModel::Error ("no faces specified");
		nFaces += m_nFaces;
		if (!(m_faces.Create (m_nFaces, "ASE::CSubModel::m_faces")))
			return CModel::Error ("out of memory");
		m_faces.Clear ();
		}
	else if (!strcmp (pszToken, "*MESH_VERTEX_LIST")) {
		if (!ReadMeshVertexList (cf))
			return CModel::Error (NULL);
		}
	else if (!strcmp (pszToken, "*MESH_FACE_LIST")) {
		if (!ReadMeshFaceList (cf))
			return CModel::Error (NULL);
		}
	else if (!strcmp (pszToken, "*MESH_NORMALS")) {
		if (!ReadMeshNormals (cf))
			return CModel::Error (NULL);
		}
	else if (!strcmp (pszToken, "*MESH_TVERTLIST")) {
		if (!ReadVertexTexCoord (cf))
			return CModel::Error (NULL);
		}
	else if (!strcmp (pszToken, "*MESH_TFACELIST")) {
		if (!ReadFaceTexCoord (cf))
			return CModel::Error (NULL);
		}
	}
return CModel::Error ("unexpected end of file");
}

//------------------------------------------------------------------------------

int32_t CSubModel::Read (CFile& cf, int32_t& nFaces, int32_t& nVerts)
{
while ((pszToken = ReadLine (cf))) {
	if (*pszToken == '}')
		return 1;
	if (!strcmp (pszToken, "*NODE_NAME")) {
		strcpy (m_szName, StrTok (" \t\""));
		if (strstr (m_szName, "GLOW") != NULL)
			m_bGlow = 1;
		if (strstr (m_szName, "FLARE") != NULL) {
			m_bGlow =
			m_bFlare =
			m_bBillboard = 1;
			}
		if (strstr (m_szName, "$GUNPNT"))
			m_nGunPoint = atoi (m_szName + 8);
		if (strstr (m_szName, "$BOMBMOUNT"))
			m_bBombMount = 1;
		else if (strstr (m_szName, "$BULLETS"))
			m_nBullets = 1;
		else if (strstr (m_szName, "$DUMMY") != NULL)
			m_bRender = 0;
		else if (strstr (m_szName, "$THRUSTER-") != NULL) {
			if (m_szName [10] == 'R') // rear
				m_bThruster |= REAR_THRUSTER;
			else if (m_szName [10] == 'F') // front
				m_bThruster |= FRONT_THRUSTER;
			if (m_szName [11] == 'L') // left
				m_bThruster |= LEFT_THRUSTER;
			else if (m_szName [11] == 'R') // right
				m_bThruster |= RIGHT_THRUSTER;
			if (m_szName [12] == 'T') // top
				m_bThruster |= TOP_THRUSTER;
			else if (m_szName [12] == 'B') // bottom
				m_bThruster |= BOTTOM_THRUSTER;
			if (!m_bThruster) // stay compatible with older models
				m_bThruster = REAR_THRUSTER;
			if (m_bThruster < 3)
				m_bThruster |= FRONTAL_THRUSTER;
			else
				m_bThruster |= LATERAL_THRUSTER;
			}
		else if (strstr (m_szName, "$THRUSTER") != NULL)
			m_bThruster = REAR_THRUSTER;
		else if (strstr (m_szName, "$WINGTIP") != NULL) {
			m_bWeapon = 1;
			m_nGun = 0;
			m_nBomb =
			m_nMissile = -1;
			m_nType = atoi (m_szName + 8) + 1;
			}
		else if (strstr (m_szName, "$GUN") != NULL) {
			m_bWeapon = 1;
			m_nGun = atoi (m_szName + 4) + 1;
			m_nWeaponPos = atoi (m_szName + 6) + 1;
			m_nBomb =
			m_nMissile = -1;
			}
		else if (strstr (m_szName, "$BARREL") != NULL) {
			m_bWeapon = 1;
			m_nGun = atoi (m_szName + 7) + 1;
			m_nWeaponPos = atoi (m_szName + 9) + 1;
			m_nBomb =
			m_nMissile = -1;
			m_bBarrel = 1;
			}
		else if (strstr (m_szName, "$MISSILE") != NULL) {
			m_bWeapon = 1;
			m_nMissile = atoi (m_szName + 8) + 1;
			m_nWeaponPos = atoi (m_szName + 10) + 1;
			m_nGun =
			m_nBomb = -1;
			}
		else if (strstr (m_szName, "$BOMB") != NULL) {
			m_bWeapon = 1;
			m_nBomb = atoi (m_szName + 6) + 1;
			m_nGun =
			m_nMissile = -1;
			}
		else if (strstr (m_szName, "HEADLIGHT") != NULL) {
			m_bHeadlight = 1;
			m_bGlow = 1;
			m_bFlare = 0;
			}
		}
	else if (!strcmp (pszToken, "*NODE_PARENT")) {
		strcpy (m_szParent, StrTok (" \t\""));
		}
	if (!strcmp (pszToken, "*NODE_TM")) {
		if (!ReadNode (cf))
			return CModel::Error (NULL);
		}
	else if (!strcmp (pszToken, "*MESH")) {
		if (!ReadMesh (cf, nFaces, nVerts))
			return CModel::Error (NULL);
		}
	else if (!strcmp (pszToken, "*MATERIAL_REF")) {
		m_nBitmap = IntTok (" \t");
		}
	}
return CModel::Error ("unexpected end of file");
}

//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------
//------------------------------------------------------------------------------

void CModel::Init (void)
{
m_subModels = NULL;
m_nModel = -1;
m_nSubModels = 0;
m_nVerts = 0;
m_nFaces = 0;
}

//------------------------------------------------------------------------------

void CModel::Destroy (void)
{
	CSubModel	*psmi, *psmj;

for (psmi = m_subModels; psmi; ) {
	psmj = psmi;
	psmi = psmi->m_next;
	delete psmj;
	}
FreeTextures ();
Init ();
}

//------------------------------------------------------------------------------

int32_t CModel::ReleaseTextures (void)
{
m_textures.Release ();
return 0;
}

//------------------------------------------------------------------------------

int32_t CModel::ReloadTextures (void)
{
return m_textures.Bind (m_bCustom);
}

//------------------------------------------------------------------------------

int32_t CModel::FreeTextures (void)
{
m_textures.Destroy ();
return 0;
}
//------------------------------------------------------------------------------

int32_t CModel::ReadTexture (CFile& cf, int32_t nBitmap)
{
	CBitmap	*pBm = m_textures.m_bitmaps + nBitmap;
	char		fn [FILENAME_LEN], *ps;
	int32_t		l;

//sprintf (pBm->Name (), "ASE model %d texture %d", m_nModel, nBitmap);
if (CharTok (" \t") != '{')
	return CModel::Error ("syntax error");
pBm->SetFlat (0);
while ((pszToken = ReadLine (cf))) {
	if (*pszToken == '}')
		return 1;
	if (!strcmp (pszToken, "*BITMAP")) {
		if (pBm->Buffer ())	//duplicate
			return CModel::Error ("duplicate bitmap");
		CFile::SplitPath (StrTok ("\""), NULL, fn, NULL);
		CTGA tga (pBm);
		if (!tga.ReadModelTexture (::strlwr (fn), m_bCustom))
			return CModel::Error ("texture not found: %s", fn);
		l = (int32_t) strlen (fn) + 1;
		char szLabel [40];
		sprintf (szLabel, "ASE::CSubModel::m_textures.m_names [%d]", nBitmap);
		if (!m_textures.m_names [nBitmap].Create (l, szLabel))
			return CModel::Error ("out of memory");
		memcpy (m_textures.m_names [nBitmap].Buffer (), fn, l);
		if ((ps = strstr (fn, "color")))
			m_textures.m_nTeam [nBitmap] = atoi (ps + 5) + 1;
		else
			m_textures.m_nTeam [nBitmap] = 0;
		pBm->SetTeam (m_textures.m_nTeam [nBitmap]);
		}
	}
return CModel::Error ("unexpected end of file");
}

//------------------------------------------------------------------------------

int32_t CModel::ReadOpacity (CFile& cf, int32_t nBitmap)
{
	CBitmap	*pBm = m_textures.m_bitmaps + nBitmap;

if (CharTok (" \t") != '{')
	return CModel::Error ("syntax error");
pBm->SetFlat (0);
while ((pszToken = ReadLine (cf))) {
	if (*pszToken == '}')
		return 1;
	if (!strcmp (pszToken, "*BITMAP")) {
		if (!pBm->Buffer ())	//duplicate
			return CModel::Error ("missing glow bitmap");
		}
	}
return CModel::Error ("unexpected end of file");
}

//------------------------------------------------------------------------------

int32_t CModel::ReadMaterial (CFile& cf)
{
	int32_t		i;
	CBitmap	*pBm;

i = IntTok (" \t");
if ((i < 0) || (i >= m_textures.m_nBitmaps))
	return CModel::Error ("invalid bitmap number");
if (CharTok (" \t") != '{')
	return CModel::Error ("syntax error");
pBm = m_textures.m_bitmaps + i;
pBm->SetFlat (1);
while ((pszToken = ReadLine (cf))) {
	if (*pszToken == '}')
		return 1;
	if (!strcmp (pszToken, "*MATERAL_DIFFUSE")) {
		//CRGBColor	avgRGB;
		//avgRGB.Red () = (uint8_t) FRound (FloatTok (" \t") * 255);
		//avgRGB.Green () = (uint8_t) FRound (FloatTok (" \t") * 255);
		//avgRGB.Blue () = (uint8_t) FRound (FloatTok (" \t") * 255);
		//pBm->SetAvgColor (avgRGB);
		}
	else if (!strcmp (pszToken, "*MAP_DIFFUSE")) {
		if (!ReadTexture (cf, i))
			return CModel::Error (NULL);
		}
	else if (!strcmp (pszToken, "*MAP_OPACITY")) {
		if (!ReadOpacity (cf, i))
			return CModel::Error (NULL);
		}
	}
return CModel::Error ("unexpected end of file");
}

//------------------------------------------------------------------------------

int32_t CModel::ReadMaterialList (CFile& cf)
{
if (CharTok (" \t") != '{')
	return CModel::Error ("syntax error");
if (!(pszToken = ReadLine (cf)))
	return CModel::Error ("unexpected end of file");
if (strcmp (pszToken, "*MATERIAL_COUNT"))
	return CModel::Error ("material count missing");
int32_t nBitmaps = IntTok (" \t");
if (!nBitmaps)
	return CModel::Error ("no bitmaps specified");
if (!(m_textures.Create (nBitmaps)))
	return CModel::Error ("out of memory");
while ((pszToken = ReadLine (cf))) {
	if (*pszToken == '}') {
		if (!nBitmaps)
			return 1;
		return CModel::Error ("bitmaps missing");
		}
	if (!strcmp (pszToken, "*MATERIAL")) {
		if (!ReadMaterial (cf))
			return CModel::Error (NULL);
		nBitmaps--;
		}
	}
return CModel::Error ("unexpected end of file");
}

//------------------------------------------------------------------------------

int32_t CModel::ReadSubModel (CFile& cf)
{
	CSubModel	*psm;

if (CharTok (" \t") != '{')
	return CModel::Error ("syntax error");
if (!(psm = NEW CSubModel))
	return CModel::Error ("out of memory");
psm->m_nSubModel = m_nSubModels++;
psm->m_next = m_subModels;
m_subModels = psm;
return psm->Read (cf, m_nFaces, m_nVerts);
}

//------------------------------------------------------------------------------

int32_t CModel::FindSubModel (const char* pszName)
{
	CSubModel *psm;

for (psm = m_subModels; psm; psm = psm->m_next)
	if (!strcmp (psm->m_szName, pszName))
		return psm->m_nSubModel;
return -1;
}

//------------------------------------------------------------------------------

void CModel::LinkSubModels (void)
{
	CSubModel	*psm;

for (psm = m_subModels; psm; psm = psm->m_next)
	psm->m_nParent = FindSubModel (psm->m_szParent);
}

//------------------------------------------------------------------------------

int32_t CModel::Read (const char* filename, int16_t nModel, int32_t bCustom)
{
#if DBG
if (nModel == nDbgModel)
	BRP;
#endif

	CFile		cf;
	int32_t		nResult = 1;

if (m_nModel >= 0)
	return 0;

if (bCacheModelData) {
	Destroy ();
	try {
		if ((IsPlayerShip (nModel) || (nModel == COCKPIT_MODEL))
			 ? ReadBinary (filename, bCustom, cf.Date (filename, gameFolders.var.szModels [bCustom], 0))
			 : ReadBinary (nModel, bCustom, cf.Date (filename, gameFolders.var.szModels [bCustom], 0)))
			return 1;
		}
	catch(...) {
		PrintLog (0, "Compiled model file 'model%03d.bin' is damaged and will be replaced\n", nModel);
		}
	}

if (bCustom
	 ? !cf.Open (filename, gameFolders.mods.szModels [bCustom - 1], "rb", 0)
	 : !cf.Open (filename, gameFolders.game.szModels, "rb", 0))
	return 0;

Destroy ();
bErrMsg = 0;
aseFile = &cf;
Init ();
m_nModel = nModel;
m_bCustom = bCustom;
#if DBG
if (nModel == nDbgModel)
	BRP;
nLine = 0;
#endif
while ((pszToken = ReadLine (cf))) {
	if (!strcmp (pszToken, "*MATERIAL_LIST")) {
		if (!(nResult = ReadMaterialList (cf)))
			break;
		}
	else if (!strcmp (pszToken, "*GEOMOBJECT")) {
		if (!(nResult = ReadSubModel (cf)))
			break;
		}
	}
cf.Close ();
if (!nResult)
	Destroy ();
else {
	LinkSubModels ();
	//gameData.modelData.bHaveHiresModel [uint32_t (this - gameData.modelData.aseModels [bCustom != 0].Buffer ())] = 1;
	if (bCacheModelData) {
		//if (IsPlayerShip (nModel))
		//	SaveBinary (filename);
		//else
			SaveBinary ();
		}
	}
aseFile = NULL;
return nResult;
}

//------------------------------------------------------------------------------

int32_t CSubModel::SaveBinary (CFile& cf)
{
#if DBG
if (!strcmp (m_szName, "$WINGTIP2-0"))
	BRP;
#endif
cf.Write (m_szName, 1, sizeof (m_szName));
cf.Write (m_szParent, 1, sizeof (m_szParent));
cf.WriteShort (m_nSubModel);
cf.WriteShort (m_nParent);
cf.WriteShort (m_nBitmap);
cf.WriteShort (m_nFaces);
cf.WriteShort (m_nVerts);
cf.WriteShort (m_nTexCoord);
cf.WriteShort (m_nIndex);
cf.WriteByte (int8_t (m_bRender));
cf.WriteByte (int8_t (m_bGlow));
cf.WriteByte (int8_t (m_bFlare));
cf.WriteByte (int8_t (m_bBillboard));
cf.WriteByte (int8_t (m_bThruster));
cf.WriteByte (int8_t (m_bWeapon));
cf.WriteByte (int8_t (m_bHeadlight));
cf.WriteByte (int8_t (m_bBombMount));
cf.WriteByte (m_nGun);
cf.WriteByte (m_nBomb);
cf.WriteByte (m_nMissile);
cf.WriteByte (m_nType);
cf.WriteByte (m_nWeaponPos);
cf.WriteByte (m_nGunPoint);
cf.WriteByte (m_nBullets);
cf.WriteByte (m_bBarrel);
cf.WriteVector (m_vOffset);
m_faces.Write (cf);
m_vertices.Write (cf);
m_texCoord.Write (cf);
return 1;
}

//------------------------------------------------------------------------------

int32_t CModel::SaveBinary (void)
{
	char		szFilename [FILENAME_LEN];

sprintf (szFilename, "model%03d.bin", m_nModel);
return SaveBinary (szFilename);
}

//------------------------------------------------------------------------------

int32_t CModel::SaveBinary (const char* szFilename)
{
if (!*gameFolders.var.szModels [m_bCustom]) {
	return 0;
	}

	CFile		cf;
	char		szBin [FILENAME_LEN];

strcpy (szBin, szFilename);
strcpy (strrchr (szBin, '.'), ".bin");

if (m_bCustom == 2)
	CFile::MkDir (gameFolders.var.szModels [m_bCustom]);
if (!cf.Open (szBin, gameFolders.var.szModels [m_bCustom], "wb", 0))
	return 0;
cf.WriteInt (MODEL_DATA_VERSION);
cf.WriteInt (m_nModel);
cf.WriteInt (m_nSubModels);
cf.WriteInt (m_nVerts);
cf.WriteInt (m_nFaces);
cf.WriteInt (m_bCustom);
cf.WriteInt (m_textures.m_nBitmaps);

int32_t h, i;

for (i = 0; i < m_textures.m_nBitmaps; i++) {
	h = int32_t (m_textures.m_names [i].Length ());
	cf.WriteInt (h);
	cf.Write (m_textures.m_names [i].Buffer (), 1, h);
	}
cf.Write (m_textures.m_nTeam.Buffer (), 1, m_textures.m_nBitmaps);

CSubModel *pSubModel = m_subModels;
for (i = 0; i < m_nSubModels; i++, pSubModel = pSubModel->m_next)
	pSubModel->SaveBinary (cf);
cf.Close ();
return 1;
}

//------------------------------------------------------------------------------

int32_t CSubModel::ReadBinary (CFile& cf)
{
m_next = NULL;
cf.Read (m_szName, 1, sizeof (m_szName));
cf.Read (m_szParent, 1, sizeof (m_szParent));
m_nSubModel = cf.ReadShort ();
m_nParent = cf.ReadShort ();
m_nBitmap = cf.ReadShort ();
m_nFaces = cf.ReadShort ();
m_nVerts = cf.ReadShort ();
m_nTexCoord = cf.ReadShort ();
m_nIndex = cf.ReadShort ();
m_bRender = uint8_t (cf.ReadByte ());
m_bGlow = uint8_t (cf.ReadByte ());
m_bFlare = uint8_t (cf.ReadByte ());
m_bBillboard = uint8_t (cf.ReadByte ());
m_bThruster = uint8_t (cf.ReadByte ());
m_bWeapon = uint8_t (cf.ReadByte ());
m_bHeadlight = uint8_t (cf.ReadByte ());
m_bBombMount = uint8_t (cf.ReadByte ());
m_nGun = cf.ReadByte ();
m_nBomb = cf.ReadByte ();
m_nMissile = cf.ReadByte ();
m_nType = cf.ReadByte ();
m_nWeaponPos = cf.ReadByte ();
m_nGunPoint = cf.ReadByte ();
m_nBullets = cf.ReadByte ();
m_bBarrel = cf.ReadByte ();
cf.ReadVector (m_vOffset);
if ((m_nFaces > 100000) || (m_nVerts > 100000) || (m_nTexCoord > 100000))	//probably invalid
	return 0;
if ((m_nFaces && !m_faces.Create (m_nFaces, "ASE::CSubModel::m_faces")) ||
	 (m_nVerts && !m_vertices.Create (m_nVerts, "ASE::CSubModel::m_vertices")) ||
	(m_nTexCoord && !m_texCoord.Create (m_nTexCoord, "ASE::CSubModel::m_texCoord")))
	return 0;
m_faces.Read (cf);
m_vertices.Read (cf);
m_texCoord.Read (cf);
return 1;
}

//------------------------------------------------------------------------------

int32_t CModel::ReadBinary (int16_t nModel, int32_t bCustom, time_t tASE)
{
	char		szFilename [FILENAME_LEN];

sprintf (szFilename, "model%03d.bin", nModel);
return ReadBinary (szFilename, bCustom, tASE);
}

//------------------------------------------------------------------------------

int32_t CModel::ReadBinary (const char* szFilename, int32_t bCustom, time_t tASE)
{
if (!(szFilename && *szFilename)) {
	return 0;
	}

	CFile		cf;
	int32_t	h, i;
	char		szBin [FILENAME_LEN];

strcpy (szBin, szFilename);
strcpy (strrchr (szBin, '.'), ".bin");

time_t tBIN = cf.Date (szBin, gameFolders.var.szModels [bCustom], 0);

if ((tBIN < 0) || (tASE > tBIN))
	return 0;

if (!cf.Open (szBin, gameFolders.var.szModels [bCustom], "rb", 0))
	return 0;
h = cf.ReadInt ();
if (h != MODEL_DATA_VERSION) {
	cf.Close ();
	Destroy ();
	return 0;
	}
m_nModel = cf.ReadInt ();
m_nSubModels = cf.ReadInt ();
m_nVerts = cf.ReadInt ();
m_nFaces = cf.ReadInt ();
m_bCustom = cf.ReadInt ();

m_subModels = NULL;
m_textures.m_nBitmaps = cf.ReadInt ();
if (m_textures.m_nBitmaps > 100) {	//probably invalid
	cf.Close ();
	Destroy ();
	return 0;
	}

if (!(m_textures.m_bitmaps.Create (m_textures.m_nBitmaps, "ASE::CModel::m_textures.m_bitmaps") &&
	   m_textures.m_names.Create (m_textures.m_nBitmaps, "ASE::CModel::m_textures.m_names") &&
		m_textures.m_nTeam.Create (m_textures.m_nBitmaps, "ASE::CModel::m_textures.m_nTeam"))) {
	cf.Close ();
	Destroy ();
	return 0;
	}

for (i = 0; i < m_textures.m_nBitmaps; i++) {
	if ((h = cf.ReadInt ())) {
		char szLabel [40];
		sprintf (szLabel, "ASE::CModel::m_textures.m_names [%d]", i);
		if (!m_textures.m_names [i].Create (h, szLabel)) {
			cf.Close ();
			Destroy ();
			return 0;
			}
		m_textures.m_names [i].Read (cf);
		CTGA tga (m_textures.m_bitmaps + i);
		if (!tga.ReadModelTexture (m_textures.m_names [i].Buffer (), m_bCustom)) {
			cf.Close ();
			Destroy ();
			return 0;
			}
		}
	}
m_textures.m_nTeam.Read (cf);

for (i = 0; i < m_textures.m_nBitmaps; i++)
	m_textures.m_bitmaps [i].SetTeam (m_textures.m_nTeam [i]);

CSubModel*	pSubModel, * pTail = NULL;

m_subModels = NULL;
for (i = 0; i < m_nSubModels; i++) {
	if (!(pSubModel = NEW CSubModel)) {
		cf.Close ();
		Destroy ();
		return 0;
		}
	if (m_subModels)
		pTail->m_next = pSubModel;
	else
		m_subModels = pSubModel;
	pTail = pSubModel;
	try {
		if (!pSubModel->ReadBinary (cf)) {
			cf.Close ();
			Destroy ();
			return 0;
			}
		}
	catch(...) {
		PrintLog (0, "Compiled model file 'model%03d.bin' is damaged and will be replaced\n", m_nModel);
		cf.Close ();
		Destroy ();
		return 0;
		}
	}

return 1;
}

//------------------------------------------------------------------------------
//eof
