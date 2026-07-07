using System.Collections.Generic;

namespace AnimalManager
{
    /// <summary>
    /// Client-side, persisted preferences. Loaded/saved via
    /// <c>api.LoadModConfig&lt;T&gt;</c> / <c>api.StoreModConfig</c> to
    /// <c>VintagestoryData/ModConfig/animalmanager.json</c>.
    /// </summary>
    public class AnimalManagerConfig
    {
        public const string Filename = "animalmanager.json";

        /// <summary>Horizontal + vertical search radius (blocks) around the player.</summary>
        public int SearchRadius = 16;

        /// <summary>If true, only list animals bred in captivity (generation &gt; 0).</summary>
        public bool OnlyDomesticated = false;

        /// <summary>
        /// Ids of stat columns the player has hidden. Anything NOT in this set is shown, so
        /// newly added columns are visible by default. Ids come from <see cref="StatColumns"/>.
        /// </summary>
        public HashSet<string> HiddenColumns = new HashSet<string>();

        /// <summary>Optional row filter: only show pregnant animals.</summary>
        public bool OnlyPregnant = false;

        /// <summary>Optional row filter: only show animals ready to breed.</summary>
        public bool OnlyReadyToBreed = false;

        /// <summary>Whether the in-dialog filter/settings panel is expanded.</summary>
        public bool ShowFilterPanel = false;

        /// <summary>
        /// Auto-hide any column for which no animal currently in range has a value (e.g. Milk
        /// when only chickens are nearby). User column hides in <see cref="HiddenColumns"/> still
        /// apply on top of this.
        /// </summary>
        public bool HideEmptyColumns = true;

        public bool IsColumnVisible(string id)
        {
            return HiddenColumns == null || !HiddenColumns.Contains(id);
        }

        public void SetColumnVisible(string id, bool visible)
        {
            HiddenColumns ??= new HashSet<string>();
            if (visible) HiddenColumns.Remove(id);
            else HiddenColumns.Add(id);
        }
    }
}
