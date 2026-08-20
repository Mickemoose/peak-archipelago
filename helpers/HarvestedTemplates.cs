using BepInEx.Logging;
using UnityEngine;

namespace Peak.AP
{
    public static class HarvestedTemplates
    {
        public static GameObject WindZone;
        public static GameObject RainStorm;

        private static Transform _holder;

        private static Transform Holder
        {
            get
            {
                if (_holder == null)
                {
                    var go = new GameObject("AP_HarvestedTemplates");
                    go.SetActive(false);
                    Object.DontDestroyOnLoad(go);
                    _holder = go.transform;
                }
                return _holder;
            }
        }

        public static GameObject Keep(GameObject sceneObject, string label, ManualLogSource log)
        {
            if (sceneObject == null) return null;
            var clone = Object.Instantiate(sceneObject, Holder);
            clone.name = "AP_" + label;
            clone.SetActive(false);
            log?.LogInfo($"[PeakPelago] Harvested template: {label}");
            return clone;
        }
    }
}
