using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using BepInEx.Logging;
using Photon.Pun;
using System.IO;
using Zorro.Core;
using TMPro;

namespace Peak.AP
{
    public static class PokemonTriviaTrapEffect
    {
        private static ManualLogSource _log;
        private static PeakArchipelagoPlugin _plugin;
        private static bool _isActive = false;

        private const float TIMEOUT_DURATION = 20f;

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
            marill,
            natu,
            wooper,
            swinub,
            politoed,
            ledyba,
            larvitar,
            corsola,
            entei,

            // hehe these arent pokemon dont choose them or u get cursed :O
            terriermon,
            wizardmon,
            cheat,
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
            { Pokemon.marill, CharacterAfflictions.STATUSTYPE.Cold },
            { Pokemon.natu, CharacterAfflictions.STATUSTYPE.Drowsy },
            { Pokemon.wooper, CharacterAfflictions.STATUSTYPE.Cold },
            { Pokemon.swinub, CharacterAfflictions.STATUSTYPE.Cold },
            { Pokemon.politoed, CharacterAfflictions.STATUSTYPE.Poison },
            { Pokemon.ledyba, CharacterAfflictions.STATUSTYPE.Web },
            { Pokemon.larvitar, CharacterAfflictions.STATUSTYPE.Injury },
            { Pokemon.corsola, CharacterAfflictions.STATUSTYPE.Cold },
            { Pokemon.entei, CharacterAfflictions.STATUSTYPE.Hot },

            // Curse for fake pokemon
            { Pokemon.terriermon, CharacterAfflictions.STATUSTYPE.Curse },
            { Pokemon.wizardmon, CharacterAfflictions.STATUSTYPE.Curse },
            { Pokemon.cheat, CharacterAfflictions.STATUSTYPE.Curse },
        };

        public static void Initialize(ManualLogSource log, PeakArchipelagoPlugin plugin)
        {
            _log = log;
            _plugin = plugin;
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
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Pokemon Trivia already active, queueing for later");
                    _plugin.StartCoroutine(TriviaUIHelper.QueueTriviaForLater(log, () => _isActive, () => ApplyPokemonTriviaTrap(log)));
                    return;
                }

                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Pokemon Trivia - no local character");
                    return;
                }

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "StartPokemonTriviaRPC",
                        RpcTarget.All
                    );
                }
                else
                {
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
            InputSpriteData inputSpriteData = SingletonAsset<InputSpriteData>.Instance;

            var (triviaUI, questionText) = TriviaUIHelper.CreateTriviaUI();

            Text countdownTimer = TriviaUIHelper.CreateCountdownTimer(triviaUI.transform);
            countdownTimer.transform.parent.gameObject.SetActive(false);

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

                TriviaUIHelper.AddInputGlyph(pokemonObj.transform, i, inputSpriteData);

                pokemonObj.SetActive(false);
                pokemonImages.Add(pokemonImage);
                answerObjects.Add(pokemonObj);
            }

            yield return TriviaUIHelper.DoCountdown(questionText);

            // Show question and answers
            questionText.text = question.Question;
            questionText.fontSize = 48;
            foreach (var pokemonObj in answerObjects)
            {
                pokemonObj.SetActive(true);
            }

            // Show countdown timer
            countdownTimer.transform.parent.gameObject.SetActive(true);

            // Wait for input
            var inputCoroutine = TriviaUIHelper.WaitForInput(TIMEOUT_DURATION, countdownTimer);
            yield return inputCoroutine;
            int selectedAnswer = (int)inputCoroutine.Current;

            // Hide countdown timer
            countdownTimer.transform.parent.gameObject.SetActive(false);

            // Default to first answer if no input
            if (selectedAnswer == -1)
            {
                selectedAnswer = 0;
            }

            // Check if answer is correct
            bool correct = question.CorrectMons.Contains(question.Options[selectedAnswer]);

            if (correct)
            {
                TriviaUIHelper.PlayCorrectSound();

                Texture2D correctTex = LoadOverlayTexture("correct.png");
                if (correctTex != null)
                {
                    AddOverlay(answerObjects[selectedAnswer], correctTex);
                }
                questionText.text = "CORRECT!";
                questionText.color = Color.green;
            }
            else
            {
                TriviaUIHelper.PlayWrongSound();

                Texture2D wrongTex = LoadOverlayTexture("wrong.png");
                if (wrongTex != null)
                {
                    AddOverlay(answerObjects[selectedAnswer], wrongTex);
                }
                questionText.text = "TOO BAD!";
                questionText.color = Color.red;

                // Apply affliction based on selected Pokemon
                Pokemon selectedPokemon = question.Options[selectedAnswer];
                if (_pokemonAfflictions.TryGetValue(selectedPokemon, out var statusType))
                {
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

        private static void AddOverlay(GameObject parent, Texture2D texture)
        {
            var overlayObj = new GameObject("Overlay");
            overlayObj.transform.SetParent(parent.transform);
            var overlayRect = overlayObj.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.offsetMin = Vector2.zero;
            overlayRect.offsetMax = Vector2.zero;

            var overlayImage = overlayObj.AddComponent<Image>();
            overlayImage.sprite = Sprite.Create(
                texture,
                new Rect(0, 0, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );
            overlayImage.preserveAspect = true;
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
            public Pokemon[] CorrectMons { get; set; }
            public Pokemon[] Options { get; set; }
            public PokemonTriviaQuestion(string question, Pokemon correctMon)
                : this(question, [correctMon])
            {
            }
            public PokemonTriviaQuestion(string question, Pokemon[] correctMons)
            {
                Question = question;
                CorrectMons = correctMons;

                var allPokemon = Enum.GetValues(typeof(Pokemon)).Cast<Pokemon>().ToList();

                // Remove ALL correct answers from the wrong answer pool
                var wrongPokemon = allPokemon
                    .Where(p => !correctMons.Contains(p))
                    .OrderBy(_ => Guid.NewGuid())
                    .Take(3) // 3 wrong answers
                    .ToList();

                var displayedCorrect = correctMons.OrderBy(_ => Guid.NewGuid()).First();
                var options = new List<Pokemon> { displayedCorrect };
                options.AddRange(wrongPokemon);
                Options = options.OrderBy(_ => Guid.NewGuid()).ToArray();
            }
        }

        private static readonly List<PokemonTriviaQuestion> PokemonTriviaQuestions = new List<PokemonTriviaQuestion>
        {
            new PokemonTriviaQuestion("Which Pokemon is the water starter of Johto?", Pokemon.totodile),
            new PokemonTriviaQuestion("Which Pokemon is the evolved form of Cyndaquil?", Pokemon.quilava),
            new PokemonTriviaQuestion("Which Pokemon can be found in the Ruins Of Alph?", Pokemon.unown),
            new PokemonTriviaQuestion("Which Pokemon eventually evolves into Raichu?", Pokemon.pichu),
            new PokemonTriviaQuestion("Which Pokemon evolves into Feraligatr?", Pokemon.totodile),
            new PokemonTriviaQuestion("Which Pokemon evolves from Cyndaquil?", Pokemon.quilava),
            new PokemonTriviaQuestion("Which Pokemon has many forms based on letters?", Pokemon.unown),
            new PokemonTriviaQuestion("Which Pokemon is known as the Guardian of the Skies?", Pokemon.lugia),
            new PokemonTriviaQuestion("Which Pokemon knows the move Sacred Fire?", Pokemon.hooh),
            new PokemonTriviaQuestion("Which of these Pokemon evolve with King's Rock?", [Pokemon.slowking, Pokemon.politoed]),
            new PokemonTriviaQuestion("Which Pokemon is the main pokemon of the 4th Movie?", Pokemon.celebi),
            new PokemonTriviaQuestion("Which of these Pokemon can be encountered in the Goldenrod City Gym?", Pokemon.miltank),
            new PokemonTriviaQuestion("Which Pokemon is obtainable from Mr. Pokemon?", Pokemon.togepi),
            new PokemonTriviaQuestion("Which Rock Type Pokemon blocks the path on Route 36?", Pokemon.sudowoodo),
            new PokemonTriviaQuestion("Which Pokemon has the highest Defense stat?", Pokemon.shuckle),
            new PokemonTriviaQuestion("Which Pokemon appears at the top of Tin Tower?", Pokemon.hooh),
            new PokemonTriviaQuestion("Which Pokemon resides in the Whirl Islands?", Pokemon.lugia),
            new PokemonTriviaQuestion("Which Pokemon is the time travel Pokemon?", Pokemon.celebi),
            new PokemonTriviaQuestion("Which Pokemon can only be found at night?", [Pokemon.hoothoot, Pokemon.misdreavus]),
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
            new PokemonTriviaQuestion("Which Pokemon is known for its round, blue body and tail?", Pokemon.marill),
            new PokemonTriviaQuestion("Which Psychic/Flying type Pokemon is known for its small size and big eyes?", Pokemon.natu),
            new PokemonTriviaQuestion("Which of these Pokemon has a regional form that evolves into Clodsire?", Pokemon.wooper),
            new PokemonTriviaQuestion("Which of these evolves from Poliwhirl?", [Pokemon.slowking, Pokemon.politoed]),
            new PokemonTriviaQuestion("Which Pokemon is caught on Mount Silver?", Pokemon.larvitar),
        };
    }
}