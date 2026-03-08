/// <summary>
/// All enemy types in the game. Used by SpawnPoint whitelist/blacklist
/// and SpawnPoolManager to route spawns correctly.
/// </summary>
public enum EnemyType
{
    Crawler = 0,
    Liquid = 1,
    Lurker = 2,
    Suffocator = 3,
}