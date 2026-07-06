using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class WaveSpawnEntry
{
    public Enemy enemyPrefab;
    public Transform spawnPoint;
    public float delayAfterSpawn = 1f;
}

[System.Serializable]
public class WaveDefinition
{
    public string waveName = "Wave";
    public List<WaveSpawnEntry> entries = new List<WaveSpawnEntry>();
    public bool overrideInterWaveDelay;
    public float interWaveDelay = 3f;
}

public class WaveSpawner : MonoBehaviour
{
    [Header("Wave Setup")]
    public List<WaveDefinition> waves = new List<WaveDefinition>();
    public bool autoStart = true;
    public float defaultInterWaveDelay = 3f;

    [Header("Debug")]
    public bool logWaveEvents = true;

    private int currentWaveIndex = -1;
    private int aliveEnemyCount;
    private bool isSpawningWave;
    private Coroutine waveRoutine;

    void Start()
    {
        if (autoStart)
        {
            StartWaves();
        }
    }

    public void StartWaves()
    {
        if (waveRoutine != null)
        {
            Debug.LogWarning("WaveSpawner is already running.");
            return;
        }

        currentWaveIndex = -1;
        waveRoutine = StartCoroutine(RunWaves());
    }

    public void StartNextWave()
    {
        if (waveRoutine != null)
        {
            Debug.LogWarning("Cannot manually start the next wave while the wave sequence is running.");
            return;
        }

        int nextWaveIndex = currentWaveIndex + 1;
        if (nextWaveIndex >= waves.Count)
        {
            Debug.Log("No more waves to start.");
            return;
        }

        waveRoutine = StartCoroutine(RunSingleWave(nextWaveIndex));
    }

    IEnumerator RunWaves()
    {
        for (int waveIndex = 0; waveIndex < waves.Count; waveIndex++)
        {
            yield return RunWaveEntries(waveIndex);
            yield return WaitForWaveToClear(waveIndex);
        }

        LogEvent("All waves completed.");
        waveRoutine = null;
    }

    IEnumerator RunSingleWave(int waveIndex)
    {
        yield return RunWaveEntries(waveIndex);
        yield return WaitForWaveToClear(waveIndex);

        waveRoutine = null;
    }

    IEnumerator RunWaveEntries(int waveIndex)
    {
        currentWaveIndex = waveIndex;
        WaveDefinition wave = waves[waveIndex];
        isSpawningWave = true;

        string waveLabel = string.IsNullOrWhiteSpace(wave.waveName) ? $"Wave {waveIndex + 1}" : wave.waveName;
        LogEvent($"Starting {waveLabel}.");

        if (wave.entries == null || wave.entries.Count == 0)
        {
            Debug.LogWarning($"{waveLabel} has no spawn entries.");
        }

        for (int entryIndex = 0; entryIndex < wave.entries.Count; entryIndex++)
        {
            WaveSpawnEntry entry = wave.entries[entryIndex];
            SpawnEntry(entry, waveLabel, entryIndex);

            if (entry != null && entry.delayAfterSpawn > 0f)
            {
                yield return new WaitForSeconds(entry.delayAfterSpawn);
            }
        }

        isSpawningWave = false;
        LogEvent($"{waveLabel} finished spawning. Waiting for {aliveEnemyCount} remaining enemies.");
    }

    IEnumerator WaitForWaveToClear(int waveIndex)
    {
        WaveDefinition wave = waves[waveIndex];
        string waveLabel = string.IsNullOrWhiteSpace(wave.waveName) ? $"Wave {waveIndex + 1}" : wave.waveName;

        yield return new WaitUntil(() => !isSpawningWave && aliveEnemyCount == 0);

        float interWaveDelay = wave.overrideInterWaveDelay ? wave.interWaveDelay : defaultInterWaveDelay;
        LogEvent($"{waveLabel} cleared.");

        if (interWaveDelay > 0f && waveIndex < waves.Count - 1)
        {
            LogEvent($"Intermission started for {interWaveDelay:0.##} seconds.");
            yield return new WaitForSeconds(interWaveDelay);
        }
    }

    void SpawnEntry(WaveSpawnEntry entry, string waveLabel, int entryIndex)
    {
        if (entry == null)
        {
            Debug.LogWarning($"{waveLabel} entry {entryIndex + 1} is missing.");
            return;
        }

        if (entry.enemyPrefab == null)
        {
            Debug.LogWarning($"{waveLabel} entry {entryIndex + 1} has no enemy prefab assigned.");
            return;
        }

        if (entry.spawnPoint == null)
        {
            Debug.LogWarning($"{waveLabel} entry {entryIndex + 1} has no spawn point assigned.");
            return;
        }

        Enemy spawnedEnemy = Instantiate(entry.enemyPrefab, entry.spawnPoint.position, entry.spawnPoint.rotation);
        spawnedEnemy.OnDied += HandleEnemyDied;
        aliveEnemyCount++;

        LogEvent($"Spawned {spawnedEnemy.name} from {entry.spawnPoint.name} for {waveLabel}. Alive enemies: {aliveEnemyCount}.");
    }

    void HandleEnemyDied(Enemy deadEnemy)
    {
        if (deadEnemy != null)
        {
            deadEnemy.OnDied -= HandleEnemyDied;
        }

        aliveEnemyCount = Mathf.Max(0, aliveEnemyCount - 1);
        LogEvent($"Enemy defeated. Remaining alive enemies: {aliveEnemyCount}.");
    }

    void LogEvent(string message)
    {
        if (!logWaveEvents)
            return;

        Debug.Log($"[WaveSpawner] {message}", this);
    }
}