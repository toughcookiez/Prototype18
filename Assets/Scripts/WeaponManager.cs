using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.UIElements;
using TMPro;
using UIButton = UnityEngine.UI.Button;
using UIToolkitButton = UnityEngine.UIElements.Button;

public class WeaponManager : MonoBehaviour
{
    [Serializable]
    public class WeaponSpawnEntry
    {
        public string displayName;
        public WeaponHandler prefab;
        public Vector3 localPosition;
        public Vector3 localEulerAngles;
        public Vector3 localScale = Vector3.one;

        public string GetLabel()
        {
            if (!string.IsNullOrWhiteSpace(displayName))
                return displayName;

            return prefab != null ? prefab.name : "Weapon";
        }
    }

    [Header("Weapons")]
    public List<WeaponSpawnEntry> weapons = new List<WeaponSpawnEntry>();
    public Transform weaponMount;
    public int startingWeaponIndex;

    [Header("Legacy (Auto Migrated)")]
    public List<WeaponHandler> weaponPrefabs = new List<WeaponHandler>();

    [Header("Switch Input")]
    public bool enableNumberKeySwitch = true;
    public bool enableMouseWheelSwitch = true;
    public bool blockSwitchWhileMenuOpen = true;

    [Header("Inventory UI")]
    public KeyCode inventoryKey = KeyCode.I;
    public UIDocument inventoryDocument;
    public string inventoryRootElementName = "inventory-window";
    public string inventoryListElementName = "weapon-list";
    public bool closeMenuAfterSelection = true;

    [Header("Inventory Tablet Animation")]
    public GameObject inventoryTabletObject;
    public Animator inventoryArmsAnimator;
    public string inventoryOpenBoolParameter = "IsInventoryOpen";
    [Min(0f)] public float inventoryOpenAnimationDelay = 0.2f;
    [Min(0f)] public float inventoryCloseAnimationDelay = 0.2f;

    [Header("Inventory Gameplay Lock")]
    public FirstPersonController playerController;
    public bool disableMovementWhileInventoryOpen = true;
    public bool disableLookWhileInventoryOpen = true;
    public bool disableShootingWhileInventoryOpen = true;
    public bool forceExitAimWhileInventoryOpen = true;
    public bool autoEnableTriggerRaycasts = true;

    [Header("Inventory UI (Legacy uGUI)")]
    public GameObject inventoryWindow;
    public Transform weaponButtonContainer;
    public UIButton weaponButtonPrefab;

    public event Action<WeaponHandler> ActiveWeaponChanged;

    public WeaponHandler ActiveWeapon { get; private set; }
    public int ActiveWeaponIndex { get; private set; } = -1;

    CursorLockMode previousCursorLockState;
    bool previousCursorVisible;
    VisualElement inventoryRootElement;
    VisualElement weaponListElement;
    readonly List<UIToolkitButton> boundWeaponButtons = new List<UIToolkitButton>();
    int inventoryOpenBoolHash;
    Coroutine inventoryTransitionRoutine;
    bool isInventoryTransitioning;
    bool inventoryControlsLocked;
    bool inventoryOpenState;
    bool previousPlayerCanMove;
    bool previousCameraCanMove;
    bool hasCachedControllerState;
    bool hadWeaponWhenInventoryOpened;

    void Awake()
    {
        if (weaponMount == null)
        {
            weaponMount = transform;
        }

        MigrateLegacyWeaponPrefabsIfNeeded();
        CleanupWeapons();
        ResolveInventoryElements();
        ResolveRuntimeReferences();
        CacheAnimatorHashes();
        EnsureInventoryWorldSpaceInputReady();
        BuildWeaponButtons();
        SetInventoryVisible(false);

        if (inventoryTabletObject != null)
            inventoryTabletObject.SetActive(false);

        if (weapons.Count == 0)
        {
            Debug.LogWarning("WeaponManager has no weapon prefabs assigned.", this);
            SetActiveWeapon(null, -1);
            return;
        }

        int clampedStartIndex = Mathf.Clamp(startingWeaponIndex, 0, weapons.Count - 1);
        EquipWeaponByIndex(clampedStartIndex, true);
    }

    void Update()
    {
        if (Input.GetKeyDown(inventoryKey))
        {
            ToggleInventoryWindow();
        }

        if (IsInventoryOpen() && blockSwitchWhileMenuOpen)
            return;

        HandleSwitchInput();
    }

    void OnDestroy()
    {
        UnbindUIToolkitWeaponButtons();

        if (inventoryTransitionRoutine != null)
            StopCoroutine(inventoryTransitionRoutine);

        if (inventoryControlsLocked)
        {
            ApplyInventoryControlLock(false);
            SetCursorForInventory(false);
        }

        if (inventoryTabletObject != null)
            inventoryTabletObject.SetActive(false);

        if (ActiveWeapon != null)
        {
            Destroy(ActiveWeapon.gameObject);
        }
    }

    public bool EquipWeaponByIndex(int index, bool force = false)
    {
        if (index < 0 || index >= weapons.Count)
            return false;

        if (!force && index == ActiveWeaponIndex && ActiveWeapon != null)
            return false;

        WeaponSpawnEntry entry = weapons[index];
        if (entry == null || entry.prefab == null)
        {
            Debug.LogWarning("Tried to equip a null weapon prefab.", this);
            return false;
        }

        if (ActiveWeapon != null)
        {
            Destroy(ActiveWeapon.gameObject);
        }

        WeaponHandler newWeapon = Instantiate(entry.prefab, weaponMount);
        newWeapon.transform.localPosition = entry.localPosition;
        newWeapon.transform.localRotation = Quaternion.Euler(entry.localEulerAngles);
        newWeapon.transform.localScale = entry.localScale;

        SetActiveWeapon(newWeapon, index);
        newWeapon.NotifyEquipped();
        return true;
    }

    public bool SelectNextWeapon()
    {
        if (weapons.Count == 0)
            return false;

        int nextIndex = ActiveWeaponIndex + 1;
        if (nextIndex >= weapons.Count)
            nextIndex = 0;

        return EquipWeaponByIndex(nextIndex);
    }

    public bool SelectPreviousWeapon()
    {
        if (weapons.Count == 0)
            return false;

        int previousIndex = ActiveWeaponIndex - 1;
        if (previousIndex < 0)
            previousIndex = weapons.Count - 1;

        return EquipWeaponByIndex(previousIndex);
    }

    public bool UnequipActiveWeapon()
    {
        if (ActiveWeapon == null)
            return false;

        Destroy(ActiveWeapon.gameObject);
        SetActiveWeapon(null, -1);
        return true;
    }

    void HandleSwitchInput()
    {
        if (weapons.Count == 0)
            return;

        if (enableNumberKeySwitch)
        {
            int numberKeyCount = Mathf.Min(9, weapons.Count);
            for (int i = 0; i < numberKeyCount; i++)
            {
                KeyCode key = KeyCode.Alpha1 + i;
                if (Input.GetKeyDown(key))
                {
                    EquipWeaponByIndex(i);
                    return;
                }
            }
        }

        if (enableMouseWheelSwitch)
        {
            float scroll = Input.GetAxis("Mouse ScrollWheel");
            if (scroll > 0.01f)
            {
                SelectNextWeapon();
            }
            else if (scroll < -0.01f)
            {
                SelectPreviousWeapon();
            }
        }
    }

    void ToggleInventoryWindow()
    {
        if (!HasInventoryUI())
        {
            Debug.LogWarning("Inventory UI is not assigned on WeaponManager.", this);
            return;
        }

        if (isInventoryTransitioning)
            return;

        if (IsInventoryOpen())
            CloseInventoryWindow();
        else
            OpenInventoryWindow();
    }

    void OpenInventoryWindow()
    {
        if (!HasInventoryUI())
            return;

        if (isInventoryTransitioning)
            return;

        if (inventoryTransitionRoutine != null)
            StopCoroutine(inventoryTransitionRoutine);

        inventoryTransitionRoutine = StartCoroutine(OpenInventorySequence());
    }

    System.Collections.IEnumerator OpenInventorySequence()
    {
        isInventoryTransitioning = true;
        inventoryOpenState = true;
        hadWeaponWhenInventoryOpened = ActiveWeapon != null;

        CacheControlRestoreState();
        ApplyInventoryControlLock(true);
        SetCursorForInventory(true);

        if (inventoryTabletObject != null)
            inventoryTabletObject.SetActive(true);

        EnsureInventoryWorldSpaceInputReady();
        SetInventoryAnimationOpen(true);

        // Re-bind UI Toolkit elements after enabling the tablet hierarchy/UIDocument.
        yield return null;
        ResolveInventoryElements();
        BuildWeaponButtons();

        float delay = Mathf.Max(0f, inventoryOpenAnimationDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        SetInventoryVisible(true);

        inventoryTransitionRoutine = null;
        isInventoryTransitioning = false;
    }

    void CloseInventoryWindow()
    {
        if (!HasInventoryUI())
            return;

        if (isInventoryTransitioning)
            return;

        if (inventoryTransitionRoutine != null)
            StopCoroutine(inventoryTransitionRoutine);

        inventoryTransitionRoutine = StartCoroutine(CloseInventorySequence());
    }

    System.Collections.IEnumerator CloseInventorySequence()
    {
        isInventoryTransitioning = true;

        SetInventoryVisible(false);
        SetInventoryAnimationOpen(false);

        float delay = Mathf.Max(0f, inventoryCloseAnimationDelay);
        if (delay > 0f)
            yield return new WaitForSeconds(delay);

        if (inventoryTabletObject != null)
            inventoryTabletObject.SetActive(false);

        ApplyInventoryControlLock(false);
        RestoreWeaponHoldAfterInventoryClose();
        SetCursorForInventory(false);
        inventoryOpenState = false;
        hadWeaponWhenInventoryOpened = false;

        inventoryTransitionRoutine = null;
        isInventoryTransitioning = false;
    }

    void BuildWeaponButtons()
    {
        ResolveInventoryElements();

        if (weaponListElement != null)
        {
            BuildUIToolkitWeaponButtons();
            return;
        }

        if (weaponButtonContainer == null || weaponButtonPrefab == null)
            return;

        for (int i = weaponButtonContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(weaponButtonContainer.GetChild(i).gameObject);
        }

        for (int i = 0; i < weapons.Count; i++)
        {
            WeaponSpawnEntry entry = weapons[i];
            if (entry == null || entry.prefab == null)
                continue;

            UIButton button = Instantiate(weaponButtonPrefab, weaponButtonContainer);
            int capturedIndex = i;

            TMP_Text tmpTextComponent = button.GetComponentInChildren<TMP_Text>();
            if (tmpTextComponent != null)
            {
                tmpTextComponent.text = entry.GetLabel();
            }
            else
            {
                Text textComponent = button.GetComponentInChildren<Text>();
                if (textComponent != null)
                {
                    textComponent.text = entry.GetLabel();
                }
            }

            button.onClick.AddListener(() => OnWeaponButtonClicked(capturedIndex));
        }
    }

    void BuildUIToolkitWeaponButtons()
    {
        UnbindUIToolkitWeaponButtons();
        boundWeaponButtons.AddRange(weaponListElement.Query<UIToolkitButton>().ToList());

        if (boundWeaponButtons.Count == 0)
        {
            Debug.LogWarning("No UI Toolkit Button elements found under weapon-list to bind.", this);
            return;
        }

        int bindCount = Mathf.Min(boundWeaponButtons.Count, weapons.Count);
        for (int i = 0; i < bindCount; i++)
        {
            if (weapons[i] == null || weapons[i].prefab == null)
                continue;

            boundWeaponButtons[i].RegisterCallback<ClickEvent>(OnDesignedWeaponButtonClicked);
        }

        if (boundWeaponButtons.Count != weapons.Count)
        {
            Debug.LogWarning($"Weapon button count ({boundWeaponButtons.Count}) does not match weapon count ({weapons.Count}). Bound by index where available.", this);
        }
    }

    void UnbindUIToolkitWeaponButtons()
    {
        for (int i = 0; i < boundWeaponButtons.Count; i++)
        {
            if (boundWeaponButtons[i] != null)
                boundWeaponButtons[i].UnregisterCallback<ClickEvent>(OnDesignedWeaponButtonClicked);
        }

        boundWeaponButtons.Clear();
    }

    void ResolveInventoryElements()
    {
        inventoryRootElement = null;
        weaponListElement = null;

        if (inventoryDocument == null || inventoryDocument.rootVisualElement == null)
            return;

        VisualElement root = inventoryDocument.rootVisualElement;

        if (!string.IsNullOrWhiteSpace(inventoryRootElementName))
            inventoryRootElement = root.Q<VisualElement>(inventoryRootElementName);

        if (inventoryRootElement == null)
            inventoryRootElement = root;

        if (!string.IsNullOrWhiteSpace(inventoryListElementName))
            weaponListElement = root.Q<VisualElement>(inventoryListElementName);

        if (weaponListElement == null && inventoryRootElement != null && !string.IsNullOrWhiteSpace(inventoryListElementName))
            weaponListElement = inventoryRootElement.Q<VisualElement>(inventoryListElementName);
    }

    bool HasUIToolkitInventory()
    {
        return inventoryDocument != null;
    }

    bool HasInventoryUI()
    {
        return inventoryDocument != null || inventoryWindow != null;
    }

    bool IsInventoryOpen()
    {
        if (inventoryOpenState)
            return true;

        if (inventoryDocument != null)
        {
            ResolveInventoryElements();
            if (inventoryRootElement != null)
                return inventoryRootElement.style.display != DisplayStyle.None;

            return false;
        }

        return inventoryWindow != null && inventoryWindow.activeSelf;
    }

    public bool IsShootingBlockedByInventory()
    {
        return inventoryControlsLocked && disableShootingWhileInventoryOpen;
    }

    void SetInventoryVisible(bool isVisible)
    {
        if (inventoryDocument != null)
        {
            ResolveInventoryElements();
            if (inventoryRootElement != null)
            {
                inventoryRootElement.style.display = isVisible ? DisplayStyle.Flex : DisplayStyle.None;
                return;
            }
        }

        if (inventoryWindow != null)
            inventoryWindow.SetActive(isVisible);
    }

    void ResolveRuntimeReferences()
    {
        if (playerController == null)
            playerController = GetComponentInParent<FirstPersonController>();

        if (inventoryArmsAnimator == null && playerController != null && playerController.armsAnimatorBridge != null)
            inventoryArmsAnimator = playerController.armsAnimatorBridge.armsAnimator;

        if (inventoryTabletObject == null && playerController != null)
        {
            Transform tabletTransform = FindChildRecursive(playerController.transform, "Tablet");
            if (tabletTransform != null)
                inventoryTabletObject = tabletTransform.gameObject;
        }
    }

    void EnsureInventoryWorldSpaceInputReady()
    {
        if (inventoryDocument == null)
            return;

        ResolveRuntimeReferences();
        EnsureWorldSpaceColliderAssigned();
        EnsurePhysicsRaycasterOnInventoryCamera();

        if (!Physics.queriesHitTriggers)
        {
            if (autoEnableTriggerRaycasts)
            {
                Physics.queriesHitTriggers = true;
            }
            else
            {
                Debug.LogWarning("Physics.queriesHitTriggers is disabled. Trigger world-space inventory colliders will not receive pointer raycasts.", this);
            }
        }

        if (EventSystem.current == null)
            Debug.LogWarning("No active EventSystem found. World-space inventory UI will not be interactable.", this);
    }

    void EnsureWorldSpaceColliderAssigned()
    {
        Collider collider = inventoryDocument.GetComponent<Collider>();
        if (collider == null)
            collider = inventoryDocument.GetComponentInChildren<Collider>(true);

        if (collider == null)
        {
            BoxCollider boxCollider = inventoryDocument.gameObject.AddComponent<BoxCollider>();
            boxCollider.isTrigger = true;
            boxCollider.size = new Vector3(1f, 1f, 0.01f);
            collider = boxCollider;
            Debug.LogWarning("No world-space collider found for inventory UIDocument. Added a BoxCollider automatically.", this);
        }

        // Older Unity versions expose the world-space collider only in serialized data,
        // not as a runtime UIDocument API property. Keeping a collider on/under the
        // UIDocument object is the compatible fallback for pointer raycasts.
    }

    void EnsurePhysicsRaycasterOnInventoryCamera()
    {
        Camera inventoryCamera = null;
        if (playerController != null && playerController.playerCamera != null)
            inventoryCamera = playerController.playerCamera;

        if (inventoryCamera == null)
            inventoryCamera = Camera.main;

        if (inventoryCamera == null)
        {
            Debug.LogWarning("No camera found for inventory UI world-space raycasts.", this);
            return;
        }

        if (inventoryCamera.GetComponent<PhysicsRaycaster>() == null)
        {
            inventoryCamera.gameObject.AddComponent<PhysicsRaycaster>();
            Debug.LogWarning("Added PhysicsRaycaster to inventory camera for world-space UI interaction.", inventoryCamera);
        }
    }

    static Transform FindChildRecursive(Transform parent, string childName)
    {
        if (parent == null || string.IsNullOrWhiteSpace(childName))
            return null;

        for (int i = 0; i < parent.childCount; i++)
        {
            Transform child = parent.GetChild(i);
            if (child.name == childName)
                return child;

            Transform nested = FindChildRecursive(child, childName);
            if (nested != null)
                return nested;
        }

        return null;
    }

    void CacheAnimatorHashes()
    {
        if (string.IsNullOrWhiteSpace(inventoryOpenBoolParameter))
            inventoryOpenBoolHash = 0;
        else
            inventoryOpenBoolHash = Animator.StringToHash(inventoryOpenBoolParameter);
    }

    void CacheControlRestoreState()
    {
        ResolveRuntimeReferences();

        hasCachedControllerState = false;
        if (playerController == null)
            return;

        previousPlayerCanMove = playerController.playerCanMove;
        previousCameraCanMove = playerController.cameraCanMove;
        hasCachedControllerState = true;
    }

    void ApplyInventoryControlLock(bool lockControls)
    {
        inventoryControlsLocked = lockControls;
        ResolveRuntimeReferences();

        if (playerController != null)
        {
            if (disableMovementWhileInventoryOpen)
                playerController.playerCanMove = lockControls ? false : (hasCachedControllerState ? previousPlayerCanMove : true);

            if (disableLookWhileInventoryOpen)
                playerController.cameraCanMove = lockControls ? false : (hasCachedControllerState ? previousCameraCanMove : true);

            if (forceExitAimWhileInventoryOpen)
            {
                if (playerController.armsAnimatorBridge != null)
                {
                    playerController.armsAnimatorBridge.SetAimState(false);
                    playerController.armsAnimatorBridge.SetSprintState(false);
                }
            }
        }

        if (disableShootingWhileInventoryOpen)
        {
            ApplyWeaponInventoryInputLock(ActiveWeapon, lockControls);
        }

        if (playerController != null)
            playerController.SetCrosshairVisible(!lockControls);
    }

    void RestoreWeaponHoldAfterInventoryClose()
    {
        if (!hadWeaponWhenInventoryOpened || ActiveWeapon == null)
            return;

        ResolveRuntimeReferences();
        if (playerController == null || playerController.armsAnimatorBridge == null)
            return;

        playerController.armsAnimatorBridge.SetAimState(false);
        playerController.armsAnimatorBridge.SetSprintState(false);
        playerController.armsAnimatorBridge.ForceHoldCurrentWeapon();
    }

    void ApplyWeaponInventoryInputLock(WeaponHandler weapon, bool lockInput)
    {
        if (weapon == null)
            return;

        weapon.SetInventoryInputBlocked(lockInput);

        if (lockInput && forceExitAimWhileInventoryOpen)
        {
            weapon.SetAimState(false);
            weapon.SetSprintState(false);
            weapon.SetMoveState(false);
        }
    }

    void SetCursorForInventory(bool isOpen)
    {
        if (isOpen)
        {
            previousCursorLockState = UnityEngine.Cursor.lockState;
            previousCursorVisible = UnityEngine.Cursor.visible;

            UnityEngine.Cursor.lockState = CursorLockMode.None;
            UnityEngine.Cursor.visible = true;
            return;
        }

        UnityEngine.Cursor.lockState = previousCursorLockState;
        UnityEngine.Cursor.visible = previousCursorVisible;
    }

    void SetInventoryAnimationOpen(bool isOpen)
    {
        if (inventoryArmsAnimator == null || inventoryOpenBoolHash == 0)
            return;

        if (!HasAnimatorBoolParameter(inventoryArmsAnimator, inventoryOpenBoolHash))
            return;

        inventoryArmsAnimator.SetBool(inventoryOpenBoolHash, isOpen);
    }

    static bool HasAnimatorBoolParameter(Animator animator, int hash)
    {
        if (animator == null)
            return false;

        AnimatorControllerParameter[] parameters = animator.parameters;
        for (int i = 0; i < parameters.Length; i++)
        {
            AnimatorControllerParameter parameter = parameters[i];
            if (parameter.nameHash == hash && parameter.type == AnimatorControllerParameterType.Bool)
                return true;
        }

        return false;
    }

    void OnWeaponButtonClicked(int index)
    {
        EquipWeaponByIndex(index);

        if (closeMenuAfterSelection)
            CloseInventoryWindow();
    }

    void OnDesignedWeaponButtonClicked(ClickEvent evt)
    {
        UIToolkitButton button = evt.currentTarget as UIToolkitButton;
        if (button == null)
            return;

        int index = boundWeaponButtons.IndexOf(button);
        if (index < 0 || index >= weapons.Count)
            return;

        WeaponSpawnEntry entry = weapons[index];
        if (entry == null || entry.prefab == null)
            return;

        OnWeaponButtonClicked(index);
    }

    void SetActiveWeapon(WeaponHandler weapon, int index)
    {
        ActiveWeapon = weapon;
        ActiveWeaponIndex = index;
        ApplyWeaponInventoryInputLock(ActiveWeapon, inventoryControlsLocked && disableShootingWhileInventoryOpen);
        ActiveWeaponChanged?.Invoke(ActiveWeapon);
    }

    void CleanupWeapons()
    {
        weapons.RemoveAll(entry => entry == null || entry.prefab == null);
    }

    void MigrateLegacyWeaponPrefabsIfNeeded()
    {
        if (weapons.Count > 0 || weaponPrefabs.Count == 0)
            return;

        for (int i = 0; i < weaponPrefabs.Count; i++)
        {
            WeaponHandler legacyPrefab = weaponPrefabs[i];
            if (legacyPrefab == null)
                continue;

            weapons.Add(new WeaponSpawnEntry
            {
                prefab = legacyPrefab,
                displayName = legacyPrefab.name,
                localPosition = Vector3.zero,
                localEulerAngles = Vector3.zero,
                localScale = Vector3.one
            });
        }

        weaponPrefabs.Clear();
    }
}
