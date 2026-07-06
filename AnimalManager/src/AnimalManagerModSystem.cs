using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace AnimalManager
{
    /// <summary>
    /// Entry point. This is a client-only mod: it reads network-synced animal attributes and
    /// renders a herd overview GUI. No server code is needed for v1.
    /// </summary>
    public class AnimalManagerModSystem : ModSystem
    {
        public const string HotkeyCode = "animalmanager";

        private ICoreClientAPI capi;
        private AnimalManagerConfig config;
        private GuiDialogAnimalManager dialog;

        // Client-only: don't load this system on a dedicated server.
        public override bool ShouldLoad(EnumAppSide side) => side == EnumAppSide.Client;

        public override void StartClientSide(ICoreClientAPI api)
        {
            capi = api;

            LoadConfig();

            dialog = new GuiDialogAnimalManager(capi, config, SaveConfig);

            capi.Input.RegisterHotKey(HotkeyCode, "Open Animal Manager", GlKeys.H,
                HotkeyType.GUIOrOtherControls);
            capi.Input.SetHotKeyHandler(HotkeyCode, OnToggleHotkey);

            capi.ChatCommands.Create("herd")
                .WithDescription("Toggle the Animal Manager herd overview window.")
                .HandleWith(_ =>
                {
                    ToggleDialog();
                    return TextCommandResult.Success();
                });

            // Diagnostic: dump every nearby animal's WatchedAttributes to client-main.log so
            // the exact husbandry attribute keys can be confirmed against a live game.
            capi.ChatCommands.Create("herddump")
                .WithDescription("Dump nearby animals' attributes to client-main.log (for stat-key verification).")
                .HandleWith(_ => OnDumpCommand());
        }

        private int dumpCounter;

        private TextCommandResult OnDumpCommand()
        {
            try
            {
                // Don't name EntityPlayer (assembly not referenced by the runtime source
                // compiler); var keeps it as the inferred type and we only touch the base Pos.
                var player = capi.World?.Player?.Entity;
                if (player == null) return TextCommandResult.Error("No player entity.");

                Vec3d origin = player.Pos.XYZ;
                Entity[] found = capi.World.GetEntitiesAround(origin, 15, 15, AnimalStatReader.IsManageableAnimal);
                if (found == null || found.Length == 0)
                {
                    return TextCommandResult.Success("No animals within 15 blocks to dump.");
                }

                dumpCounter++;
                int max = System.Math.Min(found.Length, 8);

                var sb = new System.Text.StringBuilder();
                sb.AppendLine();
                // The incrementing counter keeps each dump's text unique so the logger does not
                // collapse it as a duplicate of a previous identical dump.
                sb.AppendLine("########## AnimalManager dump #" + dumpCounter + " — "
                              + max + " of " + found.Length + " animal(s) within 15 blocks ##########");
                for (int i = 0; i < max; i++)
                {
                    try { sb.Append(HerdDump.BuildReport(found[i])); }
                    catch (System.Exception ex)
                    {
                        sb.AppendLine("<error dumping " + found[i]?.Code + ": " + ex.Message + ">");
                    }
                }

                capi.Logger.Notification(sb.ToString());
                return TextCommandResult.Success("Dumped " + max + " animal(s) to client-main.log — search 'AnimalManager dump #"
                    + dumpCounter + "'.");
            }
            catch (System.Exception ex)
            {
                capi.Logger.Error("[animalmanager] herddump failed: " + ex);
                return TextCommandResult.Error("herddump failed: " + ex.Message);
            }
        }

        private bool OnToggleHotkey(KeyCombination comb)
        {
            ToggleDialog();
            return true;
        }

        private void ToggleDialog()
        {
            if (dialog == null) return;
            if (dialog.IsOpened()) dialog.TryClose();
            else dialog.TryOpen();
        }

        private void LoadConfig()
        {
            try
            {
                config = capi.LoadModConfig<AnimalManagerConfig>(AnimalManagerConfig.Filename);
            }
            catch
            {
                config = null;
            }

            if (config == null)
            {
                config = new AnimalManagerConfig();
                SaveConfig();
            }
        }

        private void SaveConfig()
        {
            capi.StoreModConfig(config, AnimalManagerConfig.Filename);
        }

        public override void Dispose()
        {
            base.Dispose();
            dialog?.Dispose();
            dialog = null;
        }
    }
}
