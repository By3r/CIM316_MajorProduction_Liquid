using UnityEngine;

namespace Liquid.Dialogue
{
    [CreateAssetMenu(menuName = "Liquid/Dialogue/Effects/Currency Delta", fileName = "Fx_CurrencyDelta_")]
    public sealed class Effect_CurrencyDelta : DialogueEffect
    {
        [Tooltip("Positive adds currency. Negative attempts to spend currency.")]
        [SerializeField] private int delta;

        public override void Apply(IDialogueContext context)
        {
            if (context?.Currency == null) return;

            if (delta >= 0)
            {
                context.Currency.Add(delta);
                return;
            }

            int spendAmount = -delta;
            context.Currency.Spend(spendAmount);
        }
    }
}