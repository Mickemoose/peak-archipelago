using System.Collections.Generic;
using System.Linq;
using BepInEx.Logging;
using Photon.Pun;
using UnityEngine;

namespace Peak.AP
{
    public static class SoulOrbiterManager
    {
        public static ManualLogSource Log;
        private static int _orbiterViewId = -1;
        private static readonly HashSet<int> _ourOrbiterViews = new HashSet<int>();

        public static bool SoulItemOwned()
        {
            var session = PeakArchipelagoPlugin._instance?._session;
            if (session == null) return false;
            return session.Items.AllItemsReceived.Any(item =>
            {
                string name = session.Items.GetItemName(item.ItemId, item.ItemGame);
                return name != null && name.Equals("Scoutmaster's Soul", System.StringComparison.OrdinalIgnoreCase);
            });
        }

        public static bool IsOurs(int viewId) => _ourOrbiterViews.Contains(viewId);

        public static void ResetForScene()
        {
            _orbiterViewId = -1;
        }

        public static void RefreshOrbiter()
        {
            if (!PhotonNetwork.IsMasterClient || !PhotonNetwork.InRoom) return;
            if (!SoulItemOwned()) return;
            if (_orbiterViewId != -1)
            {
                var existing = PhotonView.Find(_orbiterViewId);
                if (existing != null) return;
                _orbiterViewId = -1;
            }

            var host = Character.localCharacter;
            if (host == null) return;

            var obj = PhotonNetwork.Instantiate("ScoutmasterGhostOrbiter", host.Center + Vector3.up, Quaternion.identity, 0);
            if (obj == null)
            {
                Log?.LogWarning("[PeakPelago] Failed to spawn Scoutmaster soul orbiter");
                return;
            }

            var view = obj.GetComponent<PhotonView>();
            if (view != null)
            {
                _orbiterViewId = view.ViewID;
                _ourOrbiterViews.Add(view.ViewID);
            }

            var orbiter = obj.GetComponent<Peak.ScoutmasterGhostOrbiter>();
            if (orbiter != null)
            {
                orbiter.SetOrbitCharacter(host);
            }
            Log?.LogInfo("[PeakPelago] Scoutmaster's soul now orbits the host");
        }
    }
}
