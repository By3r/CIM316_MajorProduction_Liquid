using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central spawn pool manager.
/// </summary>
public class SpawnPoolManager : MonoBehaviour
{
    #region Singleton 
    //TODO: Maybe remove the singleton since technically it only exists in game??
    public static SpawnPoolManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        InitialisePools();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    #endregion

    #region Inspector.
    [Header("Prefabs")]
    [SerializeField] private GameObject crawlerPrefab;
    [SerializeField] private GameObject liquidPrefab;
    [SerializeField] private GameObject lurkerPrefab;
    [SerializeField] private GameObject suffocatorPrefab;

    [Header("Crawler")]
    [Tooltip("Total Crawlers maintained in the pool.")]
    [SerializeField] private int crawlerPoolSize = 20;
    [Tooltip("Cooldown after full wipe before respawning all Crawlers (seconds).")]
    [SerializeField] private float crawlerFullWipeCooldown = 60f;

    [Header("Lurker")]
    [Tooltip("Cooldown after death before Lurker can respawn (seconds). " +
             "Shortened by low noise.")]
    [SerializeField] private float lurkerDeathCooldown = 60f;
    [Tooltip("Cooldown after disappear before Lurker reappears (seconds). " +
             "Shorter than death cooldown.")]
    [SerializeField] private float lurkerDisappearCooldown = 8f;
    [Tooltip("Noise loudness below which cooldowns are shortened.")]
    [Range(0f, 1f)]
    [SerializeField] private float lurkerLowNoiseLoudness = 0.15f;
    [Tooltip("Multiplier applied to Lurker cooldowns when noise is low (never 0).")]
    [Range(0.01f, 1f)]
    [SerializeField] private float lurkerNoiseCooldownMult = 0.5f;

    [Header("Suffocator")]
    [Tooltip("Cooldown after a Suffocator dies before it respawns at its pond.")]
    [SerializeField] private float suffocatorRespawnCooldown = 15f;

    [Header("Player Reference")]
    [SerializeField] private Transform playerTransform;

    #endregion

    #region Pools

    private readonly List<CrawlerEnemy> _crawlerPool = new List<CrawlerEnemy>(20);
    private LiquidEnemy _liquidInstance;
    private LurkerEnemy _lurkerInstance;
    private readonly List<SuffocatorEnemy> _suffocatorPool = new List<SuffocatorEnemy>();

    private readonly List<SpawnPoint> _spawnPoints = new List<SpawnPoint>();

    #endregion

    #region Crawler state

    private int _crawlerDeployedCount;
    private float _crawlerWipeCooldownEnd = -1f;
    private bool _crawlerWipeInProgress;

    #endregion

    #region Lurker state

    private float _lurkerAvailableAt = 0f;
    private bool _lurkerDead;

    #endregion

    #region Suffocator state

    private readonly List<SuffocatorPond> _ponds = new List<SuffocatorPond>();
    private readonly List<PendingRespawn> _pendingRespawns = new List<PendingRespawn>();

    private struct PendingRespawn
    {
        public EnemyType Type;
        public float ReadyAt;
        public SuffocatorPond Pond;
    }

    #endregion

    #region Unity lifecycle

    private void Update()
    {
        TickCrawlerRespawn();
        TickLurkerRespawn();
        TickSuffocatorRespawns();
    }

    #endregion

    #region Initialisation

    private void InitialisePools()
    {
        PrewarmCrawlers();
        PrewarmLiquid();
        PrewarmLurker();
    }

    private void PrewarmCrawlers()
    {
        if (crawlerPrefab == null) return;

        for (int i = 0; i < crawlerPoolSize; i++)
        {
            CrawlerEnemy c = InstantiatePooled<CrawlerEnemy>(crawlerPrefab);
            _crawlerPool.Add(c);
        }
    }

    private void PrewarmLiquid()
    {
        if (liquidPrefab == null) return;
        _liquidInstance = InstantiatePooled<LiquidEnemy>(liquidPrefab);
    }

    private void PrewarmLurker()
    {
        if (lurkerPrefab == null) return;
        _lurkerInstance = InstantiatePooled<LurkerEnemy>(lurkerPrefab);
    }

    private T InstantiatePooled<T>(GameObject prefab) where T : MonoBehaviour
    {
        GameObject go = Instantiate(prefab, Vector3.zero, Quaternion.identity, transform);
        go.SetActive(false);
        T component = go.GetComponent<T>();
        return component;
    }

    #endregion

    #region Spawn point registration

    public void RegisterSpawnPoint(SpawnPoint point)
    {
        if (!_spawnPoints.Contains(point))
            _spawnPoints.Add(point);
    }

    public void UnregisterSpawnPoint(SpawnPoint point)
    {
        _spawnPoints.Remove(point);
    }

    #endregion

    #region Scene initialisation.
    /// <summary>
    /// To be called after a scene finishes loading. Scans all registered
    /// SpawnPoints and spawns the initial enemy population.
    /// </summary>
    public void InitialiseScene()
    {
        SpawnInitialCrawlers();
        SpawnInitialLurker();
        SpawnAllPonds();
    }

    #endregion

    #region Crawler.
    private void SpawnInitialCrawlers()
    {
        List<SpawnPoint> valid = GetAvailablePoints(EnemyType.Crawler, crawlerPoolSize);

        int deployed = 0;
        for (int i = 0; i < valid.Count && deployed < crawlerPoolSize; i++)
        {
            CrawlerEnemy crawler = GetInactiveCrawler();
            if (crawler == null) break;

            PlaceEnemy(crawler.gameObject, valid[i]);
            deployed++;
        }

        _crawlerDeployedCount = deployed;
        _crawlerWipeInProgress = false;
    }

    private void TickCrawlerRespawn()
    {
        if (!_crawlerWipeInProgress) return;
        if (Time.time < _crawlerWipeCooldownEnd) return;

        _crawlerWipeInProgress = false;
        SpawnInitialCrawlers();
    }

    /// <summary>
    /// Called by a CrawlerEnemy when it dies (via ReturnToPool).
    /// Checks whether a full wipe has occurred.
    /// </summary>
    public void NotifyCrawlerDied(CrawlerEnemy crawler)
    {
        crawler.gameObject.SetActive(false);

        int alive = CountActive(_crawlerPool);

        if (alive == 0 && !_crawlerWipeInProgress)
        {
            _crawlerWipeInProgress = true;
            _crawlerWipeCooldownEnd = Time.time + crawlerFullWipeCooldown;
        }
    }

    private CrawlerEnemy GetInactiveCrawler()
    {
        for (int i = 0; i < _crawlerPool.Count; i++)
        {
            if (_crawlerPool[i] != null && !_crawlerPool[i].gameObject.activeSelf)
                return _crawlerPool[i];
        }
        return null;
    }

    #endregion

    #region Liquid.
    /// <summary>
    /// Called by the noise system when extreme noise is emitted.
    /// Re-enables the Liquid at the nearest valid spawn point.
    /// </summary>
    public void NotifyExtremeNoise(Vector3 noisePosition)
    {
        if (_liquidInstance == null) return;
        if (_liquidInstance.gameObject.activeSelf) return;

        SpawnPoint point = GetBestPoint(EnemyType.Liquid, noisePosition);
        if (point == null) return;

        PlaceEnemy(_liquidInstance.gameObject, point);
    }

    #endregion

    #region Lurker.
    private void SpawnInitialLurker()
    {
        if (_lurkerInstance == null) return;

        SpawnPoint point = GetBestPoint(EnemyType.Lurker, PlayerPosition());
        if (point == null) return;

        PlaceEnemy(_lurkerInstance.gameObject, point);
        _lurkerDead = false;
    }

    private void TickLurkerRespawn()
    {
        if (_lurkerInstance == null) return;
        if (_lurkerInstance.gameObject.activeSelf) return;
        if (Time.time < _lurkerAvailableAt) return;

        SpawnPoint point = GetBestPoint(EnemyType.Lurker, PlayerPosition());
        if (point == null) return;

        PlaceEnemy(_lurkerInstance.gameObject, point);
        _lurkerDead = false;
    }

    /// <summary>
    /// Called by LurkerEnemy.ReturnToPool. Uses perceived loudness to
    /// shorten the cooldown when noise is low.
    /// </summary>
    public void NotifyLurkerDied(float lastPerceivedLoudness)
    {
        _lurkerDead = true;
        _lurkerInstance.gameObject.SetActive(false);

        float cd = lurkerDeathCooldown;
        if (lastPerceivedLoudness < lurkerLowNoiseLoudness)
            cd *= Mathf.Max(0.01f, lurkerNoiseCooldownMult);

        _lurkerAvailableAt = Time.time + cd;
    }

    /// <summary>
    /// Called when the Lurker disappears (not death, just hiding expiry).
    /// Shorter cooldown than death.
    /// </summary>
    public void NotifyLurkerDisappeared(float lastPerceivedLoudness)
    {
        float cd = lurkerDisappearCooldown;
        if (lastPerceivedLoudness < lurkerLowNoiseLoudness)
            cd *= Mathf.Max(0.01f, lurkerNoiseCooldownMult);

        _lurkerAvailableAt = Time.time + cd;
    }

    #endregion

    #region Suffocator.

    private void SpawnAllPonds()
    {
        List<SpawnPoint> pondPoints = GetPondPoints();

        for (int i = 0; i < pondPoints.Count; i++)
        {
            SpawnPoint point = pondPoints[i];
            if (point.SuffocatorPondPrefab == null)
            {
                Debug.LogWarning($"SpawnPoint '{point.name}' is marked as pond spawn but has no pond prefab assigned.", point);
                continue;
            }

            SpawnPond(point);
        }
    }

    private void SpawnPond(SpawnPoint point)
    {
        GameObject pondGo = Instantiate(
            point.SuffocatorPondPrefab,
            point.transform.position,
            point.transform.rotation,
            transform);

        SuffocatorPond pond = pondGo.GetComponent<SuffocatorPond>();
        if (pond == null)
        {
            Debug.LogWarning($"Pond prefab at '{point.name}' has no SuffocatorPond component.", pondGo);
            Destroy(pondGo);
            return;
        }

        _ponds.Add(pond);

        List<SuffocatorEnemy> group = new List<SuffocatorEnemy>(pond.GroupSize);

        for (int i = 0; i < pond.GroupSize; i++)
        {
            SuffocatorEnemy s = SpawnSuffocatorForPond(pond, point);
            if (s != null) group.Add(s);
        }

        pond.Initialise(point, group);
        point.MarkUsed();
    }

    private SuffocatorEnemy SpawnSuffocatorForPond(SuffocatorPond pond, SpawnPoint point)
    {
        if (suffocatorPrefab == null) return null;

        GameObject go = Instantiate(
            suffocatorPrefab,
            point.transform.position + Random.insideUnitSphere * 1.5f,
            Quaternion.identity,
            transform);

        SuffocatorEnemy s = go.GetComponent<SuffocatorEnemy>();
        if (s == null) { Destroy(go); return null; }

        WireSuffocatorToPond(s, pond);
        _suffocatorPool.Add(s);
        return s;
    }

    private void WireSuffocatorToPond(SuffocatorEnemy s, SuffocatorPond pond)
    {
        s.SetPond(pond.PondTransform);
        if (pond.ExtractionSite != null)
            s.SetExtractionSite(pond.ExtractionSite);
    }

    private void TickSuffocatorRespawns()
    {
        for (int i = _pendingRespawns.Count - 1; i >= 0; i--)
        {
            PendingRespawn pending = _pendingRespawns[i];

            if (Time.time < pending.ReadyAt) continue;
            if (pending.Pond == null || pending.Pond.IsDead)
            {
                _pendingRespawns.RemoveAt(i);
                continue;
            }

            SuffocatorEnemy s = GetInactiveSuffocatorForPond(pending.Pond);
            if (s == null)
            {
                s = SpawnSuffocatorForPond(pending.Pond, GetPondSpawnPoint(pending.Pond));
            }

            if (s != null)
            {
                s.transform.position = pending.Pond.PondTransform.position;
                s.gameObject.SetActive(true);
                pending.Pond.RegisterEnemy(s);
            }

            _pendingRespawns.RemoveAt(i);
        }
    }

    public void NotifySuffocatorDied(SuffocatorEnemy suffocator, SuffocatorPond pond)
    {
        suffocator.gameObject.SetActive(false);

        if (pond == null || pond.IsDead) return;

        pond.UnregisterEnemy(suffocator);

        _pendingRespawns.Add(new PendingRespawn
        {
            Type = EnemyType.Suffocator,
            ReadyAt = Time.time + suffocatorRespawnCooldown,
            Pond = pond,
        });
    }

    private SuffocatorEnemy GetInactiveSuffocatorForPond(SuffocatorPond pond)
    {
        for (int i = 0; i < _suffocatorPool.Count; i++)
        {
            SuffocatorEnemy s = _suffocatorPool[i];
            if (s == null || s.gameObject.activeSelf) continue;
            if (s.OwnedByPond(pond)) return s;
        }
        return null;
    }

    private SpawnPoint GetPondSpawnPoint(SuffocatorPond pond)
    {
        for (int i = 0; i < _spawnPoints.Count; i++)
        {
            SpawnPoint sp = _spawnPoints[i];
            if (!sp.IsPondSpawn || sp.PermanentlyBlocked) continue;
            if (Vector3.Distance(sp.transform.position, pond.transform.position) < 1f)
                return sp;
        }
        return null;
    }

    private List<SpawnPoint> GetPondPoints()
    {
        List<SpawnPoint> result = new List<SpawnPoint>();
        for (int i = 0; i < _spawnPoints.Count; i++)
        {
            if (_spawnPoints[i].IsPondSpawn && !_spawnPoints[i].PermanentlyBlocked)
                result.Add(_spawnPoints[i]);
        }
        return result;
    }

    #region Spawn point selection

    private List<SpawnPoint> GetAvailablePoints(EnemyType type, int maxCount)
    {
        Vector3 playerPos = PlayerPosition();
        List<SpawnPoint> result = new List<SpawnPoint>();

        for (int i = 0; i < _spawnPoints.Count; i++)
        {
            SpawnPoint sp = _spawnPoints[i];
            if (sp.IsPondSpawn) continue;
            if (!sp.Permits(type)) continue;
            if (!sp.IsAvailable(playerPos)) continue;
            result.Add(sp);
        }

        ShuffleList(result);

        if (result.Count > maxCount)
            result.RemoveRange(maxCount, result.Count - maxCount);

        return result;
    }

    private SpawnPoint GetBestPoint(EnemyType type, Vector3 preferredPosition)
    {
        Vector3 playerPos = PlayerPosition();
        SpawnPoint best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < _spawnPoints.Count; i++)
        {
            SpawnPoint sp = _spawnPoints[i];
            if (sp.IsPondSpawn) continue;
            if (!sp.Permits(type)) continue;
            if (!sp.IsAvailable(playerPos)) continue;

            float score = Vector3.Distance(sp.transform.position, preferredPosition);
            if (score < bestScore) { bestScore = score; best = sp; }
        }

        return best;
    }

    private void PlaceEnemy(GameObject go, SpawnPoint point)
    {
        go.transform.position = point.transform.position;
        go.transform.rotation = point.transform.rotation;
        go.SetActive(true);
        point.MarkUsed();
    }

    #endregion

    #region Utilities.
    private Vector3 PlayerPosition()
    {
        return playerTransform != null ? playerTransform.position : Vector3.zero;
    }

    private int CountActive<T>(List<T> pool) where T : MonoBehaviour
    {
        int count = 0;
        for (int i = 0; i < pool.Count; i++)
            if (pool[i] != null && pool[i].gameObject.activeSelf) count++;
        return count;
    }

    private void ShuffleList<T>(List<T> list)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    #endregion

    #region Floor transition

    /// <summary>
    /// Disables all active pool-managed enemies and cancels pending respawns.
    /// Ponds are destroyed permanently (their spawn points are blocked).
    /// All pools are left intact and ready for InitialiseScene on the new floor.
    /// </summary>
    public void NotifyFloorTransition()
    {
        for (int i = 0; i < _crawlerPool.Count; i++)
        {
            if (_crawlerPool[i] != null && _crawlerPool[i].gameObject.activeSelf)
                _crawlerPool[i].gameObject.SetActive(false);
        }

        if (_liquidInstance != null && _liquidInstance.gameObject.activeSelf)
            _liquidInstance.gameObject.SetActive(false);

        if (_lurkerInstance != null && _lurkerInstance.gameObject.activeSelf)
            _lurkerInstance.gameObject.SetActive(false);

        for (int i = 0; i < _suffocatorPool.Count; i++)
        {
            if (_suffocatorPool[i] != null && _suffocatorPool[i].gameObject.activeSelf)
                _suffocatorPool[i].gameObject.SetActive(false);
        }

        for (int i = _ponds.Count - 1; i >= 0; i--)
        {
            if (_ponds[i] != null)
                _ponds[i].DestroyPond();
        }
        _ponds.Clear();

        _pendingRespawns.Clear();

        _crawlerWipeInProgress = false;
        _crawlerWipeCooldownEnd = -1f;

        _lurkerDead = false;
        _lurkerAvailableAt = 0f;
    }

    #endregion
}