using System;
using System.Linq;
using System.Drawing;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.Collections.Generic;
using ImAdjustr.Utility;
using ImAdjustr.Forms;

namespace ImAdjustr.Base {
    partial class Editor {

        private Color imageSelectOnLeave;
        private Color imageSelectOnEnter;
        private Color imageSelectBackground;
        private Point displayedImageLocation;
        private Point zoomedInMousePosition;
        private bool draggingDisplayedImage;
        private bool displayUpdating;
        private bool delayIncreasing;
        private bool toggleVerbose;
        private int lastSelectedTabIndex;
        private string currentImageTheme;
        private string currentImageFilter;
        private string currentImagePalette;
        private List<dynamic> customImageThemeValues;
        private List<dynamic> customImageFilterValues;
        private float zoomFactor;
        private int currentColorTransformMethod;
        private float currentColorTransformMethodNormalizingValue;
        private int[] activeColorTransformChannels;
        private List<int[]> activeTabs;
        private Queue<string> messageQueue;
        private ToolStripRenderer widgetRenderer;
        private PictureBox currentCanvas;

        private ToolStripMenuItem CreateMenuOption(string name, string parent) {
            ToolStripMenuItem newOption = new ToolStripMenuItem(name);
            newOption.Name = name.Replace(" ", "") + parent + "Option";
            return newOption;
        }

        private TabPage CreateLayerTab(string name, int index) {
            TabPage newTabPage = new TabPage(name);
            newTabPage.BackColor = Color.FromArgb(((int)(((byte)(50)))), ((int)(((byte)(50)))), ((int)(((byte)(50)))));
            newTabPage.BackgroundImageLayout = ImageLayout.None;
            newTabPage.ForeColor = Color.FromKnownColor(KnownColor.Info);
            newTabPage.Location = new Point(4, 29);
            newTabPage.Name = "tabPage" + index;
            newTabPage.Size = new Size(1168, 613);
            newTabPage.TabIndex = index - 1; ;
            return newTabPage;
        }

        private ToolStripButton CreateLayerButton(string name, int index) {
            ToolStripButton newButton = new ToolStripButton(name);
            newButton.AutoToolTip = false;
            newButton.BackgroundImageLayout = ImageLayout.None;
            newButton.Checked = false;
            newButton.CheckOnClick = true;
            newButton.CheckState = CheckState.Unchecked;
            newButton.DisplayStyle = ToolStripItemDisplayStyle.Text;
            newButton.ForeColor = SystemColors.Info;
            newButton.Margin = new Padding(2);
            newButton.Name = "layer" + index + "ToolStripButton";
            newButton.Tag = index - 1;
            newButton.Padding = new Padding(15, 2, 15, 2);
            newButton.TextDirection = ToolStripTextDirection.Horizontal;
            newButton.Click += new EventHandler(LayersToolStripHandler);
            newButton.MouseDown += new MouseEventHandler(LayersToolStripButton_MouseDown);
            return newButton;
        }

        private PictureBox CreateLayerCanvas(int index) {
            PictureBox newCanvas = new PictureBox();
            newCanvas.BackColor = Color.Transparent;
            newCanvas.Dock = DockStyle.Fill;
            newCanvas.InitialImage = null;
            newCanvas.Location = new Point(0, 0);
            newCanvas.Margin = new Padding(0);
            newCanvas.Name = "pictureBoxLayer" + index;
            newCanvas.SizeMode = PictureBoxSizeMode.Zoom;
            newCanvas.TabStop = false;
            newCanvas.Tag = index - 1;
            newCanvas.WaitOnLoad = true;
            newCanvas.MouseMove += new MouseEventHandler(this.PictureBoxDetectColor_MouseMove);
            CreateCanvasBackground(newCanvas);
            return newCanvas;
        }

        private void CreateCanvasBackground(PictureBox canvas) {
            Color color1 = this.BackColor;
            int r = color1.R <= 245 ? color1.R + 10 : 255;
            int g = color1.G <= 245 ? color1.G + 10 : 255;
            int b = color1.B <= 245 ? color1.B + 10 : 255;
            Color color2 = Color.FromArgb(r, g, b);
            canvas.BackgroundImage?.Dispose();
            canvas.BackgroundImage = BitmapUtility.GetTransparencyBackground(20, 20, 10, color1, color2); ;
        }

        private void ResetControls() {
            if (toggleZoomAndPan) toggleZoomAndPan = false;
            if (gifPlayback) gifPlayback = false;
            if (gifReversed) gifReversed = false;
            eyedropperButton.Checked = false;
            pasteLayerButton.Checked = false;
            selectButton.Checked = false;
            cropButton.Checked = false;
            resizeButton.Checked = false;
            zoomButton.Checked = false;
            exposureAdjustButton.Checked = true;
            frameIndexDisplay.Text = "0";
            frameIndexDisplay.Left = gifFrameCarousel.Left;
            UncheckAllDropDownItems(themesMenu);
            UncheckAllDropDownItems(filtersMenu);
            UncheckAllDropDownItems(quantiseMenu);
        }

        private void EnableControls(bool flag) {
            if (flag) {
                imageToolsPanel.Enabled = true;
                colorTransfomOptionsPanel.Enabled = true;
                infoPanel.Scrollable = true;
                intensityGroupBox.Enabled = true;
                coefficientGroupBox.Enabled = true;
                sidebarSubTableLayoutPanel.Enabled = true;
                toneAdjustGoupBox.Enabled = true;
                gifToolsGroupBox.Enabled = true;
                SetToolTips();
            }
            else {
                imageToolsPanel.Enabled = false;
                colorTransfomOptionsPanel.Enabled = false;
                infoPanel.Scrollable = false;
                intensityGroupBox.Enabled = false;
                coefficientGroupBox.Enabled = false;
                sidebarSubTableLayoutPanel.Enabled = false;
                toneAdjustGoupBox.Enabled = false;
                gifToolsGroupBox.Enabled = false;
            }
        }

        private void ZoomAndPanHandler() {
            if (toggleZoomAndPan) {
                if (currentCanvas.Image is null) {
                    UpdateStatusLabel("No base image to zoom");
                    toggleZoomAndPan = false;
                    return;
                }
                if (!isGif) zoomedReferenceImage = (Bitmap)currentCanvas.Image.Clone();
                else if (isGif && !isGifStreamed) zoomedReferenceImage = (referenceImage ?? currentImage)[gifCurrentFrameIndex].Clone();
                else if (isGif && isGifStreamed) zoomedReferenceImage = BitmapUtility.GetGifFrame(currentImage, gifCurrentFrameIndex);
                currentCanvas.Image.Dispose();
                currentCanvas.Image = null;
                currentCanvas.MouseDown += PictureBoxZoomAndPan_MouseDown;
                currentCanvas.MouseUp += PictureBoxZoomAndPan_MouseUp;
                currentCanvas.MouseMove += PictureBoxZoomAndPan_MouseMove;
                currentCanvas.MouseWheel += PictureBoxZoomAndPan_MouseWheel;
                currentCanvas.Paint += PictureBoxZoomAndPan_Paint;
            }
            else {
                zoomFactor = 1.0f;
                (displayedImageLocation.X, displayedImageLocation.Y) = (0, 0);
                currentCanvas.MouseDown -= PictureBoxZoomAndPan_MouseDown;
                currentCanvas.MouseUp -= PictureBoxZoomAndPan_MouseUp;
                currentCanvas.MouseMove -= PictureBoxZoomAndPan_MouseMove;
                currentCanvas.MouseWheel -= PictureBoxZoomAndPan_MouseWheel;
                currentCanvas.Paint -= PictureBoxZoomAndPan_Paint;
                if (!isGif) {
                    currentCanvas.Image = zoomedReferenceImage?.Clone();
                    BitmapUtility.Dispose(ref zoomedReferenceImage);
                }
                else {
                    BitmapUtility.Dispose(ref zoomedReferenceImage);
                    gifPlayback = true;
                }
                currentCanvas.Invalidate();
            }
        }

        private void ShowPlaceholderIcon(bool toggle) {
            if (toggle) {
                imageSelectIcon.Show();
                imageSelectTextbox.Show();
            }
            else {
                imageSelectIcon.Hide();
                imageSelectTextbox.Hide();
            }
        }

        private bool ShowMessageBox(string message, string title) {
            var messageDialog = new CustomMessageDialog(message, title);
            messageDialog.ShowDialog();
            var result = messageDialog.DialogResult;
            messageDialog.Dispose();
            return result == DialogResult.OK ? true : false;
        }

        private void ShowLoadingFeedback(string text = "Loading", int? max = null, char progressIcon = '.') {
            int ctr = 0;
            isLoading = true;
            bool defaultMax = max != null;
            Timer loadingTimer = new Timer() { Interval = 300 };
            loadingTimer.Tick += LoadingTimer_Tick;
            loadingTimer.Start();

            void LoadingTimer_Tick(object sender, EventArgs e) {
                if (!isLoading) loadingTimer.Stop();
                ctr = (ctr + 1) % (int)(defaultMax ? max : 4);
                Invoke((Action)(() => UpdateStatusLabel(text + new string(progressIcon, ctr))));
            }
        }

        private void UncheckAllDropDownItems(ToolStripMenuItem menu) {
            foreach (ToolStripMenuItem item in menu.DropDownItems) {
                if (item.HasDropDownItems) UncheckAllDropDownItems(item);
                else item.Checked = false;
            }
        }

        private HashSet<string> GetCurrentControlTypes(Control parentControl) {
            var controlTypes = new HashSet<string>();
            var controlsToProcess = new Stack<Control>();
            // Start with the root control
            controlsToProcess.Push(parentControl);
            while (controlsToProcess.Count > 0) {
                // Get the next control to process
                var control = controlsToProcess.Pop();
                if (!controlTypes.Contains(control.GetType().Name)) controlTypes.Add(control.GetType().Name);
                // Add child controls to the stack for later processing
                foreach (Control child in control.Controls) controlsToProcess.Push(child);
            }
            return controlTypes;
        }

        private async void IntensityScaleHandler(TrackBar intensityScale, bool saving = false) {
            int scaleValue = intensityScale.Value;
            intensityLabel.Text = scaleValue.ToString();
            if (displayUpdating) return;
            if (!isGif) {
                if (currentCanvas.Image != null || zoomedReferenceImage != null) {
                    var reference = currentCanvas.Image ?? zoomedReferenceImage;
                    if (referenceImage is null) referenceImage = (Bitmap)reference?.Clone();
                    dynamic ret, val;
                    if (currentColorTransformMethod == 1) val = (float)scaleValue * currentColorTransformMethodNormalizingValue - 1;
                    else val = (float)scaleValue * currentColorTransformMethodNormalizingValue;
                    if (currentImageTheme is null || currentImageTheme.ToLower() == "none")
                        ret = BitmapUtility.ApplyColorTransform(referenceImage, currentColorTransformMethod, val, activeColorTransformChannels);
                    else if (currentImageTheme.ToLower() == "custom")
                        ret = BitmapUtility.ApplyColorTransform(referenceImage, currentColorTransformMethod, val, activeColorTransformChannels, customImageThemeValues);
                    else
                        ret = BitmapUtility.ApplyColorTransform(referenceImage, currentColorTransformMethod, val, activeColorTransformChannels, imageThemes[currentImageTheme]);
                    await UpdatePictureBox(ret);
                    currentCanvas.Refresh();
                }
            }
         }        

        private async void ToneAdjustScaleHandler(TrackBar toneAdjustScale, bool saving = false) {
            int channel = (int)toneAdjustScale.Tag;
            string scaleValue = toneAdjustScale.Value.ToString();
            if (channel == 0) toneAdjustRed.Text = "R: " + scaleValue;
            else if (channel == 1) toneAdjustGreen.Text = "G: " + scaleValue;
            else if (channel == 2) toneAdjustBlue.Text = "B: " + scaleValue;
            else if (channel == 3) toneAdjustAlpha.Text = "A: " + scaleValue;
            if (!eyedropperButton.Checked) {
                UpdateColorPreview();
                if (displayUpdating) return;
                if (!isGif) {
                    if (currentCanvas.Image != null || zoomedReferenceImage != null) {
                        var reference = currentCanvas.Image ?? zoomedReferenceImage;
                        if (referenceImage is null) referenceImage = (Bitmap)reference?.Clone();
                        var ret = BitmapUtility.GetChannelToneAdjusted(referenceImage, (int)toneAdjustScale.Tag, toneAdjustScale.Value);
                        await UpdatePictureBox(ret);
                        currentCanvas.Refresh();
                    }
                }
                else if (isGif && !isGifStreamed) {
                    if (currentImage != null) {
                        if (!saving) {
                            gifReferenceDisplay = true;
                            var ret = BitmapUtility.GetChannelToneAdjusted(currentImage[gifCurrentFrameIndex], (int)toneAdjustScale.Tag, toneAdjustScale.Value);
                            await UpdatePictureBox(ret);
                            gifReferenceDisplay = false;
                        }
                        else {
                            if (referenceImage != null && referenceImage?.Count >= 0) BitmapUtility.Dispose(ref referenceImage);
                            referenceImage = new List<Bitmap>(new Bitmap[gifFrameCount]);
                            for (int i = 0; i < gifFrameCount; i++) {
                                referenceImage[i]?.Dispose();
                                referenceImage[i] = BitmapUtility.GetChannelToneAdjusted(currentImage[i], (int)toneAdjustScale.Tag, toneAdjustScale.Value);
                            }
                        }
                    }
                }
            }
        }

        private void LayersToolStripHandler(object sender, EventArgs e) {
            int index = layersToolStrip.Items.IndexOf(toolStripInfoLabel);
            if (index != 0) {
                var item1 = layersToolStrip.Items[index];
                var item2 = layersToolStrip.Items[0];
                layersToolStrip.Items.Remove(item1);
                layersToolStrip.Items.Remove(item2);
                layersToolStrip.Items.Insert(0, item1);
                layersToolStrip.Items.Insert(index, item2);
            }
            for (int i = 1; i < layersToolStrip.Items.Count; i++) {
                ToolStripButton button1 = (ToolStripButton)layersToolStrip.Items[i];
                int btnIndex = (int)button1.Tag;
                if (btnIndex >= 0 && btnIndex <= 2 && i > 3) {
                    ToolStripButton button2 = (ToolStripButton)layersToolStrip.Items[btnIndex + 1];
                    layersToolStrip.Items.Remove(button2);
                    layersToolStrip.Items.Remove(button1);
                    layersToolStrip.Items.Insert(btnIndex + 1, button1);
                    layersToolStrip.Items.Insert(i, button2);
                    activeTabs[btnIndex][0] = btnIndex;
                    activeTabs[btnIndex][1] = button1.Checked ? 1 : 0;
                }
                else {
                    activeTabs[i - 1][0] = btnIndex;
                    activeTabs[i - 1][1] = button1.Checked ? 1 : 0;
                }
            }
        }

        private void LayersTabHandler() {
            if (pictureBox1.Image != null) {
                if (lastSelectedTabIndex == 0) {
                    currentImage?.Dispose();
                    currentImage = pictureBox1.Image.Clone();
                }
                referenceImage?.Dispose();
                pictureBox1.Image?.Dispose();
                pictureBox1.Image = null;
                referenceImage = null;
            }
            lastSelectedTabIndex = layersTabControl.SelectedIndex;
            if (lastSelectedTabIndex <= 4) {
                pictureBox1.Parent.Controls.Remove(pictureBox1);
                layersTabControl.SelectedTab.Controls.Add(pictureBox1);
                if (lastSelectedTabIndex == 0) {
                    if (currentImage != null) {
                        pictureBox1.Image = currentImage.Clone();
                        UpdateStatusLabel("Showing base image");
                        currentImage?.Dispose();
                    }
                    else
                        UpdateStatusLabel("No base image available");
                }
                else if (lastSelectedTabIndex >= 2 && lastSelectedTabIndex <= 4) {
                    // channel tabs
                    dynamic ret;
                    int channel = lastSelectedTabIndex == 2 ? 0 : (lastSelectedTabIndex == 3 ? 1 : 2);
                    try {
                        ret = BitmapUtility.ExtractChannelFromImageUnsafe(currentImage, channel);
                    }
                    catch (Exception ex) {
                        Console.WriteLine($"Error using unsafe channel extract: {ex}");
                        ret = BitmapUtility.ExtractChannelFromImage(currentImage, channel);
                    }
                    if (ret != null) {
                        pictureBox1.Image?.Dispose();
                        pictureBox1.Image = ret.Clone();
                        UpdateStatusLabel($"Showing {(channel == 0 ? "red" : (channel == 1 ? "green" : "blue"))} channel of base image");
                        ret?.Dispose();
                    }
                    else
                        UpdateStatusLabel("No base image available");
                }
                else if (lastSelectedTabIndex == 1) {
                    // all active tabs
                    // For now, only showing active channels
                    dynamic ret;
                    try {
                        ret = BitmapUtility.CombineActiveChannelsUnsafe(currentImage, activeTabs);
                    }
                    catch (Exception) {
                        ret = null;
                    }
                    if (ret != null) {
                        pictureBox1.Image?.Dispose();
                        pictureBox1.Image = ret.Clone();
                        UpdateStatusLabel("Showing active layers");
                        ret?.Dispose();
                    }
                    else if (ret == null && currentImage != null)
                        UpdateStatusLabel("Unable to show active layers currently");
                    else
                        UpdateStatusLabel("No layers active currently");

                    // show active custom layers - TBD
                }
            }
            else {
                // show custom layer - TBD
            }
        }

        private async Task UpdatePictureBox(dynamic img) {
            if (displayUpdating) return;
            if (img is null) return;
            displayUpdating = true;
            currentTask = new TaskCompletionSource<bool>();
            try {
                await Task.Run(() => {
                    if (toggleZoomAndPan) {
                        BitmapUtility.Dispose(ref zoomedReferenceImage);
                        if (isGif && !isGifStreamed) {
                            zoomedReferenceImage = gifReferenceDisplay ? img : img[gifCurrentFrameIndex].Clone();
                        }
                        else if (isGif && isGifStreamed) {
                            zoomedReferenceImage = BitmapUtility.GetGifFrame(img, gifCurrentFrameIndex);
                        }
                        else if (!isGif) {
                            zoomedReferenceImage = img;
                        }
                    }
                    else {
                        if (!isGif || (isGif && !isGifStreamed)) {
                            currentCanvas.Image?.Dispose();
                            currentCanvas.Image = null;
                            currentCanvas.Image = (isGif && !gifReferenceDisplay) ? img[gifCurrentFrameIndex].Clone() : img;
                        }
                        else if (isGif && isGifStreamed) {
                            dynamic nextFrame = BitmapUtility.GetGifFrame(img, gifCurrentFrameIndex);
                            currentCanvas.Image?.Dispose();     // IMPORTANT: instead of having a common Dispose()
                            currentCanvas.Image = null;         // for Canvas.Image, keep it separate as it would 
                            currentCanvas.Image = nextFrame;    // otherwise cause issues with synchronisation
                        }
                    }
                });
            }
            catch (ArgumentNullException) {
                Console.WriteLine("Error displaying image frame");
            }
            finally {
                currentCanvas.Invalidate();
                displayUpdating = false;
                currentTask.SetResult(true);
            }
        }

        private async Task UpdateInfoPanel() {
            if (!toggleVerbose) return;
            else if (displayUpdating) { await currentTask.Task; }
            else if (isGif && currentImage is null) return;
            else if (!isGif && currentImage is null && currentCanvas.Image is null) return;
            else if (currentCanvas.Image is null && currentImage is null) {
                UpdateStatusLabel("No base image provided");
                return;
            }
            infoPanel.Items[0].SubItems[1].Text = $"{imageFileInfo.Name}";
            if (isGif) infoPanel.Items[1].SubItems[1].Text = $"{((float)imageFileInfo.Length / (1024 * 1024)).ToString("F3")} MB";
            else {
                var sizes = await Task.Run(() => { return BitmapUtility.GetImageSize((Bitmap)currentCanvas.Image.Clone()); });
                infoPanel.Items[1].SubItems[1].Text = $"{(int)(sizes.compressed * 1000) / 1000f} MB (Compressed) , {(int)(sizes.uncompressed * 1000) / 1000f} MB (Uncompressed)";
            }
            infoPanel.Items[2].SubItems[1].Text = $"{imageFileInfo.Extension.Substring(1).ToUpper()}";
            infoPanel.Items[3].SubItems[1].Text = $"{((new string[] { ".png", ".bmp", ".gif", ".ico" }).Any(imageFileInfo.Extension.Contains) ? "RGBA" : "RGB")}";
            if (!isGif) infoPanel.Items[5].SubItems[1].Text = $"{currentCanvas.Image?.Width}x{currentCanvas.Image?.Height}";
            else if (isGifStreamed) infoPanel.Items[5].SubItems[1].Text = $"{currentImage?.Width}x{currentImage?.Height}";
            else  infoPanel.Items[5].SubItems[1].Text = $"{currentImage[gifCurrentFrameIndex]?.Width}x{currentImage[gifCurrentFrameIndex]?.Height}";
            infoPanel.Items[4].SubItems[1].Text = $"";
            infoPanel.Items[6].SubItems[1].Text = $"";
            infoPanel.Items[7].SubItems[1].Text = $"";
            infoPanel.Items[8].SubItems[1].Text = $"";
            try {
                int colorCount = await Task.Run(() => {
                    if (!isGif) return (int)BitmapUtility.GetColorCountUnsafe((Bitmap)currentCanvas.Image);
                    else if (isGif && isGifStreamed) return (int)BitmapUtility.GetColorCountUnsafe(currentImage);
                    else return (int)BitmapUtility.GetColorCountUnsafe(currentImage[gifCurrentFrameIndex]);
                });
                infoPanel.Items[9].SubItems[1].Text = $"{colorCount}";
            }
            catch (Exception) {
                UpdateStatusLabel("Error getting color count");
            }
            finally {
                isLoading = false;
                await Task.Delay(200);
                UpdateStatusLabel("Updated Info panel");
            }
        }

        private void UpdateInfoLabel(string label, int row) {
            if (!toggleVerbose) return;
            infoPanel.Items[row - 1].SubItems[1].Text = $"{label}";
        }

        private void UpdateStatusLabel(string text) {
            messageQueue.Enqueue($"   {text}");
            if (messageQueue.Count == 1) {
                statusLabelTimer.Start();
            }
            else if (messageQueue.Count > 1) {
                statusLabelTimer.Interval = 10;
            }
        }

        private void UpdateFrameDelay() {
            if (delayIncreasing) gifFrameDelay = Math.Min(5000, gifFrameDelay + 1);
            else gifFrameDelay = Math.Max(5, gifFrameDelay - 1);
            delayLabel.Text = $"{gifFrameDelay}";
        }

        private void UpdateColorScales(byte r, byte g, byte b, byte a) {
            toneAdjustRedScale.Value = r;
            toneAdjustGreenScale.Value = g;
            toneAdjustBlueScale.Value = b;
            toneAdjustAlphaScale.Value = a;
        }

        private void UpdateColorPreview() {
            colorPreview.BackColor = Color.FromArgb(toneAdjustAlphaScale.Value, toneAdjustRedScale.Value, toneAdjustGreenScale.Value, toneAdjustBlueScale.Value);
        }

        private void UpdateControlColors(Control parentControl, string controlTypeName, Color background, Color foreground, Color onHover, Color onPress, Color onChecked) {
            void UpdateItemColors(dynamic itemParent, Color bg, Color fg) {
                var getItems = itemParent.GetType().GetProperty("Items") ?? itemParent.GetType().GetProperty("SubItems");
                if (getItems != null) {
                    var items = getItems.GetValue(itemParent) as System.Collections.ICollection;
                    foreach (dynamic item in items) {
                        item.BackColor = bg;
                        item.ForeColor = fg;
                        UpdateItemColors(item, bg, fg);
                    }
                }
            }
            
            foreach (Control control in parentControl.Controls) {
                if (control.GetType().Name == controlTypeName) {
                    control.BackColor = background;
                    control.ForeColor = foreground;
                    UpdateItemColors(control, background, foreground);
                    if (control is Button button) {
                        button.FlatAppearance.MouseOverBackColor = onHover;
                        button.FlatAppearance.MouseDownBackColor = onPress;
                        button.FlatAppearance.CheckedBackColor = onChecked;
                    }
                    else if (control is CheckBox checkBox) {
                        checkBox.FlatAppearance.MouseOverBackColor = onHover;
                        checkBox.FlatAppearance.MouseDownBackColor = onPress;
                        checkBox.FlatAppearance.CheckedBackColor = onChecked;
                    }
                    else if (control is RadioButton radioButton) {
                        radioButton.FlatAppearance.MouseOverBackColor = onHover;
                        radioButton.FlatAppearance.MouseDownBackColor = onPress;
                        radioButton.FlatAppearance.CheckedBackColor = onChecked;
                    }
                    else if (control is PictureBox pictureBox) CreateCanvasBackground(pictureBox);
                }
                if (control.HasChildren) UpdateControlColors(control, controlTypeName, background, foreground, onHover, onPress, onChecked);
            }
        }

        private void UpdateToolStripRenderer(string theme = "Default") {
            widgetRenderer = new CustomRenderer(new CustomColorTable(editorThemes[theme]["ToolstripOnly"]));
            menuStrip.Renderer = widgetRenderer;
            layersToolStrip.Renderer = widgetRenderer;
            gifOptionsToolStrip.Renderer = widgetRenderer;
            gifControlToolStrip.Renderer = widgetRenderer;
            buttonControlsToolStrip.Renderer = widgetRenderer;
            frameOptionsToolStrip.Renderer = widgetRenderer;
            UpdateStatusLabel($"Applied {theme} theme to ToolStrips");
        }

        private void UpdateEditorTheme(string theme) {
            Dictionary<string, dynamic> colors;
            int progress = 0;
            var types = GetCurrentControlTypes(this);
            UpdateToolStripRenderer(theme);
            progress++;         // ... finished updating theme for ToolStrip widgets ...
            try {
                colors = editorThemes[theme]["NonToolstrip"];
            }
            catch (KeyNotFoundException e) {
                Console.WriteLine($"Error loading NonToolstrip items in XML config: {e}");
                return;
            }
            foreach (var item in colors) {
                try {
                    dynamic background = item.Value.ContainsKey("Background") ? item.Value["Background"] : Color.Empty;
                    dynamic foreground = item.Value.ContainsKey("Foreground") ? item.Value["Foreground"] : Color.Empty;
                    dynamic hoverBackground = item.Value.ContainsKey("HoverBackground") ? item.Value["HoverBackground"] : Color.Empty;
                    dynamic pressedBackground = item.Value.ContainsKey("PressedBackground") ? item.Value["PressedBackground"] : Color.Empty;
                    dynamic checkedBackground = item.Value.ContainsKey("CheckedBackground") ? item.Value["CheckedBackground"] : Color.Empty;
                    if (item.Key == "Form") {
                        this.BackColor = background;
                        this.ForeColor = foreground;
                    }
                    if (item.Key.Contains("ImageSelect")) {
                        if (item.Key.Contains("Hover"))
                            imageSelectOnEnter = foreground;
                        else
                            imageSelectOnLeave = foreground;
                        imageSelectBackground = background;
                    }
                    else if (types.Contains(item.Key))      // ... begin updating Non-ToolStrip widgets
                        UpdateControlColors(this, item.Key, background, foreground, hoverBackground, pressedBackground, checkedBackground);
                    UpdateStatusLabel($"Applying theme... {++progress * 100/colors.Count}%");
                }
                catch (Exception) {
                    Console.WriteLine($"Error updating theme for {item.Key}");
                }
            }
        }
    }
}