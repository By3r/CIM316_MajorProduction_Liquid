using System;

namespace Liquid.Audio
{
    public interface INoiseSource
    {
        NoiseLevel CurrentLevel { get; }
        float CurrentIntensity { get; }
        event Action<NoiseLevel, float> NoiseChanged;
    }
}