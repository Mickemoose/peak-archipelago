using UnityEngine;
using System;

namespace Peak.AP
{
    public class ArchipelagoUI : MonoBehaviour
    {
        private const int BACKGROUND_RECT_X_COORD = 10;
        private const int BACKGROUND_RECT_Y_COORD = 30;

        private string _serverUrl = "archipelago.gg";
        private string _port = "38281";
        private string _slotName = string.Empty;
        private string _password = string.Empty;
        private int _fontSize = 12;

        private PeakArchipelagoPlugin _plugin;

        // PlayerPrefs keys
        private const string PREFS_SERVER = "PeakPelago_ServerUrl";
        private const string PREFS_PORT = "PeakPelago_Port";
        private const string PREFS_SLOT = "PeakPelago_SlotName";
        private const string PREFS_PASSWORD = "PeakPelago_Password";
        private const string PREFS_FONTSIZE = "PeakPelago_FontSize";

        public void Initialize(PeakArchipelagoPlugin plugin)
        {
            _plugin = plugin;
            LoadSettings();
        }

        private void LoadSettings()
        {
            // Load saved values or use defaults
            _serverUrl = PlayerPrefs.GetString(PREFS_SERVER, "archipelago.gg");
            _port = PlayerPrefs.GetString(PREFS_PORT, "38281");
            _slotName = PlayerPrefs.GetString(PREFS_SLOT, string.Empty);
            _password = PlayerPrefs.GetString(PREFS_PASSWORD, string.Empty);
            _fontSize = PlayerPrefs.GetInt(PREFS_FONTSIZE, 12);
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetString(PREFS_SERVER, _serverUrl);
            PlayerPrefs.SetString(PREFS_PORT, _port);
            PlayerPrefs.SetString(PREFS_SLOT, _slotName);
            PlayerPrefs.SetString(PREFS_PASSWORD, _password);
            PlayerPrefs.SetInt(PREFS_FONTSIZE, _fontSize);
            PlayerPrefs.Save();
        }

        private void OnGUI()
        {
            GUIStyle labelStyle = new GUIStyle(GUI.skin.label);
            GUIStyle buttonStyle = new GUIStyle(GUI.skin.button);
            GUIStyle textFieldStyle = new GUIStyle(GUI.skin.textField);
            
            _setFontSize(labelStyle, buttonStyle, textFieldStyle);
            labelStyle.normal.textColor = Color.white;

            int rectWidth = (int)Math.Round(320.0 * ((double)_fontSize / 12.0));
            int rowHeight = _fontSize + 8;
            int xPos = 16;
            int yPos = 36;
            int labelWidth = rectWidth / 2;

            if (_plugin == null || _plugin.Status != "Connected")
            {
                int totalHeight = 12 + rowHeight * 6;
                _drawShadedRectangle(new Rect(BACKGROUND_RECT_X_COORD, yPos - 6, rectWidth, totalHeight));

                GUI.Label(new Rect(xPos, yPos, labelWidth, rowHeight), "PeakPelago font size: ", labelStyle);
                if (GUI.Button(new Rect(xPos + labelWidth, yPos, _fontSize * 2, rowHeight), "-", buttonStyle))
                {
                    _fontSize = Mathf.Max(8, _fontSize - 1);
                    SaveSettings(); // Save when font size changes
                }
                if (GUI.Button(new Rect(xPos + labelWidth + _fontSize * 2 + 8, yPos, _fontSize * 2, rowHeight), "+", buttonStyle))
                {
                    _fontSize = Mathf.Min(24, _fontSize + 1);
                    SaveSettings(); // Save when font size changes
                }
                yPos += rowHeight;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                int fieldWidth = rectWidth - labelWidth;
                bool enterPressed = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;

                // Track if any field changed
                string oldServerUrl = _serverUrl;
                string oldPort = _port;
                string oldSlotName = _slotName;
                string oldPassword = _password;

                GUI.Label(new Rect(xPos, yPos, labelWidth, rowHeight), "Server: ", labelStyle);
                _serverUrl = GUI.TextField(new Rect(labelWidth, yPos, fieldWidth, rowHeight), _serverUrl, textFieldStyle);
                yPos += rowHeight;

                GUI.Label(new Rect(xPos, yPos, labelWidth, rowHeight), "Port: ", labelStyle);
                _port = GUI.TextField(new Rect(labelWidth, yPos, fieldWidth, rowHeight), _port, textFieldStyle);
                yPos += rowHeight;

                GUI.Label(new Rect(xPos, yPos, labelWidth, rowHeight), "Slot Name: ", labelStyle);
                _slotName = GUI.TextField(new Rect(labelWidth, yPos, fieldWidth, rowHeight), _slotName, textFieldStyle);
                yPos += rowHeight;

                GUI.Label(new Rect(xPos, yPos, labelWidth, rowHeight), "Password: ", labelStyle);
                _password = GUI.TextField(new Rect(labelWidth, yPos, fieldWidth, rowHeight), _password, textFieldStyle);
                yPos += rowHeight;

                // Save settings if any field changed
                if (_serverUrl != oldServerUrl || _port != oldPort || _slotName != oldSlotName || _password != oldPassword)
                {
                    SaveSettings();
                }

                if (enterPressed && Event.current.type == EventType.KeyDown)
                {
                    enterPressed = false;
                }

                if ((GUI.Button(new Rect(xPos, yPos, 76 + _fontSize * 2, rowHeight), "Connect", buttonStyle) || enterPressed) 
                    && !string.IsNullOrEmpty(_serverUrl) 
                    && !string.IsNullOrEmpty(_slotName))
                {
                    SaveSettings(); // Save before connecting
                    _plugin.SetConnectionDetails(_serverUrl, _port, _slotName, _password);
                    _plugin.Connect();
                }
            }
            else
            {
                int totalHeight = 12 + rowHeight;
                _drawShadedRectangle(new Rect(BACKGROUND_RECT_X_COORD, yPos - 6, rectWidth, totalHeight));
                GUI.Label(new Rect(xPos, yPos, 900f, rowHeight), "Archipelago configured.", labelStyle);
                
            }
        }

        private void _drawShadedRectangle(Rect rect)
        {
            Color originalColor = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, 0.5f);
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = originalColor;
        }

        private void _setFontSize(params GUIStyle[] styles)
        {
            foreach (var style in styles)
            {
                style.fontSize = _fontSize;
            }
        }
    }
}