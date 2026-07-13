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
        public string reloadCompleteTriggerParam = "ReloadComplete";
        public string aimBoolParam;
        public string sprintBoolParam;

        [NonSerialized] int holdTriggerHash;
        [NonSerialized] int fireTriggerHash;
        [NonSerialized] int aimFireTriggerHash;
        [NonSerialized] int reloadStartTriggerHash;
        [NonSerialized] int reloadCompleteTriggerHash;
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

        public int ReloadCompleteTriggerHash
        {
            get
            {
                EnsureHashesCached();
                return reloadCompleteTriggerHash;
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
            reloadCompleteTriggerHash = GetParamHash(reloadCompleteTriggerParam);
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
    }

    public void NotifyEquipped()
    {
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

        for (int i = 0; i < rayCount; i++)
        {
            Vector3 direction = GetSpreadDirection(forward, right, up);
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
        isAiming = aiming;
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

    Vector3 GetSpreadDirection(Vector3 forward, Vector3 right, Vector3 up)
    {
        float spreadDegrees = GetCurrentSpreadDegrees();
        if (spreadDegrees <= 0f)
            return forward.normalized;

        float spreadRadius = Mathf.Tan(spreadDegrees * Mathf.Deg2Rad);
        Vector2 offset = UnityEngine.Random.insideUnitCircle * spreadRadius;
        return (forward + right * offset.x + up * offset.y).normalized;
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

    void EnsureAnimatorBindings()
    {
        if (animatorBindings == null)
            animatorBindings = new WeaponAnimatorBindings();

        animatorBindings.CacheHashes();
    }
}
