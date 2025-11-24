using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using BepInEx.Logging;
using TMPro;

namespace Peak.AP
{
    public static class TriviaUIHelper
    {
        private static ManualLogSource _log;

        public static void Initialize(ManualLogSource log)
        {
            _log = log;
        }

        public static (GameObject triviaUI, Text questionText) CreateTriviaUI()
        {
            var triviaUI = new GameObject("TriviaUI");
            var canvas = triviaUI.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            var canvasScaler = triviaUI.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            triviaUI.AddComponent<GraphicRaycaster>();

            var questionBanner = new GameObject("QuestionBanner");
            questionBanner.transform.SetParent(triviaUI.transform);
            var bannerRect = questionBanner.AddComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.1f, 0.75f);
            bannerRect.anchorMax = new Vector2(0.9f, 0.95f);
            bannerRect.offsetMin = Vector2.zero;
            bannerRect.offsetMax = Vector2.zero;

            var questionTextObj = new GameObject("QuestionText");
            questionTextObj.transform.SetParent(questionBanner.transform);
            var questionRect = questionTextObj.AddComponent<RectTransform>();
            questionRect.anchorMin = new Vector2(0.05f, 0.05f);
            questionRect.anchorMax = new Vector2(0.95f, 0.95f);
            questionRect.offsetMin = Vector2.zero;
            questionRect.offsetMax = Vector2.zero;

            var questionText = questionTextObj.AddComponent<Text>();
            questionText.font = LoadCustomFont() ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            questionText.fontSize = 48;
            questionText.color = Color.white;
            questionText.alignment = TextAnchor.MiddleCenter;
            questionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            questionText.fontStyle = FontStyle.Bold;

            var textOutline = questionTextObj.AddComponent<Outline>();
            textOutline.effectColor = Color.black;
            textOutline.effectDistance = new Vector2(3, 3);

            return (triviaUI, questionText);
        }

        public static Text CreateCountdownTimer(Transform parent)
        {
            var timerObj = new GameObject("CountdownTimer");
            timerObj.transform.SetParent(parent);
            var timerRect = timerObj.AddComponent<RectTransform>();
            timerRect.anchorMin = new Vector2(0.5f, 0.4f);
            timerRect.anchorMax = new Vector2(0.5f, 0.4f);
            timerRect.sizeDelta = new Vector2(150, 150);
            timerRect.anchoredPosition = Vector2.zero;

            // Timer text
            var timerTextObj = new GameObject("TimerText");
            timerTextObj.transform.SetParent(timerObj.transform);
            var timerTextRect = timerTextObj.AddComponent<RectTransform>();
            timerTextRect.anchorMin = Vector2.zero;
            timerTextRect.anchorMax = Vector2.one;
            timerTextRect.offsetMin = Vector2.zero;
            timerTextRect.offsetMax = Vector2.zero;

            var timerText = timerTextObj.AddComponent<Text>();
            timerText.font = LoadCustomFont() ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            timerText.fontSize = 72;
            timerText.color = Color.white;
            timerText.alignment = TextAnchor.MiddleCenter;
            timerText.fontStyle = FontStyle.Bold;

            var timerOutline = timerTextObj.AddComponent<Outline>();
            timerOutline.effectColor = Color.black;
            timerOutline.effectDistance = new Vector2(2, 2);

            return timerText;
        }

        public static IEnumerator DoCountdown(Text questionText)
        {
            questionText.text = "GET READY!";
            questionText.fontSize = 72;
            for (int countdown = 3; countdown > 0; countdown--)
            {
                yield return new WaitForSeconds(1f);
            }
        }

        public static IEnumerator WaitForInput(float timeout, Text countdownTimer = null)
        {
            int selectedAnswer = -1;
            float elapsed = 0f;
            var inputKeys = new[] { KeyCode.W, KeyCode.A, KeyCode.D, KeyCode.S };
            var altInputKeys = new[] { KeyCode.UpArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.DownArrow };

            while (selectedAnswer == -1 && elapsed < timeout)
            {
                elapsed += Time.deltaTime;
                if (countdownTimer != null)
                {
                    int remaining = Mathf.CeilToInt(timeout - elapsed);
                    countdownTimer.text = remaining.ToString();
                    if (remaining <= 3)
                    {
                        countdownTimer.color = Color.red;
                    }
                    else if (remaining <= 5)
                    {
                        countdownTimer.color = Color.yellow;
                    }
                    else
                    {
                        countdownTimer.color = Color.white;
                    }
                }

                for (int i = 0; i < 4; i++)
                {
                    if (Input.GetKeyDown(inputKeys[i]) || Input.GetKeyDown(altInputKeys[i]))
                    {
                        selectedAnswer = i;
                        break;
                    }
                }

                if (selectedAnswer == -1)
                {
                    float horizontal = Input.GetAxis("Horizontal");
                    float vertical = Input.GetAxis("Vertical");

                    if (Mathf.Abs(horizontal) > 0.7f || Mathf.Abs(vertical) > 0.7f)
                    {
                        if (vertical > 0.7f) selectedAnswer = 0;
                        else if (horizontal < -0.7f) selectedAnswer = 1;
                        else if (horizontal > 0.7f) selectedAnswer = 2;
                        else if (vertical < -0.7f) selectedAnswer = 3;
                    }
                }

                yield return null;
            }

            yield return selectedAnswer;
        }

        public static void AddInputGlyph(Transform parent, int index, InputSpriteData inputSpriteData)
        {
            var glyphObj = new GameObject($"InputGlyph{index}");
            glyphObj.transform.SetParent(parent);
            var glyphRect = glyphObj.AddComponent<RectTransform>();
            glyphRect.anchorMin = new Vector2(0f, 0.5f);
            glyphRect.anchorMax = new Vector2(0f, 0.5f);
            glyphRect.pivot = new Vector2(0f, 0.5f);
            glyphRect.anchoredPosition = new Vector2(10, 0);
            glyphRect.sizeDelta = new Vector2(60, 60);

            var glyphImage = glyphObj.AddComponent<Image>();
            glyphImage.preserveAspect = true;
            glyphImage.color = Color.white;

            Sprite glyphSprite = GetInputGlyphSprite(index, inputSpriteData);
            if (glyphSprite != null)
            {
                glyphImage.sprite = glyphSprite;
            }
        }

        public static Sprite GetInputGlyphSprite(int answerIndex, InputSpriteData inputSpriteData)
        {
            if (inputSpriteData == null) return null;

            try
            {
                bool usingGamepad = Input.GetJoystickNames().Length > 0 &&
                                    !string.IsNullOrEmpty(Input.GetJoystickNames()[0]);

                TMP_SpriteAsset spriteAsset = null;
                int spriteIndex = 0;

                if (usingGamepad)
                {
                    var iconSetting = SettingsHandler.Instance?.GetSetting<ControllerIconSetting>();
                    if (iconSetting != null)
                    {
                        switch (iconSetting.Value)
                        {
                            case ControllerIconSetting.IconMode.Style1:
                                spriteAsset = inputSpriteData.xboxSprites;
                                break;
                            case ControllerIconSetting.IconMode.Style2:
                                spriteAsset = inputSpriteData.ps5Sprites;
                                break;
                            case ControllerIconSetting.IconMode.KBM:
                                usingGamepad = false;
                                break;
                            default:
                                spriteAsset = inputSpriteData.xboxSprites;
                                break;
                        }
                    }
                    else
                    {
                        spriteAsset = inputSpriteData.xboxSprites;
                    }

                    if (usingGamepad)
                    {
                        spriteIndex = answerIndex switch
                        {
                            0 => 12,
                            1 => 14,
                            2 => 15,
                            3 => 13,
                            _ => 12
                        };
                    }
                }

                if (!usingGamepad)
                {
                    spriteAsset = inputSpriteData.keyboardSprites;
                    spriteIndex = answerIndex switch
                    {
                        0 => 32,
                        1 => 10,
                        2 => 13,
                        3 => 28,
                        _ => 32
                    };
                }

                if (spriteAsset != null && spriteIndex < spriteAsset.spriteGlyphTable.Count)
                {
                    var spriteGlyph = spriteAsset.spriteGlyphTable[spriteIndex];
                    var glyphRect = spriteGlyph.glyphRect;

                    return Sprite.Create(
                        spriteAsset.spriteSheet as Texture2D,
                        new Rect(glyphRect.x, glyphRect.y, glyphRect.width, glyphRect.height),
                        new Vector2(0.5f, 0.5f)
                    );
                }
            }
            catch (Exception ex)
            {
                _log?.LogWarning($"[PeakPelago] Failed to load input glyph: {ex.Message}");
            }

            return null;
        }

        public static Font LoadCustomFont()
        {
            try
            {
                Font[] loadedFonts = Resources.FindObjectsOfTypeAll<Font>();
                foreach (Font font in loadedFonts)
                {
                    if (font.name.Contains("Daruma") || font.name.Contains("DarumaDropOne"))
                    {
                        return font;
                    }
                }
                foreach (Font font in loadedFonts)
                {
                    if (!font.name.Contains("Arial") && !font.name.Contains("Legacy"))
                    {
                        return font;
                    }
                }
            }
            catch (Exception ex)
            {
                _log?.LogWarning($"[PeakPelago] Could not find custom font: {ex.Message}");
            }

            return null;
        }

        public static void PlayCorrectSound()
        {
            var connectionLog = UnityEngine.Object.FindFirstObjectByType<PlayerConnectionLog>();
            connectionLog?.sfxJoin?.Play();
        }

        public static void PlayWrongSound()
        {
            var connectionLog = UnityEngine.Object.FindFirstObjectByType<PlayerConnectionLog>();
            connectionLog?.sfxLeave?.Play();
        }

        public static IEnumerator QueueTriviaForLater(ManualLogSource log, Func<bool> isActive, Action retryAction)
        {
            while (isActive())
            {
                yield return new WaitForSeconds(3f);
            }

            yield return new WaitForSeconds(2f);

            log.LogInfo("[PeakPelago] Starting queued trivia trap");
            retryAction();
        }
    }
}