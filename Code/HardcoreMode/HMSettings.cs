using System;

namespace Celeste.Mod.HardcoreMode
{
    public enum IconPositions
    {
        BottomLeft,
        BottomRight,
        TopRight
    }

    public class HMSettings : EverestModuleSettings
    {
        private static readonly string[] HardcoreIconStrings = new string[]
        {
            "OPTIONS_OFF",
            "MODOPTIONS_HM_HARDCOREICON_TRANSPARENT",
            "OPTIONS_ON"
        };

        private static readonly string[] IconPositionStrings = new string[]
        {
            "MODOPTIONS_HM_ICONPOSITION_BOTTOMLEFT",
            "MODOPTIONS_HM_ICONPOSITION_BOTTOMRIGHT",
            "MODOPTIONS_HM_ICONPOSITION_TOPRIGHT"
        };

        public int HardcoreIcon { get; set; } = 1;

        public IconPositions IconPosition { get; set; } = IconPositions.BottomLeft;

        public ButtonBinding QuickHardcoreToggle { get; set; }

        public bool AlwaysSpawnGoldens { get; set; } = false;

        // called automatically by Everest to override the menu entry creation
        public void CreateHardcoreIconEntry(TextMenu menu, bool inGame)
        {
            menu.Add(
                new TextMenu.Slider(
                    Dialog.Clean("MODOPTIONS_HM_HARDCOREICON"),  // label
                    i => Dialog.Clean(HardcoreIconStrings[i]),   // option choices shown
                    0,                                           // min
                    HardcoreIconStrings.Length - 1,              // max
                    HardcoreIcon                                 // initial value
                )
                .Change(UpdateHardcoreIcon)                      // called when setting is changed
            );
        }

        public void CreateIconPositionEntry(TextMenu menu, bool inGame)
        {
            menu.Add(
                new TextMenu.Slider(
                    Dialog.Clean("MODOPTIONS_HM_ICONPOSITION"),
                    i => Dialog.Clean(IconPositionStrings[i]),
                    0,
                    IconPositionStrings.Length - 1,
                    (int)IconPosition
                )
                .Change(UpdateIconPosition)
            );
        }

        private void UpdateHardcoreIcon(int pos)
        {
            HardcoreIcon = pos;
        }

        private void UpdateIconPosition(int pos)
        {
            IconPosition = (IconPositions)pos;
        }
    }
}
