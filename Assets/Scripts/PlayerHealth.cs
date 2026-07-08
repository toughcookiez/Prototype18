using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System;
using System.Collections;

public class PlayerHealth : MonoBehaviour
{
    public event Action<float> OnDamaged; // Args: damage amount
    public event Action OnDied;

    [Header("Health")]
    public float maxHealth = 100f;
    private float currentHealth;

    [Header("Damage Effect UI")]
    public Image damageEffectImage;
    [Range(0f, 1f)]
    public float criticalHealthThreshold = 0.25f; // 25% of max health
    public float damageFlashDuration = 0.3f; // Fade out time
    public float criticalPulseSpeed = 2f; // How fast the pulse oscillates

    [Header("Death Animation")]
    public Camera playerCamera;
    public Rigidbody playerRigidbody;
    public WeaponManager weaponManager;
    public Animator armsAnimator;
    public string armsDeathTriggerParam = "Death";
    public bool enableDeathDesaturation = true;
    public Volume globalVolume;
    public CanvasGroup fadeCanvasGroup; // For screen fade effect
    public Image gameOverPanel; // Game Over UI panel
    public Vector3 cameraTiltAngle = new Vector3(45f, 0f, 0f); // Camera tilt per axis on death
    public float deathAnimationDuration = 1.5f; // Duration of fade and tilt
    public KeyCode restartKey = KeyCode.R;

    private bool isDead = false;
    private bool isCriticalHealth = false;
    private Coroutine damageFlashCoroutine;
    private Coroutine criticalPulseCoroutine;
    private Coroutine deathSequenceCoroutine;
    private int armsDeathTriggerHash;
    private ColorAdjustments deathColorAdjustments;

    public float CurrentHealth => currentHealth;
    public float MaxHealth => maxHealth;
    public bool IsDead => isDead;
    public float HealthNormalized => maxHealth > 0 ? currentHealth / maxHealth : 0f;

    void Awake()
    {
        currentHealth = maxHealth;

        // Find DamageEffect image if not assigned
        if (damageEffectImage == null)
        {
            damageEffectImage = GetComponentInChildren<Image>();
            if (damageEffectImage != null && damageEffectImage.name != "DamageEffect")
            {
                // Try to find it by name
                Transform damageEffectTransform = transform.Find("DamageEffect");
                if (damageEffectTransform != null)
                {
                    damageEffectImage = damageEffectTransform.GetComponent<Image>();
                }
            }
        }

        // Initialize damage effect
        if (damageEffectImage != null)
        {
            damageEffectImage.gameObject.SetActive(false);
        }

        // Cache death animation references
        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (playerRigidbody == null)
        {
            playerRigidbody = GetComponent<Rigidbody>();
        }

        if (weaponManager == null)
        {
            weaponManager = GetComponentInChildren<WeaponManager>(true);
        }

        if (armsAnimator == null)
        {
            PlayerArmsAnimatorBridge armsBridge = GetComponentInChildren<PlayerArmsAnimatorBridge>(true);
            if (armsBridge != null)
            {
                armsAnimator = armsBridge.armsAnimator != null ? armsBridge.armsAnimator : armsBridge.GetComponent<Animator>();
            }

            if (armsAnimator == null)
            {
                armsAnimator = GetComponentInChildren<Animator>();
            }
        }

        CacheDeathColorAdjustments();

        armsDeathTriggerHash = string.IsNullOrWhiteSpace(armsDeathTriggerParam)
            ? 0
            : Animator.StringToHash(armsDeathTriggerParam);

        if (fadeCanvasGroup == null)
        {
            fadeCanvasGroup = GetComponentInChildren<CanvasGroup>();
        }

        // Initialize fade and game over panel visibility
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 0f;
        }

        if (gameOverPanel != null)
        {
            gameOverPanel.gameObject.SetActive(false);
        }
    }

    void Update()
    {
        // Handle restart input after death
        if (isDead && Input.GetKeyDown(restartKey))
        {
            RestartScene();
        }
    }

    /// <summary>
    /// Reduces player health by damageAmount. Clamps to 0.
    /// </summary>
    public void TakeDamage(float damageAmount)
    {
        if (isDead)
            return;

        float clampedDamage = Mathf.Max(0f, damageAmount);
        currentHealth -= clampedDamage;

        Debug.Log($"Player took {clampedDamage} damage. Health: {currentHealth}");

        OnDamaged?.Invoke(clampedDamage);

        // Flash damage effect on impact
        if (damageEffectImage != null)
        {
            if (damageFlashCoroutine != null)
            {
                StopCoroutine(damageFlashCoroutine);
            }
            damageFlashCoroutine = StartCoroutine(DamageFlashCoroutine());
        }

        // Check critical health state
        UpdateCriticalHealthState();

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    /// <summary>
    /// Restores health up to maxHealth.
    /// </summary>
    public void Heal(float healAmount)
    {
        if (isDead)
            return;

        float clampedHeal = Mathf.Max(0f, healAmount);
        currentHealth = Mathf.Min(currentHealth + clampedHeal, maxHealth);

        Debug.Log($"Player healed {clampedHeal}. Health: {currentHealth}");

        // Check if we've recovered from critical
        UpdateCriticalHealthState();
    }

    void UpdateCriticalHealthState()
    {
        float healthThreshold = maxHealth * criticalHealthThreshold;
        bool shouldBeCritical = currentHealth <= healthThreshold && currentHealth > 0;

        if (shouldBeCritical && !isCriticalHealth)
        {
            // Entering critical state
            isCriticalHealth = true;
            if (criticalPulseCoroutine != null)
            {
                StopCoroutine(criticalPulseCoroutine);
            }
            criticalPulseCoroutine = StartCoroutine(CriticalPulseCoroutine());
        }
        else if (!shouldBeCritical && isCriticalHealth)
        {
            // Leaving critical state
            isCriticalHealth = false;
            if (criticalPulseCoroutine != null)
            {
                StopCoroutine(criticalPulseCoroutine);
                criticalPulseCoroutine = null;
            }
            if (damageEffectImage != null)
            {
                damageEffectImage.gameObject.SetActive(false);
            }
        }
    }

    void Die()
    {
        isDead = true;
        Debug.Log("Player died!");

        ApplyDeathSaturation();

        if (weaponManager != null)
        {
            weaponManager.UnequipActiveWeapon();
            weaponManager.enabled = false;
        }

        if (armsAnimator != null && armsDeathTriggerHash != 0)
        {
            armsAnimator.SetTrigger(armsDeathTriggerHash);
        }

        // Clean up coroutines
        if (damageFlashCoroutine != null)
        {
            StopCoroutine(damageFlashCoroutine);
        }
        if (criticalPulseCoroutine != null)
        {
            StopCoroutine(criticalPulseCoroutine);
        }

        OnDied?.Invoke();

        // Start death animation sequence
        if (deathSequenceCoroutine != null)
        {
            StopCoroutine(deathSequenceCoroutine);
        }
        deathSequenceCoroutine = StartCoroutine(DeathSequenceCoroutine());
    }

    void CacheDeathColorAdjustments()
    {
        if (globalVolume == null)
        {
            Volume[] allVolumes = FindObjectsOfType<Volume>(true);
            for (int i = 0; i < allVolumes.Length; i++)
            {
                if (allVolumes[i] != null && allVolumes[i].isGlobal)
                {
                    globalVolume = allVolumes[i];
                    break;
                }
            }
        }

        if (globalVolume == null || globalVolume.profile == null)
            return;

        globalVolume.profile.TryGet(out deathColorAdjustments);
    }

    void ApplyDeathSaturation()
    {
        if (!enableDeathDesaturation)
            return;

        if (deathColorAdjustments == null)
            return;

        deathColorAdjustments.active = true;
        deathColorAdjustments.saturation.overrideState = true;
        deathColorAdjustments.saturation.value = -100f;
    }

    IEnumerator DamageFlashCoroutine()
    {
        if (damageEffectImage == null)
            yield break;

        damageEffectImage.gameObject.SetActive(true);

        // Set to full alpha and scale instantly on impact
        Color flashColor = damageEffectImage.color;
        flashColor.a = 1f;
        damageEffectImage.color = flashColor;
        damageEffectImage.transform.localScale = Vector3.one;

        // Fade out over damageFlashDuration
        float elapsed = 0f;
        while (elapsed < damageFlashDuration)
        {
            elapsed += Time.deltaTime;
            float alpha = Mathf.Lerp(1f, 0f, elapsed / damageFlashDuration);
            flashColor.a = alpha;
            damageEffectImage.color = flashColor;
            yield return null;
        }

        // Ensure final alpha is 0
        flashColor.a = 0f;
        damageEffectImage.color = flashColor;

        // Deactivate if not in critical health
        if (!isCriticalHealth)
        {
            damageEffectImage.gameObject.SetActive(false);
        }

        damageFlashCoroutine = null;
    }

    IEnumerator CriticalPulseCoroutine()
    {
        if (damageEffectImage == null)
            yield break;

        damageEffectImage.gameObject.SetActive(true);

        float minAlpha = 0.3f; // Faint baseline
        float maxAlpha = 0.6f; // Pulse peak
        float criticalScale = 1.1f; // Scaled up for critical indicator

        while (isCriticalHealth && !isDead)
        {
            // Oscillate alpha with a sine wave
            float pulse = Mathf.Sin(Time.time * Mathf.PI * criticalPulseSpeed) * 0.5f + 0.5f;
            float targetAlpha = Mathf.Lerp(minAlpha, maxAlpha, pulse);

            Color pulseColor = damageEffectImage.color;
            pulseColor.a = targetAlpha;
            damageEffectImage.color = pulseColor;

            // Keep scale at critical value
            damageEffectImage.transform.localScale = Vector3.one * criticalScale;

            yield return null;
        }

        criticalPulseCoroutine = null;
    }

    IEnumerator DeathSequenceCoroutine()
    {
        // Disable player input and movement
        FirstPersonController fpc = GetComponent<FirstPersonController>();
        if (fpc != null)
        {
            fpc.enabled = false;
        }

        // Enable ragdoll physics
        if (playerRigidbody != null)
        {
            playerRigidbody.isKinematic = false;
            playerRigidbody.useGravity = true;
            
            // Remove all rotation/position constraints to allow falling/tumbling
            //playerRigidbody.constraints = RigidbodyConstraints.None;
            
            // Apply initial velocity to make player fall
            playerRigidbody.linearVelocity = Vector3.down * 5f;
            
            // Add angular velocity for tumbling effect
            playerRigidbody.angularVelocity = new Vector3(UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(-2f, 2f), UnityEngine.Random.Range(-2f, 2f));
        }

        // Tilt player body as it falls
        float elapsed = 0f;
        Quaternion startRotation = transform.localRotation;
        Quaternion targetRotation = startRotation * Quaternion.Euler(cameraTiltAngle);

        while (elapsed < deathAnimationDuration)
        {
            elapsed += Time.deltaTime;
            float linearProgress = elapsed / deathAnimationDuration;
            float progress = Mathf.Pow(linearProgress, 3f); // Quadratic easing for acceleration

            // Tilt player body
            transform.localRotation = Quaternion.Lerp(startRotation, targetRotation, progress);

            // Fade screen to black
            if (fadeCanvasGroup != null)
            {
                fadeCanvasGroup.alpha = Mathf.Lerp(0f, 1f, progress);
            }

            yield return null;
        }

        // Ensure final state
        transform.localRotation = targetRotation;
        if (fadeCanvasGroup != null)
        {
            fadeCanvasGroup.alpha = 1f;
        }

        // Show game over panel
        if (gameOverPanel != null)
        {
            gameOverPanel.gameObject.SetActive(true);
        }

        Debug.Log($"Death animation complete. Press {restartKey} to restart.");

        deathSequenceCoroutine = null;
    }

    void RestartScene()
    {
        Time.timeScale = 1f; // Ensure time is running
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }
}
