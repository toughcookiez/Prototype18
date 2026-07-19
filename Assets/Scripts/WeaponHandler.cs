using UnityEngine;
using System;
using System.Collections;
using UnityEngine.Serialization;

public class WeaponHandler : MonoBehaviour
{
    [Serializable]
    public class BodyPartDamageSettings
    {
        [Min(0f)] public float defaultDamage = 25f;
        [Min(0f)] public float headDamage = 50f;
        [Min(0f)] public float chestDamage = 25f;
        [Min(0f)] public float stomachDamage = 20f;
        [Min(0f)] public float leftArmDamage = 15f;
        [Min(0f)] public float rightArmDamage = 15f;
        [Min(0f)] public float legsDamage = 15f;

        public float GetDamageFor(EnemyBodyPart bodyPart)
        {
            switch (bodyPart)
            {
                case EnemyBodyPart.Head:
                    return headDamage;
                case EnemyBodyPart.Chest:
                    return chestDamage;
                case EnemyBodyPart.Stomach:
                    return stomachDamage;
                case EnemyBodyPart.LeftArm:
                    return leftArmDamage;
                case EnemyBodyPart.RightArm:
                    return rightArmDamage;
                case EnemyBodyPart.Legs:
                    return legsDamage;
                default:
                    return defaultDamage;
            }
        }

        public void Clamp()
        {
            defaultDamage = Mathf.Max(0f, defaultDamage);
            headDamage = Mathf.Max(0f, headDamage);
            chestDamage = Mathf.Max(0f, chestDamage);
            stomachDamage = Mathf.Max(0f, stomachDamage);
            leftArmDamage = Mathf.Max(0f, leftArmDamage);
            rightArmDamage = Mathf.Max(0f, rightArmDamage);
            legsDamage = Mathf.Max(0f, legsDamage);
        }
    }

    [Header("References")]
    public Camera fpsCamera;
    public Transform muzzleTransform;
    public ParticleSystem muzzleFlash;
    public GameObject hitEffectPrefab;
    public HitCrosshairFeedback hitFeedback;
    public Animator weaponAnimator;

    public enum FireMode
    {
        SemiAutomatic,
        FullyAutomatic
    }

    [Serializable]
    public class WeaponAnimatorBindings
    {
        public string holdTriggerParam = "HoldDefault";
        public string fireTriggerParam = "Fire";
        public string aimFireTriggerParam;
        public string reloadStartTriggerParam = "ReloadStart";
        public string aimBoolParam;
        public string sprintBoolParam;

        [NonSerialized] int holdTriggerHash;
        [NonSerialized] int fireTriggerHash;
        [NonSerialized] int aimFireTriggerHash;
        [NonSerialized] int reloadStartTriggerHash;
        [NonSerialized] int aimBoolHash;
        [NonSerialized] int sprintBoolHash;
        [NonSerialized] bool hashesCached;

        public int HoldTriggerHash
        {
            get
            {
                EnsureHashesCached();
                return holdTriggerHash;
            }
        }

        public int FireTriggerHash
        {
            get
            {
                EnsureHashesCached();
                return fireTriggerHash;
            }
        }

        public int AimFireTriggerHash
        {
            get
            {
                EnsureHashesCached();
                return aimFireTriggerHash;
            }
        }

        public int ReloadStartTriggerHash
        {
            get
            {
                EnsureHashesCached();
                return reloadStartTriggerHash;
            }
        }

        public int AimBoolHash
        {
            get
            {
                EnsureHashesCached();
                return aimBoolHash;
            }
        }

        public int SprintBoolHash
        {
            get
            {
                EnsureHashesCached();
                return sprintBoolHash;
            }
        }

        public void CacheHashes()
        {
            holdTriggerHash = GetParamHash(holdTriggerParam);
            fireTriggerHash = GetParamHash(fireTriggerParam);
            aimFireTriggerHash = GetParamHash(aimFireTriggerParam);
            reloadStartTriggerHash = GetParamHash(reloadStartTriggerParam);
            aimBoolHash = GetParamHash(aimBoolParam);
            sprintBoolHash = GetParamHash(sprintBoolParam);
            hashesCached = true;
        }

        void EnsureHashesCached()
        {
            if (!hashesCached)
            {
                CacheHashes();
            }
        }

        static int GetParamHash(string param)
        {
            if (string.IsNullOrWhiteSpace(param))
                return 0;

            return Animator.StringToHash(param);
        }
    }

    [Header("Weapon")]
    public FireMode fireMode = FireMode.FullyAutomatic;
    public float fireRate = 10f;
    [FormerlySerializedAs("damage")]
    [Min(0f)]
    [SerializeField]
    float fallbackDamage = 25f;
    public BodyPartDamageSettings bodyPartDamage = new BodyPartDamageSettings();
    public float range = 100f;
    public LayerMask hitMask = ~0;

    [Header("Shot Pattern")]
    [Min(1)] public int raysPerShot = 1;

    [Header("Accuracy")]
    [Min(0f)] public float hipSpreadDegrees = 1.5f;
    [Min(0f)] public float aimSpreadDegrees = 0.25f;
    [Min(0f)] public float sprintSpreadDegrees = 3f;
    [Min(0f)] public float movementSpreadBonusDegrees = 0.75f;
    [Range(0.1f, 1f)] public float aimMoveSpeedMultiplier = 0.8f;

    [Header("Aiming")]
    public bool canAim = true;
    [Min(0.1f)] public float aimZoomFOV = 30f;

    [Header("Spray Pattern")]
    public bool useSprayPattern = false;
    [Range(0f, 1f)] public float sprayPatternInfluence = 0.75f;
    [Range(0f, 1f)] public float sprayPatternAimMultiplier = 0.55f;
    [Min(0f)] public float sprayPatternStrength = 1f;
    [Min(0f)] public float sprayPatternResetDelay = 0.15f;
    [Range(0f, 1f)] public float multiRayRandomScaleWhenSprayPatternEnabled = 0.35f;
    [Range(0.4f, 1f)] public float viewPunchScale = 0.7f;
    [Range(0.7f, 0.95f)] public float viewPunchDamping = 0.87f; // Higher = slower decay
    public Vector2[] sprayPatternOffsets =
    {
        new Vector2(0f, 0.06f),
        new Vector2(0.01f, 0.12f),
        new Vector2(0.01f, 0.18f),
        new Vector2(0.02f, 0.24f),
        new Vector2(0.02f, 0.30f),
        new Vector2(0.02f, 0.36f),
        new Vector2(0.03f, 0.42f),
        new Vector2(0.03f, 0.48f),
        new Vector2(0.03f, 0.54f),
        new Vector2(0.04f, 0.60f),
        new Vector2(-0.05f, 0.66f),
        new Vector2(0.06f, 0.72f),
        new Vector2(-0.05f, 0.78f),
        new Vector2(0.06f, 0.84f),
        new Vector2(-0.05f, 0.90f),
        new Vector2(0.06f, 0.96f),
        new Vector2(-0.06f, 1.02f),
        new Vector2(0.07f, 1.08f),
        new Vector2(-0.07f, 1.14f),
        new Vector2(0.08f, 1.20f)
    };

    [Header("Equip Lock")]
    [Min(0f)] public float equipLockDuration = 0.6f;

    [Header("Animation")]
    public WeaponAnimatorBindings animatorBindings = new WeaponAnimatorBindings();

    [Header("Ammo")]
    public int magazineSize = 30;
    public int currentMagazineAmmo = 30;
    public int reserveAmmo = 90;
    public float reloadDuration = 1.4f;
    public KeyCode reloadKey = KeyCode.R;

    [Header("Audio")]
    public AudioSource audioSource;
    public AudioClip shootClip;
    [Range(0f, 1f)]
    public float shootVolume = 1f;
    public float shootPitchRandomness = 0.05f;
    public AudioClip emptyAmmoClip;
    public float emptyAmmoSoundCooldown = 0.2f;

    float nextFireTime;
    float lastEmptyAmmoSoundTime = -999f;
    bool isReloading;
    Coroutine reloadRoutine;
    bool isEquipping;
    Coroutine equipLockRoutine;
    bool isAiming;
    bool isSprinting;
    bool isMoving;
    bool inventoryInputBlocked;
    WeaponManager weaponManager;
    int sprayPatternIndex;
    float lastSprayShotTime = -999f;
    Coroutine viewPunchRoutine;
    Vector3 currentViewPunchRotation = Vector3.zero; // Accumulated punch that decays

    public event Action<int, int> AmmoChanged;
    public event Action<WeaponHandler> FirePerformed;
    public event Action<WeaponHandler> ReloadStarted;
    public event Action<WeaponHandler> ReloadFinished;
    public event Action<WeaponHandler> ReloadCanceled;

    public int CurrentMagazineAmmo => currentMagazineAmmo;
    public int ReserveAmmo => reserveAmmo;
    public int MagazineSize => magazineSize;
    public bool IsReloading => isReloading;
    public bool IsInventoryInputBlocked => inventoryInputBlocked;
    public bool IsAimingForAnimation => isAiming;
    public bool IsSprintingForAnimation => isSprinting;
    public bool CanAim => canAim;
    public float AimZoomFOV => aimZoomFOV;

    void Awake()
    {
        weaponManager = GetComponentInParent<WeaponManager>();
        ClampAmmoValues();
        EnsureAnimatorBindings();
        NotifyAmmoChanged();
    }

    void OnValidate()
    {
        ClampAmmoValues();
        fallbackDamage = Mathf.Max(0f, fallbackDamage);
        if (bodyPartDamage == null)
            bodyPartDamage = new BodyPartDamageSettings();
        bodyPartDamage.Clamp();
        hipSpreadDegrees = Mathf.Max(0f, hipSpreadDegrees);
        aimSpreadDegrees = Mathf.Max(0f, aimSpreadDegrees);
        sprintSpreadDegrees = Mathf.Max(0f, sprintSpreadDegrees);
        movementSpreadBonusDegrees = Mathf.Max(0f, movementSpreadBonusDegrees);
        aimMoveSpeedMultiplier = Mathf.Clamp(aimMoveSpeedMultiplier, 0.1f, 1f);
        aimZoomFOV = Mathf.Max(0.1f, aimZoomFOV);
        sprayPatternInfluence = Mathf.Clamp01(sprayPatternInfluence);
        sprayPatternAimMultiplier = Mathf.Clamp01(sprayPatternAimMultiplier);
        sprayPatternStrength = Mathf.Max(0f, sprayPatternStrength);
        sprayPatternResetDelay = Mathf.Max(0f, sprayPatternResetDelay);
        multiRayRandomScaleWhenSprayPatternEnabled = Mathf.Clamp01(multiRayRandomScaleWhenSprayPatternEnabled);
        viewPunchScale = Mathf.Clamp(viewPunchScale, 0.4f, 1f);
        viewPunchDamping = Mathf.Clamp(viewPunchDamping, 0.7f, 0.95f);
        raysPerShot = Mathf.Max(1, raysPerShot);
        EnsureAnimatorBindings();
    }

    void OnDisable()
    {
        if (reloadRoutine != null)
        {
            StopCoroutine(reloadRoutine);
            reloadRoutine = null;
        }

        if (isReloading)
        {
            isReloading = false;
            ReloadCanceled?.Invoke(this);
        }

        if (equipLockRoutine != null)
        {
            StopCoroutine(equipLockRoutine);
            equipLockRoutine = null;
        }
        isEquipping = false;
        ResetSprayPatternState();
    }

    public void NotifyEquipped()
    {
        ResetSprayPatternState();

        if (equipLockDuration <= 0f)
            return;

        if (equipLockRoutine != null)
            StopCoroutine(equipLockRoutine);

        equipLockRoutine = StartCoroutine(EquipLockRoutine());
    }

    IEnumerator EquipLockRoutine()
    {
        isEquipping = true;
        yield return new WaitForSeconds(equipLockDuration);
        isEquipping = false;
        equipLockRoutine = null;
    }

    private void Start()
    {
        if (fpsCamera == null)
        {
            fpsCamera = Camera.main;
        }
    }

    void Update()
    {
        if (inventoryInputBlocked || IsBlockedByInventoryManager())
            return;

        if (isEquipping)
            return;

        if (Input.GetKeyDown(reloadKey))
        {
            TryStartReload();
        }

        bool canFire = Time.time >= nextFireTime;
        bool triggerPressed = fireMode == FireMode.FullyAutomatic ? Input.GetButton("Fire1") : Input.GetButtonDown("Fire1");

        if (!triggerPressed)
            return;

        if (isReloading)
            return;

        if (currentMagazineAmmo <= 0)
        {
            if (!TryStartReload())
            {
                TryPlayEmptyAmmoSound();
            }
            return;
        }

        if (canFire)
        {
            nextFireTime = Time.time + 1f / Mathf.Max(0.0001f, fireRate);
            currentMagazineAmmo--;
            NotifyAmmoChanged();
            Fire();
        }
    }

    bool TryStartReload()
    {
        if (isReloading)
            return false;

        if (currentMagazineAmmo >= magazineSize)
            return false;

        if (reserveAmmo <= 0)
            return false;

        ResetSprayPatternState();

        reloadRoutine = StartCoroutine(ReloadAfterDelay());
        ReloadStarted?.Invoke(this);
        return true;
    }

    IEnumerator ReloadAfterDelay()
    {
        isReloading = true;
        yield return new WaitForSeconds(reloadDuration);

        int ammoNeeded = Mathf.Max(0, magazineSize - currentMagazineAmmo);
        int ammoToLoad = Mathf.Min(ammoNeeded, reserveAmmo);

        if (ammoToLoad > 0)
        {
            currentMagazineAmmo += ammoToLoad;
            reserveAmmo -= ammoToLoad;
            NotifyAmmoChanged();
        }

        isReloading = false;
        reloadRoutine = null;
        ReloadFinished?.Invoke(this);
    }

    void TryPlayEmptyAmmoSound()
    {
        if (emptyAmmoClip == null)
            return;

        if (Time.time < lastEmptyAmmoSoundTime + emptyAmmoSoundCooldown)
            return;

        AudioSource source = audioSource != null ? audioSource : GetComponent<AudioSource>();
        if (source == null)
            return;

        source.PlayOneShot(emptyAmmoClip);
        lastEmptyAmmoSoundTime = Time.time;
    }

    void ClampAmmoValues()
    {
        magazineSize = Mathf.Max(1, magazineSize);
        currentMagazineAmmo = Mathf.Clamp(currentMagazineAmmo, 0, magazineSize);
        reserveAmmo = Mathf.Max(0, reserveAmmo);
        reloadDuration = Mathf.Max(0.05f, reloadDuration);
        shootVolume = Mathf.Clamp01(shootVolume);
        shootPitchRandomness = Mathf.Max(0f, shootPitchRandomness);
        emptyAmmoSoundCooldown = Mathf.Max(0f, emptyAmmoSoundCooldown);
    }

    void NotifyAmmoChanged()
    {
        AmmoChanged?.Invoke(currentMagazineAmmo, reserveAmmo);
    }

    public void Fire()
    {
        PlayMuzzleFlash();
        TryPlayShootSound();
        FirePerformed?.Invoke(this);

        Vector3 origin;
        Vector3 forward;
        Vector3 right;
        Vector3 up;

        if (fpsCamera != null)
        {
            Ray cameraRay = fpsCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
            origin = cameraRay.origin;
            forward = cameraRay.direction;
            right = fpsCamera.transform.right;
            up = fpsCamera.transform.up;
        }
        else
        {
            origin = muzzleTransform != null ? muzzleTransform.position : transform.position;
            Transform directionTransform = muzzleTransform != null ? muzzleTransform : transform;
            forward = directionTransform.forward;
            right = directionTransform.right;
            up = directionTransform.up;
        }

        int rayCount = Mathf.Max(1, raysPerShot);
        bool hitEnemyThisShot = false;
        float spreadDegrees = GetCurrentSpreadDegrees();
        float spreadRadius = spreadDegrees <= 0f ? 0f : Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
        bool sprayEnabled = useSprayPattern && spreadRadius > 0f;
        Vector2 sprayOffset = sprayEnabled ? GetSprayPatternOffset(spreadRadius) : Vector2.zero;
        float randomSpreadScale = sprayEnabled ? Mathf.Clamp01(1f - sprayPatternInfluence) : 1f;
        
        if (sprayEnabled && spreadRadius > 0f)
        {
            ApplyViewPunch(sprayOffset, spreadRadius);
        }

        for (int i = 0; i < rayCount; i++)
        {
            Vector2 randomOffset = spreadRadius > 0f
                ? UnityEngine.Random.insideUnitCircle * spreadRadius * randomSpreadScale
                : Vector2.zero;

            if (sprayEnabled && rayCount > 1)
            {
                randomOffset += UnityEngine.Random.insideUnitCircle * spreadRadius * multiRayRandomScaleWhenSprayPatternEnabled;
            }

            Vector3 direction = GetSpreadDirection(forward, right, up, sprayOffset + randomOffset);
            if (!Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Collide))
                continue;

            Enemy targetEnemy = null;
            EnemyBodyPart bodyPart = EnemyBodyPart.Unknown;

            EnemyHitbox hitbox = hit.collider.GetComponent<EnemyHitbox>();
            if (hitbox != null)
            {
                targetEnemy = hitbox.Owner != null ? hitbox.Owner : hit.collider.GetComponentInParent<Enemy>();
                bodyPart = hitbox.BodyPart;
            }
            else
            {
                targetEnemy = hit.collider.GetComponentInParent<Enemy>();
            }

            if (targetEnemy != null)
            {
                float resolvedDamage = bodyPartDamage != null
                    ? bodyPartDamage.GetDamageFor(bodyPart)
                    : fallbackDamage;
                float pelletDamage = resolvedDamage / rayCount;

                targetEnemy.TakeDamage(new EnemyDamageInfo(pelletDamage, bodyPart, hit.point));
                hitEnemyThisShot = true;
            }

            if (hit.rigidbody != null)
                hit.rigidbody.AddForce(-hit.normal * 50f, ForceMode.Impulse);

            if (hitEffectPrefab != null && targetEnemy == null)
            {
                var go2 = Instantiate(hitEffectPrefab, hit.point, Quaternion.LookRotation(-hit.normal));
                Destroy(go2, 5f);
            }
        }

        if (hitEnemyThisShot && hitFeedback != null)
            hitFeedback.PlayHitFeedback();
    }

    void TryPlayShootSound()
    {
        if (shootClip == null)
            return;

        AudioSource source = audioSource != null ? audioSource : GetComponent<AudioSource>();
        if (source == null)
            return;

        float originalPitch = source.pitch;
        float pitchOffset = UnityEngine.Random.Range(-shootPitchRandomness, shootPitchRandomness);
        source.pitch = Mathf.Clamp(originalPitch + pitchOffset, 0.1f, 3f);
        source.PlayOneShot(shootClip, shootVolume);
        source.pitch = originalPitch;
    }

    void PlayMuzzleFlash()
    {
        if (muzzleFlash != null)
        {
            muzzleFlash.Play(true);
        }
    }

    public void SetCamera(Camera cam)
    {
        fpsCamera = cam;
    }

    public void SetAimState(bool aiming)
    {
        isAiming = canAim && aiming;
    }

    public void SetSprintState(bool sprinting)
    {
        isSprinting = sprinting;
    }

    public void SetMoveState(bool moving)
    {
        isMoving = moving;
    }

    public void SetInventoryInputBlocked(bool blocked)
    {
        inventoryInputBlocked = blocked;
    }

    bool IsBlockedByInventoryManager()
    {
        if (weaponManager == null)
            weaponManager = GetComponentInParent<WeaponManager>();

        return weaponManager != null && weaponManager.IsShootingBlockedByInventory();
    }

    Vector3 GetSpreadDirection(Vector3 forward, Vector3 right, Vector3 up, Vector2 spreadOffset)
    {
        if (spreadOffset.sqrMagnitude <= Mathf.Epsilon)
            return forward.normalized;

        return (forward + right * spreadOffset.x + up * spreadOffset.y).normalized;
    }

    float GetCurrentSpreadDegrees()
    {
        float spread = isSprinting ? sprintSpreadDegrees : (isAiming ? aimSpreadDegrees : hipSpreadDegrees);
        if (isMoving)
        {
            spread += movementSpreadBonusDegrees;
        }

        return Mathf.Max(0f, spread);
    }

    Vector2 GetSprayPatternOffset(float spreadRadius)
    {
        if (sprayPatternOffsets == null || sprayPatternOffsets.Length == 0)
            return Vector2.zero;

        if (Time.time - lastSprayShotTime > sprayPatternResetDelay)
        {
            sprayPatternIndex = 0;
        }

        int index = sprayPatternIndex % sprayPatternOffsets.Length;
        sprayPatternIndex++;
        lastSprayShotTime = Time.time;

        float aimMultiplier = isAiming ? sprayPatternAimMultiplier : 1f;
        return sprayPatternOffsets[index] * spreadRadius * sprayPatternStrength * aimMultiplier;
    }

    void ResetSprayPatternState()
    {
        sprayPatternIndex = 0;
        lastSprayShotTime = -999f;
    }

    void ApplyViewPunch(Vector2 sprayOffset, float spreadRadius)
    {
        if (sprayOffset.sqrMagnitude <= Mathf.Epsilon || fpsCamera == null)
            return;

        Vector2 punch = sprayOffset * viewPunchScale;
        float punchDegreesPerUnit = 25f;
        
        Vector3 newPunch = new Vector3(
            -punch.y * punchDegreesPerUnit,
            punch.x * punchDegreesPerUnit,
            0f
        );
        
        currentViewPunchRotation += newPunch; // ADD to existing punch instead of replacing
        
        if (viewPunchRoutine != null)
            StopCoroutine(viewPunchRoutine);
        
        viewPunchRoutine = StartCoroutine(ApplyViewPunchDecay());
    }

    IEnumerator ApplyViewPunchDecay()
    {
        if (fpsCamera == null)
            yield break;

        Quaternion baseRotation = fpsCamera.transform.localRotation;
        
        while (currentViewPunchRotation.sqrMagnitude > 0.001f) // Stop when punch is nearly zero
        {
            fpsCamera.transform.localRotation = baseRotation * Quaternion.Euler(currentViewPunchRotation);
            
            // Exponential decay: multiply by damping factor each frame
            currentViewPunchRotation *= viewPunchDamping;
            
            yield return null;
        }
        
        currentViewPunchRotation = Vector3.zero;
        fpsCamera.transform.localRotation = baseRotation;
        viewPunchRoutine = null;
    }

    void EnsureAnimatorBindings()
    {
        if (animatorBindings == null)
            animatorBindings = new WeaponAnimatorBindings();

        animatorBindings.CacheHashes();
    }
}
