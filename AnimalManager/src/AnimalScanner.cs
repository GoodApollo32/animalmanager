using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace AnimalManager
{
    /// <summary>
    /// Finds the captive animals near the player. Kept separate from the GUI so the
    /// "what counts / how far" logic is easy to tune and test.
    /// </summary>
    public class AnimalScanner
    {
        private readonly ICoreClientAPI capi;

        public AnimalScanner(ICoreClientAPI capi)
        {
            this.capi = capi;
        }

        public List<Entity> FindNearby(AnimalManagerConfig config)
        {
            var result = new List<Entity>();

            // Avoid naming the EntityPlayer type (its assembly isn't referenced by the
            // runtime source compiler); the base Entity.Pos is all we need.
            if (capi.World?.Player?.Entity == null) return result;

            Vec3d pos = capi.World.Player.Entity.Pos.XYZ;
            float range = config.SearchRadius;

            Entity[] found = capi.World.GetEntitiesAround(pos, range, range, e =>
            {
                if (!AnimalStatReader.IsManageableAnimal(e)) return false;
                if (config.OnlyDomesticated && AnimalStatReader.Generation(e) <= 0) return false;
                if (config.OnlyPregnant && !AnimalStatReader.IsPregnant(e)) return false;
                if (config.OnlyReadyToBreed && !AnimalStatReader.IsReadyToBreed(e)) return false;
                return true;
            });

            if (found != null) result.AddRange(found);

            // Group by species, then by name, so a herd reads tidily.
            result.Sort((a, b) =>
            {
                int c = string.Compare(AnimalStatReader.Species(a), AnimalStatReader.Species(b),
                                       System.StringComparison.OrdinalIgnoreCase);
                if (c != 0) return c;
                return string.Compare(AnimalStatReader.DisplayName(a), AnimalStatReader.DisplayName(b),
                                      System.StringComparison.OrdinalIgnoreCase);
            });

            return result;
        }
    }
}
