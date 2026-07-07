using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;

namespace AnimalManager
{
    public enum StatCategory
    {
        Vitals,
        Age,
        Breeding,
        Products
    }

    /// <summary>
    /// One toggle-able column in the herd table. Modeled as data (id + label + category +
    /// extractor) so the GUI and the config both just iterate <see cref="StatColumns.All"/>;
    /// adding a new stat is a single entry here and it automatically becomes both a column
    /// and a filter toggle.
    /// </summary>
    public sealed class StatColumn
    {
        public readonly string Id;
        public readonly string Label;
        public readonly StatCategory Category;
        public readonly double Width;
        public readonly Func<Entity, ICoreClientAPI, string> Read;

        public StatColumn(string id, string label, StatCategory category, double width,
                          Func<Entity, ICoreClientAPI, string> read)
        {
            Id = id;
            Label = label;
            Category = category;
            Width = width;
            Read = read;
        }
    }

    public static class StatColumns
    {
        /// <summary>The full column registry, in display order.</summary>
        public static readonly IReadOnlyList<StatColumn> All = new List<StatColumn>
        {
            // Vitals  (widths give each header word room so it never breaks mid-letter)
            new StatColumn("health", "Health", StatCategory.Vitals, 66,
                (e, capi) => AnimalStatReader.Health(e)),
            // Condition/weight is the husbandry "fed-ness" indicator; animals have no separate
            // saturation meter, so there is no "hunger" column.
            new StatColumn("weight", "Condition", StatCategory.Vitals, 86,
                (e, capi) => AnimalStatReader.Weight(e)),
            // Current fullness (goats/pigs); blank on animals without a hunger tree (hens).
            new StatColumn("satiety", "Satiety", StatCategory.Vitals, 70,
                (e, capi) => AnimalStatReader.Satiety(e)),

            // Age & domestication
            new StatColumn("stage", "Stage", StatCategory.Age, 78,
                (e, capi) => AnimalStatReader.Stage(e)),
            new StatColumn("generation", "Gen.", StatCategory.Age, 64,
                (e, capi) => AnimalStatReader.GenerationLabel(e)),
            new StatColumn("age", "Age", StatCategory.Age, 56,
                (e, capi) => AnimalStatReader.Age(e)),

            // Breeding
            new StatColumn("sex", "Sex", StatCategory.Breeding, 46,
                (e, capi) => AnimalStatReader.Sex(e)),
            new StatColumn("breeding", "Breeding", StatCategory.Breeding, 90,
                (e, capi) => AnimalStatReader.Breeding(e)),

            // Products & timers
            new StatColumn("milk", "Milk", StatCategory.Products, 66,
                (e, capi) => AnimalStatReader.Milk(e)),
            new StatColumn("eggs", "Eggs", StatCategory.Products, 58,
                (e, capi) => AnimalStatReader.Eggs(e)),
            new StatColumn("harvest", "Harvest", StatCategory.Products, 76,
                (e, capi) => AnimalStatReader.Harvest(e)),
        };

        public static StatColumn ById(string id)
        {
            foreach (StatColumn c in All)
            {
                if (c.Id == id) return c;
            }
            return null;
        }
    }
}
