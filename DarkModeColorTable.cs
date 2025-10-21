using System.Drawing;
using System.Windows.Forms;

namespace Allumi.WindowsSensor
{
    /// <summary>
    /// Custom color table for dark mode rendering of tray menu
    /// </summary>
    public class DarkModeColorTable : ProfessionalColorTable
    {
        public override Color MenuItemSelected => Color.FromArgb(62, 62, 64);
        public override Color MenuItemSelectedGradientBegin => Color.FromArgb(62, 62, 64);
        public override Color MenuItemSelectedGradientEnd => Color.FromArgb(62, 62, 64);
        public override Color MenuItemBorder => Color.FromArgb(45, 45, 48);
        public override Color MenuItemPressedGradientBegin => Color.FromArgb(45, 45, 48);
        public override Color MenuItemPressedGradientEnd => Color.FromArgb(45, 45, 48);
        public override Color MenuBorder => Color.FromArgb(51, 51, 55);
        public override Color ImageMarginGradientBegin => Color.FromArgb(37, 37, 38);
        public override Color ImageMarginGradientMiddle => Color.FromArgb(37, 37, 38);
        public override Color ImageMarginGradientEnd => Color.FromArgb(37, 37, 38);
        public override Color ToolStripDropDownBackground => Color.FromArgb(37, 37, 38);
        public override Color SeparatorDark => Color.FromArgb(51, 51, 55);
        public override Color SeparatorLight => Color.FromArgb(51, 51, 55);
    }
}
