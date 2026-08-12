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
        private readonly List<LineHandler> _lineHandlers;

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
            _lineHandlers = BuildLineHandlers();
        }

        private sealed class LineHandler
        {
            public readonly Func<StateData, string> Write;
            public readonly Action<StateData, string> Read;

            public LineHandler(Func<StateData, string> write, Action<StateData, string> read)
            {
                Write = write;
                Read = read;
            }
        }

        private List<LineHandler> BuildLineHandlers()
        {
            return new List<LineHandler>
            {
                new LineHandler(
                    d => d.LastProcessedItemIndex.ToString(),
                    (d, line) => { if (int.TryParse(line.Trim(), out int idx)) { d.LastProcessedItemIndex = idx; _log.LogDebug($"[PeakPelago] Loaded item index: {idx}"); } }),
                new LineHandler(
                    d => string.Join(",", d.ReportedChecks.Select(x => x.ToString())),
                    (d, line) => { if (!string.IsNullOrEmpty(line)) ParseLongSet(line, d.ReportedChecks, "reported checks"); }),
                new LineHandler(
                    d => d.TotalLuggageOpened.ToString(),
                    (d, line) => { if (int.TryParse(line.Trim(), out int luggage)) { d.TotalLuggageOpened = luggage; _log.LogDebug($"[PeakPelago] Loaded luggage count: {luggage}"); } }),
                new LineHandler(
                    d => string.Join(",", d.UnlockedAscents.Select(x => x.ToString())),
                    (d, line) => { if (!string.IsNullOrEmpty(line)) ParseIntSet(line, d.UnlockedAscents, "unlocked ascents"); }),
                new LineHandler(
                    d => "",
                    (d, line) => { }),
                new LineHandler(
                    d => d.TotalItemsCooked.ToString(),
                    (d, line) => { if (int.TryParse(line.Trim(), out int cooked)) { d.TotalItemsCooked = cooked; _log.LogDebug($"[PeakPelago] Loaded cooked items: {cooked}"); } }),
                new LineHandler(
                    d => d.PitonsPlaced.ToString(),
                    (d, line) => { if (int.TryParse(line.Trim(), out int pitons)) { d.PitonsPlaced = pitons; _log.LogDebug($"[PeakPelago] Loaded pitons placed: {pitons}"); } }),
                new LineHandler(
                    d => d.GlizzysGobbled.ToString(),
                    (d, line) => { if (int.TryParse(line.Trim(), out int glizzys)) { d.GlizzysGobbled = glizzys; _log.LogDebug($"[PeakPelago] Loaded glizzys gobbled: {glizzys}"); } }),
                new LineHandler(
                    d => d.DamageBlockedByMilk.ToString(),
                    (d, line) => { if (float.TryParse(line.Trim(), out float damageBlocked)) { d.DamageBlockedByMilk = damageBlocked; _log.LogDebug($"[PeakPelago] Loaded damage blocked by milk: {damageBlocked}"); } }),
                new LineHandler(
                    d => d.PoisonHealed.ToString(),
                    (d, line) => { if (float.TryParse(line.Trim(), out float poisonHealed)) { d.PoisonHealed = poisonHealed; _log.LogDebug($"[PeakPelago] Loaded poison healed: {poisonHealed}"); } }),
                new LineHandler(
                    d => d.TotalBasketsScored.ToString(),
                    (d, line) => { if (int.TryParse(line.Trim(), out int baskets)) { d.TotalBasketsScored = baskets; _log.LogDebug($"[PeakPelago] Loaded baskets scored: {baskets}"); } })
            };
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

                for (int i = 0; i < _lineHandlers.Count; i++)
                {
                    if (lines.Length > i)
                        _lineHandlers[i].Read(data, lines[i]);
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

        public void Save(StateData data)
        {
            if (_isSaving) return;
            _isSaving = true;

            string[] lines = new string[_lineHandlers.Count];
            for (int i = 0; i < _lineHandlers.Count; i++)
                lines[i] = _lineHandlers[i].Write(data);

            string path = StateFilePath;
            float saveTime = UnityEngine.Time.time;

            Task.Run(() =>
            {
                try
                {
                    string tempPath = path + ".tmp";
                    File.WriteAllLines(tempPath, lines);

                    if (File.Exists(path))
                        File.Delete(path);

                    File.Move(tempPath, path);

                    _lastStateSave = saveTime;
                    StateDirty = false;
                }
                catch (Exception ex)
                {
                    _log.LogError("[PeakPelago] Failed to save state: " + ex.Message);
                }
                finally
                {
                    _isSaving = false;
                }
            });
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
        public int LastProcessedItemIndex { get; set; } = -1;
        public HashSet<long> ReportedChecks { get; } = new HashSet<long>();
        public HashSet<long> OfflineChecks { get; } = new HashSet<long>();
        public int TotalLuggageOpened { get; set; } = 0;
        public HashSet<int> UnlockedAscents { get; } = new HashSet<int>();
        public int TotalItemsCooked { get; set; } = 0;
        public int PitonsPlaced { get; set; } = 0;
        public int GlizzysGobbled { get; set; } = 0;
        public float DamageBlockedByMilk { get; set; } = 0f;
        public float PoisonHealed { get; set; } = 0f;
        public int TotalBasketsScored { get; set; } = 0;

        public void Clear()
        {
            LastProcessedItemIndex = -1;
            ReportedChecks.Clear();
            OfflineChecks.Clear();
            TotalLuggageOpened = 0;
            UnlockedAscents.Clear();
            TotalItemsCooked = 0;
            PitonsPlaced = 0;
            GlizzysGobbled = 0;
            DamageBlockedByMilk = 0f;
            PoisonHealed = 0f;
            TotalBasketsScored = 0;
        }
    }
}