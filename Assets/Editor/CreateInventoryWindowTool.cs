using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

public static class CreateInventoryWindowTool
{
    const string UiFolderPath = "Assets/UI/Inventory";
    const string PanelSettingsPath = UiFolderPath + "/InventoryPanelSettings.asset";
    const string UxmlPath = UiFolderPath + "/InventoryWindow.uxml";
    const string UssPath = UiFolderPath + "/InventoryWindow.uss";

    [MenuItem("Tools/Prototype18/Create Inventory Window (UI Toolkit)")]
    public static void CreateInventoryWindow()
    {
        EnsureAssetDirectories();
        PanelSettings panelSettings = EnsurePanelSettingsAsset();
        EnsureStyleSheetAsset();
        VisualTreeAsset visualTreeAsset = EnsureVisualTreeAsset();

        GameObject inventoryWindow = new GameObject("InventoryWindow", typeof(UIDocument));
        UIDocument document = inventoryWindow.GetComponent<UIDocument>();
        document.panelSettings = panelSettings;
        document.visualTreeAsset = visualTreeAsset;

        WeaponManager weaponManager = Object.FindObjectOfType<WeaponManager>();
        if (weaponManager != null)
        {
            Undo.RecordObject(weaponManager, "Assign Inventory Window References");
            weaponManager.inventoryDocument = document;
            weaponManager.inventoryRootElementName = "inventory-window";
            weaponManager.inventoryListElementName = "weapon-list";

            weaponManager.inventoryWindow = null;
            weaponManager.weaponButtonContainer = null;
            weaponManager.weaponButtonPrefab = null;
            EditorUtility.SetDirty(weaponManager);
        }

        Selection.activeGameObject = inventoryWindow;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("UI Toolkit inventory window created. References assigned to WeaponManager if one was found in scene.");
    }

    static void EnsureAssetDirectories()
    {
        Directory.CreateDirectory("Assets/UI");
        Directory.CreateDirectory(UiFolderPath);
    }

    static PanelSettings EnsurePanelSettingsAsset()
    {
        PanelSettings existing = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
        if (existing != null)
            return existing;

        PanelSettings panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        panelSettings.referenceResolution = new Vector2Int(1920, 1080);

        AssetDatabase.CreateAsset(panelSettings, PanelSettingsPath);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return panelSettings;
    }

    static void EnsureStyleSheetAsset()
    {
        if (File.Exists(UssPath))
            return;

        File.WriteAllText(UssPath,
@".inventory-window {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
    align-items: center;
    justify-content: center;
    display: none;
}

.inventory-backdrop {
    position: absolute;
    left: 0;
    top: 0;
    right: 0;
    bottom: 0;
    background-color: rgba(0, 0, 0, 0.55);
}

.inventory-card {
    width: 640px;
    max-height: 80%;
    background-color: rgba(18, 18, 18, 0.95);
    border-top-left-radius: 10px;
    border-top-right-radius: 10px;
    border-bottom-left-radius: 10px;
    border-bottom-right-radius: 10px;
    padding-left: 20px;
    padding-right: 20px;
    padding-top: 20px;
    padding-bottom: 20px;
}

.inventory-title {
    font-size: 30px;
    color: rgb(245, 245, 245);
    unity-text-align: middle-center;
    margin-bottom: 14px;
}

.weapon-list {
    flex-grow: 1;
}

.weapon-list .unity-scroll-view__content-container {
    padding-left: 8px;
    padding-right: 8px;
    padding-top: 8px;
    padding-bottom: 8px;
    gap: 10px;
}

.weapon-button {
    height: 50px;
    font-size: 20px;
    color: rgb(245, 245, 245);
    unity-text-align: middle-left;
    padding-left: 16px;
    border-top-left-radius: 8px;
    border-top-right-radius: 8px;
    border-bottom-left-radius: 8px;
    border-bottom-right-radius: 8px;
    background-color: rgb(44, 44, 44);
}

.weapon-button:hover {
    background-color: rgb(66, 66, 66);
}

.weapon-button:active {
    background-color: rgb(30, 30, 30);
}
");

        AssetDatabase.ImportAsset(UssPath, ImportAssetOptions.ForceSynchronousImport);
    }

    static VisualTreeAsset EnsureVisualTreeAsset()
    {
        VisualTreeAsset existing = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
        if (existing != null)
            return existing;

        File.WriteAllText(UxmlPath,
@"<ui:UXML xmlns:ui=""UnityEngine.UIElements"" xmlns:uie=""UnityEditor.UIElements"">
    <ui:Style src=""InventoryWindow.uss"" />
    <ui:VisualElement name=""inventory-window"" class=""inventory-window"">
        <ui:VisualElement class=""inventory-backdrop"" />
        <ui:VisualElement class=""inventory-card"">
            <ui:Label text=""SELECT WEAPON"" class=""inventory-title"" />
            <ui:ScrollView name=""weapon-list"" class=""weapon-list"" mode=""Vertical"" />
        </ui:VisualElement>
    </ui:VisualElement>
</ui:UXML>");

        AssetDatabase.ImportAsset(UxmlPath, ImportAssetOptions.ForceSynchronousImport);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
    }
}
