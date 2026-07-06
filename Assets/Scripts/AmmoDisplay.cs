using UnityEngine;
using UnityEngine.UI;

public class AmmoDisplay : MonoBehaviour
{
    public WeaponManager weaponManager;
    public WeaponHandler weapon;
    public Text ammoText;
    public bool showOutOfAmmoText = true;
    public string outOfAmmoLabel = "OUT OF AMMO";
    public string noWeaponLabel = "--/--";

    int lastMag = -1;
    int lastReserve = -1;

    void Awake()
    {
        if (ammoText == null)
        {
            ammoText = GetComponent<Text>();
        }
    }

    void OnEnable()
    {
        if (weaponManager != null)
        {
            weaponManager.ActiveWeaponChanged += OnActiveWeaponChanged;
            SetWeapon(weaponManager.ActiveWeapon);
        }
        else if (weapon != null)
        {
            weapon.AmmoChanged += OnAmmoChanged;
        }

        RefreshText(true);
    }

    void OnDisable()
    {
        if (weaponManager != null)
        {
            weaponManager.ActiveWeaponChanged -= OnActiveWeaponChanged;
        }

        if (weapon != null)
        {
            weapon.AmmoChanged -= OnAmmoChanged;
        }
    }

    void Update()
    {
        if (weapon == null)
            return;

        if (weapon.CurrentMagazineAmmo != lastMag || weapon.ReserveAmmo != lastReserve)
        {
            RefreshText(true);
        }
    }

    public void SetWeapon(WeaponHandler newWeapon)
    {
        if (weapon == newWeapon)
        {
            RefreshText(true);
            return;
        }

        if (weapon != null)
        {
            weapon.AmmoChanged -= OnAmmoChanged;
        }

        weapon = newWeapon;

        if (weapon != null && isActiveAndEnabled)
        {
            weapon.AmmoChanged += OnAmmoChanged;
        }

        RefreshText(true);
    }

    public void SetWeaponManager(WeaponManager newWeaponManager)
    {
        if (weaponManager == newWeaponManager)
        {
            RefreshText(true);
            return;
        }

        if (weaponManager != null)
        {
            weaponManager.ActiveWeaponChanged -= OnActiveWeaponChanged;
        }

        weaponManager = newWeaponManager;

        if (weaponManager != null && isActiveAndEnabled)
        {
            weaponManager.ActiveWeaponChanged += OnActiveWeaponChanged;
            SetWeapon(weaponManager.ActiveWeapon);
        }
        else
        {
            RefreshText(true);
        }
    }

    void OnActiveWeaponChanged(WeaponHandler activeWeapon)
    {
        SetWeapon(activeWeapon);
    }

    void OnAmmoChanged(int currentMag, int reserve)
    {
        UpdateText(currentMag, reserve);
    }

    void RefreshText(bool force)
    {
        if (ammoText == null)
            return;

        if (weapon == null)
        {
            ammoText.text = noWeaponLabel;
            lastMag = -1;
            lastReserve = -1;
            return;
        }

        int currentMag = weapon.CurrentMagazineAmmo;
        int reserve = weapon.ReserveAmmo;

        if (!force && currentMag == lastMag && reserve == lastReserve)
            return;

        UpdateText(currentMag, reserve);
    }

    void UpdateText(int currentMag, int reserve)
    {
        lastMag = currentMag;
        lastReserve = reserve;

        if (ammoText == null)
            return;

        if (showOutOfAmmoText && currentMag <= 0 && reserve <= 0)
        {
            ammoText.text = outOfAmmoLabel + " 0/0";
        }
        else
        {
            ammoText.text = currentMag + "/" + reserve;
        }
    }
}
