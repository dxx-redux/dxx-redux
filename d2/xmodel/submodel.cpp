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

#include "xdescent.h"
#include "carray.h"
#include "tga.h"
#include "ase.h"
#include "submodel.h"

#define LASER_INDEX 0
#define SUPER_LASER_INDEX 5

int32_t ExcludeSubModel (ASE::CSubModel *pSubModel, int32_t nGunId, int32_t nBombId, int32_t nMissileId, int32_t nMissiles)
{
	//static int32_t bCenterGuns [] = {0, 1, 1, 0, 0, 0, 1, 1, 0, 1};
	if (!pSubModel->m_bRender)
		return 1;
	#if 1
	if (pSubModel->m_bFlare)
		return 1;
	#endif
	if (pSubModel->m_nGunPoint >= 0)
		return 1;
	if (pSubModel->m_nBullets > 0) //if (pSubModel->m_bBullets)
		return 1;
	#if 1
	if (pSubModel->m_bThruster && ((pSubModel->m_bThruster & (REAR_THRUSTER | FRONTAL_THRUSTER)) != (REAR_THRUSTER | FRONTAL_THRUSTER)))
		return 1;
	#endif
	if (pSubModel->m_bHeadlight)
		return 1; //(!HeadlightIsOn (nId))
	if (pSubModel->m_bBombMount)
		return nBombId == 0;
	if (pSubModel->m_bWeapon) {
		//CPlayerData	*pPlayer = gameData.multiplayer.players + nId;
		int32_t		bLasers = (nGunId == LASER_INDEX) || (nGunId == SUPER_LASER_INDEX);
		int32_t		bSuperLasers = 0; //pPlayer->HasSuperLaser ();
		int32_t		bQuadLasers = 0; //gameData.multiplayer.weaponStates [N_LOCALPLAYER].bQuadLasers;
		//int32_t		bCenterGun = bCenterGuns [nGunId];
		int32_t		nWingtip = bQuadLasers ? bSuperLasers : 2; //gameOpts->render.ship.nWingtip;

		//gameOpts->render.ship.nWingtip = nWingtip;
		if (nWingtip == 0)
			nWingtip = bLasers && bSuperLasers && bQuadLasers;
		else if (nWingtip == 1)
			nWingtip = !bLasers || bSuperLasers;

		#if 0
		if (EGI_FLAG (bShowWeapons, 0, 1, 0)) {
			if (pSubModel->m_nGun == nGunId + 1) {
				if (pSubModel->m_nGun == FUSION_INDEX + 1) {
					if ((pSubModel->m_nWeaponPos == 3) && !gameData.multiplayer.weaponStates [N_LOCALPLAYER].bTripleFusion)
						return 1;
					}
				else if (bLasers) {
					if ((pSubModel->m_nWeaponPos > 2) && !bQuadLasers && (nWingtip != bSuperLasers))
						return 1;
					}
				}
			else if (pSubModel->m_nGun == LASER_INDEX + 1) {
				if (nWingtip)
					return 1;
				RETVAL (!bCenterGun && (pSubModel->m_nWeaponPos < 3))
				}
			else if (pSubModel->m_nGun == SUPER_LASER_INDEX + 1) {
				if (nWingtip != 1)
					return 1;
				RETVAL (!bCenterGun && (pSubModel->m_nWeaponPos < 3))
				}
			else if (!pSubModel->m_nGun) {
				if (bLasers && bQuadLasers)
					return 1;
				if (pSubModel->m_nType != gameOpts->render.ship.nWingtip)
					return 1;
				return 0;
				}
			else if (pSubModel->m_nBomb == nBombId)
				RETVAL ((nId == N_LOCALPLAYER) && !AllowedToFireMissile (nId, 0))
			else if (pSubModel->m_nMissile == nMissileId) {
				if (pSubModel->m_nWeaponPos > nMissiles)
					return 1;
				else {
					static int32_t nMslPos [] = {-1, 1, 0, 3, 2};
					int32_t nLaunchPos = gameData.multiplayer.weaponStates [nId].nMslLaunchPos;
					RETVAL ((nId == N_LOCALPLAYER) && !AllowedToFireMissile (nId, 0) && (nLaunchPos == (nMslPos [(int32_t) pSubModel->m_nWeaponPos])))
					}
				}
			else
				return 1;
			}
		else
		#endif
		{
			if (pSubModel->m_nGun == 1)
				return 0;
			if ((pSubModel->m_nGun < 0) && (pSubModel->m_nMissile == 1))
				return 0;
			return 1;
			}
		}
	return 0;
}
