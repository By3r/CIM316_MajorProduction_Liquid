using UnityEngine;

namespace Liquid.Damage
{
    /// <summary>
    /// Carries all context about a single hit: how much damage, where it landed,
    /// who caused it, and what type of damage it is. Passed to IDamageable.TakeDamage().
    /// </summary>
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 HitPoint;
        public Vector3 HitNormal;
        public GameObject Instigator;
        public DamageType Type;
    }
}
