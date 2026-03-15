using System;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Zorro.UI;

namespace Peak.AP
{
    [HarmonyPatch(typeof(UIPageHandler), "Start")]
    public static class UIPageHandlerStartPatch
    {
        static void Postfix(UIPageHandler __instance)
        {
            // Only patch the pause menu handler
            var pauseMainPage = __instance.GetComponentInChildren<PauseMenuMainPage>(true);
            if (pauseMainPage == null) return;

            // Don't add twice
            if (__instance.GetComponentInChildren<ArchipelagoSettingsPage>(true) != null) return;

            // Create AP settings page GameObject as a child of the handler
            var pageGO = new GameObject("ArchipelagoSettingsPage");
            pageGO.transform.SetParent(__instance.transform, false);

            var rect = pageGO.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var page = pageGO.AddComponent<ArchipelagoSettingsPage>();
            pageGO.SetActive(false);

            // Register in _pages dictionary
            var pages = __instance._pages;
            pages[typeof(ArchipelagoSettingsPage)] = page;
        }

    }

    [HarmonyPatch(typeof(PauseMenuSettingsMenuPage), "Start")]
    public static class SettingsPageTemplatePatch
    {
        static void Postfix(PauseMenuSettingsMenuPage __instance)
        {
            if (ArchipelagoSettingsPage.TemplateInputCell != null) return;

            try
            {
                var shared = __instance.GetComponentInChildren<SharedSettingsMenu>(true);
                var content = shared?.transform.Find("Content");
                var parent = content?.Find("Parent");
                if (parent == null || parent.childCount == 0) return;

                for (int i = 0; i < parent.childCount; i++)
                {
                    var cell = parent.GetChild(i);
                    // Find a cell with TMP_InputField (for text input template)
                    if (ArchipelagoSettingsPage.TemplateInputCell == null
                        && cell.GetComponentInChildren<TMP_InputField>(true) != null)
                    {
                        ArchipelagoSettingsPage.TemplateInputCell = cell.gameObject;
                    }
                    // Find a cell with TMP_Dropdown (for dropdown template)
                    if (ArchipelagoSettingsPage.TemplateDropdownCell == null
                        && cell.GetComponentInChildren<TMP_Dropdown>(true) != null)
                    {
                        ArchipelagoSettingsPage.TemplateDropdownCell = cell.gameObject;
                    }
                    if (ArchipelagoSettingsPage.TemplateInputCell != null
                        && ArchipelagoSettingsPage.TemplateDropdownCell != null)
                        break;
                }

                Debug.Log($"[PeakPelago] Template cells captured: input={ArchipelagoSettingsPage.TemplateInputCell != null} dropdown={ArchipelagoSettingsPage.TemplateDropdownCell != null}");
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] Error capturing template cells: {ex.Message}");
            }
        }
    }

    [HarmonyPatch(typeof(PauseMenuMainPage), "Start")]
    public static class PauseMenuMainPageStartPatch
    {
        static void Postfix(PauseMenuMainPage __instance)
        {
            try
            {
                // Only show for the host
                if (!PhotonNetwork.IsMasterClient) return;

                // Don't add twice
                var existing = __instance.transform.parent?.Find("ArchipelagoButton");
                if (existing != null) return;

                // Clone the settings button for our "Archipelago" button
                var templateButton = __instance.m_settingsButton;
                if (templateButton == null) return;

                var apButtonGO = UnityEngine.Object.Instantiate(templateButton.gameObject, templateButton.transform.parent);
                apButtonGO.name = "ArchipelagoButton";

                // Destroy any localization components that would override our text
                foreach (var component in apButtonGO.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    var typeName = component.GetType().Name;
                    if (typeName.Contains("Localize") || typeName.Contains("LocalizedString"))
                    {
                        UnityEngine.Object.DestroyImmediate(component);
                    }
                }

                // Set all text components to "Archipelago"
                foreach (var tmpText in apButtonGO.GetComponentsInChildren<TextMeshProUGUI>(true))
                {
                    tmpText.text = "ARCHIPELAGO";
                }
                var textTmp = apButtonGO.GetComponentInChildren<TextMeshProUGUI>(true);
                if (textTmp != null)
                {
                    var iconObj = new GameObject("APIcon");
                    iconObj.transform.SetParent(textTmp.transform.parent, false);
                    var iconRect = iconObj.AddComponent<RectTransform>();
                    iconRect.anchorMin = new Vector2(0.5f, 0.5f);
                    iconRect.anchorMax = new Vector2(0.5f, 0.5f);
                    iconRect.pivot = new Vector2(1f, 0.5f);
                    iconRect.anchoredPosition = new Vector2(-85f, 0f);
                    iconRect.sizeDelta = new Vector2(28f, 28f);

                    var rawImg = iconObj.AddComponent<RawImage>();
                    rawImg.raycastTarget = false;

                    try
                    {
                        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                        using (var stream = assembly.GetManifestResourceStream("PeakArchipelagoPlugin.color-icon.png"))
                        {
                            if (stream != null)
                            {
                                byte[] data = new byte[stream.Length];
                                stream.Read(data, 0, data.Length);
                                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                                tex.LoadImage(data);
                                tex.filterMode = FilterMode.Bilinear;
                                rawImg.texture = tex;
                            }
                        }
                    }
                    catch { }
                }

                var panel = apButtonGO.transform.Find("Panel")?.GetComponent<Image>();
                if (panel != null) panel.color = new Color(0.180f, 0.180f, 0.180f, 1f);
                foreach (Transform child in apButtonGO.transform)
                {
                    if (child.name == "Border")
                    {
                        var img = child.GetComponent<Image>();
                        if (img != null) img.color = new Color(0.357f, 0.357f, 0.357f, 1f);
                    }
                }
                var button = apButtonGO.GetComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() =>
                {
                    var pageHandler = __instance.GetComponentInParent<UIPageHandler>();
                    if (pageHandler != null)
                    {
                        pageHandler.TransistionToPage<ArchipelagoSettingsPage>();
                    }
                });
                var quitButton = __instance.m_quitButton;
                if (quitButton != null)
                {
                    apButtonGO.transform.SetSiblingIndex(quitButton.transform.GetSiblingIndex());
                }
            }
            catch (Exception ex)
            {
                Debug.LogError($"[PeakPelago] Error adding Archipelago button to pause menu: {ex.Message}");
            }
        }
    }
}
