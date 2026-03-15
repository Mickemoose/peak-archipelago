using UnityEngine;
using UnityEngine.UI;
using System.Reflection;
using TMPro;

namespace Peak.AP
{
    public class ArchipelagoUI : MonoBehaviour
    {
        private PeakArchipelagoPlugin _plugin;

        // Icon textures loaded from embedded resources
        private Texture2D _whiteIcon;
        private Texture2D _colorIcon;

        // Icon layout
        private const int ICON_SIZE = 32;
        private const int ICON_MARGIN = 10;

        // UI elements on our canvas
        private GameObject _canvasObject;
        private RawImage _iconImage;
        private TextMeshProUGUI _versionTMP;
        private TextMeshProUGUI _statusTMP;
        private bool _setupAttempted;

        // Spin state
        private RectTransform _iconRect;

        public void Initialize(PeakArchipelagoPlugin plugin)
        {
            _plugin = plugin;
            LoadIcons();
        }

        private void LoadIcons()
        {
            _whiteIcon = LoadEmbeddedTexture("PeakArchipelagoPlugin.white-icon.png");
            _colorIcon = LoadEmbeddedTexture("PeakArchipelagoPlugin.color-icon.png");
        }

        private Texture2D LoadEmbeddedTexture(string resourceName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return null;
                    byte[] data = new byte[stream.Length];
                    stream.Read(data, 0, data.Length);
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    tex.LoadImage(data);
                    tex.filterMode = FilterMode.Bilinear;
                    return tex;
                }
            }
            catch
            {
                return null;
            }
        }

        private void SetupUI()
        {
            if (_setupAttempted) return;

            // Find the game's VersionString to clone its font
            var versionString = FindFirstObjectByType<VersionString>();
            if (versionString == null) return;

            var sourceText = versionString.GetComponent<TextMeshProUGUI>();
            if (sourceText == null) return;

            _setupAttempted = true;

            // Create screen-space overlay canvas
            _canvasObject = new GameObject("PeakPelagoOverlay");
            var canvas = _canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            _canvasObject.AddComponent<CanvasScaler>();

            var iconObj = new GameObject("Icon");
            iconObj.transform.SetParent(_canvasObject.transform, false);
            _iconImage = iconObj.AddComponent<RawImage>();
            _iconImage.texture = _whiteIcon;
            _iconImage.raycastTarget = false;

            _iconRect = iconObj.GetComponent<RectTransform>();
            _iconRect.anchorMin = new Vector2(0f, 0f);
            _iconRect.anchorMax = new Vector2(0f, 0f);
            _iconRect.pivot = new Vector2(0.5f, 0.5f);
            _iconRect.anchoredPosition = new Vector2(ICON_MARGIN + ICON_SIZE * 0.5f, ICON_MARGIN + ICON_SIZE * 0.5f);
            _iconRect.sizeDelta = new Vector2(ICON_SIZE, ICON_SIZE);
            var textObj = new GameObject("VersionText");
            textObj.transform.SetParent(_canvasObject.transform, false);

            _versionTMP = textObj.AddComponent<TextMeshProUGUI>();
            _versionTMP.font = sourceText.font;
            _versionTMP.fontSize = sourceText.fontSize;
            _versionTMP.fontStyle = sourceText.fontStyle;
            _versionTMP.color = sourceText.color;
            _versionTMP.alignment = TextAlignmentOptions.MidlineLeft;
            _versionTMP.text = "v" + _plugin.Info.Metadata.Version;
            _versionTMP.raycastTarget = false;

            var textRect = textObj.GetComponent<RectTransform>();
            textRect.anchorMin = new Vector2(0f, 0f);
            textRect.anchorMax = new Vector2(0f, 0f);
            textRect.pivot = new Vector2(0f, 0f);
            textRect.anchoredPosition = new Vector2(ICON_MARGIN + ICON_SIZE + 4, ICON_MARGIN);
            textRect.sizeDelta = new Vector2(200f, ICON_SIZE);

            var statusObj = new GameObject("StatusText");
            statusObj.transform.SetParent(_canvasObject.transform, false);

            _statusTMP = statusObj.AddComponent<TextMeshProUGUI>();
            _statusTMP.font = sourceText.font;
            _statusTMP.fontSize = sourceText.fontSize * 0.85f;
            _statusTMP.color = Color.white;
            _statusTMP.alignment = TextAlignmentOptions.BottomLeft;
            _statusTMP.text = "";
            _statusTMP.raycastTarget = false;

            var statusRect = statusObj.GetComponent<RectTransform>();
            statusRect.anchorMin = new Vector2(0f, 0f);
            statusRect.anchorMax = new Vector2(0f, 0f);
            statusRect.pivot = new Vector2(0f, 0f);
            statusRect.anchoredPosition = new Vector2(ICON_MARGIN, ICON_MARGIN + ICON_SIZE + 4);
            statusRect.sizeDelta = new Vector2(300f, 40f);
        }

        private void Update()
        {
            if (_plugin == null) return;

            // Re-attempt setup if canvas was destroyed (e.g. scene change)
            if (_canvasObject == null)
            {
                _setupAttempted = false;
                SetupUI();
            }

            if (_iconImage == null) return;

            bool isConnected = _plugin.Status == "Connected";
            bool isConnecting = _plugin._isConnecting;
            bool isReconnecting = _plugin.IsReconnecting;

            // Update icon texture and alpha
            float alpha;
            bool spin;

            if (isConnected)
            {
                _iconImage.texture = _colorIcon ?? _whiteIcon;
                alpha = 1f;
                spin = false;
            }
            else if (isConnecting || isReconnecting)
            {
                _iconImage.texture = _whiteIcon;
                alpha = 1f;
                spin = true;
            }
            else
            {
                _iconImage.texture = _whiteIcon;
                alpha = 0.75f;
                spin = false;
            }

            _iconImage.color = new Color(1f, 1f, 1f, alpha);

            // Spin the icon when connecting
            if (spin)
            {
                float angle = Time.unscaledTime * 90f % 360f;
                _iconRect.localRotation = Quaternion.Euler(0, 0, -angle);
            }
            else
            {
                _iconRect.localRotation = Quaternion.identity;
            }

            if (_versionTMP != null)
            {
                Color c = _versionTMP.color;
                _versionTMP.color = new Color(c.r, c.g, c.b, alpha);
            }
            if (_statusTMP != null)
            {
                if (isReconnecting)
                {
                    int attempts = _plugin._reconnectAttempts;
                    int max = PeakArchipelagoPlugin.MAX_RECONNECT_ATTEMPTS;
                    int offline = _plugin.OfflineCheckCount;

                    string text = isConnecting
                        ? $"Connecting... (Attempt {attempts}/{max})"
                        : $"Waiting to reconnect... ({attempts}/{max})";

                    if (offline > 0)
                        text += $"\n{offline} checks queued";

                    _statusTMP.text = text;
                }
                else if (isConnecting)
                {
                    _statusTMP.text = "Connecting...";
                }
                else
                {
                    _statusTMP.text = "";
                }
            }
        }

        private void OnDestroy()
        {
            if (_canvasObject != null)
            {
                Destroy(_canvasObject);
            }
        }
    }
}
