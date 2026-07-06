using System.Collections.Generic;
using System.Text;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Datastructures;

namespace AnimalManager
{
    /// <summary>
    /// Diagnostic helper. Dumps an animal's attached behaviors and its full WatchedAttributes
    /// tree so we can confirm the exact husbandry attribute keys against a live game (see the
    /// ".herddump" command). This is how the "VERIFY" keys in <see cref="AnimalStatReader"/>
    /// get pinned down without decompiling.
    /// </summary>
    public static class HerdDump
    {
        public static string BuildReport(Entity e)
        {
            var sb = new StringBuilder();
            sb.AppendLine();
            sb.AppendLine("=== AnimalManager dump: " + e.Code + " (id " + e.EntityId + ") ===");

            sb.AppendLine("Behaviors:");
            List<EntityBehavior> behaviors = e.SidedProperties?.Behaviors;
            if (behaviors != null)
            {
                foreach (EntityBehavior b in behaviors)
                {
                    sb.AppendLine("  - " + b.GetType().Name + "  (propertyName: " + b.PropertyName() + ")");
                }
            }

            sb.AppendLine("WatchedAttributes:");
            sb.Append(DumpTree(e.WatchedAttributes, 1));
            sb.AppendLine("=== end dump ===");
            return sb.ToString();
        }

        private static string DumpTree(IEnumerable<KeyValuePair<string, IAttribute>> tree, int indent)
        {
            var sb = new StringBuilder();
            string pad = new string(' ', indent * 2);
            foreach (KeyValuePair<string, IAttribute> pair in tree)
            {
                if (pair.Value is TreeAttribute sub)
                {
                    sb.AppendLine(pad + pair.Key + ": {");
                    sb.Append(DumpTree(sub, indent + 1));
                    sb.AppendLine(pad + "}");
                }
                else
                {
                    object val = pair.Value?.GetValue();
                    sb.AppendLine(pad + pair.Key + " = " + val + "  [" + pair.Value?.GetType().Name + "]");
                }
            }
            return sb.ToString();
        }
    }
}
