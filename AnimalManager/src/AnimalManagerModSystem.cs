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

            // Diagnostic: dump the nearest animal's behaviors + WatchedAttributes to the log so
            // the exact husbandry attribute keys can be confirmed against a live game.
            capi.ChatCommands.Create("herddump")
                .WithDescription("Dump the nearest animal's attributes to client-main.log (for stat-key verification).")
                .HandleWith(_ => OnDumpCommand());
        }

        private TextCommandResult OnDumpCommand()
        {
            // Don't name EntityPlayer (assembly not referenced by the runtime source
            // compiler); var keeps it as the inferred type and we only touch the base Pos.
            var player = capi.World?.Player?.Entity;
            if (player == null) return TextCommandResult.Error("No player.");

            Vec3d origin = player.Pos.XYZ;
            Entity nearest = null;
            double best = double.MaxValue;
            foreach (Entity e in capi.World.GetEntitiesAround(origin, 20, 20, AnimalStatReader.IsManageableAnimal))
            {
                double d = origin.DistanceTo(e.Pos.XYZ);
                if (d < best) { best = d; nearest = e; }
            }

            if (nearest == null) return TextCommandResult.Success("No manageable animal within 20 blocks.");

            capi.Logger.Notification(HerdDump.BuildReport(nearest));
            return TextCommandResult.Success("Dumped '" + nearest.Code
                + "' to client-main.log — search for 'AnimalManager dump'.");
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
