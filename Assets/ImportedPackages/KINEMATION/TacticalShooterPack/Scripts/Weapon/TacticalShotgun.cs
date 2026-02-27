// Copyright (c) 2026 KINEMATION.
// All rights reserved.

using UnityEngine;

namespace KINEMATION.TacticalShooterPack.Scripts.Weapon
{
    [AddComponentMenu("KINEMATION/Tactical Shooter Pack/Tactical Shotgun")]
    public class TacticalShotgun : TacticalShooterWeapon
    {
        protected bool _skipFirstShell;

        public override void Reload()
        {
            if (_activeAmmo == animationData.ammoCapacity) return;

            _skipFirstShell = _activeAmmo > 0;
            
            PlayCharacterWeaponAnimation(_activeAmmo == 0
                ? TacShooterUtility.Animator_ReloadStartEmpty.hash
                : TacShooterUtility.Animator_ReloadStart.hash);
            
            PlaySound(_activeAmmo == 0 ? animationData.reloadEmptySound : animationData.reloadTacSound);
        }

        public override void ReloadWeapon()
        {
            if (!_skipFirstShell) _activeAmmo++;
            _skipFirstShell = false;

            bool isFull = _activeAmmo == animationData.ammoCapacity;
            PlayCharacterWeaponAnimation(isFull ? TacShooterUtility.Animator_ReloadEnd.hash 
                : TacShooterUtility.Animator_ReloadLoop.hash);

            PlaySound(isFull ? animationData.reloadEndSound : animationData.reloadLoopSound);
        }
    }
}