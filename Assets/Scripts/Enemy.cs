using UnityEngine;
using UnityEngine.AI;
using System;
using System.Collections.Generic;

public class Enemy : MonoBehaviour
{
    [Serializable]
    public struct BodyPartHurtTrigger
    {
        public EnemyBodyPart bodyPart;
        public string triggerParam;
    }

    public event Action<Enemy> OnDied;

    [Header("References")]
    public string playerTag = "Player";
    private Transform playerTransform;
    private NavMeshAgent navMeshAgent;
    public Animator animator;

    [Header("Animation")]
    public string idleBoolParam = "IsIdle";
    public string moveBoolParam = "IsMoving";
    public string attackTriggerParam = "Attack";
    public string[] hurtTriggerParams = { "Hurt" };
    public BodyPartHurtTrigger[] bodyPartHurtTriggers =
    {
        new BodyPartHurtTrigger { bodyPart = EnemyBodyPart.Head, triggerParam = "HurtHead" },
        new BodyPartHurtTrigger { bodyPart = EnemyBodyPart.Chest, triggerParam = "HurtChest" },
        new BodyPartHurtTrigger { bodyPart = EnemyBodyPart.Stomach, triggerParam = "HurtStomach" },
        new BodyPartHurtTrigger { bodyPart = EnemyBodyPart.LeftArm, triggerParam = "HurtLeftArm" },
        new BodyPartHurtTrigger { bodyPart = EnemyBodyPart.RightArm, triggerParam = "HurtRightArm" },
        new BodyPartHurtTrigger { bodyPart = EnemyBodyPart.Legs, triggerParam = "HurtLegs" }
    };
    public string hurtLayerName = "Hurt";
    private int idleBoolHash;
    private int moveBoolHash;
    private int attackTriggerHash;
    private int[] hurtTriggerHashes = Array.Empty<int>();
    private Dictionary<EnemyBodyPart, int> bodyPartHurtTriggerHashes = new Dictionary<EnemyBodyPart, int>();
    private int hurtLayerIndex = -1;

    [Header("Stats")]
    public float health = 100f;
    private float currentHealth;

    [Header("Combat")]
    public float attackRange = 2f;
    public float attackCooldown = 1f;
    public float damage = 10f;
    [Header("Combat Variation")]
    public Vector2 firstAttackDelayRange = new Vector2(0.1f, 0.9f);
    public Vector2 attackCooldownMultiplierRange = new Vector2(0.85f, 1.2f);
    public Vector2 animatorSpeedRange = new Vector2(0.95f, 1.08f);
    private float nextAttackTime;
    private bool isAttacking = false;
    private bool hasDealtDamageThisSwing = false;

    [Header("Hit Stagger")]
    public bool staggerOnHeavyHitsOnly = true;
    public float heavyHitThreshold = 35f;
    public float hitStunDuration = 0.15f;
    public float maxHitStunDuration = 0.25f;
    public float hurtAnimationDuration = 0.5f;
    private Coroutine hitStunCoroutine;
    private Coroutine hurtLayerCoroutine;
    private bool isHitStunned;
    private bool isDead;

    [Header("Formation")]
    public float formationRadius = 1f;
    private Vector3 formationOffset;
    public Vector2Int avoidancePriorityRange = new Vector2Int(30, 70);

    [Header("Ragdoll")]
    public bool useRagdoll = true;
    public bool destroyAfterDeath = true;
    public float destroyAfterSeconds = 8f;
    public Collider movementCollider;
    Collider[] ragdollColliders = Array.Empty<Collider>();
    Rigidbody[] ragdollRigidbodies = Array.Empty<Rigidbody>();

    void Awake()
    {
        navMeshAgent = GetComponent<NavMeshAgent>();
        if (navMeshAgent == null)
        {
            Debug.LogError("Enemy requires a NavMeshAgent component!");
            enabled = false;
            return;
        }

        // Set a random avoidance priority to help prevent enemies from clustering too much
        navMeshAgent.avoidancePriority = UnityEngine.Random.Range(avoidancePriorityRange.x, avoidancePriorityRange.y + 1);

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        idleBoolHash = Animator.StringToHash(idleBoolParam);
        moveBoolHash = Animator.StringToHash(moveBoolParam);
        attackTriggerHash = Animator.StringToHash(attackTriggerParam);
        CacheHurtTriggerHashes();
        CacheBodyPartHurtTriggerHashes();

        // Initialize hurt layer
        if (animator != null)
        {
            hurtLayerIndex = animator.GetLayerIndex(hurtLayerName);
            if (hurtLayerIndex != -1)
            {
                animator.SetLayerWeight(hurtLayerIndex, 0f);
            }

            // Slight per-enemy animator speed variance helps avoid visual sync.
            float randomAnimatorSpeed = UnityEngine.Random.Range(animatorSpeedRange.x, animatorSpeedRange.y);
            animator.speed = Mathf.Max(0.1f, randomAnimatorSpeed);
        }

        currentHealth = health;

        float angle = UnityEngine.Random.Range(0f, 360f) * Mathf.Deg2Rad;
        formationOffset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * formationRadius;
        nextAttackTime = Time.time + UnityEngine.Random.Range(firstAttackDelayRange.x, firstAttackDelayRange.y);

        InitializeRagdollParts();
        SetRagdollActive(false);
    }

    void OnValidate()
    {
        CacheHurtTriggerHashes();
        CacheBodyPartHurtTriggerHashes();

        if (firstAttackDelayRange.x > firstAttackDelayRange.y)
            firstAttackDelayRange = new Vector2(firstAttackDelayRange.y, firstAttackDelayRange.x);

        if (attackCooldownMultiplierRange.x > attackCooldownMultiplierRange.y)
            attackCooldownMultiplierRange = new Vector2(attackCooldownMultiplierRange.y, attackCooldownMultiplierRange.x);

        attackCooldownMultiplierRange.x = Mathf.Max(0.05f, attackCooldownMultiplierRange.x);
        attackCooldownMultiplierRange.y = Mathf.Max(0.05f, attackCooldownMultiplierRange.y);

        if (animatorSpeedRange.x > animatorSpeedRange.y)
            animatorSpeedRange = new Vector2(animatorSpeedRange.y, animatorSpeedRange.x);

        animatorSpeedRange.x = Mathf.Max(0.1f, animatorSpeedRange.x);
        animatorSpeedRange.y = Mathf.Max(0.1f, animatorSpeedRange.y);
    }

    void Start()
    {
        // Find the player by tag
        GameObject playerObj = GameObject.FindGameObjectWithTag(playerTag);
        if (playerObj != null)
        {
            playerTransform = playerObj.transform;
        }
        else
        {
            Debug.LogWarning($"Player with tag '{playerTag}' not found!");
        }
    }

    void Update()
    {
        if (currentHealth <= 0 || playerTransform == null)
        {
            SetMovementAnimation(false);
            return;
        }

        if (isHitStunned)
        {
            navMeshAgent.isStopped = true;
            SetMovementAnimation(false);
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        if (distanceToPlayer > attackRange)
        {
            // Chase the player, spreading out via a lerped formation offset
            navMeshAgent.isStopped = false;
            float t = Mathf.InverseLerp(attackRange * 2f, attackRange, distanceToPlayer);
            Vector3 offset = Vector3.Lerp(formationOffset, Vector3.zero, t);
            navMeshAgent.SetDestination(playerTransform.position + offset);
            isAttacking = false;
            SetMovementAnimation(true);
        }
        else
        {
            // In attack range
            navMeshAgent.isStopped = true;
            isAttacking = true;
            SetMovementAnimation(false);

            // Check if cooldown has expired
            if (Time.time >= nextAttackTime)
            {
                Attack();
                float cooldownMultiplier = UnityEngine.Random.Range(attackCooldownMultiplierRange.x, attackCooldownMultiplierRange.y);
                float randomizedCooldown = attackCooldown * cooldownMultiplier;
                nextAttackTime = Time.time + Mathf.Max(0.05f, randomizedCooldown);
            }
        }
    }

    void Attack()
    {
        if (animator != null)
        {
            animator.SetTrigger(attackTriggerHash);
        }

        // Reset damage gate for this swing
        hasDealtDamageThisSwing = false;
    }

    /// <summary>
    /// Called by animation event during attack swing to apply damage to player.
    /// Ensures damage is only applied once per swing despite multiple animation events.
    /// </summary>
    public void DealAttackDamage()
    {
        // Guard clauses
        if (isDead)
            return;

        if (hasDealtDamageThisSwing)
            return;

        if (playerTransform == null)
        {
            Debug.LogWarning("Player transform not found, cannot deal damage.");
            return;
        }

        // Check if still in range
        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (distanceToPlayer > attackRange)
        {
            return;
        }

        // Try to get player health and apply damage
        PlayerHealth playerHealth = playerTransform.GetComponent<PlayerHealth>();
        if (playerHealth == null || playerHealth.IsDead)
        {
            return;
        }

        // Apply damage
        playerHealth.TakeDamage(damage);
        hasDealtDamageThisSwing = true;
        Debug.Log($"Enemy dealt {damage} damage to player!");
    }

    void SetMovementAnimation(bool isMoving)
    {
        if (animator == null)
            return;

        animator.SetBool(moveBoolHash, isMoving);
        animator.SetBool(idleBoolHash, !isMoving);
    }

    void PlayHurtAnimation(EnemyBodyPart bodyPart)
    {
        if (animator == null)
            return;

        if (bodyPartHurtTriggerHashes.TryGetValue(bodyPart, out int bodyPartHash))
        {
            animator.SetTrigger(bodyPartHash);
            return;
        }

        if (hurtTriggerHashes.Length == 0)
            return;

        int selectedIndex = UnityEngine.Random.Range(0, hurtTriggerHashes.Length);
        animator.SetTrigger(hurtTriggerHashes[selectedIndex]);
    }

    void CacheHurtTriggerHashes()
    {
        if (hurtTriggerParams == null || hurtTriggerParams.Length == 0)
        {
            hurtTriggerHashes = Array.Empty<int>();
            return;
        }

        List<int> hashes = new List<int>(hurtTriggerParams.Length);
        for (int i = 0; i < hurtTriggerParams.Length; i++)
        {
            string param = hurtTriggerParams[i];
            if (string.IsNullOrWhiteSpace(param))
                continue;

            hashes.Add(Animator.StringToHash(param));
        }

        hurtTriggerHashes = hashes.ToArray();
    }

    void CacheBodyPartHurtTriggerHashes()
    {
        if (bodyPartHurtTriggerHashes == null)
            bodyPartHurtTriggerHashes = new Dictionary<EnemyBodyPart, int>();

        bodyPartHurtTriggerHashes.Clear();

        if (bodyPartHurtTriggers == null || bodyPartHurtTriggers.Length == 0)
            return;

        for (int i = 0; i < bodyPartHurtTriggers.Length; i++)
        {
            BodyPartHurtTrigger mapping = bodyPartHurtTriggers[i];
            if (mapping.bodyPart == EnemyBodyPart.Unknown)
                continue;

            if (string.IsNullOrWhiteSpace(mapping.triggerParam))
                continue;

            bodyPartHurtTriggerHashes[mapping.bodyPart] = Animator.StringToHash(mapping.triggerParam);
        }
    }

    public void TakeDamage(float damageAmount)
    {
        TakeDamage(new EnemyDamageInfo(damageAmount, EnemyBodyPart.Unknown, transform.position));
    }

    public void TakeDamage(EnemyDamageInfo damageInfo)
    {
        if (isDead)
            return;

        float damageAmount = Mathf.Max(0f, damageInfo.DamageAmount);
        currentHealth -= damageAmount;
        Debug.Log($"Enemy took {damageAmount} damage to {damageInfo.BodyPart}. Health: {currentHealth}");

        if (currentHealth > 0)
        {
            PlayHurtAnimation(damageInfo.BodyPart);

            // Manage hurt layer animation
            if (hurtLayerCoroutine != null)
                StopCoroutine(hurtLayerCoroutine);
            hurtLayerCoroutine = StartCoroutine(HurtLayerAnimation());

            if (ShouldApplyHitStun(damageAmount))
            {
                if (hitStunCoroutine != null)
                    StopCoroutine(hitStunCoroutine);
                hitStunCoroutine = StartCoroutine(HitStun());
            }
        }

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    bool ShouldApplyHitStun(float damageAmount)
    {
        if (hitStunDuration <= 0f || maxHitStunDuration <= 0f)
            return false;

        if (!staggerOnHeavyHitsOnly)
            return true;

        return damageAmount >= Mathf.Max(0f, heavyHitThreshold);
    }

    System.Collections.IEnumerator HitStun()
    {
        isHitStunned = true;
        navMeshAgent.isStopped = true;

        float stunDuration = Mathf.Clamp(hitStunDuration, 0.02f, maxHitStunDuration);
        yield return new WaitForSeconds(stunDuration);

        isHitStunned = false;
        hitStunCoroutine = null;
    }

    System.Collections.IEnumerator HurtLayerAnimation()
    {
        if (hurtLayerIndex == -1 || animator == null)
            yield break;

        // Set hurt layer to active
        animator.SetLayerWeight(hurtLayerIndex, 1f);

        // Wait for animation to complete
        yield return new WaitForSeconds(hurtAnimationDuration);

        // Set hurt layer back to inactive
        animator.SetLayerWeight(hurtLayerIndex, 0f);
        hurtLayerCoroutine = null;
    }

    void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (hitStunCoroutine != null)
        {
            StopCoroutine(hitStunCoroutine);
            hitStunCoroutine = null;
        }
        if (hurtLayerCoroutine != null)
        {
            StopCoroutine(hurtLayerCoroutine);
            hurtLayerCoroutine = null;
        }
        isHitStunned = false;

        SetMovementAnimation(false);
        Debug.Log("Enemy died!");
        OnDied?.Invoke(this);

        if (useRagdoll)
        {
            SetRagdollActive(true);
        }

        if (destroyAfterDeath)
        {
            Destroy(gameObject, Mathf.Max(0f, destroyAfterSeconds));
        }
    }

    void InitializeRagdollParts()
    {
        if (movementCollider == null)
        {
            movementCollider = GetComponent<Collider>();
        }

        Collider[] allColliders = GetComponentsInChildren<Collider>(true);
        List<Collider> ragdollColliderList = new List<Collider>(allColliders.Length);
        for (int i = 0; i < allColliders.Length; i++)
        {
            Collider col = allColliders[i];
            if (col == null)
                continue;

            if (col == movementCollider)
                continue;

            ragdollColliderList.Add(col);
        }

        ragdollColliders = ragdollColliderList.ToArray();

        Rigidbody[] allBodies = GetComponentsInChildren<Rigidbody>(true);
        List<Rigidbody> ragdollBodyList = new List<Rigidbody>(allBodies.Length);
        for (int i = 0; i < allBodies.Length; i++)
        {
            Rigidbody body = allBodies[i];
            if (body == null)
                continue;

            if (body.gameObject == gameObject)
                continue;

            ragdollBodyList.Add(body);
        }

        ragdollRigidbodies = ragdollBodyList.ToArray();
    }

    void SetRagdollActive(bool active)
    {
        if (!useRagdoll)
            return;

        for (int i = 0; i < ragdollColliders.Length; i++)
        {
            Collider col = ragdollColliders[i];
            if (col == null)
                continue;

            col.enabled = true;
            col.isTrigger = !active;
        }

        for (int i = 0; i < ragdollRigidbodies.Length; i++)
        {
            Rigidbody body = ragdollRigidbodies[i];
            if (body == null)
                continue;

            body.isKinematic = !active;
            body.detectCollisions = true;
        }

        if (movementCollider != null)
        {
            movementCollider.enabled = !active;
        }

        if (navMeshAgent != null)
        {
            navMeshAgent.enabled = !active;
        }

        if (animator != null)
        {
            animator.enabled = !active;
        }
    }
}
