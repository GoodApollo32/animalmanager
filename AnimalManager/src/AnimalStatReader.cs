using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;
using Vintagestory.GameContent;

namespace AnimalManager
{
    /// <summary>
    /// Reads husbandry data off an animal <see cref="Entity"/>.
    ///
    /// Design note: everything is read from <c>entity.WatchedAttributes</c> (the
    /// network-synced attribute tree, so this works client-side) or from the presence of a
    /// well-known behavior type. We deliberately avoid touching uncertain public members on
    /// the husbandry behaviors so the mod always compiles and always runs; when an attribute
    /// key is wrong or the value is absent, the column simply shows <see cref="Dash"/>.
    ///
    /// The attribute KEYS below marked "VERIFY" should be confirmed against the decompiled
    /// game source (see README "Verifying attribute keys"). The confident ones (health,
    /// generation) are stable across versions.
    /// </summary>
    public static class AnimalStatReader
    {
        public const string Dash = "—";

        // ---- Attribute keys ------------------------------------------------------------
        // Confident (stable):
        private const string KeyGeneration = "generation";
        private const string KeyHealthTree = "health";
        private const string KeyHealthCurrent = "currenthealth";
        private const string KeyHealthMax = "maxhealth";
        private const string KeyNameTagTree = "nametag";
        private const string KeyNameTagName = "name";
        // Verified against live 1.22 dumps (game:chicken-hen + valais/turdag goats):
        private const string KeyWeight = "animalWeight";                  // top-level float, body condition
        private const string KeyBirthTotalDays = "birthTotalDays";        // top-level double
        private const string KeyHungerTree = "hunger";                    // present on goats/pigs, not hens
        private const string KeyHungerSaturation = "saturation";
        private const string KeyMultiplyTree = "multiply";
        private const string KeyIsPregnant = "isPregnant";                // bool, set while gestating
        private const string KeyPregnancyStartDays = "totalDaysPregnancyStart";
        private const string KeyCooldownUntilDays = "totalDaysCooldownUntil";
        private const string KeyLastBirthDays = "totalDaysLastBirth";     // -9999 == never calved
        private const string KeyLastMilkedHours = "lastMilkedTotalHours";  // top-level float; dairy species only

        // ================================================================================
        //  Membership
        // ================================================================================

        /// <summary>
        /// True for animals we want to manage: a living, non-player agent that is part of the
        /// husbandry system (has a generation attribute, or a breeding/growth behavior).
        /// Wild predators without these are naturally excluded.
        /// </summary>
        public static bool IsManageableAnimal(Entity e)
        {
            // Keep this broad: any living creature that isn't a player. The husbandry-specific
            // signals (generation attribute / multiply behavior) missed many animals —
            // creative-spawned ones may lack the attribute, and egg-layers use a different
            // multiply subclass. In practice the creatures around you are your livestock; the
            // "Domesticated only" filter narrows to generation > 0 when you want that.
            // (Exclude the player by entity code rather than naming EntityPlayer, whose
            // assembly the runtime source compiler doesn't reference.)
            if (e is not EntityAgent || !e.Alive) return false;
            return e.Code?.Path != "player";
        }

        // ================================================================================
        //  Identity
        // ================================================================================

        public static int Generation(Entity e) => e.WatchedAttributes.GetInt(KeyGeneration, 0);

        /// <summary>Custom nametag if the animal was named, otherwise its species display name.</summary>
        public static string DisplayName(Entity e)
        {
            ITreeAttribute tag = e.WatchedAttributes.GetTreeAttribute(KeyNameTagTree);
            string custom = tag?.GetString(KeyNameTagName);
            if (!string.IsNullOrEmpty(custom)) return custom;

            string name = e.GetName();
            return string.IsNullOrEmpty(name) ? Species(e) : StripSexSuffix(name);
        }

        // The auto-generated name embeds sex, e.g. "Valais goat (female)". The Sex column
        // already shows that, so trim it to keep the Animal column short (English names only;
        // other locales keep the full name and rely on the widened column).
        private static string StripSexSuffix(string name)
        {
            foreach (string suffix in new[] { " (female)", " (male)" })
            {
                if (name.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
                    return name.Substring(0, name.Length - suffix.Length);
            }
            return name;
        }

        /// <summary>Short species label derived from the entity code, e.g. "sheep-bighorn".</summary>
        public static string Species(Entity e)
        {
            string path = e.Code?.Path ?? "?";
            // Trim a trailing sex/age variant so herds group nicely.
            foreach (string suffix in new[] { "-male", "-female", "-baby" })
            {
                if (path.EndsWith(suffix)) return path.Substring(0, path.Length - suffix.Length);
            }
            return path;
        }

        public static string Sex(Entity e)
        {
            string p = (e.Code?.Path ?? "").ToLowerInvariant();
            if (ContainsToken(p, "female", "hen", "ewe", "sow", "cow", "doe", "mare", "jenny")) return "♀";
            if (ContainsToken(p, "male", "rooster", "cockerel", "cock", "ram", "boar", "bull", "buck", "stag", "stallion")) return "♂";
            return Dash;
        }

        // ================================================================================
        //  Vitals
        // ================================================================================

        public static string Health(Entity e)
        {
            // Read from the synced "health" tree (currenthealth/maxhealth) rather than the
            // behavior's public members — the tree keys are stable across versions.
            ITreeAttribute t = e.WatchedAttributes.GetTreeAttribute(KeyHealthTree);
            if (t == null) return Dash;
            return Ratio(t.GetFloat(KeyHealthCurrent), t.GetFloat(KeyHealthMax));
        }

        public static string Weight(Entity e)
        {
            float w = e.WatchedAttributes.GetFloat(KeyWeight, -1f);
            if (w < 0) return Dash;
            return (w * 100f).ToString("0") + "%";
        }

        /// <summary>Current fullness from the "hunger" tree (goats/pigs have it, hens don't).</summary>
        public static string Satiety(Entity e)
        {
            ITreeAttribute t = e.WatchedAttributes.GetTreeAttribute(KeyHungerTree);
            if (t == null || !t.HasAttribute(KeyHungerSaturation)) return Dash;
            return t.GetFloat(KeyHungerSaturation).ToString("0.#");
        }

        // ================================================================================
        //  Age & domestication
        // ================================================================================

        public static string Stage(Entity e)
        {
            bool juvenile = e.GetBehavior<EntityBehaviorGrow>() != null
                            || ContainsToken((e.Code?.Path ?? "").ToLowerInvariant(),
                                             "baby", "chick", "piglet", "lamb", "calf", "kid", "cub");
            return juvenile ? "Juvenile" : "Adult";
        }

        public static string GenerationLabel(Entity e)
        {
            int gen = Generation(e);
            return gen <= 0 ? "Wild" : "Gen " + gen;
        }

        public static string Age(Entity e)
        {
            // birthTotalDays can be negative (animals spawned/imported "before" day 0), so test
            // for the attribute's presence rather than a negative-means-missing sentinel.
            if (!e.WatchedAttributes.HasAttribute(KeyBirthTotalDays)) return Dash;
            double birth = e.WatchedAttributes.GetDouble(KeyBirthTotalDays);
            double days = (e.World?.Calendar?.TotalDays ?? birth) - birth;
            if (days < 0) return Dash;
            return days < 1 ? "<1d" : days.ToString("0") + "d";
        }

        // ================================================================================
        //  Breeding
        // ================================================================================

        // Hens lay eggs; their reproduction is reported in the Eggs column instead of Breeding.
        private static bool IsEggLayer(Entity e)
        {
            return ContainsToken((e.Code?.Path ?? "").ToLowerInvariant(), "hen");
        }

        /// <summary>Pregnant / on-cooldown / ready, from the synced "multiply" tree.</summary>
        public static string Breeding(Entity e)
        {
            if (IsEggLayer(e)) return Dash; // egg-layers report via the Eggs column

            // Gate on the tree, not the behavior type: egg-layers (e.g. chickens) attach a
            // different multiply subclass, so GetBehavior<EntityBehaviorMultiply>() misses them
            // even though the multiply tree — verified present on a chicken-hen — is right here.
            ITreeAttribute t = e.WatchedAttributes.GetTreeAttribute(KeyMultiplyTree);
            if (t == null) return Dash;

            if (IsPregnantTree(t)) return "Pregnant";

            double now = e.World?.Calendar?.TotalDays ?? 0;
            double cooldown = t.GetDouble(KeyCooldownUntilDays, -1);
            if (cooldown > now) return "CD " + (cooldown - now).ToString("0.0") + "d";
            return "Ready";
        }

        public static bool IsPregnant(Entity e)
        {
            ITreeAttribute t = e.WatchedAttributes.GetTreeAttribute(KeyMultiplyTree);
            return t != null && IsPregnantTree(t);
        }

        // Use only multiply.isPregnant — it resets to false at birth. totalDaysPregnancyStart
        // is NOT cleared after birth, so testing it caused does to read "Pregnant" even once
        // they had calved and were ready to mate again.
        private static bool IsPregnantTree(ITreeAttribute multiply)
        {
            return multiply.GetBool(KeyIsPregnant, false);
        }

        public static bool IsReadyToBreed(Entity e)
        {
            if (IsPregnant(e)) return false;
            ITreeAttribute t = e.WatchedAttributes.GetTreeAttribute(KeyMultiplyTree);
            if (t == null) return false;
            double now = e.World?.Calendar?.TotalDays ?? 0;
            return t.GetDouble(KeyCooldownUntilDays, -1) <= now;
        }

        // ================================================================================
        //  Products & timers
        // ================================================================================

        // Milk cooldown in in-game hours (goats/cows are milkable once per day).
        private const double MilkCooldownHours = 24;

        public static string Milk(Entity e)
        {
            // Dairy species (goats/cows confirmed) carry a top-level "lastMilkedTotalHours";
            // hens don't have it at all -> not a dairy animal.
            if (!e.WatchedAttributes.HasAttribute(KeyLastMilkedHours)) return Dash;

            // A doe only lactates after giving birth. multiply.totalDaysLastBirth == -9999 (or
            // <= 0) means she has never calved, so she cannot be milked yet -> "Dry".
            ITreeAttribute m = e.WatchedAttributes.GetTreeAttribute(KeyMultiplyTree);
            double lastBirth = m?.GetDouble(KeyLastBirthDays, -9999) ?? -9999;
            if (lastBirth <= 0) return "Dry";

            // Lactating: ready unless still within the post-milking cooldown.
            float lastMilked = e.WatchedAttributes.GetFloat(KeyLastMilkedHours, 0);
            double nowHours = (e.World?.Calendar?.TotalDays ?? 0) * (e.World?.Calendar?.HoursPerDay ?? 24);
            double since = nowHours - lastMilked;
            return since >= MilkCooldownHours ? "Ready" : "in " + (MilkCooldownHours - since).ToString("0") + "h";
        }

        public static string Eggs(Entity e)
        {
            if (!IsEggLayer(e)) return Dash;

            // There is no synced per-egg countdown (routine laying is computed server-side from
            // food/time). The multiply cooldown is the hen's reproduction-readiness signal, so
            // surface that: "Ready" == can lay/breed now, otherwise the days remaining.
            ITreeAttribute t = e.WatchedAttributes.GetTreeAttribute(KeyMultiplyTree);
            if (t == null) return "Layer";
            double now = e.World?.Calendar?.TotalDays ?? 0;
            double cooldown = t.GetDouble(KeyCooldownUntilDays, -1);
            return cooldown > now ? "in " + (cooldown - now).ToString("0.0") + "d" : "Ready";
        }

        public static string Harvest(Entity e)
        {
            if (e.GetBehavior<EntityBehaviorHarvestable>() == null) return Dash;
            // Drops scale with weight; surface the weight as a proxy for yield.
            string w = Weight(e);
            return w == Dash ? "Yes" : "~" + w;
        }

        // ================================================================================
        //  Helpers
        // ================================================================================

        private static string Ratio(float cur, float max)
        {
            if (max <= 0) return Dash;
            return cur.ToString("0.#") + "/" + max.ToString("0.#");
        }

        private static bool ContainsToken(string haystack, params string[] tokens)
        {
            foreach (string token in tokens)
            {
                if (haystack.Contains(token)) return true;
            }
            return false;
        }
    }
}
