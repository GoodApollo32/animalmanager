using Vintagestory.API.Client;
using Vintagestory.API.Common;

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
                    return Vintagestory.API.Common.TextCommandResult.Success();
                });
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
