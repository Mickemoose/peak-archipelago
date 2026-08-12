using System.Collections.Generic;
using System.Linq;

namespace Peak.AP
{
    public static class TrapHelpers
    {
        public static List<Character> GetValidCharacters(bool excludePassedOut = true)
        {
            if (Character.AllCharacters == null) return new List<Character>();
            return Character.AllCharacters.Where(c =>
                c != null &&
                c.gameObject.activeInHierarchy &&
                !c.data.dead &&
                (!excludePassedOut || !c.data.fullyPassedOut)
            ).ToList();
        }

        public static Character GetRandomValidCharacter(bool excludePassedOut = true)
        {
            var valid = GetValidCharacters(excludePassedOut);
            return valid.Count == 0 ? null : valid[UnityEngine.Random.Range(0, valid.Count)];
        }
    }
}
