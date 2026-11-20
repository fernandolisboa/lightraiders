using UnityEngine;

namespace LightRaiders
{
    /// <summary>
    /// Marks one Surface district of a map (CONTEXT.md "Undercroft": an entrance
    /// in each district). Static scene data, generated with the map; Waygates,
    /// Exfil sites and Hatches are placed per district by later slices.
    /// </summary>
    public sealed class District : MonoBehaviour
    {
        [SerializeField]
        private int _index;
        [SerializeField]
        private string _displayName = "District";

        public int Index => _index;
        public string DisplayName => _displayName;

        public void Configure(int index, string displayName)
        {
            _index = index;
            _displayName = displayName;
        }
    }
}
