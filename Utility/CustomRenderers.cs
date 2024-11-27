using System;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace ImAdjustr.Utility {
    internal class CustomColorTable : ProfessionalColorTable {
        internal readonly Color foreground;
        private Dictionary<string, dynamic> colors;

        internal CustomColorTable(Dictionary<string, dynamic> colors) {
            base.UseSystemColors = false;
            this.colors = colors;
            // IMPORTANT: do not change the tag names in Themes.xml; fill in the provided empty theme to make new ones
            foreground = this.colors["TextForeground"];
        }

        private Color AssignColorToProperty(Func<Color> baseColorGetter, [CallerMemberName] string property = null) {
            try {
                dynamic color = null;
                if (this.colors.ContainsKey(property)) color = this.colors[property];
                return color != Color.Empty ? color : baseColorGetter();
            }
            catch (KeyNotFoundException) {
                Console.WriteLine($"Error updating theme for {property}");
                return baseColorGetter();
            }
        }

        public override Color MenuBorder => AssignColorToProperty(() => base.MenuBorder);
        public override Color MenuItemBorder => AssignColorToProperty(() => base.MenuItemBorder);
        public override Color MenuItemSelected => AssignColorToProperty(() => base.MenuItemSelected);
        public override Color MenuItemSelectedGradientBegin => AssignColorToProperty(() => base.MenuItemSelectedGradientBegin);
        public override Color MenuItemSelectedGradientEnd => AssignColorToProperty(() => base.MenuItemSelectedGradientEnd);
        public override Color MenuStripGradientBegin => AssignColorToProperty(() => base.MenuStripGradientBegin);
        public override Color MenuStripGradientEnd => AssignColorToProperty(() => base.MenuStripGradientEnd);
        public override Color MenuItemPressedGradientBegin => AssignColorToProperty(() => base.MenuItemPressedGradientBegin);
        public override Color MenuItemPressedGradientMiddle => AssignColorToProperty(() => base.MenuItemPressedGradientMiddle);
        public override Color MenuItemPressedGradientEnd => AssignColorToProperty(() => base.MenuItemPressedGradientEnd);
        public override Color ToolStripBorder => AssignColorToProperty(() => base.ToolStripBorder);
        public override Color ToolStripDropDownBackground => AssignColorToProperty(() => base.ToolStripDropDownBackground);
        public override Color ToolStripGradientBegin => AssignColorToProperty(() => base.ToolStripGradientBegin);
        public override Color ToolStripGradientMiddle => AssignColorToProperty(() => base.ToolStripGradientMiddle);
        public override Color ToolStripGradientEnd => AssignColorToProperty(() => base.ToolStripGradientEnd);
        public override Color ToolStripPanelGradientBegin => AssignColorToProperty(() => base.ToolStripPanelGradientBegin);
        public override Color ToolStripPanelGradientEnd => AssignColorToProperty(() => base.ToolStripPanelGradientEnd);
        public override Color ToolStripContentPanelGradientBegin => AssignColorToProperty(() => base.ToolStripContentPanelGradientBegin);
        public override Color ToolStripContentPanelGradientEnd => AssignColorToProperty(() => base.ToolStripContentPanelGradientEnd);
        public override Color ButtonSelectedBorder => AssignColorToProperty(() => base.ButtonSelectedBorder);
        public override Color ButtonSelectedHighlight => AssignColorToProperty(() => base.ButtonSelectedHighlight);
        public override Color ButtonSelectedHighlightBorder => AssignColorToProperty(() => base.ButtonSelectedHighlightBorder);
        public override Color ButtonSelectedGradientBegin => AssignColorToProperty(() => base.ButtonSelectedGradientBegin);
        public override Color ButtonSelectedGradientMiddle => AssignColorToProperty(() => base.ButtonSelectedGradientMiddle);
        public override Color ButtonSelectedGradientEnd => AssignColorToProperty(() => base.ButtonSelectedGradientEnd);
        public override Color ButtonPressedBorder => AssignColorToProperty(() => base.ButtonPressedBorder);
        public override Color ButtonPressedHighlight => AssignColorToProperty(() => base.ButtonPressedHighlight);
        public override Color ButtonPressedHighlightBorder => AssignColorToProperty(() => base.ButtonPressedHighlightBorder);
        public override Color ButtonPressedGradientBegin => AssignColorToProperty(() => base.ButtonPressedGradientBegin);
        public override Color ButtonPressedGradientMiddle => AssignColorToProperty(() => base.ButtonPressedGradientMiddle);
        public override Color ButtonPressedGradientEnd => AssignColorToProperty(() => base.ButtonPressedGradientEnd);
        public override Color ButtonCheckedHighlight => AssignColorToProperty(() => base.ButtonCheckedHighlight);
        public override Color ButtonCheckedHighlightBorder => AssignColorToProperty(() => base.ButtonCheckedHighlightBorder);
        public override Color ButtonCheckedGradientBegin => AssignColorToProperty(() => base.ButtonCheckedGradientBegin);
        public override Color ButtonCheckedGradientMiddle => AssignColorToProperty(() => base.ButtonCheckedGradientMiddle);
        public override Color ButtonCheckedGradientEnd => AssignColorToProperty(() => base.ButtonCheckedGradientEnd);
        public override Color CheckBackground => AssignColorToProperty(() => base.CheckBackground);
        public override Color CheckSelectedBackground => AssignColorToProperty(() => base.CheckSelectedBackground);
        public override Color CheckPressedBackground => AssignColorToProperty(() => base.CheckPressedBackground);
        public override Color ImageMarginGradientBegin => AssignColorToProperty(() => base.ImageMarginGradientBegin);
        public override Color ImageMarginGradientMiddle => AssignColorToProperty(() => base.ImageMarginGradientMiddle);
        public override Color ImageMarginGradientEnd => AssignColorToProperty(() => base.ImageMarginGradientEnd);
        public override Color OverflowButtonGradientBegin => AssignColorToProperty(() => base.OverflowButtonGradientBegin);
        public override Color OverflowButtonGradientMiddle => AssignColorToProperty(() => base.OverflowButtonGradientMiddle);
        public override Color OverflowButtonGradientEnd => AssignColorToProperty(() => base.OverflowButtonGradientEnd);
        public override Color SeparatorDark => AssignColorToProperty(() => base.SeparatorDark);
    }

    internal class CustomRenderer : ToolStripProfessionalRenderer {
        private readonly Color baseThemeForeground;

        internal CustomRenderer(CustomColorTable table) : base(table) { baseThemeForeground = table.foreground; }

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e) {
            if (e.Item is ToolStripButton button) {
                if (button.CheckOnClick)
                    if (button.Checked) e.TextColor = baseThemeForeground;
                    else e.TextColor = Color.FromKnownColor(KnownColor.DimGray);
                else e.TextColor = baseThemeForeground;
            }
            else e.TextColor = baseThemeForeground;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e) {
            e.ArrowColor = baseThemeForeground;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderItemCheck(ToolStripItemImageRenderEventArgs e) {
            using (Pen pen = new Pen(baseThemeForeground, 2f)) {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.DrawLine(pen, 9, 9, 12, 13);
                e.Graphics.DrawLine(pen, 12, 11, 17, 6);
            }
        }
    }
}
