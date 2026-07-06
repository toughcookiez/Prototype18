using UnityEngine;

public class PlayerArmsAnimatorBridge : MonoBehaviour
{
    [Header("References")]
    public WeaponManager weaponManager;
    public Animator armsAnimator;

    [Header("Debug")]
    public bool logConfigurationWarnings = true;

    WeaponHandler activeWeapon;
    Animator activeGunAnimator;

    void Awake()
    {
        if (weaponManager == null)
            weaponManager = GetComponentInParent<WeaponManager>();

        if (armsAnimator == null)
            armsAnimator = GetComponent<Animator>();
    }

    void OnEnable()
    {
        if (weaponManager == null)
        {
            Warn("WeaponManager reference is missing.");
            return;
        }

        weaponManager.ActiveWeaponChanged += OnActiveWeaponChanged;
        OnActiveWeaponChanged(weaponManager.ActiveWeapon);
    }

    void OnDisable()
    {
        if (weaponManager != null)
            weaponManager.ActiveWeaponChanged -= OnActiveWeaponChanged;

        DetachWeaponEvents(activeWeapon);
        activeWeapon = null;
        activeGunAnimator = null;
    }

    public void SetAimState(bool isAiming)
    {
        if (activeWeapon == null)
            return;

        TrySetBool(activeWeapon.animatorBindings.AimBoolHash, activeWeapon.animatorBindings.aimBoolParam, isAiming);
    }

    public void SetSprintState(bool isSprinting)
    {
        if (activeWeapon == null)
            return;

        TrySetBool(activeWeapon.animatorBindings.SprintBoolHash, activeWeapon.animatorBindings.sprintBoolParam, isSprinting);
    }

    void OnActiveWeaponChanged(WeaponHandler weapon)
    {
        if (activeWeapon == weapon)
            return;

        DetachWeaponEvents(activeWeapon);
        activeWeapon = weapon;
        activeGunAnimator = ResolveGunAnimator(activeWeapon);
        AttachWeaponEvents(activeWeapon);
        ApplyHoldAnimation(activeWeapon);
    }

    void AttachWeaponEvents(WeaponHandler weapon)
    {
        if (weapon == null)
            return;

        weapon.FirePerformed += OnWeaponFired;
        weapon.ReloadStarted += OnWeaponReloadStarted;
        weapon.ReloadFinished += OnWeaponReloadFinished;
        weapon.ReloadCanceled += OnWeaponReloadCanceled;
    }

    void DetachWeaponEvents(WeaponHandler weapon)
    {
        if (weapon == null)
            return;

        weapon.FirePerformed -= OnWeaponFired;
        weapon.ReloadStarted -= OnWeaponReloadStarted;
        weapon.ReloadFinished -= OnWeaponReloadFinished;
        weapon.ReloadCanceled -= OnWeaponReloadCanceled;
    }

    void ApplyHoldAnimation(WeaponHandler weapon)
    {
        if (weapon == null)
            return;

        TrySetTrigger(weapon.animatorBindings.HoldTriggerHash, weapon.animatorBindings.holdTriggerParam);
        TrySetGunTrigger(weapon.animatorBindings.HoldTriggerHash, weapon.animatorBindings.holdTriggerParam);
    }

    void OnWeaponFired(WeaponHandler weapon)
    {
        if (weapon != activeWeapon)
            return;

        TrySetTrigger(weapon.animatorBindings.FireTriggerHash, weapon.animatorBindings.fireTriggerParam);
    }

    void OnWeaponReloadStarted(WeaponHandler weapon)
    {
        if (weapon != activeWeapon)
            return;

        TrySetTrigger(weapon.animatorBindings.ReloadStartTriggerHash, weapon.animatorBindings.reloadStartTriggerParam);
        TrySetGunTrigger(weapon.animatorBindings.ReloadStartTriggerHash, weapon.animatorBindings.reloadStartTriggerParam);
    }

    void OnWeaponReloadFinished(WeaponHandler weapon)
    {
        if (weapon != activeWeapon)
            return;

        TrySetTrigger(weapon.animatorBindings.ReloadCompleteTriggerHash, weapon.animatorBindings.reloadCompleteTriggerParam);
        TrySetGunTrigger(weapon.animatorBindings.ReloadCompleteTriggerHash, weapon.animatorBindings.reloadCompleteTriggerParam);
    }

    void OnWeaponReloadCanceled(WeaponHandler weapon)
    {
        if (weapon != activeWeapon)
            return;

        TryResetTrigger(armsAnimator, weapon.animatorBindings.ReloadStartTriggerHash);
        TryResetTrigger(armsAnimator, weapon.animatorBindings.ReloadCompleteTriggerHash);
        TryResetTrigger(activeGunAnimator, weapon.animatorBindings.ReloadStartTriggerHash);
        TryResetTrigger(activeGunAnimator, weapon.animatorBindings.ReloadCompleteTriggerHash);

        // Default fallback: return both rigs to hold pose if a reload is canceled (e.g., weapon switch).
        TrySetTrigger(weapon.animatorBindings.HoldTriggerHash, weapon.animatorBindings.holdTriggerParam);
        TrySetGunTrigger(weapon.animatorBindings.HoldTriggerHash, weapon.animatorBindings.holdTriggerParam);
    }

    Animator ResolveGunAnimator(WeaponHandler weapon)
    {
        if (weapon == null)
            return null;

        if (weapon.weaponAnimator != null)
            return weapon.weaponAnimator;

        return weapon.GetComponentInChildren<Animator>(true);
    }

    void TrySetTrigger(int hash, string paramName)
    {
        if (armsAnimator == null)
        {
            Warn("Arms Animator reference is missing.");
            return;
        }

        if (hash == 0)
            return;

        if (!HasParameter(armsAnimator, hash, AnimatorControllerParameterType.Trigger))
        {
            Warn($"Missing trigger parameter '{paramName}' on '{armsAnimator.runtimeAnimatorController?.name}'.");
            return;
        }

        armsAnimator.SetTrigger(hash);
    }

    void TrySetBool(int hash, string paramName, bool value)
    {
        if (armsAnimator == null)
        {
            Warn("Arms Animator reference is missing.");
            return;
        }

        if (hash == 0)
            return;

        if (!HasParameter(armsAnimator, hash, AnimatorControllerParameterType.Bool))
        {
            Warn($"Missing bool parameter '{paramName}' on '{armsAnimator.runtimeAnimatorController?.name}'.");
            return;
        }

        armsAnimator.SetBool(hash, value);
    }

    void TrySetGunTrigger(int hash, string paramName)
    {
        if (activeGunAnimator == null)
            return;

        if (hash == 0)
            return;

        if (!HasParameter(activeGunAnimator, hash, AnimatorControllerParameterType.Trigger))
        {
            Warn($"Missing trigger parameter '{paramName}' on gun animator '{activeGunAnimator.runtimeAnimatorController?.name}'.");
            return;
        }

        activeGunAnimator.SetTrigger(hash);
    }

    static void TryResetTrigger(Animator animator, int hash)
    {
        if (animator == null || hash == 0)
            return;

        animator.ResetTrigger(hash);
    }

    static bool HasParameter(Animator animator, int nameHash, AnimatorControllerParameterType type)
    {
        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == nameHash && parameter.type == type)
                return true;
        }

        return false;
    }

    void Warn(string message)
    {
        if (!logConfigurationWarnings)
            return;

        Debug.LogWarning($"PlayerArmsAnimatorBridge: {message}", this);
    }
}
