using System;
using System.Collections.Generic;
using Vintagestory.API.Client;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.MathTools;

namespace AnimalManager
{
    /// <summary>
    /// The herd overview window: a table listing every captive animal near the player, one
    /// row each, with a toggle-able set of stat columns and an expandable filter panel.
    /// </summary>
    public class GuiDialogAnimalManager : GuiDialog
    {
        private const int VisibleRows = 15;
        private const double TitleH = 30;
        private const double RowH = 24;
        private const double HeaderH = 26;
        private const double NameW = 155;
        private const double CellGap = 6;
        private const double MinContentW = 380;

        private readonly AnimalManagerConfig config;
        private readonly AnimalScanner scanner;
        private readonly Action saveConfig;

        private List<Entity> rows = new List<Entity>();
        private int scrollOffset;
        private long tickListenerId;

        public GuiDialogAnimalManager(ICoreClientAPI capi, AnimalManagerConfig config, Action saveConfig)
            : base(capi)
        {
            this.config = config;
            this.saveConfig = saveConfig;
            this.scanner = new AnimalScanner(capi);
        }

        public override string ToggleKeyCombinationCode => AnimalManagerModSystem.HotkeyCode;

        public override void OnGuiOpened()
        {
            base.OnGuiOpened();
            scrollOffset = 0;
            Compose();
            // Live refresh so timers/cooldowns update. Pause it while the filter panel is
            // open so toggles don't get yanked out from under the cursor.
            tickListenerId = capi.Event.RegisterGameTickListener(_ =>
            {
                if (IsOpened() && !config.ShowFilterPanel) Compose();
            }, 1000);
        }

        public override void OnGuiClosed()
        {
            base.OnGuiClosed();
            if (tickListenerId != 0)
            {
                capi.Event.UnregisterGameTickListener(tickListenerId);
                tickListenerId = 0;
            }
        }

        public override void OnMouseWheel(MouseWheelEventArgs args)
        {
            base.OnMouseWheel(args);
            if (!IsOpened() || rows.Count <= VisibleRows) return;

            int maxOffset = Math.Max(0, rows.Count - VisibleRows);
            scrollOffset = GameMath.Clamp(scrollOffset - Math.Sign(args.deltaPrecise), 0, maxOffset);
            Compose();
            args.SetHandled();
        }

        // ====================================================================================

        private List<StatColumn> VisibleColumns()
        {
            var list = new List<StatColumn>();
            foreach (StatColumn c in StatColumns.All)
            {
                if (config.IsColumnVisible(c.Id)) list.Add(c);
            }
            return list;
        }

        private void Compose()
        {
            rows = scanner.FindNearby(config);

            int maxOffset = Math.Max(0, rows.Count - VisibleRows);
            if (scrollOffset > maxOffset) scrollOffset = maxOffset;

            List<StatColumn> cols = VisibleColumns();

            // ---- geometry ----
            double tableW = NameW;
            foreach (StatColumn c in cols) tableW += c.Width + CellGap;
            double contentW = Math.Max(tableW, MinContentW);

            double ctrlY = TitleH + 6;
            double ctrlH = 26;

            double panelY = ctrlY + ctrlH + 6;
            double panelH = config.ShowFilterPanel ? FilterPanelHeight(cols) : 0;

            double headerY = panelY + panelH + (config.ShowFilterPanel ? 8 : 0);
            double bodyY = headerY + HeaderH;

            int shown = Math.Min(VisibleRows, rows.Count);
            double bodyH = Math.Max(RowH, shown * RowH);

            bool paged = rows.Count > VisibleRows;
            double pagerY = bodyY + bodyH + 6;

            // ---- shell ----
            ElementBounds dialogBounds = ElementStdBounds.AutosizedMainDialog
                .WithAlignment(EnumDialogArea.CenterMiddle);
            ElementBounds bgBounds = ElementBounds.Fill.WithFixedPadding(GuiStyle.ElementToDialogPadding);
            bgBounds.BothSizing = ElementSizing.FitToChildren;

            CairoFont headerFont = CairoFont.WhiteSmallishText().WithWeight(Cairo.FontWeight.Bold);
            CairoFont cellFont = CairoFont.WhiteSmallText();
            CairoFont infoFont = CairoFont.WhiteDetailText();

            var c2 = capi.Gui.CreateCompo("animalmanager-dialog", dialogBounds)
                .AddShadedDialogBG(bgBounds)
                .AddDialogTitleBar("Animal Manager", OnTitleBarClose)
                .BeginChildElements(bgBounds);

            // ---- top controls ----
            string info = rows.Count + (rows.Count == 1 ? " animal" : " animals")
                          + " within " + config.SearchRadius + " blocks";
            c2.AddStaticText(info, infoFont, ElementBounds.Fixed(0, ctrlY + 4, contentW - 130, ctrlH));

            c2.AddSmallButton(config.ShowFilterPanel ? "Filters ▲" : "Filters ▼",
                OnToggleFilterPanel,
                ElementBounds.Fixed(contentW - 120, ctrlY, 120, ctrlH));

            // ---- filter panel ----
            if (config.ShowFilterPanel) ComposeFilterPanel(c2, cols, panelY, contentW);

            // ---- header row ----
            AddRowCells(c2, headerFont, headerY, cols, null);

            // ---- body rows (windowed) ----
            for (int i = 0; i < shown; i++)
            {
                Entity e = rows[scrollOffset + i];
                AddRowCells(c2, cellFont, bodyY + i * RowH, cols, e);
            }
            if (rows.Count == 0)
            {
                c2.AddStaticText("No captive animals nearby. Widen the radius or relax the filters.",
                    infoFont, ElementBounds.Fixed(0, bodyY + 2, contentW, RowH));
            }

            // ---- pager ----
            if (paged)
            {
                int first = scrollOffset + 1;
                int last = scrollOffset + shown;
                c2.AddSmallButton("▲ Prev", OnPagePrev, ElementBounds.Fixed(0, pagerY, 90, ctrlH));
                c2.AddStaticText("showing " + first + "–" + last + " of " + rows.Count + "  (scroll to page)",
                    infoFont, ElementBounds.Fixed(100, pagerY + 4, contentW - 200, ctrlH));
                c2.AddSmallButton("Next ▼", OnPageNext, ElementBounds.Fixed(contentW - 90, pagerY, 90, ctrlH));
            }

            SingleComposer = c2.EndChildElements().Compose();

            // Restore switch/slider states after compose.
            if (config.ShowFilterPanel) SyncFilterPanelState(cols);
        }

        private void AddRowCells(GuiComposer c, CairoFont font, double y, List<StatColumn> cols, Entity e)
        {
            // Name / header-label for the first (Animal) column.
            string nameText = e == null ? "Animal" : AnimalStatReader.DisplayName(e);
            c.AddStaticText(Clip(nameText, 24), font, ElementBounds.Fixed(0, y + 3, NameW, RowH));

            double x = NameW;
            foreach (StatColumn col in cols)
            {
                string text = e == null ? col.Label : SafeRead(col, e);
                c.AddStaticText(text, font, ElementBounds.Fixed(x, y + 3, col.Width, RowH));
                x += col.Width + CellGap;
            }
        }

        private string SafeRead(StatColumn col, Entity e)
        {
            try { return col.Read(e, capi) ?? AnimalStatReader.Dash; }
            catch { return AnimalStatReader.Dash; }
        }

        // ====================================================================================
        //  Filter panel
        // ====================================================================================

        private double FilterPanelHeight(List<StatColumn> _)
        {
            // Row-filter line + radius line + one line of column toggles per category.
            int categories = Enum.GetValues(typeof(StatCategory)).Length;
            return 34 /*row filters*/ + 30 /*radius*/ + categories * 30 + 10;
        }

        private void ComposeFilterPanel(GuiComposer c, List<StatColumn> cols, double y, double contentW)
        {
            CairoFont label = CairoFont.WhiteDetailText();
            double switchSize = 22;

            // --- row filters ---
            double x = 0;
            c.AddSwitch(on => OnRowFilter("domesticated", on),
                ElementBounds.Fixed(x, y, switchSize, switchSize), "flt-domesticated", switchSize);
            c.AddStaticText("Domesticated only", label, ElementBounds.Fixed(x + 28, y + 3, 150, 24));
            x += 190;
            c.AddSwitch(on => OnRowFilter("pregnant", on),
                ElementBounds.Fixed(x, y, switchSize, switchSize), "flt-pregnant", switchSize);
            c.AddStaticText("Pregnant only", label, ElementBounds.Fixed(x + 28, y + 3, 120, 24));
            x += 160;
            c.AddSwitch(on => OnRowFilter("ready", on),
                ElementBounds.Fixed(x, y, switchSize, switchSize), "flt-ready", switchSize);
            c.AddStaticText("Ready to breed", label, ElementBounds.Fixed(x + 28, y + 3, 130, 24));

            // --- radius slider ---
            double ry = y + 34;
            c.AddStaticText("Radius", label, ElementBounds.Fixed(0, ry + 3, 60, 24));
            c.AddSlider(OnRadiusChanged, ElementBounds.Fixed(64, ry, 220, 24), "flt-radius");

            // --- column toggles, grouped by category ---
            double cy = ry + 30;
            foreach (StatCategory cat in Enum.GetValues(typeof(StatCategory)))
            {
                c.AddStaticText(cat.ToString(), CairoFont.WhiteSmallishText(),
                    ElementBounds.Fixed(0, cy + 3, 90, 24));
                double cx = 95;
                foreach (StatColumn col in StatColumns.All)
                {
                    if (col.Category != cat) continue;
                    c.AddSwitch(on => OnColumnToggle(col.Id, on),
                        ElementBounds.Fixed(cx, cy, switchSize, switchSize), "col-" + col.Id, switchSize);
                    c.AddStaticText(col.Label, label, ElementBounds.Fixed(cx + 26, cy + 3, col.Width + 10, 24));
                    cx += 26 + col.Width + 18;
                }
                cy += 30;
            }
        }

        private void SyncFilterPanelState(List<StatColumn> _)
        {
            SetSwitch("flt-domesticated", config.OnlyDomesticated);
            SetSwitch("flt-pregnant", config.OnlyPregnant);
            SetSwitch("flt-ready", config.OnlyReadyToBreed);
            SingleComposer.GetSlider("flt-radius")?.SetValues(config.SearchRadius, 4, 64, 1, " blocks");
            foreach (StatColumn col in StatColumns.All)
            {
                SetSwitch("col-" + col.Id, config.IsColumnVisible(col.Id));
            }
        }

        private void SetSwitch(string key, bool on)
        {
            GuiElementSwitch sw = SingleComposer.GetSwitch(key);
            if (sw != null) sw.On = on;
        }

        // ====================================================================================
        //  Callbacks
        // ====================================================================================

        private bool OnToggleFilterPanel()
        {
            config.ShowFilterPanel = !config.ShowFilterPanel;
            saveConfig();
            Compose();
            return true;
        }

        private void OnRowFilter(string which, bool on)
        {
            switch (which)
            {
                case "domesticated": config.OnlyDomesticated = on; break;
                case "pregnant": config.OnlyPregnant = on; break;
                case "ready": config.OnlyReadyToBreed = on; break;
            }
            saveConfig();
            Compose();
        }

        private void OnColumnToggle(string id, bool on)
        {
            config.SetColumnVisible(id, on);
            saveConfig();
            Compose();
        }

        private bool OnRadiusChanged(int value)
        {
            config.SearchRadius = value;
            saveConfig();
            Compose();
            return true;
        }

        private bool OnPagePrev()
        {
            scrollOffset = Math.Max(0, scrollOffset - VisibleRows);
            Compose();
            return true;
        }

        private bool OnPageNext()
        {
            int maxOffset = Math.Max(0, rows.Count - VisibleRows);
            scrollOffset = Math.Min(maxOffset, scrollOffset + VisibleRows);
            Compose();
            return true;
        }

        private void OnTitleBarClose()
        {
            TryClose();
        }

        private static string Clip(string s, int max)
        {
            if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
            return s.Substring(0, max - 1) + "…";
        }
    }
}
