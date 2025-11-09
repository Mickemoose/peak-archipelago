using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using BepInEx.Logging;
using Photon.Pun;
using System.IO;

namespace Peak.AP
{
    public static class PokemonTriviaTrapEffect
    {
        private static ManualLogSource _log;
        private static PeakArchipelagoPlugin _plugin;
        private static bool _isActive = false;

        public enum Pokemon
        {
            totodile,
            quilava,
            unown,
            pichu,
            hooh,
            lugia,
            celebi,
            hoothoot,
            delibird,
            bellossom,
            umbreon,
            espeon,
            miltank,
            heracross,
            girafarig,
            slowking,
            togepi,
            shuckle,
            sudowoodo,
            scizor,
            porygon2,
            dunsparce,
            qwilfish,
            crobat,
            steelix,
            suicune,
            misdreavus,
            hitmontop,
            spinarak,
            chinchou,
            slugma,
            elekid,
        }

        private static Dictionary<Pokemon, CharacterAfflictions.STATUSTYPE> _pokemonAfflictions = new()
        {
            { Pokemon.totodile, CharacterAfflictions.STATUSTYPE.Injury },
            { Pokemon.quilava, CharacterAfflictions.STATUSTYPE.Hot },
            { Pokemon.unown, CharacterAfflictions.STATUSTYPE.Hunger },
            { Pokemon.pichu, CharacterAfflictions.STATUSTYPE.Hunger },
            { Pokemon.hooh, CharacterAfflictions.STATUSTYPE.Hot },
            { Pokemon.lugia, CharacterAfflictions.STATUSTYPE.Cold },
            { Pokemon.celebi, CharacterAfflictions.STATUSTYPE.Drowsy },
            { Pokemon.hoothoot, CharacterAfflictions.STATUSTYPE.Drowsy },
            { Pokemon.delibird, CharacterAfflictions.STATUSTYPE.Cold },
            { Pokemon.bellossom, CharacterAfflictions.STATUSTYPE.Poison },
            { Pokemon.umbreon, CharacterAfflictions.STATUSTYPE.Hunger },
            { Pokemon.espeon, CharacterAfflictions.STATUSTYPE.Drowsy },
            { Pokemon.miltank, CharacterAfflictions.STATUSTYPE.Injury },
            { Pokemon.heracross, CharacterAfflictions.STATUSTYPE.Poison },
            { Pokemon.girafarig, CharacterAfflictions.STATUSTYPE.Drowsy },
            { Pokemon.slowking, CharacterAfflictions.STATUSTYPE.Drowsy },
            { Pokemon.togepi, CharacterAfflictions.STATUSTYPE.Spores },
            { Pokemon.shuckle, CharacterAfflictions.STATUSTYPE.Poison },
            { Pokemon.sudowoodo, CharacterAfflictions.STATUSTYPE.Injury },
            { Pokemon.scizor, CharacterAfflictions.STATUSTYPE.Poison },
            { Pokemon.porygon2, CharacterAfflictions.STATUSTYPE.Web },
            { Pokemon.dunsparce, CharacterAfflictions.STATUSTYPE.Hunger },
            { Pokemon.qwilfish, CharacterAfflictions.STATUSTYPE.Thorns },
            { Pokemon.crobat, CharacterAfflictions.STATUSTYPE.Poison },
            { Pokemon.steelix, CharacterAfflictions.STATUSTYPE.Injury },
            { Pokemon.suicune, CharacterAfflictions.STATUSTYPE.Cold },
            { Pokemon.misdreavus, CharacterAfflictions.STATUSTYPE.Drowsy },
            { Pokemon.hitmontop, CharacterAfflictions.STATUSTYPE.Injury },
            { Pokemon.spinarak, CharacterAfflictions.STATUSTYPE.Web },
            { Pokemon.chinchou, CharacterAfflictions.STATUSTYPE.Hunger },
            { Pokemon.slugma, CharacterAfflictions.STATUSTYPE.Hot },
            { Pokemon.elekid, CharacterAfflictions.STATUSTYPE.Spores },
        };

        public static void Initialize(ManualLogSource log, PeakArchipelagoPlugin plugin)
        {
            _log = log;
            _plugin = plugin;
            _log.LogInfo($"[PeakPelago] Pokemon Trivia initialized with embedded resources");
        }

        public static void ApplyPokemonTriviaTrapLocal(ManualLogSource log)
        {
            try
            {
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Pokemon Trivia already active, skipping");
                    return;
                }

                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Pokemon Trivia - no local character");
                    return;
                }

                log.LogInfo("[PeakPelago] Starting Pokemon Trivia trap locally");
                _plugin.StartCoroutine(PokemonTriviaCoroutine(log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Pokemon Trivia trap: {ex.Message}");
            }
        }

        public static void ApplyPokemonTriviaTrap(ManualLogSource log)
        {
            try
            {
                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Pokemon Trivia - no local character");
                    return;
                }

                log.LogInfo("[PeakPelago] Triggering Pokemon Trivia for all players via RPC");

                // Send RPC to ALL clients to start their trivia
                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "StartPokemonTriviaRPC",
                        RpcTarget.All
                    );
                }
                else
                {
                    log.LogWarning("[PeakPelago] PhotonView not available, starting locally only");
                    _plugin.StartCoroutine(PokemonTriviaCoroutine(log));
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Pokemon Trivia trap: {ex.Message}");
            }
        }

        private static IEnumerator PokemonTriviaCoroutine(ManualLogSource log)
        {
            _isActive = true;
            var question = GetRandomQuestion();
            log.LogInfo($"[PeakPelago] Pokemon Trivia Question: {question.Question}");
            log.LogInfo($"[PeakPelago] Correct Answer: {question.CorrectMon}");
            var triviaUI = new GameObject("PokemonTriviaUI");
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
            Font customFont = LoadCustomFont();
            questionText.font = customFont ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            questionText.fontSize = 48;
            questionText.color = Color.white;
            questionText.alignment = TextAnchor.MiddleCenter;
            questionText.horizontalOverflow = HorizontalWrapMode.Wrap;
            questionText.fontStyle = FontStyle.Bold;
            var textOutline = questionTextObj.AddComponent<Outline>();
            textOutline.effectColor = Color.black;
            textOutline.effectDistance = new Vector2(3, 3);
            var answerPositions = new[]
            {
                new Vector2(0.5f, 0.60f),
                new Vector2(0.25f, 0.38f),
                new Vector2(0.75f, 0.38f),
                new Vector2(0.5f, 0.20f)
            };
            var answerObjects = new List<GameObject>();
            var pokemonImages = new List<Image>();
            for (int i = 0; i < 4; i++)
            {
                var pokemonObj = new GameObject($"Pokemon{i}");
                pokemonObj.transform.SetParent(triviaUI.transform);
                var pokemonRect = pokemonObj.AddComponent<RectTransform>();
                pokemonRect.anchorMin = answerPositions[i];
                pokemonRect.anchorMax = answerPositions[i];
                pokemonRect.sizeDelta = new Vector2(400, 400);
                pokemonRect.anchoredPosition = Vector2.zero;

                var pokemonImage = pokemonObj.AddComponent<Image>();
                pokemonImage.preserveAspect = true;
                pokemonImage.color = Color.white;
                
                // Load Pokemon sprite
                Texture2D pokemonTex = LoadPokemonTexture(question.Options[i]);
                if (pokemonTex != null)
                {
                    pokemonImage.sprite = Sprite.Create(
                        pokemonTex,
                        new Rect(0, 0, pokemonTex.width, pokemonTex.height),
                        new Vector2(0.5f, 0.5f)
                    );
                }

                pokemonObj.SetActive(false);
                pokemonImages.Add(pokemonImage);
                answerObjects.Add(pokemonObj);
            }
            questionText.text = "GET READY!";
            questionText.fontSize = 72;
            for (int countdown = 3; countdown > 0; countdown--)
            {
                yield return new WaitForSeconds(1f);
            }

            questionText.text = question.Question;
            questionText.fontSize = 48;
            foreach (var pokemonObj in answerObjects)
            {
                pokemonObj.SetActive(true);
            }
            int selectedAnswer = -1;
            float timeoutDuration = 10f;
            float elapsed = 0f;
            var inputKeys = new[] { KeyCode.W, KeyCode.A, KeyCode.D, KeyCode.S };
            var altInputKeys = new[] { KeyCode.UpArrow, KeyCode.LeftArrow, KeyCode.RightArrow, KeyCode.DownArrow };
            var answerIndices = new[] { 0, 1, 2, 3 };
            while (selectedAnswer == -1 && elapsed < timeoutDuration)
            {
                elapsed += Time.deltaTime;
                // Check keyboard input
                for (int i = 0; i < 4; i++)
                {
                    if (Input.GetKeyDown(inputKeys[i]) || Input.GetKeyDown(altInputKeys[i]))
                    {
                        selectedAnswer = answerIndices[i];
                        break;
                    }
                }
                // Check joystick input
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

                        if (selectedAnswer != -1)
                        {
                            yield return new WaitForSeconds(0.3f);
                            break;
                        }
                    }
                }

                yield return null;
            }
            bool correct = false;
            if (selectedAnswer == -1)
            {
                log.LogInfo("[PeakPelago] Pokemon Trivia timed out - treating as incorrect");
                selectedAnswer = 0;
            }
            else
            {
                correct = question.Options[selectedAnswer] == question.CorrectMon;
                log.LogInfo($"[PeakPelago] Pokemon Trivia answer: {question.Options[selectedAnswer]} (Correct: {correct})");
            }
            if (correct)
            {
                var connectionLog = UnityEngine.Object.FindFirstObjectByType<PlayerConnectionLog>();
                if (connectionLog != null && connectionLog.sfxJoin != null)
                {
                    connectionLog.sfxJoin.Play();
                }

                Texture2D correctTex = LoadOverlayTexture("correct.png");
                if (correctTex != null)
                {
                    var overlayObj = new GameObject("CorrectOverlay");
                    overlayObj.transform.SetParent(answerObjects[selectedAnswer].transform);
                    var overlayRect = overlayObj.AddComponent<RectTransform>();
                    overlayRect.anchorMin = Vector2.zero;
                    overlayRect.anchorMax = Vector2.one;
                    overlayRect.offsetMin = Vector2.zero;
                    overlayRect.offsetMax = Vector2.zero;

                    var overlayImage = overlayObj.AddComponent<Image>();
                    overlayImage.sprite = Sprite.Create(
                        correctTex,
                        new Rect(0, 0, correctTex.width, correctTex.height),
                        new Vector2(0.5f, 0.5f)
                    );
                    overlayImage.preserveAspect = true;
                }
                questionText.text = "CORRECT!";
                questionText.color = Color.green;
            }
            else
            {
                var connectionLog = UnityEngine.Object.FindFirstObjectByType<PlayerConnectionLog>();
                if (connectionLog != null && connectionLog.sfxLeave != null)
                {
                    connectionLog.sfxLeave.Play();
                }
                Texture2D wrongTex = LoadOverlayTexture("wrong.png");
                if (wrongTex != null)
                {
                    var overlayObj = new GameObject("WrongOverlay");
                    overlayObj.transform.SetParent(answerObjects[selectedAnswer].transform);
                    var overlayRect = overlayObj.AddComponent<RectTransform>();
                    overlayRect.anchorMin = Vector2.zero;
                    overlayRect.anchorMax = Vector2.one;
                    overlayRect.offsetMin = Vector2.zero;
                    overlayRect.offsetMax = Vector2.zero;

                    var overlayImage = overlayObj.AddComponent<Image>();
                    overlayImage.sprite = Sprite.Create(
                        wrongTex,
                        new Rect(0, 0, wrongTex.width, wrongTex.height),
                        new Vector2(0.5f, 0.5f)
                    );
                    overlayImage.preserveAspect = true;
                }
                questionText.text = "TOO BAD!";
                questionText.color = Color.red;
                Pokemon selectedPokemon = question.Options[selectedAnswer];
                if (_pokemonAfflictions.ContainsKey(selectedPokemon))
                {
                    var statusType = _pokemonAfflictions[selectedPokemon];
                    log.LogInfo($"[PeakPelago] Applying {statusType} for selecting {selectedPokemon}");
                    StatusOverTimeTrapEffect.ApplyStatusOverTime(
                        log,
                        StatusOverTimeTrapEffect.TargetMode.LocalPlayer,
                        statusType,
                        amountPerTick: 0.1f,
                        tickInterval: 1.0f,
                        duration: 5.0f
                    );
                }
            }
            yield return new WaitForSeconds(3f);
            UnityEngine.Object.Destroy(triviaUI);
            _isActive = false;
            log.LogInfo("[PeakPelago] Pokemon Trivia trap completed");
        }
        private static Font LoadCustomFont()
        {
            try
            {
                Font[] loadedFonts = Resources.FindObjectsOfTypeAll<Font>();
                foreach (Font font in loadedFonts)
                {
                    if (font.name.Contains("Daruma") || font.name.Contains("DarumaDropOne"))
                    {
                        _log.LogInfo($"[PeakPelago] Found PEAK's font: {font.name}");
                        return font;
                    }
                }
                _log.LogInfo("[PeakPelago] Searching through all loaded fonts:");
                foreach (Font font in loadedFonts)
                {
                    _log.LogInfo($"[PeakPelago]   - {font.name}");
                }
                foreach (Font font in loadedFonts)
                {
                    if (!font.name.Contains("Arial") && !font.name.Contains("Legacy"))
                    {
                        _log.LogInfo($"[PeakPelago] Using font: {font.name}");
                        return font;
                    }
                }
            }
            catch (Exception ex)
            {
                _log.LogWarning($"[PeakPelago] Could not find PEAK font: {ex.Message}");
            }
            
            return null;
        }
        private static Texture2D LoadPokemonTexture(Pokemon pokemon)
        {
            try
            {
                string resourceName = $"PeakArchipelagoPlugin.pkmns.{pokemon.ToString().ToLower()}.png";
                return LoadEmbeddedTexture(resourceName);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error loading Pokemon texture: {ex.Message}");
            }
            return null;
        }
        private static Texture2D LoadOverlayTexture(string filename)
        {
            try
            {
                string resourceName = $"PeakArchipelagoPlugin.pkmns.{filename}";
                return LoadEmbeddedTexture(resourceName);
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error loading overlay texture: {ex.Message}");
            }
            return null;
        }
        private static Texture2D LoadEmbeddedTexture(string resourceName)
        {
            try
            {
                var assembly = System.Reflection.Assembly.GetExecutingAssembly();
                
                using (var stream = assembly.GetManifestResourceStream(resourceName))
                {
                    if (stream == null)
                    {
                        _log.LogError($"[PeakPelago] Embedded resource not found: {resourceName}");
                        return null;
                    }

                    byte[] fileData = new byte[stream.Length];
                    stream.Read(fileData, 0, fileData.Length);
                    Texture2D tex = new Texture2D(2, 2);
                    tex.LoadImage(fileData);
                    return tex;
                }
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error loading embedded texture: {ex.Message}");
                return null;
            }
        }
        private static PokemonTriviaQuestion GetRandomQuestion()
        {
            var random = new System.Random();
            return PokemonTriviaQuestions[random.Next(PokemonTriviaQuestions.Count)];
        }
        private class PokemonTriviaQuestion
        {
            public string Question { get; set; }
            public Pokemon CorrectMon { get; set; }
            public Pokemon[] Options { get; set; }

            public PokemonTriviaQuestion(string question, Pokemon correctMon)
            {
                Question = question;
                CorrectMon = correctMon;
                var allPokemon = Enum.GetValues(typeof(Pokemon)).Cast<Pokemon>().ToList();
                var wrongPokemon = allPokemon.Where(p => p != correctMon).OrderBy(_ => Guid.NewGuid()).Take(3).ToList();
                var options = new List<Pokemon> { correctMon };
                options.AddRange(wrongPokemon);
                Options = options.OrderBy(_ => Guid.NewGuid()).ToArray();
            }
        }

        private static readonly List<PokemonTriviaQuestion> PokemonTriviaQuestions = new List<PokemonTriviaQuestion>
        {
            new PokemonTriviaQuestion("Which Pokemon is the water starter of Johto?", Pokemon.totodile),
            new PokemonTriviaQuestion("Which Pokemon is a the evolve form of Cyndaquill?", Pokemon.quilava),
            new PokemonTriviaQuestion("Which Pokemon can be found in the Ruins Of Alph?", Pokemon.unown),
            new PokemonTriviaQuestion("Which Pokemon eventually evolves into Raichu?", Pokemon.pichu),
            new PokemonTriviaQuestion("Which Pokemon evolves into Feraligatr?", Pokemon.totodile),
            new PokemonTriviaQuestion("Which Pokemon evolves from Cyndaquil?", Pokemon.quilava),
            new PokemonTriviaQuestion("Which Pokemon has many forms based on letters?", Pokemon.unown),
            new PokemonTriviaQuestion("Which Pokemon eventually evolves into Raichu?", Pokemon.pichu),
            new PokemonTriviaQuestion("Which Pokemon is known as the Guardian of the Skies?", Pokemon.lugia),
            new PokemonTriviaQuestion("Which Pokemon knows the move Sacred Fire?", Pokemon.hooh),
            new PokemonTriviaQuestion("Which of these Pokemon evolve with King's Rock?", Pokemon.slowking),
            new PokemonTriviaQuestion("Which Pokemon is the main pokemon of the 4th Movie?", Pokemon.celebi),
            new PokemonTriviaQuestion("Which of these Pokemon can be encountered in the Goldenrod City Gym?", Pokemon.miltank),
            new PokemonTriviaQuestion("Which Pokemon is obtainable from Mr. Pokemon?", Pokemon.togepi),
            new PokemonTriviaQuestion("Which Rock Type Pokemon blocks the path on Route 36?", Pokemon.sudowoodo),
            new PokemonTriviaQuestion("Which Pokemon has the highest Defense stat?", Pokemon.shuckle),
            new PokemonTriviaQuestion("Which Pokemon appears at the top of Tin Tower?", Pokemon.hooh),
            new PokemonTriviaQuestion("Which Pokemon resides in the Whirl Islands?", Pokemon.lugia),
            new PokemonTriviaQuestion("Which Pokemon is the time travel Pokemon?", Pokemon.celebi),
            new PokemonTriviaQuestion("Which Pokemon can only be found at night?", Pokemon.hoothoot),
            new PokemonTriviaQuestion("Which Pokemon is evolved into while holding Metal Coat on Scyther?", Pokemon.scizor),
            new PokemonTriviaQuestion("Which Pokemon evolves from Eevee with high friendship at night?", Pokemon.umbreon),
            new PokemonTriviaQuestion("Which Pokemon evolves from Eevee with high friendship during day?", Pokemon.espeon),
            new PokemonTriviaQuestion("Which Pokemon evolves from Gloom with a Sun Stone?", Pokemon.bellossom),
            new PokemonTriviaQuestion("Which Pokemon delivers presents?", Pokemon.delibird),
            new PokemonTriviaQuestion("Which Pokemon is the main Pokemon of Pokemon 2000?", Pokemon.lugia),
            new PokemonTriviaQuestion("Which Pokemon does Ash see on his first day as a trainer?", Pokemon.hooh),
            new PokemonTriviaQuestion("Which Pokemon does Eusine constantly chase after?", Pokemon.suicune),
            new PokemonTriviaQuestion("Which Pokemon is the regular form of Paradox Pokemon Flutter Mane?", Pokemon.misdreavus),
            new PokemonTriviaQuestion("Which Pokemon can learn the move Triple Kick?", Pokemon.hitmontop),
            new PokemonTriviaQuestion("Which Pokemon is known for spinning webs to catch prey?", Pokemon.spinarak),
            new PokemonTriviaQuestion("Which Pokemon is an Electric/Water type?", Pokemon.chinchou),
            new PokemonTriviaQuestion("Which Pokemon is a Fire type that resembles molten lava?", Pokemon.slugma),
            new PokemonTriviaQuestion("Which Pokemon eventually evolves into Electivire?", Pokemon.elekid),


        };
    }
}