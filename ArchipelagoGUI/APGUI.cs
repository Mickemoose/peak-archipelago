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

        public void Initialize(PeakArchipelagoPlugin plugin)
        {
            _plugin = plugin;
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
                // Calculate total height needed
                int totalHeight = 12 + rowHeight * 6; // Font size + Server + Port + Slot + Password + Connect button
                _drawShadedRectangle(new Rect(BACKGROUND_RECT_X_COORD, yPos - 6, rectWidth, totalHeight));

                // Font size controls
                GUI.Label(new Rect(xPos, yPos, labelWidth, rowHeight), "PeakPelago font size: ", labelStyle);
                if (GUI.Button(new Rect(xPos + labelWidth, yPos, _fontSize * 2, rowHeight), "-", buttonStyle))
                {
                    _fontSize = Mathf.Max(8, _fontSize - 1);
                }
                if (GUI.Button(new Rect(xPos + labelWidth + _fontSize * 2 + 8, yPos, _fontSize * 2, rowHeight), "+", buttonStyle))
                {
                    _fontSize = Mathf.Min(24, _fontSize + 1);
                }
                yPos += rowHeight;

                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;

                int fieldWidth = rectWidth - labelWidth;
                bool enterPressed = Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return;

                // Server URL
                GUI.Label(new Rect(xPos, yPos, labelWidth, rowHeight), "Server: ", labelStyle);
                _serverUrl = GUI.TextField(new Rect(labelWidth, yPos, fieldWidth, rowHeight), _serverUrl, textFieldStyle);
                yPos += rowHeight;

                // Port
                GUI.Label(new Rect(xPos, yPos, labelWidth, rowHeight), "Port: ", labelStyle);
                _port = GUI.TextField(new Rect(labelWidth, yPos, fieldWidth, rowHeight), _port, textFieldStyle);
                yPos += rowHeight;

                // Slot Name
                GUI.Label(new Rect(xPos, yPos, labelWidth, rowHeight), "Slot Name: ", labelStyle);
                _slotName = GUI.TextField(new Rect(labelWidth, yPos, fieldWidth, rowHeight), _slotName, textFieldStyle);
                yPos += rowHeight;

                // Password
                GUI.Label(new Rect(xPos, yPos, labelWidth, rowHeight), "Password: ", labelStyle);
                _password = GUI.TextField(new Rect(labelWidth, yPos, fieldWidth, rowHeight), _password, textFieldStyle);
                yPos += rowHeight;

                // Consume the enter key event
                if (enterPressed && Event.current.type == EventType.KeyDown)
                {
                    enterPressed = false;
                }

                // Connect button
                if ((GUI.Button(new Rect(xPos, yPos, 76 + _fontSize * 2, rowHeight), "Connect", buttonStyle) || enterPressed) 
                    && !string.IsNullOrEmpty(_serverUrl) 
                    && !string.IsNullOrEmpty(_slotName))
                {
                    _plugin.SetConnectionDetails(_serverUrl, _port, _slotName, _password);
                    _plugin.Connect();
                }
            }
            else
            {
                int totalHeight = 12 + rowHeight;
                _drawShadedRectangle(new Rect(BACKGROUND_RECT_X_COORD, yPos - 6, rectWidth, totalHeight));
                GUI.Label(new Rect(xPos, yPos, 900f, rowHeight), "Archipelago configured.", labelStyle);
                
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
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