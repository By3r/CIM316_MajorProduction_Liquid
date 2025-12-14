using UnityEngine;

public interface IEnemyDebugTarget
{
    string DebugDisplayName { get; }
    Transform DebugTransform { get; }
    string GetDebugText();
}