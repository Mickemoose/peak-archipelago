using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using BepInEx;
using BepInEx.Logging;
using Photon.Pun;
using Zorro.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Peak.AP
{
    public static class CustomTriviaTrapEffect
    {
        private static ManualLogSource _log;
        private static PeakArchipelagoPlugin _plugin;
        private static bool _isActive = false;
        private static readonly List<CustomTriviaQuestion> _loadedQuestions = [];
        private static bool _questionsLoaded = false;

        private static readonly List<CharacterAfflictions.STATUSTYPE> PossibleAfflictions =
        [
            CharacterAfflictions.STATUSTYPE.Hunger,
            CharacterAfflictions.STATUSTYPE.Cold,
            CharacterAfflictions.STATUSTYPE.Poison,
            CharacterAfflictions.STATUSTYPE.Drowsy,
            CharacterAfflictions.STATUSTYPE.Hot,
            CharacterAfflictions.STATUSTYPE.Spores,
            //CharacterAfflictions.STATUSTYPE.Injury
            //CharacterAfflictions.STATUSTYPE.Curse
            //these 2 are pretty rough lets keep em commented out for now
        ];

        private const float DEFAULT_TIMER = 30f;

        public static void Initialize(ManualLogSource log, PeakArchipelagoPlugin plugin)
        {
            _log = log;
            _plugin = plugin;
            LoadQuestionsFromFolders();
        }

        public static void ReloadQuestions()
        {
            _questionsLoaded = false;
            _loadedQuestions.Clear();
            LoadQuestionsFromFolders();
        }

        private static void LoadQuestionsFromFolders()
        {
            if (_questionsLoaded) return;

            _loadedQuestions.Clear();

            try
            {
                string folderName = _plugin.GetCustomTriviaFolder();
                bool includeStandard = _plugin.GetIncludeStandardTrivia();
                List<string> searchPaths = [];

                if (Path.IsPathRooted(folderName))
                {
                    searchPaths.Add(folderName);
                }
                else
                {
                    // Thunderstore installs to plugins folder
                    searchPaths.Add(Path.Combine(Paths.PluginPath, folderName));
                    // Also check BepInEx root
                    searchPaths.Add(Path.Combine(Paths.BepInExRootPath, folderName));
                    // And config folder for manual installs
                    searchPaths.Add(Path.Combine(Paths.ConfigPath, folderName));
                    // IS THIS HOW WE SHOULD DO IT??? IDK I GUESS PEOPLE WILL FIND OUT IF IT BREAKS LOL
                }

                var deserializer = new DeserializerBuilder()
                    .WithNamingConvention(UnderscoredNamingConvention.Instance)
                    .Build();

                int totalFilesFound = 0;

                foreach (string searchPath in searchPaths)
                {
                    if (!Directory.Exists(searchPath))
                    {
                        _log.LogDebug($"[PeakPelago] Trivia path not found: {searchPath}");
                        continue;
                    }

                    _log.LogInfo($"[PeakPelago] Searching for trivia in: {searchPath}");

                    // look recursively for YAML files
                    var yamlFiles = Directory.GetFiles(searchPath, "*.yaml", SearchOption.AllDirectories)
                        .Concat(Directory.GetFiles(searchPath, "*.yml", SearchOption.AllDirectories))
                        .ToArray();

                    totalFilesFound += yamlFiles.Length;

                    foreach (string file in yamlFiles)
                    {
                        LoadYamlFile(file, deserializer);
                    }
                }

                _log.LogInfo($"[PeakPelago] Found {totalFilesFound} YAML files across all search paths");

                // Add default questions if enabled or if no custom questions loaded
                if (includeStandard || _loadedQuestions.Count == 0)
                {
                    _loadedQuestions.AddRange(DefaultQuestions);
                    _log.LogInfo($"[PeakPelago] Added {DefaultQuestions.Count} standard questions");
                }

                _log.LogInfo($"[PeakPelago] Total trivia questions available: {_loadedQuestions.Count}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error loading trivia questions: {ex.Message}");
                _loadedQuestions.AddRange(DefaultQuestions);
            }

            _questionsLoaded = true;
        }

        private static void LoadYamlFile(string filePath, IDeserializer deserializer)
        {
            try
            {
                string yaml = File.ReadAllText(filePath);
                var triviaFile = deserializer.Deserialize<TriviaYamlFile>(yaml);

                if (triviaFile?.Questions == null || triviaFile.Questions.Count == 0)
                {
                    _log.LogWarning($"[PeakPelago] No questions found in {Path.GetFileName(filePath)}");
                    return;
                }

                int loaded = 0;
                foreach (var q in triviaFile.Questions)
                {
                    if (IsValidQuestion(q))
                    {
                        _loadedQuestions.Add(new CustomTriviaQuestion(
                            q.Question,
                            q.CorrectAnswer,
                            q.Options,
                            q.Timer ?? 0
                        ));
                        loaded++;
                    }
                    else
                    {
                        _log.LogWarning($"[PeakPelago] Invalid question in {Path.GetFileName(filePath)}: {q.Question ?? "(no question text)"}");
                    }
                }

                _log.LogInfo($"[PeakPelago] Loaded {loaded} questions from {Path.GetFileName(filePath)}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error parsing {Path.GetFileName(filePath)}: {ex.Message}");
            }
        }

        private static bool IsValidQuestion(YamlQuestion q)
        {
            if (string.IsNullOrWhiteSpace(q.Question)) return false;
            if (string.IsNullOrWhiteSpace(q.CorrectAnswer)) return false;
            if (q.Options == null || q.Options.Length != 4) return false;
            if (!q.Options.Contains(q.CorrectAnswer)) return false;
            return true;
        }

        public static void ApplyCustomTriviaTrapLocal(ManualLogSource log)
        {
            try
            {
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Custom Trivia already active, skipping");
                    return;
                }

                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Custom Trivia - no local character");
                    return;
                }
                _plugin.StartCoroutine(CustomTriviaCoroutine(log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Custom Trivia trap: {ex.Message}");
            }
        }

        public static void ApplyCustomTriviaTrap(ManualLogSource log)
        {
            try
            {
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Custom Trivia already active, queueing for later");
                    _plugin.StartCoroutine(TriviaUIHelper.QueueTriviaForLater(log, () => _isActive, () => ApplyCustomTriviaTrap(log)));
                    return;
                }

                if (Character.localCharacter == null)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Custom Trivia - no local character");
                    return;
                }

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC("StartCustomTriviaRPC", RpcTarget.All);
                }
                else
                {
                    _plugin.StartCoroutine(CustomTriviaCoroutine(log));
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Custom Trivia trap: {ex.Message}");
            }
        }

        private static CharacterAfflictions.STATUSTYPE GetRandomAffliction()
        {
            var random = new System.Random();
            return PossibleAfflictions[random.Next(PossibleAfflictions.Count)];
        }

        /// <summary>
        /// Coroutine that runs the custom trivia UI and logic
        /// </summary>
        /// <param name="log"></param>
        /// <returns></returns>
        private static IEnumerator CustomTriviaCoroutine(ManualLogSource log)
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
                new Vector2(0.25f, 0.40f),
                new Vector2(0.75f, 0.40f),
                new Vector2(0.5f, 0.20f)
            };

            var answerObjects = new List<GameObject>();

            for (int i = 0; i < 4; i++)
            {
                var answerObj = CreateTextAnswer(triviaUI.transform, answerPositions[i], question.Options[i], i, inputSpriteData);
                answerObj.SetActive(false);
                answerObjects.Add(answerObj);
            }

            yield return TriviaUIHelper.DoCountdown(questionText);

            questionText.text = question.Question;
            questionText.fontSize = 48;
            foreach (var answerObj in answerObjects)
            {
                answerObj.SetActive(true);
            }
            
            countdownTimer.transform.parent.gameObject.SetActive(true);

            float timerDuration = question.TimerSeconds > 0 ? question.TimerSeconds : DEFAULT_TIMER;
            var inputCoroutine = TriviaUIHelper.WaitForInput(timerDuration, countdownTimer);
            yield return inputCoroutine;
            int selectedAnswer = (int)inputCoroutine.Current;
            countdownTimer.transform.parent.gameObject.SetActive(false);

            if (selectedAnswer == -1) selectedAnswer = 0;

            bool correct = question.Options[selectedAnswer] == question.CorrectAnswer;
            var selectedBg = answerObjects[selectedAnswer].GetComponent<Image>();

            if (correct)
            {
                TriviaUIHelper.PlayCorrectSound();
                selectedBg.color = new Color(0, 0.8f, 0, 0.8f);
                questionText.text = "CORRECT!";
                questionText.color = Color.green;
            }
            else
            {
                TriviaUIHelper.PlayWrongSound();
                selectedBg.color = new Color(0.8f, 0, 0, 0.8f);

                int correctIndex = Array.IndexOf(question.Options, question.CorrectAnswer);
                if (correctIndex >= 0)
                {
                    var correctBg = answerObjects[correctIndex].GetComponent<Image>();
                    correctBg.color = new Color(0, 0.8f, 0, 0.8f);
                }

                questionText.text = "TOO BAD!";
                questionText.color = Color.red;

                var affliction = GetRandomAffliction();
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
            UnityEngine.Object.Destroy(triviaUI);
            _isActive = false;
            log.LogInfo("[PeakPelago] Custom Trivia trap completed");
        }

        /// <summary>
        /// Creates a UI element for a text answer
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="position"></param>
        /// <param name="answerText"></param>
        /// <param name="index"></param>
        /// <param name="inputSpriteData"></param>
        /// <returns></returns>
        private static GameObject CreateTextAnswer(Transform parent, Vector2 position, string answerText, int index, InputSpriteData inputSpriteData)
        {
            var answerObj = new GameObject($"Answer{index}");
            answerObj.transform.SetParent(parent);
            var answerRect = answerObj.AddComponent<RectTransform>();
            answerRect.anchorMin = position;
            answerRect.anchorMax = position;
            answerRect.sizeDelta = new Vector2(400, 100);
            answerRect.anchoredPosition = Vector2.zero;

            var bgImage = answerObj.AddComponent<Image>();
            bgImage.color = new Color(0, 0, 0, 0.7f);

            var answerTextObj = new GameObject("AnswerText");
            answerTextObj.transform.SetParent(answerObj.transform);
            var answerTextRect = answerTextObj.AddComponent<RectTransform>();
            answerTextRect.anchorMin = new Vector2(0.15f, 0f);
            answerTextRect.anchorMax = new Vector2(1f, 1f);
            answerTextRect.offsetMin = Vector2.zero;
            answerTextRect.offsetMax = Vector2.zero;

            var text = answerTextObj.AddComponent<Text>();
            text.font = TriviaUIHelper.LoadCustomFont() ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = 28;
            text.color = Color.white;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.text = answerText;

            var outline = answerTextObj.AddComponent<Outline>();
            outline.effectColor = Color.black;
            outline.effectDistance = new Vector2(2, 2);

            TriviaUIHelper.AddInputGlyph(answerObj.transform, index, inputSpriteData);

            return answerObj;
        }

        private static CustomTriviaQuestion GetRandomQuestion()
        {
            if (_loadedQuestions.Count == 0)
            {
                LoadQuestionsFromFolders();
            }

            var random = new System.Random();
            return _loadedQuestions[random.Next(_loadedQuestions.Count)];
        }

        private class TriviaYamlFile
        {
            public List<YamlQuestion> Questions { get; set; }
        }

        private class YamlQuestion
        {
            public string Question { get; set; }
            public string CorrectAnswer { get; set; }
            public string[] Options { get; set; }
            public float? Timer { get; set; }
        }

        private class CustomTriviaQuestion
        {
            public string Question { get; set; }
            public string CorrectAnswer { get; set; }
            public string[] Options { get; set; }
            public float TimerSeconds { get; set; }

            public CustomTriviaQuestion(string question, string correctAnswer, string[] options, float timerSeconds = 0)
            {
                Question = question;
                CorrectAnswer = correctAnswer;
                Options = options;
                TimerSeconds = timerSeconds;
            }
        }

        /// <summary>
        /// Default trivia questions so theres always some ready. will probably add some more later
        /// </summary>
        private static readonly List<CustomTriviaQuestion> DefaultQuestions =
        [
            new("What is the capital of France?", "Paris", ["Paris", "London", "Berlin", "Madrid"]),
            new("Which planet is known as the Red Planet?", "Mars", ["Venus", "Mars", "Jupiter", "Saturn"]),
            new("What is 2 + 2?", "4", ["3", "4", "5", "22"]),
            new("Who wrote 'Hamlet'?", "William Shakespeare", ["Charles Dickens", "Mark Twain", "William Shakespeare", "Jane Austen"]),
            new("What is the largest ocean on Earth?", "Pacific Ocean", ["Atlantic Ocean", "Indian Ocean", "Arctic Ocean", "Pacific Ocean"]),
            new("What is the chemical symbol for water?", "H2O", ["CO2", "O2", "H2O", "NaCl"]),
            new("Who painted the Mona Lisa?", "Leonardo da Vinci", ["Vincent van Gogh", "Pablo Picasso", "Leonardo da Vinci", "Claude Monet"]),
            new("What is the hardest natural substance on Earth?", "Diamond", ["Gold", "Iron", "Diamond", "Silver"]),
            new("Which country is known as the Land of the Rising Sun?", "Japan", ["China", "Japan", "Thailand", "South Korea"]),
            new("What is the smallest prime number?", "2", ["1", "2", "3", "5"]),
            new("In which year did the Titanic sink?", "1912", ["1905", "1912", "1918", "1923"]),
            new("What is the main ingredient in guacamole?", "Avocado", ["Tomato", "Onion", "Avocado", "Pepper"]),
        ];

    }
}