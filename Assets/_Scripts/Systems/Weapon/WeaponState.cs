namespace _Scripts.Systems.Weapon
{
    /// <summary>
    /// States a weapon can be in. Drives Animator parameters and input gating.
    /// </summary>
    public enum WeaponState
    {
        /// <summary>Not equipped / viewmodel disabled.</summary>
        Inactive,

        /// <summary>Playing draw animation.</summary>
        Drawing,

        /// <summary>Ready to fire or swing.</summary>
        Idle,

        /// <summary>Ranged: firing animation playing.</summary>
        Firing,

        /// <summary>Ranged: reload animation playing.</summary>
        Reloading,

        /// <summary>Melee: attack animation playing.</summary>
        MeleeSwing,

        /// <summary>Playing holster animation, will go Inactive when done.</summary>
        Holstering,

        /// <summary>Aiming down sights. Can fire/reload from this state.</summary>
        Aiming
    }
}
