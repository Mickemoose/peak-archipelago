using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using BepInEx.Logging;
using Photon.Pun;
using Zorro.Core;
using TMPro;

namespace Peak.AP
{
    public static class PokemonCountTrapEffect
    {
        private static ManualLogSource _log;
        private static PeakArchipelagoPlugin _plugin;
        private static bool _isActive = false;

        private const float ANSWER_TIMEOUT = 15f;
        private const float SPRITE_SIZE = 120f;
        private const float WALK_SPEED_MIN = 180f;
        private const float WALK_SPEED_MAX = 320f;
        private const float SPAWN_INTERVAL_MIN = 0.3f;
        private const float SPAWN_INTERVAL_MAX = 0.8f;
        private const float Y_MIN = 0.10f;
        private const float Y_MAX = 0.75f;

        // Valid pokemon for this trap (exclude fake ones)
        private static readonly PokemonTriviaTrapEffect.Pokemon[] _validPokemon =
            Enum.GetValues(typeof(PokemonTriviaTrapEffect.Pokemon))
                .Cast<PokemonTriviaTrapEffect.Pokemon>()
                .Where(p => p != PokemonTriviaTrapEffect.Pokemon.terriermon &&
                            p != PokemonTriviaTrapEffect.Pokemon.wizardmon &&
                            p != PokemonTriviaTrapEffect.Pokemon.cheat)
                .ToArray();

        // All pokemon including digimon for distractors
        private static readonly PokemonTriviaTrapEffect.Pokemon[] _allPokemon =
            Enum.GetValues(typeof(PokemonTriviaTrapEffect.Pokemon))
                .Cast<PokemonTriviaTrapEffect.Pokemon>()
                .ToArray();

        public static void Initialize(ManualLogSource log, PeakArchipelagoPlugin plugin)
        {
            _log = log;
            _plugin = plugin;
        }

        public static void ApplyPokemonCountTrap(ManualLogSource log)
        {
            try
            {
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Pokemon Count already active, queueing for later");
                    _plugin.StartCoroutine(TriviaUIHelper.QueueTriviaForLater(log, () => _isActive, () => ApplyPokemonCountTrap(log)));
                    return;
                }

                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Pokemon Count - no local character");
                    return;
                }

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "StartPokemonCountRPC",
                        RpcTarget.All
                    );
                }
                else
                {
                    _plugin.StartCoroutine(PokemonCountCoroutine(log));
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Pokemon Count trap: {ex.Message}");
            }
        }

        public static void ApplyPokemonCountTrapLocal(ManualLogSource log)
        {
            try
            {
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Pokemon Count already active, skipping");
                    return;
                }

                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Pokemon Count - no local character");
                    return;
                }
                _plugin.StartCoroutine(PokemonCountCoroutine(log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Pokemon Count trap: {ex.Message}");
            }
        }

        private static IEnumerator PokemonCountCoroutine(ManualLogSource log)
        {
            _isActive = true;
            var random = new System.Random();

            // Pick the target pokemon and count
            var targetPokemon = _validPokemon[random.Next(_validPokemon.Length)];
            int targetCount = random.Next(6, 17); // 6-16

            // Build the parade: targetCount of the target + random distractors
            int distractorCount = random.Next(8, 16);
            var parade = new List<PokemonTriviaTrapEffect.Pokemon>();

            for (int i = 0; i < targetCount; i++)
                parade.Add(targetPokemon);

            // Distractors: any pokemon EXCEPT the target (digimon included)
            var distractorPool = _allPokemon.Where(p => p != targetPokemon).ToArray();
            for (int i = 0; i < distractorCount; i++)
                parade.Add(distractorPool[random.Next(distractorPool.Length)]);

            // Shuffle the parade
            for (int i = parade.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (parade[i], parade[j]) = (parade[j], parade[i]);
            }

            // Create the UI
            var countUI = new GameObject("PokemonCountUI");
            var canvas = countUI.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10000;
            var canvasScaler = countUI.AddComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            canvasScaler.referenceResolution = new Vector2(1920, 1080);
            countUI.AddComponent<GraphicRaycaster>();

            // Question banner at top
            var bannerObj = new GameObject("Banner");
            bannerObj.transform.SetParent(countUI.transform);
            var bannerRect = bannerObj.AddComponent<RectTransform>();
            bannerRect.anchorMin = new Vector2(0.1f, 0.85f);
            bannerRect.anchorMax = new Vector2(0.9f, 0.95f);
            bannerRect.offsetMin = Vector2.zero;
            bannerRect.offsetMax = Vector2.zero;

            var bannerTextObj = new GameObject("BannerText");
            bannerTextObj.transform.SetParent(bannerObj.transform);
            var bannerTextRect = bannerTextObj.AddComponent<RectTransform>();
            bannerTextRect.anchorMin = Vector2.zero;
            bannerTextRect.anchorMax = Vector2.one;
            bannerTextRect.offsetMin = Vector2.zero;
            bannerTextRect.offsetMax = Vector2.zero;

            var bannerText = bannerTextObj.AddComponent<Text>();
            bannerText.font = TriviaUIHelper.LoadCustomFont() ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            bannerText.fontSize = 48;
            bannerText.color = Color.white;
            bannerText.alignment = TextAnchor.MiddleCenter;
            bannerText.fontStyle = FontStyle.Bold;

            var bannerOutline = bannerTextObj.AddComponent<Outline>();
            bannerOutline.effectColor = Color.black;
            bannerOutline.effectDistance = new Vector2(3, 3);

            // Show target pokemon reference in the corner
            var refObj = new GameObject("TargetRef");
            refObj.transform.SetParent(countUI.transform);
            var refRect = refObj.AddComponent<RectTransform>();
            refRect.anchorMin = new Vector2(0.02f, 0.85f);
            refRect.anchorMax = new Vector2(0.02f, 0.85f);
            refRect.pivot = new Vector2(0f, 0.5f);
            refRect.sizeDelta = new Vector2(100, 100);
            refRect.anchoredPosition = Vector2.zero;

            var refImage = refObj.AddComponent<Image>();
            refImage.preserveAspect = true;
            Texture2D refTex = LoadPokemonTexture(targetPokemon);
            if (refTex != null)
            {
                refImage.sprite = Sprite.Create(refTex, new Rect(0, 0, refTex.width, refTex.height), new Vector2(0.5f, 0.5f));
            }

            // Countdown
            bannerText.text = "COUNT THE POKEMON!";
            bannerText.fontSize = 72;
            yield return new WaitForSeconds(1f);
            for (int countdown = 3; countdown > 0; countdown--)
            {
                bannerText.text = countdown.ToString();
                yield return new WaitForSeconds(1f);
            }

            string pokemonName = targetPokemon.ToString();
            pokemonName = char.ToUpper(pokemonName[0]) + pokemonName.Substring(1);
            bannerText.text = $"How many {pokemonName}?";
            bannerText.fontSize = 48;

            // Spawn the parade
            var activeWalkers = new List<GameObject>();
            float totalSpawnTime = 0f;

            foreach (var pokemon in parade)
            {
                float delay = (float)(SPAWN_INTERVAL_MIN + random.NextDouble() * (SPAWN_INTERVAL_MAX - SPAWN_INTERVAL_MIN));
                yield return new WaitForSeconds(delay);
                totalSpawnTime += delay;

                var walkerObj = new GameObject($"Walker_{pokemon}");
                walkerObj.transform.SetParent(countUI.transform);
                var walkerRect = walkerObj.AddComponent<RectTransform>();

                // Random Y position
                float yAnchor = (float)(Y_MIN + random.NextDouble() * (Y_MAX - Y_MIN));
                walkerRect.anchorMin = new Vector2(1.1f, yAnchor);
                walkerRect.anchorMax = new Vector2(1.1f, yAnchor);
                walkerRect.sizeDelta = new Vector2(SPRITE_SIZE, SPRITE_SIZE);
                walkerRect.anchoredPosition = Vector2.zero;

                var walkerImage = walkerObj.AddComponent<Image>();
                walkerImage.preserveAspect = true;
                Texture2D tex = LoadPokemonTexture(pokemon);
                if (tex != null)
                {
                    walkerImage.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }

                float speed = (float)(WALK_SPEED_MIN + random.NextDouble() * (WALK_SPEED_MAX - WALK_SPEED_MIN));
                var walker = walkerObj.AddComponent<PokemonWalker>();
                walker.speed = speed;

                activeWalkers.Add(walkerObj);
            }

            // Wait for all walkers to cross the screen
            float waitTime = 0f;
            float maxWait = 15f;
            while (waitTime < maxWait)
            {
                waitTime += Time.deltaTime;

                bool allGone = true;
                foreach (var w in activeWalkers)
                {
                    if (w != null)
                    {
                        var rt = w.GetComponent<RectTransform>();
                        if (rt != null && rt.anchorMin.x > -0.15f)
                        {
                            allGone = false;
                            break;
                        }
                    }
                }
                if (allGone) break;

                yield return null;
            }

            // Clean up walkers
            foreach (var w in activeWalkers)
            {
                if (w != null) UnityEngine.Object.Destroy(w);
            }

            // Now show the answer choices
            bannerText.text = $"How many {pokemonName} did you count?";
            refObj.SetActive(true);

            // Generate 4 answer choices: correct + 3 wrong
            var answers = new List<int> { targetCount };
            while (answers.Count < 4)
            {
                // Wrong answers within a reasonable range
                int wrong = targetCount + random.Next(-4, 5);
                if (wrong < 1) wrong = 1;
                if (wrong != targetCount && !answers.Contains(wrong))
                    answers.Add(wrong);
            }

            // Shuffle answers
            for (int i = answers.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                (answers[i], answers[j]) = (answers[j], answers[i]);
            }

            int correctIndex = answers.IndexOf(targetCount);

            InputSpriteData inputSpriteData = SingletonAsset<InputSpriteData>.Instance;
            var answerPositions = new[]
            {
                new Vector2(0.5f, 0.65f),
                new Vector2(0.25f, 0.42f),
                new Vector2(0.75f, 0.42f),
                new Vector2(0.5f, 0.20f)
            };

            var answerObjects = new List<GameObject>();
            for (int i = 0; i < 4; i++)
            {
                var ansObj = new GameObject($"Answer{i}");
                ansObj.transform.SetParent(countUI.transform);
                var ansRect = ansObj.AddComponent<RectTransform>();
                ansRect.anchorMin = answerPositions[i];
                ansRect.anchorMax = answerPositions[i];
                ansRect.sizeDelta = new Vector2(200, 120);
                ansRect.anchoredPosition = Vector2.zero;

                var ansTextObj = new GameObject("Text");
                ansTextObj.transform.SetParent(ansObj.transform);
                var ansTextRect = ansTextObj.AddComponent<RectTransform>();
                ansTextRect.anchorMin = Vector2.zero;
                ansTextRect.anchorMax = Vector2.one;
                ansTextRect.offsetMin = Vector2.zero;
                ansTextRect.offsetMax = Vector2.zero;

                var ansText = ansTextObj.AddComponent<Text>();
                ansText.font = TriviaUIHelper.LoadCustomFont() ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
                ansText.fontSize = 64;
                ansText.color = Color.white;
                ansText.alignment = TextAnchor.MiddleCenter;
                ansText.fontStyle = FontStyle.Bold;
                ansText.text = answers[i].ToString();

                var ansOutline = ansTextObj.AddComponent<Outline>();
                ansOutline.effectColor = Color.black;
                ansOutline.effectDistance = new Vector2(2, 2);

                TriviaUIHelper.AddInputGlyph(ansObj.transform, i, inputSpriteData);

                answerObjects.Add(ansObj);
            }

            // Countdown timer
            Text countdownTimer = TriviaUIHelper.CreateCountdownTimer(countUI.transform);

            // Wait for input
            var inputCoroutine = TriviaUIHelper.WaitForInput(ANSWER_TIMEOUT, countdownTimer);
            yield return inputCoroutine;
            int selectedAnswer = (int)inputCoroutine.Current;

            countdownTimer.transform.parent.gameObject.SetActive(false);

            if (selectedAnswer == -1)
                selectedAnswer = 0;

            bool correct = answers[selectedAnswer] == targetCount;

            if (correct)
            {
                TriviaUIHelper.PlayCorrectSound();
                bannerText.text = "CORRECT!";
                bannerText.color = Color.green;
            }
            else
            {
                TriviaUIHelper.PlayWrongSound();
                bannerText.text = $"WRONG! It was {targetCount}!";
                bannerText.color = Color.red;

                // Apply a random affliction as punishment
                var afflictions = new[]
                {
                    CharacterAfflictions.STATUSTYPE.Poison,
                    CharacterAfflictions.STATUSTYPE.Cold,
                    CharacterAfflictions.STATUSTYPE.Hot,
                    CharacterAfflictions.STATUSTYPE.Drowsy,
                    CharacterAfflictions.STATUSTYPE.Hunger,
                    CharacterAfflictions.STATUSTYPE.Injury,
                };
                var affliction = afflictions[random.Next(afflictions.Length)];

                StatusOverTimeTrapEffect.ApplyStatusOverTime(
                    log,
                    StatusOverTimeTrapEffect.TargetMode.LocalPlayer,
                    affliction,
                    amountPerTick: 0.1f,
                    tickInterval: 1.0f,
                    duration: 5.0f
                );
            }

            yield return new WaitForSeconds(3f);
            UnityEngine.Object.Destroy(countUI);
            _isActive = false;
            PeakArchipelagoPlugin._instance?._trapLinkService?.NotifyTrapComplete();
            log.LogInfo("[PeakPelago] Pokemon Count trap completed");
        }

        private static Texture2D LoadPokemonTexture(PokemonTriviaTrapEffect.Pokemon pokemon)
        {
            try
            {
                string resourceName = $"PeakArchipelagoPlugin.pkmns.{pokemon.ToString().ToLower()}.png";
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null) return null;
                    byte[] fileData = new byte[stream.Length];
                    stream.Read(fileData, 0, fileData.Length);
                    Texture2D tex = new Texture2D(2, 2);
                    tex.LoadImage(fileData);
                    return tex;
                }
            }
            catch (Exception ex)
            {
                _log?.LogError($"[PeakPelago] Error loading Pokemon texture: {ex.Message}");
            }
            return null;
        }
    }

    public class PokemonWalker : MonoBehaviour
    {
        public float speed = 200f;

        private RectTransform _rect;

        void Start()
        {
            _rect = GetComponent<RectTransform>();
        }

        void Update()
        {
            if (_rect == null) return;

            // Move left in screen-space pixels
            Vector2 min = _rect.anchorMin;
            Vector2 max = _rect.anchorMax;
            float delta = (speed / 1920f) * Time.deltaTime;
            min.x -= delta;
            max.x -= delta;
            _rect.anchorMin = min;
            _rect.anchorMax = max;

            // Destroy when off screen
            if (min.x < -0.15f)
            {
                Destroy(gameObject);
            }
        }
    }
}