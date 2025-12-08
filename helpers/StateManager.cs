using BepInEx;
using BepInEx.Logging;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

namespace Peak.AP
{
    public class StateManager
    {
        private readonly ManualLogSource _log;
        private readonly PeakArchipelagoPlugin _plugin;
        
        private string _lastApSessionUuid = "";
        private bool _isSaving = false;
        private float _lastStateSave = 0f;
        private const float STATE_SAVE_INTERVAL = 5f;
        
        public bool StateDirty { get; set; } = false;
        public float LastStateSave => _lastStateSave;
        public float SaveInterval => STATE_SAVE_INTERVAL;
        public bool IsSaving => _isSaving;
        
        public string StateFilePath
        {
            get
            {
                string safeUuid = string.IsNullOrEmpty(_lastApSessionUuid) 
                    ? "default" 
                    : _lastApSessionUuid.Replace("/", "_").Replace("\\", "_").Replace(":", "_");
                return Path.Combine(Paths.ConfigPath, $"Peak.AP.state.{safeUuid}.txt");
            }
        }
        
        public string OfflineChecksPath => StateFilePath + ".offline";

        public StateManager(ManualLogSource log, PeakArchipelagoPlugin plugin)
        {
            _log = log;
            _plugin = plugin;
        }

        public void SetSessionUuid(string uuid)
        {
            _lastApSessionUuid = uuid;
        }

        public string GetSessionUuid() => _lastApSessionUuid;

        public void CheckAndHandleSessionChange(string currentSessionId, Action onSessionChanged)
        {
            try
            {
                if (string.IsNullOrEmpty(currentSessionId))
                {
                    _log.LogWarning("[PeakPelago] No session_id in slot data");
                    return;
                }

                if (!string.IsNullOrEmpty(_lastApSessionUuid) && _lastApSessionUuid != currentSessionId)
                {
                    _log.LogInfo($"[PeakPelago] Different session detected (was {_lastApSessionUuid}, now {currentSessionId}) - clearing cache");
                    onSessionChanged?.Invoke();
                }

                _lastApSessionUuid = currentSessionId;
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Error checking session change: " + ex.Message);
            }
        }

        public void Load(StateData data)
        {
            try
            {
                if (!File.Exists(StateFilePath))
                {
                    _log.LogInfo("[PeakPelago] No state file found - starting fresh");
                    return;
                }

                string[] lines = File.ReadAllLines(StateFilePath);
                _log.LogInfo($"[PeakPelago] Loading state file with {lines.Length} lines");

                // Line 0: Item index
                if (lines.Length >= 1 && int.TryParse(lines[0].Trim(), out int idx))
                {
                    data.LastProcessedItemIndex = idx;
                    _log.LogDebug($"[PeakPelago] Loaded item index: {idx}");
                }

                // Line 1: Reported checks
                if (lines.Length >= 2 && !string.IsNullOrEmpty(lines[1]))
                {
                    ParseLongSet(lines[1], data.ReportedChecks, "reported checks");
                }

                // Line 2: Total luggage
                if (lines.Length >= 3 && int.TryParse(lines[2].Trim(), out int luggage))
                {
                    data.TotalLuggageOpened = luggage;
                    _log.LogDebug($"[PeakPelago] Loaded luggage count: {luggage}");
                }

                // Line 3: Unlocked ascents
                if (lines.Length >= 4 && !string.IsNullOrEmpty(lines[3]))
                {
                    ParseIntSet(lines[3], data.UnlockedAscents, "unlocked ascents");
                }

                // Line 4: Stamina state
                if (lines.Length >= 5 && !string.IsNullOrEmpty(lines[4]))
                {
                    data.StaminaState = lines[4];
                }

                // Line 5: Items cooked
                if (lines.Length >= 6 && int.TryParse(lines[5].Trim(), out int cooked))
                {
                    data.TotalItemsCooked = cooked;
                    _log.LogDebug($"[PeakPelago] Loaded cooked items: {cooked}");
                }

                // Line 6: Pitons placed
                if (lines.Length >= 7 && int.TryParse(lines[6].Trim(), out int pitons))
                {
                    data.PitonsPlaced = pitons;
                    _log.LogDebug($"[PeakPelago] Loaded pitons placed: {pitons}");
                }

                // Line 7: Glizzys gobbled
                if (lines.Length >= 8 && int.TryParse(lines[7].Trim(), out int glizzys))
                {
                    data.GlizzysGobbled = glizzys;
                    _log.LogDebug($"[PeakPelago] Loaded glizzys gobbled: {glizzys}");
                }

                // Line 8: Damage blocked by milk
                if (lines.Length >= 9 && float.TryParse(lines[8].Trim(), out float damageBlocked))
                {
                    data.DamageBlockedByMilk = damageBlocked;
                    _log.LogDebug($"[PeakPelago] Loaded damage blocked by milk: {damageBlocked}");
                }

                // Line 9: Poison healed
                if (lines.Length >= 10 && float.TryParse(lines[9].Trim(), out float poisonHealed))
                {
                    data.PoisonHealed = poisonHealed;
                    _log.LogDebug($"[PeakPelago] Loaded poison healed: {poisonHealed}");
                }

                // Line 10: Baskets scored
                if (lines.Length >= 11 && int.TryParse(lines[10].Trim(), out int baskets))
                {
                    data.TotalBasketsScored = baskets;
                    _log.LogDebug($"[PeakPelago] Loaded baskets scored: {baskets}");
                }

                _log.LogInfo($"[PeakPelago] State loaded: {data.ReportedChecks.Count} checks, {data.TotalLuggageOpened} luggage, {data.UnlockedAscents.Count} ascents");
                
                LoadOfflineChecks(data.OfflineChecks);
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] CRITICAL ERROR loading state: " + ex.Message);
                _log.LogError("[PeakPelago] Stack trace: " + ex.StackTrace);
                _log.LogWarning("[PeakPelago] Starting with fresh state");
                data.Clear();
            }
        }

        public void Save(StateData data, string staminaState)
        {
            if (_isSaving) return;
            _isSaving = true;

            string[] lines = new string[]
            {
                data.LastProcessedItemIndex.ToString(),
                string.Join(",", data.ReportedChecks.Select(x => x.ToString())),
                data.TotalLuggageOpened.ToString(),
                string.Join(",", data.UnlockedAscents.Select(x => x.ToString())),
                staminaState ?? "0,1.00",
                data.TotalItemsCooked.ToString(),
                data.PitonsPlaced.ToString(),
                data.GlizzysGobbled.ToString(),
                data.DamageBlockedByMilk.ToString(),
                data.PoisonHealed.ToString(),
                data.TotalBasketsScored.ToString()
            };

            string path = StateFilePath;

            Task.Run(() =>
            {
                try
                {
                    string tempPath = path + ".tmp";
                    File.WriteAllLines(tempPath, lines);

                    if (File.Exists(path))
                        File.Delete(path);

                    File.Move(tempPath, path);
                }
                catch (Exception)
                {
                    // Silent fail on background thread
                }
                finally
                {
                    _isSaving = false;
                }
            });

            _lastStateSave = UnityEngine.Time.time;
            StateDirty = false;
        }

        public void SaveOfflineChecks(HashSet<long> offlineChecks)
        {
            try
            {
                string line = string.Join(",", offlineChecks.Select(x => x.ToString()));
                string tempPath = OfflineChecksPath + ".tmp";
                
                File.WriteAllText(tempPath, line);

                if (File.Exists(OfflineChecksPath))
                    File.Delete(OfflineChecksPath);

                File.Move(tempPath, OfflineChecksPath);
                _log.LogDebug($"[PeakPelago] Saved {offlineChecks.Count} offline checks");
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Failed to save offline checks: " + ex.Message);
            }
        }

        public void LoadOfflineChecks(HashSet<long> offlineChecks)
        {
            try
            {
                if (!File.Exists(OfflineChecksPath))
                    return;

                string line = File.ReadAllText(OfflineChecksPath);
                if (string.IsNullOrEmpty(line))
                    return;

                ParseLongSet(line, offlineChecks, "offline checks");
            }
            catch (Exception ex)
            {
                _log.LogError("[PeakPelago] Failed to load offline checks: " + ex.Message);
            }
        }

        public void DeleteOfflineChecksFile()
        {
            try
            {
                if (File.Exists(OfflineChecksPath))
                    File.Delete(OfflineChecksPath);
            }
            catch { }
        }

        private void ParseLongSet(string line, HashSet<long> set, string name)
        {
            try
            {
                var parts = line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                int success = 0;

                foreach (var p in parts)
                {
                    if (long.TryParse(p.Trim(), out long id))
                    {
                        set.Add(id);
                        success++;
                    }
                }

                _log.LogInfo($"[PeakPelago] Loaded {success} {name}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error parsing {name}: {ex.Message}");
            }
        }

        private void ParseIntSet(string line, HashSet<int> set, string name)
        {
            try
            {
                var parts = line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
                int success = 0;

                foreach (var p in parts)
                {
                    if (int.TryParse(p.Trim(), out int val))
                    {
                        set.Add(val);
                        success++;
                    }
                }

                _log.LogInfo($"[PeakPelago] Loaded {success} {name}: {string.Join(", ", set.OrderBy(x => x))}");
            }
            catch (Exception ex)
            {
                _log.LogError($"[PeakPelago] Error parsing {name}: {ex.Message}");
            }
        }
    }

    public class StateData
    {
        public int LastProcessedItemIndex { get; set; } = 0;
        public HashSet<long> ReportedChecks { get; } = new HashSet<long>();
        public HashSet<long> OfflineChecks { get; } = new HashSet<long>();
        public int TotalLuggageOpened { get; set; } = 0;
        public HashSet<int> UnlockedAscents { get; } = new HashSet<int>();
        public string StaminaState { get; set; } = "";
        public int TotalItemsCooked { get; set; } = 0;
        public int PitonsPlaced { get; set; } = 0;
        public int GlizzysGobbled { get; set; } = 0;
        public float DamageBlockedByMilk { get; set; } = 0f;
        public float PoisonHealed { get; set; } = 0f;
        public int TotalBasketsScored { get; set; } = 0;

        public void Clear()
        {
            LastProcessedItemIndex = 0;
            ReportedChecks.Clear();
            OfflineChecks.Clear();
            TotalLuggageOpened = 0;
            UnlockedAscents.Clear();
            StaminaState = "";
            TotalItemsCooked = 0;
            PitonsPlaced = 0;
            GlizzysGobbled = 0;
            DamageBlockedByMilk = 0f;
            PoisonHealed = 0f;
            TotalBasketsScored = 0;
        }
    }
}