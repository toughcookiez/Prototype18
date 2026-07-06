using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class CreateInventoryWindowTool
{
    const string ButtonPrefabPath = "Assets/Prefabs/UI/WeaponButtonTMP.prefab";

    [MenuItem("Tools/Prototype18/Create Inventory Window (TMP)")]
    public static void CreateInventoryWindow()
    {
        Canvas canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            GameObject canvasGO = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
        }

        EnsureEventSystemExists();

        GameObject inventoryWindow = new GameObject("InventoryWindow", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
        inventoryWindow.transform.SetParent(canvas.transform, false);

        RectTransform windowRect = inventoryWindow.GetComponent<RectTransform>();
        windowRect.anchorMin = new Vector2(0.5f, 0.5f);
        windowRect.anchorMax = new Vector2(0.5f, 0.5f);
        windowRect.pivot = new Vector2(0.5f, 0.5f);
        windowRect.sizeDelta = new Vector2(640f, 500f);
        windowRect.anchoredPosition = Vector2.zero;

        Image windowImage = inventoryWindow.GetComponent<Image>();
        windowImage.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);

        GameObject titleGO = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
        titleGO.transform.SetParent(inventoryWindow.transform, false);
        RectTransform titleRect = titleGO.GetComponent<RectTransform>();
        titleRect.anchorMin = new Vector2(0f, 1f);
        titleRect.anchorMax = new Vector2(1f, 1f);
        titleRect.pivot = new Vector2(0.5f, 1f);
        titleRect.sizeDelta = new Vector2(-40f, 70f);
        titleRect.anchoredPosition = new Vector2(0f, -20f);

        TextMeshProUGUI titleText = titleGO.GetComponent<TextMeshProUGUI>();
        titleText.text = "SELECT WEAPON";
        titleText.fontSize = 36f;
        titleText.alignment = TextAlignmentOptions.Center;
        titleText.color = Color.white;

        GameObject scrollView = CreateScrollView(inventoryWindow.transform);
        Transform content = scrollView.transform.Find("Viewport/Content");

        SetupContentLayout(content.GetComponent<RectTransform>());

        Button buttonPrefab = EnsureButtonPrefab();

        inventoryWindow.SetActive(false);

        WeaponManager weaponManager = Object.FindObjectOfType<WeaponManager>();
        if (weaponManager != null)
        {
            Undo.RecordObject(weaponManager, "Assign Inventory Window References");
            weaponManager.inventoryWindow = inventoryWindow;
            weaponManager.weaponButtonContainer = content;
            weaponManager.weaponButtonPrefab = buttonPrefab;
            EditorUtility.SetDirty(weaponManager);
        }

        Selection.activeGameObject = inventoryWindow;
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Inventory window created. References assigned to WeaponManager if one was found in scene.");
    }

    static void EnsureEventSystemExists()
    {
        EventSystem eventSystem = Object.FindObjectOfType<EventSystem>();
        if (eventSystem != null)
            return;

        GameObject eventSystemGO = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        Undo.RegisterCreatedObjectUndo(eventSystemGO, "Create EventSystem");
    }

    static GameObject CreateScrollView(Transform parent)
    {
        GameObject scrollView = new GameObject("WeaponScroll", typeof(RectTransform), typeof(Image), typeof(Mask), typeof(ScrollRect));
        scrollView.transform.SetParent(parent, false);

        RectTransform scrollRect = scrollView.GetComponent<RectTransform>();
        scrollRect.anchorMin = new Vector2(0f, 0f);
        scrollRect.anchorMax = new Vector2(1f, 1f);
        scrollRect.offsetMin = new Vector2(24f, 24f);
        scrollRect.offsetMax = new Vector2(-24f, -90f);

        Image scrollBg = scrollView.GetComponent<Image>();
        scrollBg.color = new Color(0.14f, 0.14f, 0.14f, 0.95f);

        Mask mask = scrollView.GetComponent<Mask>();
        mask.showMaskGraphic = true;

        GameObject viewport = new GameObject("Viewport", typeof(RectTransform), typeof(Image), typeof(Mask));
        viewport.transform.SetParent(scrollView.transform, false);

        RectTransform viewportRect = viewport.GetComponent<RectTransform>();
        viewportRect.anchorMin = Vector2.zero;
        viewportRect.anchorMax = Vector2.one;
        viewportRect.offsetMin = Vector2.zero;
        viewportRect.offsetMax = Vector2.zero;

        Image viewportImage = viewport.GetComponent<Image>();
        viewportImage.color = new Color(1f, 1f, 1f, 0.02f);

        Mask viewportMask = viewport.GetComponent<Mask>();
        viewportMask.showMaskGraphic = false;

        GameObject content = new GameObject("Content", typeof(RectTransform));
        content.transform.SetParent(viewport.transform, false);

        RectTransform contentRect = content.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 1f);
        contentRect.anchorMax = new Vector2(1f, 1f);
        contentRect.pivot = new Vector2(0.5f, 1f);
        contentRect.anchoredPosition = Vector2.zero;
        contentRect.sizeDelta = new Vector2(0f, 0f);

        ScrollRect sr = scrollView.GetComponent<ScrollRect>();
        sr.viewport = viewportRect;
        sr.content = contentRect;
        sr.horizontal = false;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 24f;

        return scrollView;
    }

    static void SetupContentLayout(RectTransform content)
    {
        VerticalLayoutGroup layout = content.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 10f;
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        ContentSizeFitter fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;
    }

    static Button EnsureButtonPrefab()
    {
        Directory.CreateDirectory("Assets/Prefabs");
        Directory.CreateDirectory("Assets/Prefabs/UI");

        Button existing = AssetDatabase.LoadAssetAtPath<Button>(ButtonPrefabPath);
        if (existing != null)
            return existing;

        GameObject root = new GameObject("WeaponButtonTMP", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.sizeDelta = new Vector2(0f, 56f);

        Image rootImage = root.GetComponent<Image>();
        rootImage.color = new Color(0.22f, 0.22f, 0.22f, 1f);

        ColorBlock colors = root.GetComponent<Button>().colors;
        colors.normalColor = new Color(0.22f, 0.22f, 0.22f, 1f);
        colors.highlightedColor = new Color(0.32f, 0.32f, 0.32f, 1f);
        colors.pressedColor = new Color(0.15f, 0.15f, 0.15f, 1f);
        colors.selectedColor = colors.highlightedColor;
        root.GetComponent<Button>().colors = colors;

        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredHeight = 56f;

        GameObject labelGO = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelGO.transform.SetParent(root.transform, false);

        RectTransform labelRect = labelGO.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(16f, 0f);
        labelRect.offsetMax = new Vector2(-16f, 0f);

        TextMeshProUGUI label = labelGO.GetComponent<TextMeshProUGUI>();
        label.text = "Weapon";
        label.fontSize = 28f;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.color = Color.white;
        label.raycastTarget = false;

        GameObject prefabAsset = PrefabUtility.SaveAsPrefabAsset(root, ButtonPrefabPath);
        Object.DestroyImmediate(root);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        return prefabAsset.GetComponent<Button>();
    }
}
