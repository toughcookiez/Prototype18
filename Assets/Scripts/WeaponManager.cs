using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

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
    public GameObject inventoryWindow;
    public Transform weaponButtonContainer;
    public Button weaponButtonPrefab;
    public bool closeMenuAfterSelection = true;

    public event Action<WeaponHandler> ActiveWeaponChanged;

    public WeaponHandler ActiveWeapon { get; private set; }
    public int ActiveWeaponIndex { get; private set; } = -1;

    CursorLockMode previousCursorLockState;
    bool previousCursorVisible;

    void Awake()
    {
        if (weaponMount == null)
        {
            weaponMount = transform;
        }

        MigrateLegacyWeaponPrefabsIfNeeded();
        CleanupWeapons();
        BuildWeaponButtons();

        if (inventoryWindow != null)
        {
            inventoryWindow.SetActive(false);
        }

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

        if (inventoryWindow != null && inventoryWindow.activeSelf && blockSwitchWhileMenuOpen)
            return;

        HandleSwitchInput();
    }

    void OnDestroy()
    {
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
        if (inventoryWindow == null)
        {
            Debug.LogWarning("Inventory window is not assigned on WeaponManager.", this);
            return;
        }

        if (inventoryWindow.activeSelf)
            CloseInventoryWindow();
        else
            OpenInventoryWindow();
    }

    void OpenInventoryWindow()
    {
        if (inventoryWindow == null)
            return;

        previousCursorLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        inventoryWindow.SetActive(true);
    }

    void CloseInventoryWindow()
    {
        if (inventoryWindow == null)
            return;

        inventoryWindow.SetActive(false);
        Cursor.lockState = previousCursorLockState;
        Cursor.visible = previousCursorVisible;
    }

    void BuildWeaponButtons()
    {
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

            Button button = Instantiate(weaponButtonPrefab, weaponButtonContainer);
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

    void OnWeaponButtonClicked(int index)
    {
        EquipWeaponByIndex(index);

        if (closeMenuAfterSelection)
            CloseInventoryWindow();
    }

    void SetActiveWeapon(WeaponHandler weapon, int index)
    {
        ActiveWeapon = weapon;
        ActiveWeaponIndex = index;
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
