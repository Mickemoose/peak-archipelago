using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class ChaosControlTrapEffect
    {
        private static ManualLogSource _log;
        private static PeakArchipelagoPlugin _plugin;
        private static bool _isActive = false;

        public static void Initialize(ManualLogSource log, PeakArchipelagoPlugin plugin)
        {
            _log = log;
            _plugin = plugin;
        }

        public static void ApplyChaosControlTrap(ManualLogSource log)
        {
            try
            {
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Chaos Control Trap already active, skipping");
                    return;
                }

                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Chaos Control Trap - no characters found");
                    return;
                }

                var validCharacters = TrapHelpers.GetValidCharacters();

                if (validCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Chaos Control Trap - no valid characters found");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Chaos Control Trap to {validCharacters.Count} player(s) via RPC!");

                if (PeakArchipelagoPlugin._instance != null && PeakArchipelagoPlugin._instance.PhotonView != null)
                {
                    PeakArchipelagoPlugin._instance.PhotonView.RPC(
                        "StartChaosControlTrapRPC",
                        RpcTarget.All
                    );
                }
                else
                {
                    log.LogWarning("[PeakPelago] PhotonView not available, starting locally only");
                    ApplyChaosControlTrapLocal(log);
                }
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Chaos Control Trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        public static void ApplyChaosControlTrapLocal(ManualLogSource log)
        {
            try
            {
                if (_isActive)
                {
                    log.LogInfo("[PeakPelago] Chaos Control Trap already active locally");
                    return;
                }

                log.LogInfo("[PeakPelago] Starting Chaos Control Trap locally");
                _plugin.StartCoroutine(ChaosControlCoroutine(log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Chaos Control Trap locally: {ex.Message}");
            }
        }

        private static IEnumerator ChaosControlCoroutine(ManualLogSource log)
        {
            _isActive = true;
            float duration = 10f;

            // Wait for loading screen and passed out state
            while (LoadingScreenHandler.loading
                || Character.localCharacter == null
                || Character.localCharacter.data.passedOutOnTheBeach > 0f)
            {
                yield return new WaitForSecondsRealtime(0.25f);
            }

            // --- Screen effect via curseSVFX ---
            ScreenVFX curseSVFX = null;
            if (GUIManager.instance != null)
            {
                curseSVFX = GUIManager.instance.curseSVFX;
            }
            if (curseSVFX == null)
            {
                log.LogWarning("[PeakPelago] Chaos Control: curseSVFX not found, continuing without screen effect");
            }
            else
            {
                curseSVFX.StartFX(0.5f);
                log.LogInfo("[PeakPelago] Chaos Control: Screen effect activated");
            }

            // --- Countdown overlay ---
            Camera mainCamera = Camera.main;
            ChaosControlOverlay overlay = null;
            if (mainCamera != null)
            {
                overlay = mainCamera.gameObject.AddComponent<ChaosControlOverlay>();
                overlay.Initialize(duration);
            }

            // --- Freeze all characters' ragdoll parts ---
            var character = Character.localCharacter;
            float originalMovementModifier = 0f;
            var savedPartStates = new List<(Bodypart part, bool wasKinematic)>();

            if (character != null && character.refs.movement != null && character.refs.ragdoll != null)
            {
                originalMovementModifier = character.refs.movement.movementModifier;
                character.refs.movement.movementModifier = -1f;

                foreach (var part in character.refs.ragdoll.partList)
                {
                    if (part != null && part.Rig != null)
                    {
                        savedPartStates.Add((part, part.Rig.isKinematic));
                        part.Rig.linearVelocity = Vector3.zero;
                        part.Rig.angularVelocity = Vector3.zero;
                        part.Rig.isKinematic = true;
                    }
                }

                log.LogInfo($"[PeakPelago] Chaos Control: Player frozen ({savedPartStates.Count} ragdoll parts)");
            }

            log.LogInfo($"[PeakPelago] Chaos Control active for {duration} seconds");

            // Hold frozen for duration
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            // --- Restore movement ---
            if (character != null && character.refs.movement != null)
            {
                character.refs.movement.movementModifier = originalMovementModifier;

                foreach (var state in savedPartStates)
                {
                    if (state.part != null && state.part.Rig != null)
                    {
                        state.part.Rig.isKinematic = state.wasKinematic;
                        if (!state.wasKinematic)
                        {
                            state.part.Rig.linearVelocity = Vector3.down * 0.1f;
                            state.part.Rig.WakeUp();
                        }
                    }
                }

                yield return new WaitForFixedUpdate();
                yield return new WaitForFixedUpdate();

                log.LogInfo("[PeakPelago] Chaos Control: Player unfrozen");
            }

            // --- Clean up screen effect ---
            if (curseSVFX != null)
            {
                curseSVFX.EndFX();
            }

            // Clean up overlay
            if (overlay != null)
            {
                UnityEngine.Object.Destroy(overlay);
            }

            log.LogInfo("[PeakPelago] Chaos Control Trap complete!");
            _isActive = false;
            PeakArchipelagoPlugin._instance?._trapLinkService?.NotifyTrapComplete();
        }
    }

    /// <summary>
    /// MonoBehaviour attached to the camera that draws a centered countdown timer.
    /// </summary>
    public class ChaosControlOverlay : MonoBehaviour
    {
        private float _timeRemaining;
        private Font _font;
        private GUIStyle _countdownStyle;
        private GUIStyle _labelStyle;
        private bool _initialized = false;

        public void Initialize(float duration)
        {
            _timeRemaining = duration;
            _font = TriviaUIHelper.LoadCustomFont();
            _initialized = true;
        }

        void Update()
        {
            if (!_initialized) return;

            _timeRemaining -= Time.deltaTime;
            if (_timeRemaining <= 0f)
            {
                Destroy(this);
            }
        }

        void OnGUI()
        {
            if (!_initialized || _timeRemaining <= 0f) return;

            if (_countdownStyle == null)
            {
                _countdownStyle = new GUIStyle(GUI.skin.label);
                _countdownStyle.fontSize = 120;
                _countdownStyle.alignment = TextAnchor.MiddleCenter;
                _countdownStyle.normal.textColor = Color.white;
                _countdownStyle.fontStyle = FontStyle.Bold;
                if (_font != null) _countdownStyle.font = _font;

                _labelStyle = new GUIStyle(GUI.skin.label);
                _labelStyle.fontSize = 36;
                _labelStyle.alignment = TextAnchor.MiddleCenter;
                _labelStyle.normal.textColor = Color.white;
                _labelStyle.fontStyle = FontStyle.Bold;
                if (_font != null) _labelStyle.font = _font;
            }

            int seconds = Mathf.CeilToInt(_timeRemaining);

            // "CHAOS CONTROL!" label
            Rect labelRect = new Rect(0, Screen.height * 0.25f, Screen.width, 50f);
            GUI.Label(labelRect, "CHAOS CONTROL!", _labelStyle);

            // Big countdown number
            Rect countdownRect = new Rect(0, Screen.height * 0.35f, Screen.width, 150f);
            GUI.Label(countdownRect, seconds.ToString(), _countdownStyle);
        }
    }
}
