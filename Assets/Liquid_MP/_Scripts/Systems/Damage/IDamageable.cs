namespace Liquid.Damage
{
    /// <summary>
    /// Implement on anything that can receive damage: enemies, breakable objects, barrels, etc.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(DamageInfo damageInfo);
        bool IsDead { get; }
    }
}
