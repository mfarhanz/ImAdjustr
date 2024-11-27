using System;
using System.IO;
using System.Linq;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using System.Collections.Generic;

namespace ImAdjustr.Base {
    partial class Editor {

        private void SetupColorPreviewRegion() {
            var circularPanelMask = new System.Drawing.Drawing2D.GraphicsPath();
            circularPanelMask.AddEllipse(0, 0, colorPreview.Width, colorPreview.Height);
            colorPreview.Region = new Region(circularPanelMask);
        }

        private void SubscribeClickEventsToDropdownItems() {
            void SubscribeClickEventToItem(ToolStripMenuItem menu, EventHandler handler) {
                foreach (ToolStripMenuItem item in menu.DropDownItems) {
                    if (item.HasDropDownItems) SubscribeClickEventToItem(item, handler);
                    else item.Click += handler;
                }
            }
            // Add any new menu along with its corresponding Click event in order here
            ToolStripMenuItem[] dropdownMenu =
                { themesMenu, filtersMenu , quantiseMenu , editorThemesMenu, distanceMetricOptionsMenu, themeOptionsMenu, filterOptionsMenu, ditherOptionsMenu};
            EventHandler[] clickEvents =
                { ThemesMenuItem_Click, FiltersMenuItem_Click, QuantiseMenuItem_Click, EditorThemesMenuItem_Click, DistanceMetricOptionsMenuItem_Click, ThemeOptionsMenuItem_Click, FilterOptionsMenuItem_Click, DitherOptionsMenuItem_Click};
            for (int i = 0; i < dropdownMenu.Length; i++) SubscribeClickEventToItem(dropdownMenu[i], clickEvents[i]);
        }

        private void SetupLookupTableThemesDropdown() {
            // Currently only supporting .cube files for LUTs
            int progress = 0;
            List<ToolStripMenuItem> options = new List<ToolStripMenuItem>();
            var filenames = Directory.GetFiles("assets\\LUTs\\", "*.cube");
            foreach (var file in filenames) {
                string lutName = Path.GetFileNameWithoutExtension(file);
                options.Add(CreateMenuOption(lutName, lookupTableThemesMenu.Text));
                UpdateStatusLabel($"Initializing {lookupTableThemesMenu.Text} menu... {++progress * 100 / filenames.Length}%");
            }
            lookupTableThemesMenu.DropDownItems.AddRange(options.ToArray());
        }

        private void SetupDropdownMenuItems() {
            List<Dictionary<string, dynamic>> dicts = new List<Dictionary<string, dynamic>> { imageThemes, imageFilters, imagePalettes, editorThemes };
            List<ToolStripMenuItem> menus = new List<ToolStripMenuItem> { defaultThemesMenu, filtersMenu, quantiseMenu, editorThemesMenu };
            TextInfo capitalizer = new CultureInfo("en-US", false).TextInfo;
            SetupLookupTableThemesDropdown();
            for (int i = 0; i < dicts.Count; i++) {
                int progress = 0;
                if (dicts[i].First().Value is Dictionary<string, dynamic> sub) {
                    if (sub.Keys.Contains("category")) {
                        HashSet<string> categories = new HashSet<string>();
                        List<ToolStripMenuItem> dropdowns = new List<ToolStripMenuItem>();
                        Dictionary<string, HashSet<string>> subCategories = new Dictionary<string, HashSet<string>>();
                        Dictionary<string, List<ToolStripMenuItem>> subDropdowns = new Dictionary<string, List<ToolStripMenuItem>>();
                        foreach (var item in dicts[i]) {
                            try {
                                string category = item.Value["category"].ToLower();
                                if (categories.Add(category)) dropdowns.Add(CreateMenuOption(capitalizer.ToTitleCase(category.ToLower()), menus[i].Text));
                                if (category == "dither") {
                                    string subCategory = item.Value["type"].ToLower();
                                    if (!subCategories.ContainsKey(category)) subCategories[category] = new HashSet<string>();
                                    if (subCategories[category].Add(subCategory)) {
                                        if (!subDropdowns.ContainsKey(category)) subDropdowns[category] = new List<ToolStripMenuItem>();
                                        subDropdowns[category].Add(CreateMenuOption(capitalizer.ToTitleCase(subCategory.ToLower()), category));
                                    }
                                }
                                if (subCategories.ContainsKey(category)) {
                                    var currentDropdown = subDropdowns[category].FirstOrDefault(dropdown => dropdown.Text.ToLower() == item.Value["type"].ToLower());
                                    currentDropdown.DropDownItems.Add(CreateMenuOption(item.Key, currentDropdown.Text + menus[i].Text));
                                }
                                else {
                                    var currentDropdown = dropdowns.FirstOrDefault(dropdown => dropdown.Text.ToLower() == item.Value["category"].ToLower());
                                    currentDropdown.DropDownItems.Add(CreateMenuOption(item.Key, currentDropdown.Text + menus[i].Text));
                                }
                                UpdateStatusLabel($"Initializing {menus[i].Text} menu... {++progress * 100 / dicts[i].Count}%");
                            }
                            catch (Exception) {
                                Console.WriteLine($"Error adding {item.Key} option");
                            }
                        }
                        foreach (var dropdown in dropdowns)
                            if (subDropdowns.ContainsKey(dropdown.Text.ToLower())) dropdown.DropDownItems.AddRange(subDropdowns[dropdown.Text.ToLower()].ToArray());
                        menus[i].DropDownItems.AddRange(dropdowns.ToArray());
                    }
                    else {
                        foreach (var item in dicts[i]) {
                            try {
                                menus[i].DropDownItems.Add(CreateMenuOption(item.Key, menus[i].Text));
                                UpdateStatusLabel($"Initializing {menus[i].Text} menu... {++progress * 100 / dicts[i].Count}%");
                            }
                            catch (Exception) {
                                Console.WriteLine($"Error adding {item.Key} option");
                            }
                        }
                    }
                }
                else if (dicts[i].First().Value is List<dynamic>) {
                    List<ToolStripMenuItem> options = new List<ToolStripMenuItem>();
                    foreach (var item in dicts[i]) {
                        try {
                            options.Add(CreateMenuOption(item.Key, menus[i].Text));
                            UpdateStatusLabel($"Initializing {menus[i].Text} menu... {++progress * 100 / dicts[i].Count}%");
                        }
                        catch (Exception) {
                            Console.WriteLine($"Error adding {item.Key} option");
                        }
                    }
                    menus[i].DropDownItems.AddRange(options.ToArray());
                }
            }
        }

        private void SetToolTips() {
            widgetAssistToolTip.SetToolTip(layersTabControl, "Choose Original to display the latest version of initial image." +
                                                                "\nChoose any channel layer to view corresponding color channel of the Original layer.");
            widgetAssistToolTip.SetToolTip(infoPanel, "Info Panel - Double click to update");
            widgetAssistToolTip.SetToolTip(exposureAdjustButton, "Using the Intensity scale adjusts exposure\nwhile applying color transforms.");
            widgetAssistToolTip.SetToolTip(transparencyAdjustButton, "Using the Intensity scale adjusts color transparency");
            widgetAssistToolTip.SetToolTip(intensityAdjustButton, "Using the Intensity scale adjusts color intensity.");
            widgetAssistToolTip.SetToolTip(redAdjustButton, "Toggles red channel access for color transforms.");
            widgetAssistToolTip.SetToolTip(greenAdjustButton, "Toggles green channel access for color transforms.");
            widgetAssistToolTip.SetToolTip(blueAdjustButton, "Toggles blue channel access for color transforms.");
            widgetAssistToolTip.SetToolTip(intensityGroupBox, "Adjust exposure/transparency/color intensity.");
            widgetAssistToolTip.SetToolTip(coefficientGroupBox, "Adjust coefficient added to transform matrix.");
            widgetAssistToolTip.SetToolTip(variableGroupBox, "Adjust radius of transform matrix.");
            widgetAssistToolTip.SetToolTip(thresholdGroupBox, "Set cutoff threshold for pixels when applying transform.");
            widgetAssistToolTip.SetToolTip(toneAdjustGoupBox, "Adjust individual channel tones of image.");
        }
    }
}
