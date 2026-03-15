using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Zorro.UI;
using Zorro.ControllerSupport;

namespace Peak.AP
{
    public class ArchipelagoSettingsPage : UIPage, IHaveParentPage, INavigationPage
    {
        private bool _built;
        private TMP_InputField _serverField;
        private TMP_InputField _portField;
        private TMP_InputField _slotField;
        private TMP_InputField _passwordField;
        private Button _connectButton;
        private TextMeshProUGUI _connectButtonText;
        private TextMeshProUGUI _statusText;
        private Button _backButton;
        private Button _connectionTabBtn;
        private Button _linksTabBtn;
        private RectTransform _connectionContent;
        private RectTransform _linksContent;

        private const string PREFS_SERVER = "PeakPelago_ServerUrl";
        private const string PREFS_PORT = "PeakPelago_Port";
        private const string PREFS_SLOT = "PeakPelago_SlotName";
        private const string PREFS_PASSWORD = "PeakPelago_Password";

        // Cached references for styling
        private TMP_FontAsset _font;
        private ColorBlock _buttonColors;
        private Sprite _buttonSprite;
        private SpriteState _buttonSpriteState;
        private Sprite _blurSprite;
        private Material _blurMaterial;
        private Sprite _blurOutlineSprite; // UI_Blur_Outlne_Thick
        private Sprite _arrowSprite;       // Reticle_Climb (dropdown arrow)

        // Template cells cloned from the settings page
        public static GameObject TemplateInputCell;
        public static GameObject TemplateDropdownCell;

        public override void OnPageEnter()
        {
            base.OnPageEnter();
            if (!_built)
            {
                // If templates aren't ready yet, try to trigger settings page init
                if (TemplateInputCell == null || TemplateDropdownCell == null)
                {
                    var handler = GetComponentInParent<UIPageHandler>();
                    var settingsPage = handler?.GetComponentInChildren<PauseMenuSettingsMenuPage>(true);
                    if (settingsPage != null)
                    {
                        settingsPage.gameObject.SetActive(true);
                        settingsPage.gameObject.SetActive(false);
                    }
                }
                BuildUI();
            }
            // Delay LoadSettings by one frame so layout calculates first
            StartCoroutine(LoadSettingsDelayed());
            // Animate rows in
            var activeContent = _connectionContent.gameObject.activeSelf ? _connectionContent : _linksContent;
            StartCoroutine(AnimateRowsIn(activeContent));
            UpdateStatus();
        }

        private System.Collections.IEnumerator LoadSettingsDelayed()
        {
            yield return null;
            LoadSettings();
        }

        private System.Collections.IEnumerator AnimateRowsIn(RectTransform container)
        {
            // Add CanvasGroup to each row and start invisible
            var rows = new System.Collections.Generic.List<CanvasGroup>();
            for (int i = 0; i < container.childCount; i++)
            {
                var child = container.GetChild(i);
                var cg = child.GetComponent<CanvasGroup>();
                if (cg == null) cg = child.gameObject.AddComponent<CanvasGroup>();
                cg.alpha = 0f;
                rows.Add(cg);
            }

            // Stagger fade-in
            foreach (var cg in rows)
            {
                float elapsed = 0f;
                float duration = 0.15f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime;
                    cg.alpha = Mathf.Clamp01(elapsed / duration);
                    yield return null;
                }
                cg.alpha = 1f;
            }
        }

        private void Update()
        {
            if (_built) UpdateStatus();
        }

        private void UpdateStatus()
        {
            var plugin = PeakArchipelagoPlugin._instance;
            if (plugin == null) return;

            bool connected = plugin.Status == "Connected";
            bool connecting = plugin._isConnecting;

            if (_connectButtonText != null)
            {
                if (connected)
                    _connectButtonText.text = "Disconnect";
                else if (connecting)
                    _connectButtonText.text = "Connecting...";
                else
                    _connectButtonText.text = "Connect";
            }

            if (_statusText != null)
            {
                _statusText.text = plugin.Status;
                _statusText.color = connected ? new Color(0.4f, 1f, 0.4f) :
                                    connecting ? new Color(1f, 1f, 0.4f) :
                                    new Color(1f, 1f, 1f, 0.6f);
            }

            // Disable connect button while connecting
            if (_connectButton != null)
                _connectButton.interactable = !connecting;
        }

        private void GrabStyling()
        {
            var handler = GetComponentInParent<UIPageHandler>();
            var settingsPage = handler?.GetComponentInChildren<PauseMenuSettingsMenuPage>(true);
            if (settingsPage != null)
            {
                var shared = settingsPage.GetComponentInChildren<SharedSettingsMenu>(true);
                var header = shared?.transform.Find("Header")?.GetComponent<TextMeshProUGUI>();
                if (header != null)
                    _font = header.font;
                else
                {
                    var firstTMP = settingsPage.GetComponentInChildren<TextMeshProUGUI>(true);
                    if (firstTMP != null) _font = firstTMP.font;
                }
            }

            // Fallback: grab any TMP font
            if (_font == null)
            {
                var existingTMP = handler?.GetComponentInChildren<TextMeshProUGUI>(true);
                if (existingTMP != null)
                    _font = existingTMP.font;
            }

            // Find UI sprites from existing Images
            foreach (var img in Resources.FindObjectsOfTypeAll<Image>())
            {
                if (img.sprite == null) continue;
                if (img.sprite.name == "UI_Blur" && _blurSprite == null)
                {
                    _blurSprite = img.sprite;
                    _blurMaterial = img.material;
                }
                else if (img.sprite.name == "UI_Blur_Outlne_Thick" && _blurOutlineSprite == null)
                {
                    _blurOutlineSprite = img.sprite;
                }
                else if (img.sprite.name == "Reticle_Climb" && _arrowSprite == null)
                {
                    _arrowSprite = img.sprite;
                }
                if (_blurSprite != null && _blurOutlineSprite != null && _arrowSprite != null) break;
            }

            // Find an existing button for color/sprite styling
            var mainPage = handler?.GetComponentInChildren<PauseMenuMainPage>(true);
            if (mainPage != null)
            {
                var templateBtn = mainPage.m_settingsButton;
                if (templateBtn != null)
                {
                    _buttonColors = templateBtn.colors;
                    var img = templateBtn.GetComponent<Image>();
                    if (img != null)
                    {
                        _buttonSprite = img.sprite;
                    }
                    _buttonSpriteState = templateBtn.spriteState;
                }
            }
        }

        private void BuildUI()
        {
            _built = true;
            GrabStyling();
            var titleObj = new GameObject("Header");
            titleObj.transform.SetParent(transform, false);
            var titleTMP = titleObj.AddComponent<TextMeshProUGUI>();
            titleTMP.text = "ARCHIPELAGO";
            titleTMP.fontSize = 48;
            titleTMP.fontStyle = FontStyles.Normal;
            titleTMP.color = Color.white;
            titleTMP.alignment = TextAlignmentOptions.MidlineLeft;
            if (_font != null) titleTMP.font = _font;
            titleTMP.raycastTarget = false;

            var titleRect = titleObj.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(0f, 1f);
            titleRect.pivot = new Vector2(0f, 0.5f);
            titleRect.anchoredPosition = new Vector2(30f, -59.5f);
            titleRect.sizeDelta = new Vector2(400f, 64.9f);
            titleTMP.overflowMode = TextOverflowModes.Overflow;
            titleTMP.textWrappingMode = TextWrappingModes.NoWrap;
            _backButton = CreateBackButton();
            // Content area — match settings page Content rect exactly
            var content = CreateChild(transform, "Content");
            content.anchorMin = Vector2.zero;
            content.anchorMax = Vector2.one;
            content.anchoredPosition = new Vector2(148.64f, -19.73f);
            content.sizeDelta = new Vector2(-558.73f, -101.31f);

            // Parent — match settings page Parent (VerticalLayoutGroup inside Content)
            var layoutParent = CreateChild(content, "Parent");
            layoutParent.anchorMin = Vector2.zero;
            layoutParent.anchorMax = Vector2.one;
            layoutParent.anchoredPosition = new Vector2(0f, -30.92f);
            layoutParent.sizeDelta = new Vector2(0f, -61.85f);
            var vlg = layoutParent.gameObject.AddComponent<VerticalLayoutGroup>();
            vlg.spacing = 4;
            vlg.childAlignment = TextAnchor.UpperCenter;
            vlg.childControlWidth = true;
            vlg.childControlHeight = false;
            vlg.childForceExpandWidth = true;
            vlg.childForceExpandHeight = false;

            // Tabs — Connection and Links, matching settings page TABS layout
            var tabsRect = CreateChild(content, "TABS");
            tabsRect.anchorMin = new Vector2(0f, 1f);
            tabsRect.anchorMax = new Vector2(1f, 1f);
            tabsRect.anchoredPosition = Vector2.zero;
            tabsRect.sizeDelta = new Vector2(0f, 40f);
            var tabsHLG = tabsRect.gameObject.AddComponent<HorizontalLayoutGroup>();
            tabsHLG.spacing = 8;
            tabsHLG.childControlWidth = true;
            tabsHLG.childControlHeight = true;
            tabsHLG.childForceExpandWidth = true;
            tabsHLG.childForceExpandHeight = true;

            _connectionTabBtn = CreateTabButton(tabsRect, "CONNECTION");
            _linksTabBtn = CreateTabButton(tabsRect, "LINKS");

            // --- Connection tab content ---
            _connectionContent = layoutParent;
            _serverField = CreateInputRow(layoutParent, "SERVER", "archipelago.gg");
            _portField = CreateInputRow(layoutParent, "PORT", "38281");
            _slotField = CreateInputRow(layoutParent, "SLOT NAME", "");
            _passwordField = CreateInputRow(layoutParent, "PASSWORD", "");
            _passwordField.contentType = TMP_InputField.ContentType.Password;
            CreateSpacer(layoutParent, 16);
            _statusText = CreateLabel(layoutParent, "Status", "Disconnected", 22, TextAlignmentOptions.Center);
            CreateSpacer(layoutParent, 8);
            _connectButton = CreateConnectButton(layoutParent);

            // --- Links tab content (separate parent, initially hidden) ---
            _linksContent = CreateChild(content, "LinksParent");
            _linksContent.anchorMin = Vector2.zero;
            _linksContent.anchorMax = Vector2.one;
            _linksContent.anchoredPosition = new Vector2(0f, -30.92f);
            _linksContent.sizeDelta = new Vector2(0f, -61.85f);
            var linksVLG = _linksContent.gameObject.AddComponent<VerticalLayoutGroup>();
            linksVLG.spacing = 4;
            linksVLG.childAlignment = TextAnchor.UpperCenter;
            linksVLG.childControlWidth = true;
            linksVLG.childControlHeight = false;
            linksVLG.childForceExpandWidth = true;
            linksVLG.childForceExpandHeight = false;

            // Link toggle rows — all always interactable, send ConnectUpdatePacket on change
            CreateLinkToggleRow(_linksContent, "DEATH LINK", () =>
            {
                var p = PeakArchipelagoPlugin._instance;
                return p != null && p._deathLinkEnabled && p.cfgDeathLinkEnabled.Value;
            }, (val) =>
            {
                var p = PeakArchipelagoPlugin._instance;
                if (p == null) return;
                p._deathLinkEnabled = val;
                p.cfgDeathLinkEnabled.Value = val;
                if (val)
                    p._deathLinkService?.EnableDeathLink();
                else
                    p._deathLinkService?.DisableDeathLink();
                p.SendUpdatedLinkTags();
            });

            CreateLinkToggleRow(_linksContent, "RING LINK", () =>
            {
                return PeakArchipelagoPlugin._instance?._ringLinkEnabled ?? false;
            }, (val) =>
            {
                var p = PeakArchipelagoPlugin._instance;
                if (p == null) return;
                p._ringLinkEnabled = val;
                p._ringLinkService?.SetEnabled(val);
                p.SendUpdatedLinkTags();
            });

            CreateLinkToggleRow(_linksContent, "HARD RING LINK", () =>
            {
                return PeakArchipelagoPlugin._instance?._hardRingLinkEnabled ?? false;
            }, (val) =>
            {
                var p = PeakArchipelagoPlugin._instance;
                if (p == null) return;
                p._hardRingLinkEnabled = val;
                p._hardRingLinkService?.SetEnabled(val);
                p.SendUpdatedLinkTags();
            });

            CreateLinkToggleRow(_linksContent, "TRAP LINK", () =>
            {
                return PeakArchipelagoPlugin._instance?._trapLinkEnabled ?? false;
            }, (val) =>
            {
                var p = PeakArchipelagoPlugin._instance;
                if (p == null) return;
                p._trapLinkEnabled = val;
                p._trapLinkService?.SetEnabled(val);
                p.SendUpdatedLinkTags();
            });

            CreateLinkToggleRow(_linksContent, "BREATH LINK", () =>
            {
                return PeakArchipelagoPlugin._instance?._breathLinkEnabled ?? false;
            }, (val) =>
            {
                var p = PeakArchipelagoPlugin._instance;
                if (p == null) return;
                p._breathLinkEnabled = val;
                p._breathLinkService?.SetEnabled(val);
                p.SendUpdatedLinkTags();
            });

            _linksContent.gameObject.SetActive(false);

            // Wire tab buttons
            _connectionTabBtn.onClick.AddListener(() => SwitchTab(true));
            _linksTabBtn.onClick.AddListener(() => SwitchTab(false));
            _connectButtonText = _connectButton.GetComponentInChildren<TextMeshProUGUI>();

            // Start on Connection tab (must be after both content panels exist)
            SwitchTab(true);
        }

        private void LoadSettings()
        {
            if (_serverField != null)
                _serverField.text = PlayerPrefs.GetString(PREFS_SERVER, "archipelago.gg");
            if (_portField != null)
                _portField.text = PlayerPrefs.GetString(PREFS_PORT, "38281");
            if (_slotField != null)
                _slotField.text = PlayerPrefs.GetString(PREFS_SLOT, "");
            if (_passwordField != null)
                _passwordField.text = PlayerPrefs.GetString(PREFS_PASSWORD, "");
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetString(PREFS_SERVER, _serverField.text);
            PlayerPrefs.SetString(PREFS_PORT, _portField.text);
            PlayerPrefs.SetString(PREFS_SLOT, _slotField.text);
            PlayerPrefs.SetString(PREFS_PASSWORD, _passwordField.text);
            PlayerPrefs.Save();
        }

        private void OnConnectClicked()
        {
            var plugin = PeakArchipelagoPlugin._instance;
            if (plugin == null) return;

            if (plugin.Status == "Connected")
            {
                plugin._intentionalDisconnect = true;
                plugin.Session?.Socket?.Disconnect();
                plugin._status = "Disconnected";
            }
            else
            {
                SaveSettings();
                plugin.SetConnectionDetails(_serverField.text, _portField.text, _slotField.text, _passwordField.text);
                plugin.Connect();
            }
        }

        private void OnBackClicked()
        {
            pageHandler.TransistionToPage<PauseMenuMainPage>();
        }

        public (UIPage, PageTransistion) GetParentPage()
        {
            return (pageHandler.GetPage<PauseMenuMainPage>(), new SetActivePageTransistion());
        }

        public GameObject GetFirstSelectedGameObject()
        {
            return _serverField != null ? _serverField.gameObject :
                   _backButton != null ? _backButton.gameObject : gameObject;
        }

        private void CreateLinkToggleRow(RectTransform parent, string label,
            System.Func<bool> getCurrent, System.Action<bool> setCurrent)
        {
            TMP_Dropdown dropdown = null;

            if (TemplateDropdownCell != null)
            {
                // Clone the real SettingsCell with Dropdown
                var clone = Instantiate(TemplateDropdownCell, parent);
                clone.name = label + "Row";

                // Destroy settings-specific components
                foreach (var comp in clone.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    var typeName = comp.GetType().Name;
                    if (typeName.Contains("Localize") || typeName.Contains("SettingUI")
                        || typeName.Contains("EnumSettingUI") || typeName == "SettingsUICell")
                        DestroyImmediate(comp);
                }

                // Set label
                var labelTMP = clone.transform.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>();
                if (labelTMP != null) labelTMP.text = label;

                // Hide "OnlyOnMainMenu"
                var onlyMain = clone.transform.Find("OnlyOnMainMenu");
                if (onlyMain != null) onlyMain.gameObject.SetActive(false);

                // Configure dropdown with ON/OFF options
                dropdown = clone.GetComponentInChildren<TMP_Dropdown>(true);
                if (dropdown != null)
                {
                    dropdown.ClearOptions();
                    dropdown.AddOptions(new System.Collections.Generic.List<string> { "OFF", "ON" });
                }

                var le = clone.GetComponent<LayoutElement>();
                if (le == null) le = clone.AddComponent<LayoutElement>();
                le.preferredHeight = 90;
            }

            if (dropdown == null)
            {
                // Fallback: build a simple toggle row manually
                var row = CreateChild(parent, label + "Row");
                var rowLE = row.gameObject.AddComponent<LayoutElement>();
                rowLE.preferredHeight = 90;

                var bgRect = CreateChild(row, "Image");
                bgRect.anchorMin = Vector2.zero;
                bgRect.anchorMax = Vector2.one;
                bgRect.offsetMin = Vector2.zero;
                bgRect.offsetMax = Vector2.zero;
                var bgImg = bgRect.gameObject.AddComponent<Image>();
                bgImg.color = new Color(0.179f, 0.125f, 0.090f, 0.729f);
                ApplyBlurSprite(bgImg);

                var labelRect = CreateChild(row, "Text (TMP)");
                labelRect.anchorMin = new Vector2(0f, 0f);
                labelRect.anchorMax = new Vector2(0f, 1f);
                labelRect.pivot = new Vector2(0f, 0.5f);
                labelRect.anchoredPosition = Vector2.zero;
                labelRect.sizeDelta = new Vector2(600f, 0f);
                var labelTMP = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
                labelTMP.text = label;
                labelTMP.fontSize = 28;
                labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
                labelTMP.color = Color.white;
                if (_font != null) labelTMP.font = _font;

                var inputParent = CreateChild(row, "InputParent");
                inputParent.anchorMin = new Vector2(1f, 0f);
                inputParent.anchorMax = new Vector2(1f, 1f);
                inputParent.pivot = new Vector2(1f, 0.5f);
                inputParent.sizeDelta = new Vector2(468f, 0f);

                // Dropdown — match settings page ENUM DROPDOWN structure
                var ddRect = CreateChild(inputParent, "Dropdown");
                ddRect.anchorMin = Vector2.zero;
                ddRect.anchorMax = Vector2.one;
                ddRect.offsetMin = new Vector2(20f, 15f);
                ddRect.offsetMax = new Vector2(-20f, -15f);

                var ddImg = ddRect.gameObject.AddComponent<Image>();
                ddImg.color = Color.white;
                ApplyBlurSprite(ddImg);

                // Border
                var borderRect = CreateChild(ddRect, "Border");
                borderRect.anchorMin = Vector2.zero;
                borderRect.anchorMax = Vector2.one;
                borderRect.offsetMin = Vector2.zero;
                borderRect.offsetMax = Vector2.zero;
                var borderImg = borderRect.gameObject.AddComponent<Image>();
                borderImg.color = new Color(0.196f, 0.196f, 0.196f, 1f);
                if (_blurOutlineSprite != null) { borderImg.sprite = _blurOutlineSprite; borderImg.type = Image.Type.Sliced; borderImg.pixelsPerUnitMultiplier = 15f; }
                borderImg.raycastTarget = false;

                dropdown = ddRect.gameObject.AddComponent<TMP_Dropdown>();
                dropdown.AddOptions(new System.Collections.Generic.List<string> { "OFF", "ON" });

                // Label
                var ddLabelRect = CreateChild(ddRect, "Label");
                ddLabelRect.anchorMin = Vector2.zero;
                ddLabelRect.anchorMax = Vector2.one;
                ddLabelRect.offsetMin = new Vector2(10f, 0f);
                ddLabelRect.offsetMax = new Vector2(-60f, -5f);
                var ddLabelTMP = ddLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
                ddLabelTMP.fontSize = 26;
                ddLabelTMP.color = Color.black;
                ddLabelTMP.alignment = TextAlignmentOptions.MidlineLeft;
                if (_font != null) ddLabelTMP.font = _font;
                dropdown.captionText = ddLabelTMP;

                // Arrow
                var arrowRect = CreateChild(ddRect, "Arrow");
                arrowRect.anchorMin = new Vector2(1f, 0.5f);
                arrowRect.anchorMax = new Vector2(1f, 0.5f);
                arrowRect.anchoredPosition = new Vector2(-21.4f, 0f);
                arrowRect.sizeDelta = new Vector2(20f, 20f);
                var arrowImg = arrowRect.gameObject.AddComponent<Image>();
                arrowImg.color = new Color(0.196f, 0.196f, 0.196f, 1f);
                if (_arrowSprite != null) arrowImg.sprite = _arrowSprite;
                arrowImg.raycastTarget = false;

                // Template
                var templateRect = CreateChild(ddRect, "Template");
                templateRect.anchorMin = new Vector2(0f, 0f);
                templateRect.anchorMax = new Vector2(1f, 0f);
                templateRect.pivot = new Vector2(0.5f, 1f);
                templateRect.anchoredPosition = Vector2.zero;
                templateRect.sizeDelta = new Vector2(0f, 150f);
                var templateImg = templateRect.gameObject.AddComponent<Image>();
                templateImg.color = Color.black;
                ApplyBlurSprite(templateImg);
                var scrollRect = templateRect.gameObject.AddComponent<ScrollRect>();

                var viewportRect = CreateChild(templateRect, "Viewport");
                viewportRect.anchorMin = Vector2.zero;
                viewportRect.anchorMax = Vector2.one;
                viewportRect.offsetMin = Vector2.zero;
                viewportRect.offsetMax = new Vector2(-17f, 0f);
                viewportRect.gameObject.AddComponent<Mask>();
                var viewportImg = viewportRect.gameObject.AddComponent<Image>();
                viewportImg.color = new Color(0.085f, 0.085f, 0.085f, 1f);
                ApplyBlurSprite(viewportImg);
                scrollRect.viewport = viewportRect;

                var ddContent = CreateChild(viewportRect, "Content");
                ddContent.anchorMin = new Vector2(0f, 1f);
                ddContent.anchorMax = new Vector2(1f, 1f);
                ddContent.anchoredPosition = Vector2.zero;
                ddContent.sizeDelta = new Vector2(0f, 40f);
                scrollRect.content = ddContent;

                // Item template
                var itemRect = CreateChild(ddContent, "Item");
                itemRect.anchorMin = new Vector2(0f, 0.5f);
                itemRect.anchorMax = new Vector2(1f, 0.5f);
                itemRect.sizeDelta = new Vector2(0f, 40f);
                var itemToggle = itemRect.gameObject.AddComponent<Toggle>();

                var itemBg = CreateChild(itemRect, "Item Background");
                itemBg.anchorMin = Vector2.zero;
                itemBg.anchorMax = Vector2.one;
                itemBg.offsetMin = new Vector2(2f, 1f);
                itemBg.offsetMax = new Vector2(-2f, -1f);
                var itemBgImg = itemBg.gameObject.AddComponent<Image>();
                itemBgImg.color = Color.white;
                ApplyBlurSprite(itemBgImg);

                var itemCheck = CreateChild(itemRect, "Item Checkmark");
                itemCheck.anchorMin = new Vector2(0f, 0f);
                itemCheck.anchorMax = new Vector2(0f, 1f);
                itemCheck.anchoredPosition = new Vector2(2f, 0f);
                itemCheck.sizeDelta = new Vector2(20f, -4f);
                var checkImg = itemCheck.gameObject.AddComponent<Image>();
                checkImg.color = new Color(1f, 0.63f, 0f, 0.769f);
                itemToggle.graphic = checkImg;

                var itemLabelRect = CreateChild(itemRect, "Item Label");
                itemLabelRect.anchorMin = Vector2.zero;
                itemLabelRect.anchorMax = Vector2.one;
                itemLabelRect.offsetMin = new Vector2(20f, 0f);
                itemLabelRect.offsetMax = Vector2.zero;
                var itemLabelTMP = itemLabelRect.gameObject.AddComponent<TextMeshProUGUI>();
                itemLabelTMP.fontSize = 24;
                itemLabelTMP.color = Color.black;
                itemLabelTMP.alignment = TextAlignmentOptions.MidlineLeft;
                if (_font != null) itemLabelTMP.font = _font;
                dropdown.itemText = itemLabelTMP;

                // Scrollbar
                var scrollbarRect = CreateChild(templateRect, "Scrollbar");
                scrollbarRect.anchorMin = new Vector2(1f, 0f);
                scrollbarRect.anchorMax = new Vector2(1f, 1f);
                scrollbarRect.sizeDelta = new Vector2(20f, 0f);
                var scrollbarImg = scrollbarRect.gameObject.AddComponent<Image>();
                scrollbarImg.color = new Color(0.805f, 0.805f, 0.805f, 1f);
                ApplyBlurSprite(scrollbarImg);
                var scrollbar = scrollbarRect.gameObject.AddComponent<Scrollbar>();
                scrollbar.direction = Scrollbar.Direction.BottomToTop;
                scrollRect.verticalScrollbar = scrollbar;

                var slidingArea = CreateChild(scrollbarRect, "Sliding Area");
                slidingArea.anchorMin = Vector2.zero;
                slidingArea.anchorMax = Vector2.one;
                slidingArea.offsetMin = new Vector2(10f, 10f);
                slidingArea.offsetMax = new Vector2(-10f, -10f);

                var handleRect = CreateChild(slidingArea, "Handle");
                handleRect.anchorMin = Vector2.zero;
                handleRect.anchorMax = Vector2.one;
                handleRect.offsetMin = new Vector2(-10f, -10f);
                handleRect.offsetMax = new Vector2(10f, 10f);
                var handleImg = handleRect.gameObject.AddComponent<Image>();
                handleImg.color = Color.white;
                ApplyBlurSprite(handleImg);
                scrollbar.handleRect = handleRect;
                scrollbar.targetGraphic = handleImg;

                templateRect.gameObject.SetActive(false);
                dropdown.template = templateRect;
            }

            dropdown.SetValueWithoutNotify(getCurrent() ? 1 : 0);
            dropdown.onValueChanged.AddListener((val) => setCurrent(val == 1));

            var updater = dropdown.gameObject.AddComponent<LinkToggleUpdater>();
            updater.Initialize(dropdown, getCurrent);
        }

        private void SwitchTab(bool connectionTab)
        {
            _connectionContent.gameObject.SetActive(connectionTab);
            _linksContent.gameObject.SetActive(!connectionTab);

            var activeContent = connectionTab ? _connectionContent : _linksContent;
            StartCoroutine(AnimateRowsIn(activeContent));

            // Toggle the "Selected" child on each tab
            var connSelected = _connectionTabBtn?.transform.Find("Selected");
            var linksSelected = _linksTabBtn?.transform.Find("Selected");
            if (connSelected != null) connSelected.gameObject.SetActive(connectionTab);
            if (linksSelected != null) linksSelected.gameObject.SetActive(!connectionTab);

            // Change text color — dark when selected (white bg), white when not
            var connText = _connectionTabBtn?.GetComponentInChildren<TextMeshProUGUI>(true);
            var linksText = _linksTabBtn?.GetComponentInChildren<TextMeshProUGUI>(true);
            if (connText != null) connText.color = connectionTab ? Color.black : Color.white;
            if (linksText != null) linksText.color = !connectionTab ? Color.black : Color.white;
        }

        private Button CreateTabButton(RectTransform parent, string label)
        {
            // Try to clone a real tab from the settings page
            var handler = GetComponentInParent<UIPageHandler>();
            var settingsPage = handler?.GetComponentInChildren<PauseMenuSettingsMenuPage>(true);
            var shared = settingsPage?.GetComponentInChildren<SharedSettingsMenu>(true);
            var tabsContainer = shared?.transform.Find("Content")?.Find("TABS");

            if (tabsContainer != null && tabsContainer.childCount > 0)
            {
                // Clone the first tab button (General)
                var templateTab = tabsContainer.GetChild(0).gameObject;
                var clone = Instantiate(templateTab, parent);
                clone.name = label;

                // Destroy localization and settings-specific components
                foreach (var comp in clone.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    var typeName = comp.GetType().Name;
                    if (typeName.Contains("Localize") || typeName.Contains("SettingsTABSButton"))
                        DestroyImmediate(comp);
                }

                // Set text
                var tmpText = clone.GetComponentInChildren<TextMeshProUGUI>(true);
                if (tmpText != null) tmpText.text = label;

                var btn = clone.GetComponent<Button>();
                if (btn == null)
                {
                    // Button was lost — re-add it
                    btn = clone.AddComponent<Button>();
                    var targetImg = clone.transform.Find("Image")?.GetComponent<Image>()
                                   ?? clone.GetComponent<Image>();
                    if (targetImg != null) btn.targetGraphic = targetImg;
                }
                btn.onClick.RemoveAllListeners();

                return btn;
            }

            // Fallback
            var tabObj = new GameObject(label);
            tabObj.transform.SetParent(parent, false);
            tabObj.AddComponent<RectTransform>();
            var tabImg = tabObj.AddComponent<Image>();
            tabImg.color = new Color(1f, 1f, 1f, 0f);
            var fallbackBtn = tabObj.AddComponent<Button>();
            fallbackBtn.targetGraphic = tabImg;

            var textGO = new GameObject("Text (TMP)");
            textGO.transform.SetParent(tabObj.transform, false);
            var textRect = textGO.AddComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0.5f);
            textRect.anchorMax = new Vector2(1f, 0.5f);
            textRect.sizeDelta = new Vector2(-10f, 50f);
            var tmp = textGO.AddComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 24;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            if (_font != null) tmp.font = _font;

            return fallbackBtn;
        }

        private void ApplyBlurSprite(Image img)
        {
            if (_blurSprite == null) return;
            img.sprite = _blurSprite;
            img.type = Image.Type.Sliced;
            img.pixelsPerUnitMultiplier = 15f;
            if (_blurMaterial != null) img.material = _blurMaterial;
        }

        // --- UI Builder Helpers ---

        private RectTransform CreateChild(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var rect = go.AddComponent<RectTransform>();
            return rect;
        }

        private TextMeshProUGUI CreateLabel(RectTransform parent, string name, string text,
            int fontSize, TextAlignmentOptions alignment)
        {
            var rect = CreateChild(parent, name);
            // Only add LayoutElement if parent uses a layout group
            if (parent.GetComponent<VerticalLayoutGroup>() != null || parent.GetComponent<HorizontalLayoutGroup>() != null)
            {
                var le = rect.gameObject.AddComponent<LayoutElement>();
                le.preferredHeight = fontSize + 12;
            }

            var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            if (_font != null) tmp.font = _font;
            tmp.raycastTarget = false;
            return tmp;
        }

        private void CreateSpacer(RectTransform parent, float height)
        {
            var rect = CreateChild(parent, "Spacer");
            var le = rect.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = height;
        }

        private TMP_InputField CreateInputRow(RectTransform parent, string label, string defaultValue)
        {
            if (TemplateInputCell != null)
            {
                // Clone the real SettingsCell with InputField
                var clone = Instantiate(TemplateInputCell, parent);
                clone.name = label + "Row";

                // Destroy settings-specific components
                var settingsUI = clone.GetComponent<SettingsUICell>();
                if (settingsUI != null) DestroyImmediate(settingsUI);
                foreach (var comp in clone.GetComponentsInChildren<Component>(true))
                {
                    if (comp == null) continue;
                    var typeName = comp.GetType().Name;
                    if (typeName.Contains("Localize") || typeName.Contains("SettingUI") || typeName.Contains("FloatSettingUI"))
                        DestroyImmediate(comp);
                }

                // Set label text
                var labelTMP = clone.transform.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>();
                if (labelTMP != null) labelTMP.text = label;

                // Hide "OnlyOnMainMenu" warning
                var onlyMain = clone.transform.Find("OnlyOnMainMenu");
                if (onlyMain != null) onlyMain.gameObject.SetActive(false);

                // Remove the slider (FLOAT INPUT has both slider + input field)
                var slider = clone.GetComponentInChildren<Slider>(true);
                if (slider != null) DestroyImmediate(slider.gameObject);

                // The TMP_InputField may have lost references after destroying components
                // Find or recreate it
                var existingInput = clone.GetComponentInChildren<TMP_InputField>(true);
                if (existingInput != null)
                {
                    // Re-wire references in case they were broken
                    var textArea = existingInput.transform.Find("Text Area");
                    if (textArea != null)
                    {
                        existingInput.textViewport = textArea.GetComponent<RectTransform>();
                        var textChild = textArea.Find("Text");
                        if (textChild != null) existingInput.textComponent = textChild.GetComponent<TextMeshProUGUI>();
                        var placeholder = textArea.Find("Placeholder");
                        if (placeholder != null) existingInput.placeholder = placeholder.GetComponent<TextMeshProUGUI>();
                    }

                    existingInput.text = defaultValue;
                    existingInput.contentType = TMP_InputField.ContentType.Standard;

                    // Widen since we removed the slider — stretch to fill InputParent
                    var fieldRect = existingInput.GetComponent<RectTransform>();
                    fieldRect.anchorMin = new Vector2(0f, 0.5f);
                    fieldRect.anchorMax = new Vector2(1f, 0.5f);
                    fieldRect.anchoredPosition = Vector2.zero;
                    fieldRect.offsetMin = new Vector2(20f, -25f);
                    fieldRect.offsetMax = new Vector2(-20f, 25f);
                }

                var inputField = existingInput;

                // Add LayoutElement for the VLG
                var le = clone.GetComponent<LayoutElement>();
                if (le == null) le = clone.AddComponent<LayoutElement>();
                le.preferredHeight = 90;

                return inputField;
            }

            // Fallback: simple manually-built row
            return CreateInputRowFallback(parent, label, defaultValue);
        }

        private TMP_InputField CreateInputRowFallback(RectTransform parent, string label, string defaultValue)
        {
            var row = CreateChild(parent, label + "Row");
            var rowLE = row.gameObject.AddComponent<LayoutElement>();
            rowLE.preferredHeight = 90;

            var bgRect = CreateChild(row, "Image");
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.offsetMin = Vector2.zero;
            bgRect.offsetMax = Vector2.zero;
            var bgImg = bgRect.gameObject.AddComponent<Image>();
            bgImg.color = new Color(0.179f, 0.125f, 0.090f, 0.729f);
            ApplyBlurSprite(bgImg);

            var labelRect = CreateChild(row, "Text (TMP)");
            labelRect.anchorMin = new Vector2(0f, 0f);
            labelRect.anchorMax = new Vector2(0f, 1f);
            labelRect.pivot = new Vector2(0f, 0.5f);
            labelRect.anchoredPosition = Vector2.zero;
            labelRect.sizeDelta = new Vector2(600f, 0f);
            var labelTMP = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            labelTMP.text = label;
            labelTMP.fontSize = 28;
            labelTMP.alignment = TextAlignmentOptions.MidlineLeft;
            labelTMP.color = Color.white;
            if (_font != null) labelTMP.font = _font;

            var inputParent = CreateChild(row, "InputParent");
            inputParent.anchorMin = new Vector2(1f, 0f);
            inputParent.anchorMax = new Vector2(1f, 1f);
            inputParent.pivot = new Vector2(1f, 0.5f);
            inputParent.sizeDelta = new Vector2(468f, 0f);

            var fieldRect = CreateChild(inputParent, "InputField (TMP)");
            fieldRect.anchorMin = Vector2.zero;
            fieldRect.anchorMax = Vector2.one;
            fieldRect.offsetMin = new Vector2(20f, 15f);
            fieldRect.offsetMax = new Vector2(-20f, -15f);
            var fieldImg = fieldRect.gameObject.AddComponent<Image>();
            fieldImg.color = Color.white;
            ApplyBlurSprite(fieldImg);

            var textArea = CreateChild(fieldRect, "Text Area");
            textArea.anchorMin = Vector2.zero;
            textArea.anchorMax = Vector2.one;
            textArea.anchoredPosition = new Vector2(0f, -2.3f);
            textArea.sizeDelta = new Vector2(-20f, 0f);
            textArea.gameObject.AddComponent<RectMask2D>();

            var placeholderRect = CreateChild(textArea, "Placeholder");
            placeholderRect.anchorMin = Vector2.zero;
            placeholderRect.anchorMax = Vector2.one;
            var placeholderTMP = placeholderRect.gameObject.AddComponent<TextMeshProUGUI>();
            placeholderTMP.text = label + "...";
            placeholderTMP.fontSize = 24;
            placeholderTMP.fontStyle = FontStyles.Italic;
            placeholderTMP.alignment = TextAlignmentOptions.MidlineLeft;
            placeholderTMP.color = new Color(0f, 0f, 0f, 0.3f);
            if (_font != null) placeholderTMP.font = _font;

            var textChildRect = CreateChild(textArea, "Text");
            textChildRect.anchorMin = Vector2.zero;
            textChildRect.anchorMax = Vector2.one;
            var textTMP = textChildRect.gameObject.AddComponent<TextMeshProUGUI>();
            textTMP.fontSize = 24;
            textTMP.color = Color.black;
            textTMP.alignment = TextAlignmentOptions.MidlineLeft;
            if (_font != null) textTMP.font = _font;

            var inputField = fieldRect.gameObject.AddComponent<TMP_InputField>();
            inputField.textViewport = textArea;
            inputField.textComponent = textTMP;
            inputField.placeholder = placeholderTMP;
            inputField.text = defaultValue;
            inputField.caretColor = Color.black;
            inputField.selectionColor = new Color(0.3f, 0.5f, 1f, 0.4f);
            inputField.caretWidth = 2;
            return inputField;
        }

        private Button CreateStyledButton(RectTransform parent, string name, string text, UnityEngine.Events.UnityAction onClick)
        {
            var rect = CreateChild(parent, name);
            var le = rect.gameObject.AddComponent<LayoutElement>();
            le.preferredHeight = 40;

            var img = rect.gameObject.AddComponent<Image>();
            if (_buttonSprite != null)
            {
                img.sprite = _buttonSprite;
                img.type = Image.Type.Sliced;
            }
            else
            {
                img.color = new Color(0.25f, 0.25f, 0.25f, 1f);
            }

            var button = rect.gameObject.AddComponent<Button>();
            button.colors = _buttonColors;
            button.spriteState = _buttonSpriteState;
            button.targetGraphic = img;
            button.onClick.AddListener(onClick);

            // Button text
            var textRect = CreateChild(rect, "Text");
            textRect.anchorMin = Vector2.zero;
            textRect.anchorMax = Vector2.one;
            textRect.offsetMin = Vector2.zero;
            textRect.offsetMax = Vector2.zero;
            var tmp = textRect.gameObject.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            if (_font != null) tmp.font = _font;
            tmp.raycastTarget = false;

            return button;
        }

        private Button CreateBackButton()
        {
            // Try to clone the back button from the settings page for consistent styling
            var settingsPage = GetComponentInParent<UIPageHandler>()
                ?.GetComponentInChildren<PauseMenuSettingsMenuPage>(true);

            if (settingsPage == null || settingsPage.backButton == null)
                return null;

            var backGO = Instantiate(settingsPage.backButton.gameObject, transform);
            backGO.name = "BackButton";

            // Destroy localization components so text stays
            foreach (var component in backGO.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                var typeName = component.GetType().Name;
                if (typeName.Contains("Localize") || typeName.Contains("LocalizedString"))
                    DestroyImmediate(component);
            }

            // Match Settings page back button exactly
            var backRect = backGO.GetComponent<RectTransform>();
            backRect.anchorMin = new Vector2(0f, 1f);
            backRect.anchorMax = new Vector2(0f, 1f);
            backRect.pivot = new Vector2(0.5f, 0.5f);
            backRect.anchoredPosition = new Vector2(171f, -157.9f);
            backRect.sizeDelta = new Vector2(150f, 67f);

            var button = backGO.GetComponent<Button>();
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(OnBackClicked);

            return button;
        }

        private Button CreateConnectButton(RectTransform parent)
        {
            // Clone the back button from settings page, then recolor green
            var settingsPage = GetComponentInParent<UIPageHandler>()
                ?.GetComponentInChildren<PauseMenuSettingsMenuPage>(true);

            if (settingsPage != null && settingsPage.backButton != null)
            {
                var connectGO = Instantiate(settingsPage.backButton.gameObject, parent);
                connectGO.name = "ConnectButton";

                  // Destroy localization components so text stays
                foreach (var component in connectGO.GetComponentsInChildren<Component>(true))
                {
                    if (component == null) continue;
                    var typeName = component.GetType().Name;
                    if (typeName.Contains("Localize") || typeName.Contains("LocalizedString"))
                        DestroyImmediate(component);
                }

                foreach (var tmp in connectGO.GetComponentsInChildren<TextMeshProUGUI>(true))
                    tmp.text = "CONNECT";

                var panel = connectGO.transform.Find("Panel")?.GetComponent<Image>();
                if (panel != null) panel.color = new Color(0.142f, 0.292f, 0.117f, 1f);
                foreach (Transform child in connectGO.transform)
                {
                    if (child.name == "Border")
                    {
                        var img = child.GetComponent<Image>();
                        if (img != null) img.color = new Color(0.051f, 0.434f, 0.115f, 1f);
                    }
                }

                // Add layout element for the vertical layout
                var le = connectGO.GetComponent<LayoutElement>() ?? connectGO.AddComponent<LayoutElement>();
                le.preferredHeight = 40;

                var button = connectGO.GetComponent<Button>();
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(OnConnectClicked);

                return button;
            }

            // Fallback to styled button
            return CreateStyledButton(parent, "ConnectButton", "Connect", OnConnectClicked);
        }
    }

    public class LinkToggleUpdater : MonoBehaviour
    {
        private TMP_Dropdown _dropdown;
        private System.Func<bool> _getCurrent;

        public void Initialize(TMP_Dropdown dropdown, System.Func<bool> getCurrent)
        {
            _dropdown = dropdown;
            _getCurrent = getCurrent;
        }

        void Update()
        {
            if (_dropdown == null) return;
            int expected = _getCurrent() ? 1 : 0;
            if (_dropdown.value != expected)
                _dropdown.SetValueWithoutNotify(expected);
        }
    }
}
