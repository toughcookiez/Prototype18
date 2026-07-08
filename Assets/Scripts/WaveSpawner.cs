using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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

    [Header("UI")]
    public GameObject titleTextObject;
    public TMP_Text titleTmpText;
    public Text titleText;

    [Header("Wave Title Exit")]
    public float waveIndexVisibleDuration = 3f;

    [Header("Debug")]
    public bool logWaveEvents = true;

    private int currentWaveIndex = -1;
    private int aliveEnemyCount;
    private bool isSpawningWave;
    private Coroutine waveRoutine;
    private Coroutine waveTitleExitRoutine;
    private string lastDisplayedTitle;

    const string TextEffectComponentTypeName = "TextEffect";
    const string TextEffectExitTriggerMethodName = "StartManualEffects";


    void Awake()
    {
        ResolveTitleReferences();
    }

    void Start()
    {
        ResolveTitleReferences();

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

        ShowWaveIndex(waveIndex);

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
            int nextWaveIndex = waveIndex + 1;
            float remaining = interWaveDelay;

            ReactivateTitleForInterWaveCountdown();

            while (remaining > 0f)
            {
                ShowInterWaveCountdown(nextWaveIndex, remaining);
                remaining -= Time.deltaTime;
                yield return null;
            }

            ShowInterWaveCountdown(nextWaveIndex, 0f);
        }
    }

    void ReactivateTitleForInterWaveCountdown()
    {
        ResolveTitleReferences();

        if (titleTextObject == null)
            return;

        titleTextObject.SetActive(true);
    }

    void ResolveTitleReferences()
    {
        if (titleTextObject == null)
        {
            if (titleTmpText != null)
            {
                titleTextObject = titleTmpText.gameObject;
            }
            else if (titleText != null)
            {
                titleTextObject = titleText.gameObject;
            }
            else
            {
                titleTextObject = GameObject.Find("TitleText");
                if (titleTextObject == null)
                {
                    titleTextObject = GameObject.Find("Title");
                }
            }
        }

        if (titleTextObject != null)
        {
            if (titleTmpText == null)
            {
                titleTmpText = titleTextObject.GetComponent<TMP_Text>();
            }

            if (titleText == null)
            {
                titleText = titleTextObject.GetComponent<Text>();
            }
        }
    }

    void ShowWaveIndex(int waveIndex)
    {
        string waveIndexLabel = GetWaveIndexLabel(waveIndex);
        SetTitleText(waveIndexLabel, true);
        StartWaveTitleExitTimer();
    }

    void ShowInterWaveCountdown(int nextWaveIndex, float secondsRemaining)
    {
        string title = $"{GetWaveIndexLabel(nextWaveIndex)}\n{FormatCountdown(secondsRemaining)}";
        SetTitleText(title, false);
    }

    string GetWaveIndexLabel(int waveIndex)
    {
        return $"Wave {waveIndex + 1}";
    }

    string FormatCountdown(float secondsRemaining)
    {
        int totalSeconds = Mathf.Max(0, Mathf.CeilToInt(secondsRemaining));
        int minutes = totalSeconds / 60;
        int seconds = totalSeconds % 60;
        return $"{minutes:00}:{seconds:00}";
    }

    void SetTitleText(string value, bool restartTextEffect)
    {
        ResolveTitleReferences();

        if (titleTmpText == null && titleText == null)
            return;

        if (!restartTextEffect && value == lastDisplayedTitle)
            return;

        bool canRestartObject = restartTextEffect && titleTextObject != null;

        if (canRestartObject)
        {
            titleTextObject.SetActive(false);
        }

        if (titleTmpText != null)
        {
            titleTmpText.text = value;
        }
        else if (titleText != null)
        {
            titleText.text = value;
        }

        lastDisplayedTitle = value;

        if (canRestartObject)
        {
            titleTextObject.SetActive(true);
        }
    }

    void StartWaveTitleExitTimer()
    {
        if (waveTitleExitRoutine != null)
        {
            StopCoroutine(waveTitleExitRoutine);
        }

        if (waveIndexVisibleDuration <= 0f)
        {
            TriggerWaveTitleExit();
            return;
        }

        waveTitleExitRoutine = StartCoroutine(TriggerWaveTitleExitAfterDelay());
    }

    IEnumerator TriggerWaveTitleExitAfterDelay()
    {
        yield return new WaitForSeconds(waveIndexVisibleDuration);
        TriggerWaveTitleExit();
        waveTitleExitRoutine = null;
    }

    void TriggerWaveTitleExit()
    {
        ResolveTitleReferences();

        if (titleTextObject == null)
            return;

        Component[] components = titleTextObject.GetComponents<MonoBehaviour>();

        for (int i = 0; i < components.Length; i++)
        {
            Component component = components[i];
            if (component == null)
                continue;

            System.Type componentType = component.GetType();
            bool typeNameMatches = componentType.Name == TextEffectComponentTypeName ||
                                   componentType.FullName == TextEffectComponentTypeName ||
                                   componentType.Name.Contains(TextEffectComponentTypeName);

            if (!typeNameMatches)
                continue;

            MethodInfo method = componentType.GetMethod(TextEffectExitTriggerMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, System.Type.EmptyTypes, null);
            if (method != null)
            {
                method.Invoke(component, null);
                return;
            }

            MethodInfo methodWithString = componentType.GetMethod(TextEffectExitTriggerMethodName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(string) }, null);
            if (methodWithString != null)
            {
                methodWithString.Invoke(component, new object[] { string.Empty });
                return;
            }
        }

        LogEvent($"Text Effect trigger method '{TextEffectExitTriggerMethodName}' was not found on '{titleTextObject.name}'.");
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