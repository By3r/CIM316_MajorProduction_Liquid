namespace Liquid.Audio
{
    public interface INoiseController
    {
        // Hello! Make other scripts call this to raise the player’s noise level.
        // It will never lower the value instantly. There will be decay handles that over time.
        void SetNoiseLevel(NoiseLevel level);
    }
}