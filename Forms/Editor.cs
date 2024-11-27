using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics;
using System.Threading.Tasks;
using ImAdjustr.Forms;
using ImAdjustr.Internal.Controllers;
using ImAdjustr.Internal.Services;
using ImAdjustr.Utility;
using System.Drawing.Drawing2D;

namespace ImAdjustr.Base {
    internal partial class Editor : Form {

        private DebugLogger logger;
        private BackendController control;

        internal Editor(BackendController controller, DebugLogger debugLogger) {
            InitializeComponent();
            currentImage = null;
            toggleVerbose = true;
            zoomFactor = 1.0f;
            lastSelectedTabIndex = 0;
            currentColorTransformMethod = 0;
            currentColorTransformMethodNormalizingValue = 0.00990099f;
            activeTabs = new List<int[]> { new int[] { 0, 1 }, new int[] { 1, 1 }, new int[] { 2, 1 } };   // all channels active initially
            activeColorTransformChannels = new int[] { 1, 1, 1 };
            currentCanvas = pictureBox1;
            execTime = new Stopwatch();
            messageQueue = new Queue<string>();
            control = controller;
            logger = debugLogger;
            imageSelectOnLeave = Color.FromArgb(205, 207, 209);
            imageSelectOnEnter = Color.FromArgb(131, 142, 149);
        }

        private bool toggleZoomAndPan { get => zoomAndPanViewOption.Checked; set => zoomAndPanViewOption.Checked = value; }
        private bool gifPlayback { get => gifPlayButton.Checked; set => gifPlayButton.Checked = value; }
        private bool gifReversed { get => gifReverseButton.Checked; set => gifReverseButton.Checked = value; }
        private bool gifStopped { get => gifStopButton.Checked; set => gifStopButton.Checked = value; }

        private async void Editor_Load(object sender, EventArgs e) {
            CreateCanvasBackground(currentCanvas);
            EnableControls(false);
            if (control.validated) await control.SetupListeners();
        }

        private async void Editor_Shown(object sender, EventArgs e) {
            await Task.Delay(200);
            LoadAppData();
            SetupDropdownMenuItems();
            SubscribeClickEventsToDropdownItems();
            UpdateToolStripRenderer();
            SetupColorPreviewRegion();
        }

        private async void InitiateEditorClosing(object sender, FormClosingEventArgs e) {
            e.Cancel = true;
            ShowLoadingFeedback(text: "Cleaning up ");
            await control.ShutdownController();
            this.FormClosing -= InitiateEditorClosing;
            isLoading = false;
            await Task.Delay(400);
            UpdateStatusLabel("Done");
            await Task.Delay(500);
            this.Close();
        }

        private void ImageSelectIcon_MouseEnter(object sender, EventArgs e) {
            imageSelectTextbox.ForeColor = imageSelectOnEnter;
            imageSelectIcon.ForeColor = imageSelectOnEnter;
        }

        private void ImageSelectIcon_MouseLeave(object sender, EventArgs e) {
            imageSelectTextbox.ForeColor = imageSelectOnLeave;
            imageSelectTextbox.BackColor = imageSelectBackground;
            imageSelectIcon.ForeColor = imageSelectOnLeave;
            imageSelectIcon.BackColor = imageSelectBackground;
        }

        private void Widget_MouseLeave(object sender, EventArgs e) { this.ActiveControl = null; }

        private void Editor_DragEnter(object sender, DragEventArgs e) {
            if (e.Data.GetDataPresent(DataFormats.FileDrop)) {
                e.Effect = DragDropEffects.Copy;
                imageSelectTextbox.Text = "  Drop File";
                imageSelectIcon.Text = "\U0001F4CB";
            }
        }

        private void Editor_DragLeave(object sender, EventArgs e) {
            imageSelectTextbox.Text = "Load an Image";
            imageSelectIcon.Text = "📂";
        }

        private void Editor_DragDrop(object sender, DragEventArgs e) { LoadImageFromDroppedFile((string[])e.Data.GetData(DataFormats.FileDrop));  }

        private void ImageSelectIcon_MouseClick(object sender, MouseEventArgs e) { LoadImageFromSystem(); }

        private void NewFileOption_Click(object sender, EventArgs e) {
            if (!imageSelectIcon.Visible) {
                imageSelectIcon.Show();
                imageSelectTextbox.Show();
            }
            ResetControls();
            ClearBuffers();
        }

        private void OpenFileOption_Click(object sender, EventArgs e) { LoadImageFromSystem(); }

        private void SaveFileOption_Click(object sender, EventArgs e) { SaveDisplayedImage(); }

        private void SaveAsFileOption_Click(object sender, EventArgs e) { SaveDisplayedImageToSystem(); }

        private void ZoomAndPanViewOption_CheckedChanged(object sender, EventArgs e) { ZoomAndPanHandler(); }

        private void VerboseViewOption_Click(object sender, EventArgs e) {
            if (currentCanvas.Image != null) {
                toggleVerbose = !toggleVerbose;
                infoPanel.Scrollable = toggleVerbose;
            }
        }

        private void CustomThemesMenu_Click(object sender, EventArgs e) {
            ToolStripMenuItem clickedItem = sender as ToolStripMenuItem;
            if (currentCanvas.Image != null) {
                using (var dialog = customImageThemeValues is null ? new CustomThemeDialog() : new CustomThemeDialog(customImageThemeValues)) {
                    dialog.ShowDialog();
                    if (dialog.DialogResult == DialogResult.OK) {
                        customImageThemeValues = dialog.returnValues;
                        currentImageTheme = clickedItem.Text;
                    }
                    else if (dialog.DialogResult == DialogResult.Cancel || dialog.DialogResult == DialogResult.Abort) {
                        clickedItem.Checked = false;
                    }
                }
            }
        }

        private void ThemesMenuItem_Click(object sender, EventArgs e) {
            // IMPORTANT: here the row index provided is not following zero-based numbering
            ToolStripMenuItem clickedItem = sender as ToolStripMenuItem;
            if (currentCanvas.Image != null) {
                UncheckAllDropDownItems(themesMenu);
                clickedItem.Checked = true;
                currentImageTheme = clickedItem.Text;
                UpdateInfoLabel(currentImageTheme, 7);
            }
        }

        private void FiltersMenuItem_Click(object sender, EventArgs e) {
            // IMPORTANT: here the row index provided is not following zero-based numbering
            ToolStripMenuItem clickedItem = sender as ToolStripMenuItem;
            if (currentCanvas.Image != null) {
                UncheckAllDropDownItems(filtersMenu);
                clickedItem.Checked = true;
                currentImageFilter = clickedItem.Text;
                UpdateInfoLabel(currentImageFilter, 8);
            }
        }

        private void QuantiseMenuItem_Click(object sender, EventArgs e) {
            // IMPORTANT: here the row index provided is not following zero-based numbering
            ToolStripMenuItem clickedItem = sender as ToolStripMenuItem;
            if (currentCanvas.Image != null) {
                UncheckAllDropDownItems(quantiseMenu);
                clickedItem.Checked = true;
                currentImagePalette = clickedItem.Text;
                UpdateInfoLabel(currentImagePalette, 9);
                try {
                    if (!isGif || isGifStreamed) UpdateInfoLabel($"{BitmapUtility.GetColorCountUnsafe((Bitmap)currentCanvas.Image)}", 10);
                    else if (currentImage != null) UpdateInfoLabel($"{BitmapUtility.GetColorCountUnsafe(currentImage[gifCurrentFrameIndex])}", 10);
                }
                catch (Exception) {
                    UpdateStatusLabel("Error getting color count");
                }
            }
        }

        private void ThemeOptionsMenuItem_Click(object sender, EventArgs e) {
            ToolStripMenuItem clickedItem = sender as ToolStripMenuItem;
            UncheckAllDropDownItems(themeOptionsMenu);
            clickedItem.Checked = true;
        }

        private void FilterOptionsMenuItem_Click(object sender, EventArgs e) {
            ToolStripMenuItem clickedItem = sender as ToolStripMenuItem;
            UncheckAllDropDownItems(filterOptionsMenu);
            clickedItem.Checked = true;
        }

        private void DitherOptionsMenuItem_Click(object sender, EventArgs e) {
            ToolStripMenuItem clickedItem = sender as ToolStripMenuItem;
            UncheckAllDropDownItems(ditherOptionsMenu);
            clickedItem.Checked = true;
        }

        private void DistanceMetricOptionsMenuItem_Click(object sender, EventArgs e) {
            ToolStripMenuItem clickedItem = sender as ToolStripMenuItem;
            UncheckAllDropDownItems(distanceMetricOptionsMenu);
            clickedItem.Checked = true;
        }

        private void EditorThemesMenuItem_Click(object sender, EventArgs e) {
            ToolStripMenuItem clickedItem = sender as ToolStripMenuItem;
            UncheckAllDropDownItems(editorThemesMenu);
            clickedItem.Checked = true;
            UpdateEditorTheme(clickedItem.Text);
        }

        private void ToggleTransparencyGridOption_Click(object sender, EventArgs e) {
            foreach (TabPage tabPage in layersTabControl.TabPages) {
                foreach (Control control in tabPage.Controls) {
                    if (control is PictureBox canvas) {
                        if (!(sender as ToolStripMenuItem).Checked) {
                            canvas.BackgroundImage?.Dispose();
                            canvas.BackgroundImage = null;
                        }
                        else CreateCanvasBackground(canvas);
                    }
                }
            }
        }

        private void ChannelCheckButton_Click(object sender, EventArgs e) {
            CheckBox clicked = sender as CheckBox;
            if (clicked == null) return;
            int index = (int)clicked.Tag;
            activeColorTransformChannels[index] = (activeColorTransformChannels[index] == 1) ? 0 : 1;
        }

        private void ColorTransformAdjustButton_OnCheckChanged(object sender, EventArgs e) {
            RadioButton clicked = sender as RadioButton;
            if (clicked == null) return;
            currentColorTransformMethod = (int)clicked.Tag;
            // IMPORTANT: the values set below must remain the same at all times
            switch (currentColorTransformMethod) {
                case 0:
                    intensityScale.Maximum = 100;
                    intensityScale.Minimum = -100;
                    intensityScale.Value = 0;
                    currentColorTransformMethodNormalizingValue = 0.00990099f;
                    break;
                case 1:
                    intensityScale.Maximum = 255;
                    intensityScale.Minimum = 0;
                    intensityScale.Value = 128;
                    currentColorTransformMethodNormalizingValue = 0.0078431f;
                    break;
                case 2:
                    intensityScale.Maximum = 500;
                    intensityScale.Minimum = 0;
                    intensityScale.Value = 50;
                    currentColorTransformMethodNormalizingValue = 0.02f;
                    break;
                default:
                    break;
            }
        }

        private void IntensityScale_ValueChanged(object sender, EventArgs e) { IntensityScaleHandler(sender as TrackBar); }

        private void ThresholdScale_ValueChanged(object sender, EventArgs e) { thresholdLabel.Text = thresholdScale.Value.ToString(); }

        private void CoefficientScale_ValueChanged(object sender, EventArgs e) { coefficientLabel.Text = coefficientScale.Value.ToString(); }

        private void VariableScale_ValueChanged(object sender, EventArgs e) { variableLabel.Text = variableScale.Value.ToString(); }

        private void ToneAdjustScale_ValueChanged(object sender, EventArgs e) { ToneAdjustScaleHandler(sender as TrackBar); }

        private void InvokeScaleOperationForAllFrames(TrackBar scale) {
            if (scale.Name.ToLower().Contains("tone")) ToneAdjustScaleHandler(scale, saving: true);
            else if (scale.Name.ToLower().Contains("intensity")) IntensityScaleHandler(scale, saving: true);
        }

        private void ScaleDefault_MouseDown(object sender, MouseEventArgs e) { 
            if (gifPlayback) gifPlayback = false; 
        }

        private async void ScaleDefault_MouseUp(object sender, MouseEventArgs e) {
            if (isGifSaving) return;
            if (isGif) {
                isGifSaving = true;
                InvokeScaleOperationForAllFrames(sender as TrackBar);
                isGifSaving = false;
                if (!gifPlayback) gifPlayback = true;
            }
        }

        private void InfoPanel_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e) {
            if (e.IsSelected) {
                e.Item.Selected = false;
                e.Item.Focused = false;
            }
        }

        private async void InfoPanel_DoubleClick(object sender, EventArgs e) {
            this.Cursor = Cursors.WaitCursor;
            try {
                await UpdateInfoPanel();
            }
            finally {
                this.Cursor = Cursors.Default;
            }
        }

        private void LayersContextMenuDelete_Click(object sender, EventArgs e) {
            if (contextMenuDelete.Tag != null && contextMenuDelete.Tag is TabPage tabToDelete) {
                string toolStripButtonName = ((TabPage)contextMenuDelete.Tag).Text;
                layersTabControl.TabPages.Remove(tabToDelete);
                var buttonToRemove = layersToolStrip.Items.OfType<ToolStripButton>()
                                                .FirstOrDefault(b => b.Text == toolStripButtonName);
                for (int i = 1; i < layersToolStrip.Items.Count; i++)
                    if (layersToolStrip.Items[i] is ToolStripButton button)
                        if (button.Text == toolStripButtonName) {
                            activeTabs.RemoveAt(i - 1);
                            break;
                        }
                if (buttonToRemove != null) layersToolStrip.Items.Remove(buttonToRemove);
            }
        }

        private void AddLayerToolStripMenuItem_Click(object sender, EventArgs e) {
            int newIndex = 0;
            foreach (var item in layersToolStrip.Items)
                if (item is ToolStripButton button)
                    if ((int)button.Tag > newIndex)
                        newIndex = (int)button.Tag;
            newIndex += 4;  // +2 to skip Original tab, Active tab and +1 due to not using zero-based numbering
            string tabName = "Layer " + (newIndex).ToString();
            activeTabs.Add(new int[] { newIndex, 0 });
            layersTabControl.TabPages.Add(CreateLayerTab(tabName, newIndex));
            layersToolStrip.Items.Add(CreateLayerButton(tabName, newIndex - 2));
            layersTabControl.TabPages[newIndex-1].Controls.Add(CreateLayerCanvas(newIndex));
        }

        private void LayersToolStripButton_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Right) {
                ToolStripButton button = sender as ToolStripButton;
                List<string> reserved = new List<string> { "Original", "Active", "Red", "Green", "Blue" };
                if (button != null && !reserved.Contains(button.Text)) {
                    foreach (TabPage tab in layersTabControl.TabPages) {
                        if (tab.Text == button.Text) {
                            contextMenuDelete.Tag = tab;
                            layersContextMenu.Show(Control.MousePosition);
                            break;
                        }
                    }
                }
            }
        }

        private void LayersTabControl_DrawItem(object sender, DrawItemEventArgs e) {
            Rectangle rec = layersTabControl.ClientRectangle;
            StringFormat StrFormat = new StringFormat();
            SolidBrush backColor = new SolidBrush(originalTab.BackColor);
            SolidBrush fontColor;
            Font fntTab = e.Font;
            StrFormat.Alignment = StringAlignment.Center;
            e.Graphics.FillRectangle(backColor, rec);
            for (int i = 0; i < layersTabControl.TabPages.Count; i++) {
                bool bSelected = (layersTabControl.SelectedIndex == i);
                Rectangle recBounds = layersTabControl.GetTabRect(i);
                if (bSelected) {
                    fontColor = new SolidBrush(originalTab.ForeColor);
                    e.Graphics.DrawString(layersTabControl.TabPages[i].Text, fntTab, fontColor, recBounds, StrFormat);
                }
                else {
                    fontColor = new SolidBrush(Color.DimGray);
                    e.Graphics.DrawString(layersTabControl.TabPages[i].Text, fntTab, fontColor, recBounds, StrFormat);
                }
            }
        }

        private void LayersTabControl_SelectedIndexChanged(object sender, EventArgs e) {
            LayersTabHandler();
        }

        private void ColorPreview_Paint(object sender, PaintEventArgs e) {
            using (Pen greyPen = new Pen(Color.FromArgb(75, 75, 75), 3)) {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                e.Graphics.DrawEllipse(greyPen, 0, 0, colorPreview.Width, colorPreview.Height);
            }
        }

        private void PictureBoxZoomAndPan_MouseDown(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                draggingDisplayedImage = true;
                zoomedInMousePosition = e.Location; // Store the initial mouse position
                (sender as PictureBox).Capture = true; // Capture mouse input
            }
        }

        private void PictureBoxZoomAndPan_MouseUp(object sender, MouseEventArgs e) {
            if (e.Button == MouseButtons.Left) {
                draggingDisplayedImage = false; // Stop dragging
                (sender as PictureBox).Capture = false; // Release mouse input
            }
        }

        private void PictureBoxZoomAndPan_MouseWheel(object sender, MouseEventArgs e) {
            Point mousePos = e.Location;
            float relativeX = (mousePos.X - displayedImageLocation.X) / zoomFactor;
            float relativeY = (mousePos.Y - displayedImageLocation.Y) / zoomFactor;
            if (e.Delta > 0)
                zoomFactor *= 1.1f;
            else
                zoomFactor /= 1.1f;
            zoomFactor = Math.Max(0.1f, Math.Min(zoomFactor, 15f)); // Clamp zoom factor
            displayedImageLocation.X = mousePos.X - (int)(relativeX * zoomFactor);
            displayedImageLocation.Y = mousePos.Y - (int)(relativeY * zoomFactor);
            (sender as PictureBox).Invalidate();
        }

        private void PictureBoxZoomAndPan_MouseMove(object sender, MouseEventArgs e) {
            if (draggingDisplayedImage) {
                // Calculate the change in mouse position
                int deltaX = (int)(e.X - zoomedInMousePosition.X);
                int deltaY = (int)(e.Y - zoomedInMousePosition.Y);
                // Update the displayed image location
                displayedImageLocation.X += deltaX;
                displayedImageLocation.Y += deltaY;
                // Update last mouse position
                zoomedInMousePosition = e.Location;
                (sender as PictureBox).Invalidate();
            }
        }

        private void PictureBoxDetectColor_MouseMove(object sender, MouseEventArgs e) {
            if (eyedropperButton.Checked) {
                PictureBox canvas = sender as PictureBox;
                (int imgX, int imgY) = CommonUtility.GetAdjustedCoordsOnCanvas(e.X, e.Y, canvas);
                // Ensure the coordinates are within bounds of the image
                UpdateInfoLabel($"{imgX}, {imgY}", 5);
                if (imgX >= 0 && imgX < canvas.Image.Width && imgY >= 0 && imgY < canvas.Image.Height) {
                    Color pixelColor = ((Bitmap)canvas.Image).GetPixel(imgX, imgY);
                    UpdateColorScales(pixelColor.R, pixelColor.G, pixelColor.B, pixelColor.A);
                    UpdateColorPreview();
                }
            }
        }

        private void PictureBoxZoomAndPan_Paint(object sender, PaintEventArgs e) {
            if (zoomedReferenceImage == null) return;
            //int scaledWidth = (int)(zoomedReferenceImage.Width * zoomFactor);
            //int scaledHeight = (int)(zoomedReferenceImage.Height * zoomFactor);
            InterpolationMode interpolationMode = InterpolationMode.Default;
            if (zoomFactor > 1 && zoomFactor < 2.5) interpolationMode = InterpolationMode.Bicubic;
            else if (zoomFactor > 2.5) interpolationMode = InterpolationMode.NearestNeighbor;
            e.Graphics.InterpolationMode = interpolationMode;
            // Translate and scale
            e.Graphics.TranslateTransform(displayedImageLocation.X, displayedImageLocation.Y);
            e.Graphics.ScaleTransform(zoomFactor, zoomFactor);
            // Redraw zoomed image
            if (!isGif) e.Graphics.DrawImage(zoomedReferenceImage, 0, 0, zoomedReferenceImage.Width, zoomedReferenceImage.Height);
            else if (isGif && isGifStreamed) e.Graphics.DrawImage(BitmapUtility.GetGifFrame(currentImage, gifCurrentFrameIndex), 0, 0, zoomedReferenceImage.Width, zoomedReferenceImage.Height);
            else if (isGif && !isGifStreamed) e.Graphics.DrawImage(zoomedReferenceImage, 0, 0, zoomedReferenceImage.Width, zoomedReferenceImage.Height);
        }

        private void EyedropperButton_Click(object sender, EventArgs e) {
            if ((sender as ToolStripButton).Checked) {
                toneAdjustGoupBox.Text = "Pixel Info";
                currentCanvas.Cursor = Cursors.Cross;
            }
            else {
                toneAdjustGoupBox.Text = "Tone Adjust";
                currentCanvas.Cursor = Cursors.Default;
            }
        }

        private void GifPlayButton_CheckedChanged(object sender, EventArgs e) {
            if (!isGif) {
                gifPlayButton.Checked = false;
                return;
            }
            gifStopped = false;
            if (!gifPlaybackTimer.Enabled) gifPlaybackTimer.Start();
            else gifPlaybackTimer.Stop();
        }

        private async void GifStopButton_Click(object sender, EventArgs e) {
            if (!isGif) {
                gifStopped = false;
                return;
            }
            if (gifPlayback) gifPlayback = false;
            if (gifCurrentFrameIndex != 0) {
                gifCurrentFrameIndex = 0;
                await UpdatePictureBox(referenceImage ?? currentImage);
            }
            gifFrameCarousel.Value = 0;
            await Task.Delay(100);
            gifStopped = false;
        }

        private void GifReverseButton_Click(object sender, EventArgs e) { if (!isGif) gifReversed = false; }

        private void GifFrameCarousel_ValueChanged(object sender, EventArgs e) {
            frameIndexDisplay.Text = $"{gifFrameCarousel.Value}";
            frameIndexDisplay.Left = gifFrameCarousel.Left + gifFrameCarousel.Value * (int)(gifFrameCarousel.Width/gifFrameCarousel.Maximum);
        }

        private async void GifFrameCarousel_Scroll(object sender, EventArgs e) {
            if (displayUpdating || isGifSaving) return;
            if (gifPlayback) gifPlayback = false;
            gifCurrentFrameIndex = gifFrameCarousel.Value;
            await UpdatePictureBox(referenceImage ?? currentImage);
        }

        private void DelayButton_MouseDown(object sender, MouseEventArgs e) {
            if ((sender as ToolStripButton).Name.ToLower().Contains("increase")) delayIncreasing = true;
            else delayIncreasing = false;
            delayButtonTimer.Start();
        }

        private void DelayButton_MouseUp(object sender, MouseEventArgs e) => delayButtonTimer.Stop();

        private void DelayButton_MouseEnter(object sender, EventArgs e) => delayLabel.Text = $"{gifFrameDelay}";

        private void DelayButton_MouseLeave(object sender, EventArgs e) => delayLabel.Text = "Delay";

        private void DelayButton_Click(object sender, EventArgs e) => UpdateFrameDelay();

        private void DelayButtonTimer_Tick(object sender, EventArgs e) => UpdateFrameDelay();

        private void StatusLabelDelay_Tick(object sender, EventArgs e) {
            void ProcessPendingStatusMessages(){
                if (messageQueue.Count > 0) {
                    statusLabelTimer.Start();
                }
                else
                    statusLabelTimer.Interval = 10;
            }

            statusLabelTimer.Stop();
            string mssg = messageQueue.Dequeue();
            if (mssg.ToLower().Trim() == "ready") {
                if (messageQueue.Count == 0)
                    statusLabel.Text = mssg.ToUpper();
            }
            else
                statusLabel.Text = mssg.ToUpper();
            ProcessPendingStatusMessages();
            if ((messageQueue.Count == 0) && (statusLabel.Text.ToLower().Trim() != "ready")) {
                statusLabelTimer.Interval = 3000;
                UpdateStatusLabel("ready");
            }
        }

        private async void GifPlaybackTimer_Tick(object sender, EventArgs e) {
            if (displayUpdating || isGifSaving) return;
            await UpdatePictureBox(referenceImage ?? currentImage);
            gifCurrentFrameIndex = (!gifReverseButton.Checked ? (gifCurrentFrameIndex + 1) : (gifCurrentFrameIndex - 1 + gifFrameCount)) % gifFrameCount;
            gifFrameCarousel.Value = gifCurrentFrameIndex;
        }

        private async void randomiseButton_Click(object sender, EventArgs e) {
            //var gifStream = new MemoryStream();
            //string theme = GetCheckedDropDownItem(defaultThemesMenu)?.Text;
            //execTime.Restart();
            //for (int i = 0; i < gifFrameCount; i++) {
            //    var frame = BitmapUtility.ApplyColorTransform(gifFrames[i], 0, 50, new int[] { 1, 0, 1 });
            //    gifFrames[i].Dispose();
            //    gifFrames[i] = frame;
            //    UpdateStatusLabel($"Applying... {i * 100 / (gifFrameCount)}%");
            //    await Task.Delay(1);
            //}

            //using (var encoder = new GifEncoder(gifStream)) {
            //    for (var i = 0; i < gifFrameCount; i++) {
            //        var frame1 = BitmapUtility.GetGifFrame(currentImage, i);
            //        var frame = BitmapUtility.ApplyColorTransform(frame1, 0, 50, new int[] { 1, 0, 1 });
            //        frame1.Dispose();
            //        encoder.AddFrame(frame, 0, 0, TimeSpan.FromMilliseconds(gifFrameDelay));
            //        UpdateStatusLabel($"Applying... {(int)Math.Round((float)i * 100 / (gifFrameCount + 1))}%");
            //        // Allow the UI to refresh
            //        await Task.Delay(1);
            //    }
            //}
            //gifStream.Position = 0;
            //execTime.Stop();
            //Console.WriteLine(execTime.Elapsed.TotalMilliseconds);
            //currentImage.Dispose();
            //currentImage = Image.FromStream(gifStream);
            //currentImage.Save("C:\\Users\\mfarh\\OneDrive\\Pictures\\Downloads\\test.gif");
        }

        private async void gifShuffleButton_Click(object sender, EventArgs e) {

            //pictureBox1.Image.Dispose();
            //pictureBox1.Image = null;
            //execTime.Restart();
            //List<Bitmap> frames = await Task.Run(() => BitmapUtility.ApplyGlitchEffect(pictureBox1.Image, 1, 89));
            //for (int i = 0; i < frames.Count; i++) {
            //    currentImage[i]?.Dispose();
            //    currentImage[i] = (Bitmap)frames[i].Clone();
            //    frames[i].Dispose();
            //}
            //pictureBox1.Image?.Dispose();
            //pictureBox1.Image = null;
            //currentImage?.Dispose();
            //currentImage = null;
            //currentImage = new List<Bitmap>();
            //for (int i = 0; i < frames.Count; i++) {
            //    currentImage.Add((Bitmap)frames[i].Clone());
            //    frames[i].Dispose();
            //}
            //gifFrameCount = currentImage.Count;
            //gifCurrentFrameIndex = 0;
            //frames.Clear();
            //execTime.Stop();
            //Console.WriteLine(execTime.Elapsed.TotalMilliseconds);

            Console.WriteLine(gifFrameDelay);

        }

        private void helpMenu_Click(object sender, EventArgs e) {
            // Get original image size
            Size originalImageSize = pictureBox1.Image != null ? pictureBox1.Image.Size : Size.Empty;
            Bitmap resultBitmap = new Bitmap(pictureBox1.Image.Width, pictureBox1.Image.Height);

            // Calculate scale factors
            float scaleX = (float)pictureBox1.Width / originalImageSize.Width;
            float scaleY = (float)pictureBox1.Height / originalImageSize.Height;
            float scaleFactor = Math.Min(scaleX, scaleY); // Uniform scaling

            int scaledWidth = (int)(originalImageSize.Width * scaleFactor);
            int scaledHeight = (int)(originalImageSize.Height * scaleFactor);

            // Calculate the position to center the image
            int offsetX = (pictureBox1.Width - scaledWidth) / 2;
            int offsetY = (pictureBox1.Height - scaledHeight) / 2;

            using (Graphics g = Graphics.FromImage(resultBitmap)) {
                // Draw the original image if it exists
                if (pictureBox1.Image != null) {
                    g.DrawImage(pictureBox1.Image, 0, 0, pictureBox1.Image.Width, pictureBox1.Image.Height);
                }

                // Draw the zoomed reference image, adjusting for scaling and centering
                if (zoomedReferenceImage != null) {
                    // Calculate adjusted position for the zoomed reference image
                    g.ScaleTransform(zoomFactor, zoomFactor);
                    g.DrawImage(zoomedReferenceImage, offsetX*scaleFactor + displayedImageLocation.X / scaleFactor, (displayedImageLocation.Y / scaleFactor)- (offsetY/zoomFactor)/scaleFactor); // Draw the image at the transformed position
                }
            }
            //EnableZoomAndPan(false);
            pictureBox1.Image?.Dispose();
            pictureBox1.Image = null;
            pictureBox1.Invalidate();
            pictureBox1.Refresh();
            pictureBox1.Image = resultBitmap;
        }
    }       
}
