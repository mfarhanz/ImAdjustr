using System;
using System.IO;
using System.Drawing;
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Forms;
using System.Drawing.Imaging;
using System.Threading.Tasks;
using System.Collections.Generic;
using ImAdjustr.Utility;
using ImAdjustr.Internal.Static;

namespace ImAdjustr.Base {
    partial class Editor {

        private string imageFilePath;
        private dynamic imageThemes;
        private dynamic imageFilters;
        private dynamic imagePalettes;
        private dynamic editorThemes;
        private dynamic currentImage;
        private dynamic referenceImage;
        private dynamic zoomedReferenceImage;
        private bool isLoading;
        private bool isGif;
        private bool isGifStreamed;
        private bool isGifSaving;
        private bool gifReferenceDisplay;
        private int gifFrameCount;
        private int gifFrameDelay;
        private int gifCurrentFrameIndex;
        private List<Bitmap> gifCachedFrames;
        private List<Bitmap> gifCroppedFrames;
        private FileInfo imageFileInfo;
        private Stopwatch execTime;
        public TaskCompletionSource<bool> currentTask;
        public delegate List<Bitmap> EffectDelegate(dynamic input, int param1, int param2);

        private async void ClearBuffers() {
            // IMPORTANT: Always set image variables to null after disposing
            if (displayUpdating) await currentTask.Task;
            foreach (TabPage tabPage in layersTabControl.TabPages) {
                foreach (Control control in tabPage.Controls) {
                    if (control is PictureBox pictureBox) {
                        pictureBox.Image?.Dispose();
                        pictureBox.Image = null;
                    }
                }
            }
            BitmapUtility.Dispose(ref referenceImage);
            BitmapUtility.Dispose(ref currentImage);
        }

        private void LoadAppData() {
            void LoadAndUpdateStatus(ref dynamic dictVar, string filepath) {
                switch (Path.GetExtension(filepath).TrimStart('.')) {
                    case "json":
                        dictVar = FileReader.LoadJsonData(filepath);
                        break;
                    case "xml":
                        dictVar = FileReader.LoadXMLConfig(filepath);
                        break;
                    // any other file types that need to be loaded,
                    // along with their corresponding method in FileReader
                    default:
                        break;
                }
                UpdateStatusLabel($"Loaded {filepath}");
            }
            LoadAndUpdateStatus(ref imageThemes, Paths.ImageThemes);
            LoadAndUpdateStatus(ref imageFilters, Paths.ImageKernels);
            LoadAndUpdateStatus(ref imagePalettes, Paths.ImagePalettes);
            LoadAndUpdateStatus(ref editorThemes, Paths.AppThemes);
        }

        internal Bitmap ResizeImage(Bitmap image, int maxWidth, int maxHeight) {
            Bitmap YieldResizedBitmap(Bitmap bitmap) {
                float ratio = Math.Min((float)maxWidth / image.Width, (float)maxHeight / image.Height);
                int newWidth = (int)(image.Width * ratio);
                int newHeight = (int)(image.Height * ratio);
                Bitmap newBitmap = new Bitmap(newWidth, newHeight);
                using (Graphics g = Graphics.FromImage(newBitmap)) {
                    g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.DrawImage(bitmap, 0, 0, newWidth, newHeight);
                }
                return newBitmap;
            }

            Bitmap resized = null;
            int frameCount;
            int frameDelay = BitmapUtility.GetFrameDelay(image);
            try {
                frameCount = image.GetFrameCount(FrameDimension.Time);
            }
            catch (Exception) {
                frameCount = 0;
            }
            if (frameCount > 1) {
                var gifStream = new MemoryStream();
                using (var encoder = new GifEncoder(gifStream)) {
                    for (var i = 0; i < frameCount; i++) {
                        try {
                            Bitmap frame = YieldResizedBitmap(BitmapUtility.GetGifFrame(image, i));
                            encoder.AddFrame(frame, 0, 0, TimeSpan.FromMilliseconds(frameDelay));
                        }
                        catch (Exception) {
                            Console.WriteLine($"Error resizing frame {i + 1} of GIF");
                            throw;
                        }
                    }
                }
                gifStream.Position = 0;
                resized = (Bitmap)Image.FromStream(gifStream);
            }
            else resized = YieldResizedBitmap(image);
            return resized;
        }

        private async Task<bool> LoadImageBuffers() {
            if (lastSelectedTabIndex != 0) layersTabControl.SelectedIndex = 0;
            try {
                ShowLoadingFeedback();
                isGif = imageFileInfo.Extension.TrimStart('.') == "gif" ? true : false;
                if (isGif) {
                    currentImage = (Bitmap)Image.FromFile(imageFilePath);
                    gifFrameDelay = Math.Max(BitmapUtility.GetFrameDelay(currentImage), 5);
                    gifFrameCount = currentImage.GetFrameCount(FrameDimension.Time);
                    if (currentImage.Width > 1280) {
                        bool confirm = ShowMessageBox($"The dimensions of the selected GIF are very large ({currentImage.Width}x{currentImage.Height})." +
                                        $"\r\nWould you like the GIF to be resized to the recommended dimensions?", "Confirmation");
                        if (confirm) {
                            currentImage = await Task.Run(() => ResizeImage(currentImage, 1280, 720));
                        }
                    }
                    if (currentImage.Width * currentImage.Height * gifFrameCount < 20000000) { // Only store as frames in a list if gif is small
                        using (Bitmap temp = currentImage.Clone()) {    // otherwise stream gif directly
                            currentImage.Dispose();
                            currentImage = BitmapUtility.GetGifFrames(temp);
                            isGifStreamed = false;
                        }
                    }
                    else isGifStreamed = true;
                    gifPlaybackTimer.Interval = gifFrameDelay;
                    gifPlayback = true;
                }
                else {
                    gifFrameDelay = 30;
                    gifFrameCount = 0;
                    currentCanvas.Image = Image.FromFile(imageFilePath);
                }
                gifCurrentFrameIndex = 0;
                gifFrameCarousel.Minimum = 0;
                gifFrameCarousel.Maximum = gifFrameCount == 0 ? 0 : gifFrameCount - 1;
                UpdateStatusLabel($"Loaded {imageFileInfo.Name}");
                return true;
            }
            catch (Exception) {
                UpdateStatusLabel($"Error loading {imageFileInfo.Name}");
                return false;
            }
        }

        private async void LoadImageRoutine() {
            ClearBuffers();
            ResetControls();
            ShowPlaceholderIcon(false);
            if (await LoadImageBuffers()) {
                EnableControls(true);
                await UpdateInfoPanel();
            }
            else ShowPlaceholderIcon(true);
        }

        private void LoadImageFromDroppedFile(string[] files) {
            bool IsImageFile(string filePath) {
                string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".ico" };
                string extension = Path.GetExtension(filePath).ToLower();
                return Array.Exists(imageExtensions, ext => ext.Equals(extension));
            }
            if (File.Exists(files[0])) {
                imageFilePath = files[0];
                if (!IsImageFile(imageFilePath)) {
                    imageSelectTextbox.Text = "Load an Image";
                    imageSelectIcon.Text = "📂";
                    return;
                }
                imageFileInfo = new FileInfo(imageFilePath);
                LoadImageRoutine();
            }
        }

        private void LoadImageFromSystem() {
            var dlg = new OpenFileDialog();
            dlg.Multiselect = false;
            dlg.Filter = "JPG Files (*.jpg)|*.jpg|" +
                        "PNG Files (*.png)|*.png|" +
                        "JPEG Files (*.jpeg)|*.jpeg|" +
                        "Bitmap Files (*.bmp)|*.bmp|" +
                        "Icon Files (*.ico)|*.ico|" +
                        "GIF Files (*.gif)|*.gif";
            if (dlg.ShowDialog() != DialogResult.OK)
                return;
            imageFilePath = dlg.FileName;
            imageFileInfo = new FileInfo(imageFilePath);
            dlg.Dispose();
            LoadImageRoutine();
        }

        private async void SaveDisplayedImage() {
            if (!isGif) {
                if (currentCanvas.Image != null || currentImage != null) {
                    BitmapUtility.Dispose(ref referenceImage);
                    referenceImage = (Image)currentCanvas.Image?.Clone();
                    UpdateStatusLabel("Saved new edits");
                }
                else UpdateStatusLabel("Nothing to save currently");
            }
            else {
                if (currentImage != null && referenceImage != null) {
                    try {
                        isGifSaving = true;
                        if (!isGifStreamed) {
                            for (int i = 0; i < currentImage.Count; i++) {
                                currentImage[i]?.Dispose();
                                currentImage[i] = referenceImage[i];
                                UpdateStatusLabel($"Saving... {(i + 1) * 100 / gifFrameCount}%");
                            }
                            referenceImage?.Clear();
                            referenceImage = null;
                        }
                        else {
                            if (!gifPlayback) gifPlayback = true;
                            currentImage?.Dispose();
                            var res = await SaveGifWithEncoder(referenceImage);
                            currentImage = res.image;
                            if (res.status == 1) UpdateStatusLabel("GIF partially saved: could not save some frames of gif");
                        }
                    }
                    finally {
                        isGifSaving = false;
                    }
                }
                else UpdateStatusLabel("Nothing to save currently");
            }
        }

        private async void SaveDisplayedImageToSystem() {
            if (currentCanvas.Image != null) {
                try {
                    using (SaveFileDialog saveFileDialog = new SaveFileDialog()) {
                        if (isGif) saveFileDialog.Filter = "GIF File|*.gif|" + "All Files|*.*";
                        else
                            saveFileDialog.Filter = "PNG File|*.png|" +
                                                    "JPG File|*.jpg|" +
                                                    "JPEG File|*.jpeg|" +
                                                    "Bitmap File|*.bmp|" +
                                                    "Icon File|*.ico|" +
                                                    "GIF File|*.gif|" +
                                                    "All Files|*.*";
                        saveFileDialog.Title = "Save As";
                        saveFileDialog.DefaultExt = isGif ? "gif" : "png"; // Default file extension
                        if (saveFileDialog.ShowDialog() == DialogResult.OK) {
                            if (!isGif) {
                                currentCanvas.Image.Save(saveFileDialog.FileName);
                                UpdateStatusLabel($"Image saved at {saveFileDialog.FileName}");
                            }
                            else {
                                isGifSaving = true;
                                (bool value, int status) saved = (false, -1);
                                try {
                                    (Image image, int status) res = await SaveGifWithEncoder(currentImage);
                                    using (res.image) res.image.Save(saveFileDialog.FileName);
                                    saved = (true, res.status);
                                }
                                catch (Exception) {
                                    (bool result, int status) res = await SaveGifWithPIL(currentImage, saveFileDialog.FileName, gifFrameDelay);
                                    if (res.result) saved = (true, res.status);
                                }
                                finally {
                                    isGifSaving = false;
                                    if (saved.value) {
                                        if (saved.status == 0) UpdateStatusLabel($"GIF saved at {saveFileDialog.FileName}");
                                        else if (saved.status == 1) UpdateStatusLabel($"Could not save all frames of GIF, file saved at {saveFileDialog.FileName}");
                                        else UpdateStatusLabel($"Error saving GIF, check log for details");
                                    }
                                    else UpdateStatusLabel($"Error saving GIF at {saveFileDialog.FileName}:  GIF file may be in use");
                                }
                            }
                        }
                    }
                }
                catch (Exception) {
                    UpdateStatusLabel("Error saving image file");
                    throw;
                }
            }
            else UpdateStatusLabel("Nothing to save currently");
        }

        private async Task<(Image, int)> SaveGifWithEncoder(dynamic gif) {
            int saved = 0;
            var gifStream = new MemoryStream();
            using (var encoder = new GifEncoder(gifStream)) {
                for (var i = 0; i < gifFrameCount; i++) {
                    try {
                        dynamic frame;
                        if (isGifStreamed) frame = BitmapUtility.GetGifFrame(gif, i);
                        else frame = gif[i];
                        encoder.AddFrame(frame, 0, 0, TimeSpan.FromMilliseconds(gifFrameDelay));
                        UpdateStatusLabel($"Saving... {(int)(++saved) * 100 / gifFrameCount}%");
                        // Allow the UI to refresh
                        await Task.Delay(1);
                    }
                    catch (Exception) {
                        Console.WriteLine($"Error saving frame {i + 1} of GIF");
                        throw;
                    }
                }
            }
            gifStream.Position = 0;
            return (Image.FromStream(gifStream), (saved == gifFrameCount) ? 0 : 1);
        }

        private async Task<(bool, int)> SaveGifWithPIL(List<Bitmap> frames, string outputPath, int delay) {
            string tempFolder = Path.Combine(Path.GetTempPath(), Process.GetCurrentProcess().ProcessName + "Temp");
            if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder); // Temporary folder to store images
            try {
                // Save each image to the temporary folder with a numbered filename
                for (int i = 0; i < frames.Count; i++) {
                    string tempPath = Path.Combine(tempFolder, $"batch_{i:D4}.png");
                    frames[i].Save(tempPath, ImageFormat.Png);
                }
                string pythonScriptPath = Paths.GifSaver;
                string pythonPath;
                try {
                    pythonPath = Path.Combine(control.pythonHome, "python.exe");
                }
                catch (FileNotFoundException) {
                    try {
                        pythonPath = CommonUtility.FindPythonExecutable();
                    }
                    catch (Exception) {
                        Invoke((Action)(() => UpdateStatusLabel("Unable to save GIF: requires Python installed or added to PATH")));
                        return (false, -1);
                    }
                }

                return await Task.Run(() => {
                    try {
                        using (Process process = new Process()) {
                            process.StartInfo.FileName = pythonPath; // Python executable path
                            process.StartInfo.Arguments = $"\"{pythonScriptPath}\" \"{tempFolder}\" \"{outputPath}\" {delay}";
                            process.StartInfo.UseShellExecute = false;
                            process.StartInfo.RedirectStandardOutput = true;
                            process.StartInfo.RedirectStandardError = true;
                            process.StartInfo.CreateNoWindow = true;
                            string output = string.Empty;
                            process.OutputDataReceived += (sender, e) => {
                                if (!string.IsNullOrEmpty(e.Data)) {
                                    output = e.Data;
                                    if (int.TryParse(e.Data, out int index)) {
                                        Invoke((Action)(() => UpdateStatusLabel($"Saving... {(int)(index + 1) * 100 / gifFrameCount}%")));
                                    }
                                }
                            };
                            process.Start();
                            process.BeginOutputReadLine();
                            string err = process.StandardError.ReadToEnd();
                            process.WaitForExit();
                            // Check for errors with python script
                            if (!string.IsNullOrWhiteSpace(err)) {
                                Invoke((Action)(() => UpdateStatusLabel("Error saving GIF")));
                                Console.WriteLine("Python Error: " + err);
                            }
                            // Check for the success message in the output
                            var response = JsonSerializer.Deserialize<CommonUtility.ScriptResponse>(output.Trim());
                            if (response.Status == "success") {
                                if (response.Data[1] > 0 || response.Data[0] != gifFrameCount) {
                                    return (true, 1); 
                                }
                                else if (response.Data[1] == 0 && response.Data[0] == gifFrameCount) {
                                    return (true, 0); 
                                }
                                else {
                                    return (false, -1); 
                                }
                            }
                            else if (response.Status == "error") {
                                Console.WriteLine(response.Message);
                                return (false, -1);
                            }
                            else {
                                Console.WriteLine("JSON Error: could not parse json result from gif_saver.py");
                                return (false, -1);
                            }
                        }
                    }
                    catch (Exception ex) {
                        Console.WriteLine($"Error starting Python: {ex.Message}");
                        return (false, -1);
                    }
                });
            }
            finally {
                // Clean up created temporary files/folder
                if (Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
            }
        }
    }
}
