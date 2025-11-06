using System;
using System.Collections;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class GustTrapEffect
    {
        private static bool _gustActive = false;
        private static float _originalTimeUntilSwitch = 0f;
        private static float _originalTimeUntilNextWind = 0f;
        private static bool _originalWindActive = false;
        private static GameObject _temporaryWindZone = null;
        
        public static void ApplyGustTrap(ManualLogSource log)
        {
            try
            {
                // Check if we're already running a gust trap
                if (_gustActive)
                {
                    log.LogWarning("[PeakPelago] Gust Trap already active, skipping");
                    return;
                }

                // Check if any valid characters exist
                if (Character.AllCharacters == null || Character.AllCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Gust Trap - no characters found");
                    return;
                }

                var validCharacters = Character.AllCharacters.Where(c => 
                    c != null && 
                    c.gameObject.activeInHierarchy && 
                    !c.data.dead
                ).ToList();

                if (validCharacters.Count == 0)
                {
                    log.LogWarning("[PeakPelago] Cannot apply Gust Trap - no valid characters found");
                    return;
                }

                log.LogInfo($"[PeakPelago] Applying Gust Trap! Wind will blow for 10 seconds");

                // Start the gust coroutine
                PeakArchipelagoPlugin._instance.StartCoroutine(ActivateGust(log));
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error applying Gust Trap: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
            }
        }

        private static IEnumerator ActivateGust(ManualLogSource log)
        {
            _gustActive = true;

            // Find existing WindChillZone (it's always in the scene, just might have small bounds)
            WindChillZone windZone = UnityEngine.Object.FindFirstObjectByType<WindChillZone>();
            
            if (windZone == null)
            {
                log.LogError("[PeakPelago] Cannot find WindChillZone - this shouldn't happen!");
                _gustActive = false;
                yield break;
            }

            Vector3 storedWindDirection = Vector3.zero;
            Bounds originalBounds = windZone.windZoneBounds;
            bool isTemporary = false;

            // Check if any player is within the original wind zone bounds (Roots biome check)
            bool inRootsBiome = false;
            if (Character.AllCharacters != null)
            {
                foreach (var character in Character.AllCharacters)
                {
                    if (character != null && originalBounds.Contains(character.Center))
                    {
                        inRootsBiome = true;
                        break;
                    }
                }
            }

            if (!inRootsBiome)
            {
                log.LogInfo("[PeakPelago] Not in Roots biome - temporarily expanding wind zone to cover entire map");
                // Expand bounds to cover entire map
                windZone.windZoneBounds = new Bounds(Vector3.zero, new Vector3(10000f, 10000f, 10000f));
                isTemporary = true;
            }
            else
            {
                log.LogInfo("[PeakPelago] Using existing WindChillZone (Roots biome)");
                // Store original wind state for existing zone
                _originalWindActive = windZone.windActive;
                _originalTimeUntilSwitch = windZone.GetUntilSwitch();
                _originalTimeUntilNextWind = windZone.GetTimeUntilNextWind();
                storedWindDirection = windZone.GetCurrentWindDirection();
            }

            // Only activate wind if we're the master client
            if (PhotonNetwork.IsMasterClient)
            {
                // Get the PhotonView for RPC
                var photonView = windZone.GetComponent<PhotonView>();
                
                if (photonView != null)
                {
                    // Generate random wind direction
                    Vector3 windDirection = GenerateRandomWindDirection();
                    
                    // Force wind on for 10 seconds
                    photonView.RPC("RPCA_ToggleWind", RpcTarget.All, true, windDirection, 10f);
                    
                    log.LogInfo($"[PeakPelago] Gust Trap activated - wind direction: {windDirection}");
                }
                else
                {
                    log.LogError("[PeakPelago] WindChillZone has no PhotonView component!");
                    
                    // Restore original bounds if we changed them
                    if (isTemporary)
                    {
                        windZone.windZoneBounds = originalBounds;
                    }
                    
                    _gustActive = false;
                    yield break;
                }
            }

            // Wait for 10 seconds
            yield return new WaitForSeconds(10f);

            // Restore based on whether we're in Roots or not
            if (isTemporary)
            {
                // Restore original bounds and turn wind off
                if (PhotonNetwork.IsMasterClient)
                {
                    var photonView = windZone.GetComponent<PhotonView>();
                    if (photonView != null)
                    {
                        Vector3 windDirection = GenerateRandomWindDirection();
                        photonView.RPC("RPCA_ToggleWind", RpcTarget.All, false, windDirection, 0f);
                    }
                    
                    windZone.windZoneBounds = originalBounds;
                    log.LogInfo("[PeakPelago] Gust Trap ended - restored original bounds and turned off wind");
                }
            }
            else
            {
                // Restore original wind state for Roots biome
                if (PhotonNetwork.IsMasterClient)
                {
                    var photonView = windZone.GetComponent<PhotonView>();
                    
                    if (photonView != null)
                    {
                        // If wind was originally off, turn it back off
                        if (!_originalWindActive)
                        {
                            Vector3 windDirection = GenerateRandomWindDirection();
                            photonView.RPC("RPCA_ToggleWind", RpcTarget.All, false, windDirection, _originalTimeUntilNextWind);
                            log.LogInfo("[PeakPelago] Gust Trap ended - wind turned back off");
                        }
                        else
                        {
                            // Wind was already on, restore the timer
                            photonView.RPC("RPCA_ToggleWind", RpcTarget.All, true, storedWindDirection, _originalTimeUntilSwitch);
                            log.LogInfo("[PeakPelago] Gust Trap ended - restored original wind state");
                        }
                    }
                }
            }

            log.LogInfo("[PeakPelago] Gust Trap complete!");
            _gustActive = false;
        }

        private static WindChillZone CreateTemporaryWindZone(ManualLogSource log)
        {
            try
            {
                // Create a new GameObject for the wind zone
                _temporaryWindZone = PhotonNetwork.Instantiate("WindChillZone_Temp", Vector3.zero, Quaternion.identity, 0);
                
                if (_temporaryWindZone == null)
                {
                    log.LogError("[PeakPelago] Failed to instantiate temporary WindChillZone via Photon!");
                    
                    // Fallback: create locally if Photon instantiation fails
                    _temporaryWindZone = new GameObject("TempWindChillZone");
                }
                
                // Add or get WindChillZone component
                WindChillZone windZone = _temporaryWindZone.GetComponent<WindChillZone>();
                if (windZone == null)
                {
                    windZone = _temporaryWindZone.AddComponent<WindChillZone>();
                }
                
                // Add PhotonView if it doesn't exist
                PhotonView photonView = _temporaryWindZone.GetComponent<PhotonView>();
                if (photonView == null)
                {
                    photonView = _temporaryWindZone.AddComponent<PhotonView>();
                }
                
                // Configure the wind zone to cover the entire map
                windZone.windZoneBounds = new Bounds(Vector3.zero, new Vector3(10000f, 10000f, 10000f));
                windZone.windForce = 15f;
                windZone.statusApplicationPerSecond = 0.01f;
                windZone.statusType = CharacterAfflictions.STATUSTYPE.Cold;
                windZone.grabStaminaMultiplierDuringWind = 1f;
                windZone.forceRadius = 2f;
                windZone.delayBeforeForce = 0f; // No delay for trap
                windZone.ragdolledWindForceMult = 0.5f;
                windZone.windMovesItems = true;
                windZone.setSlippy = true;
                windZone.useIntensityCurve = false;
                windZone.useRaycast = false;
                windZone.windItemFactor = 1f;
                
                // Set up wind time ranges (not used for trap, but required)
                windZone.windTimeRangeOn = new Vector2(10f, 10f);
                windZone.windTimeRangeOff = new Vector2(0f, 0f);
                
                // Set up light volume thresholds (0 = always full intensity)
                windZone.lightVolumeSampleThreshold_lower = 0f;
                windZone.lightVolumeSampleThreshold_margin = 0.1f;
                
                // Initialize the curve (required even if not used)
                windZone.windIntensityCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);
                
                log.LogInfo("[PeakPelago] Created temporary WindChillZone covering entire map");
                
                return windZone;
            }
            catch (Exception ex)
            {
                log.LogError($"[PeakPelago] Error creating temporary WindChillZone: {ex.Message}");
                log.LogError($"[PeakPelago] Stack trace: {ex.StackTrace}");
                return null;
            }
        }

        private static Vector3 GenerateRandomWindDirection()
        {
            // Generate a random wind direction (similar to the game's logic)
            float randomValue = UnityEngine.Random.value;
            Vector3 baseDirection = Vector3.right * (randomValue > 0.5f ? 1f : -1f);
            return Vector3.Lerp(baseDirection, Vector3.forward, 0.2f).normalized;
        }
    }

    // Extension methods to access private fields via reflection
    public static class WindChillZoneExtensions
    {
        public static float GetUntilSwitch(this WindChillZone windZone)
        {
            var field = typeof(WindChillZone).GetField("untilSwitch", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                return (float)field.GetValue(windZone);
            }
            return 0f;
        }

        public static float GetTimeUntilNextWind(this WindChillZone windZone)
        {
            var field = typeof(WindChillZone).GetField("timeUntilNextWind", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                return (float)field.GetValue(windZone);
            }
            return 0f;
        }

        public static Vector3 GetCurrentWindDirection(this WindChillZone windZone)
        {
            var field = typeof(WindChillZone).GetField("currentWindDirection", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            if (field != null)
            {
                return (Vector3)field.GetValue(windZone);
            }
            return Vector3.right;
        }
    }
}