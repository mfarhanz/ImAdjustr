using System;
using System.IO;
using System.Drawing;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.ExceptionServices;

namespace ImAdjustr.Utility {
    internal class BitmapUtility {

        internal static void Dispose(ref dynamic image) {
            if (image is Bitmap || image is Image) {
                image?.Dispose();
                image = null;
            }
            else if (image is List<Bitmap> || image is List<Image>) {
                for (int fi = 0; fi < image.Count; fi++) {   // fi - frame index
                    image[fi]?.Dispose();
                    image[fi] = null;
                }
                image?.Clear();
                image = null;
            }
        }

        internal static Color GetColorAt(int x, int y) {
            Bitmap bmp = new Bitmap(1, 1);
            Rectangle bounds = new Rectangle(x, y, 1, 1);
            using (Graphics g = Graphics.FromImage(bmp))
                g.CopyFromScreen(bounds.Location, Point.Empty, bounds.Size);
            return bmp.GetPixel(0, 0);
        }

        internal static (double compressed, double uncompressed) GetImageSize(Bitmap bitmap) {
            dynamic sizeCompressed, sizeUncompressed;
            using (MemoryStream memoryStream = new MemoryStream()) {
                bitmap.Save(memoryStream, ImageFormat.Jpeg);
                sizeCompressed = memoryStream.Length / (1024.0 * 1024.0);
                sizeUncompressed = bitmap.Width * bitmap.Height * Image.GetPixelFormatSize(bitmap.PixelFormat) / 8 * 1 / (1024.0 * 1024);
            }
            return (sizeCompressed, sizeUncompressed);
        }

        internal static Bitmap GetTransparencyBackground(int width, int height, int squareSize, Color color1, Color color2) {
            Bitmap bmp = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(bmp)) {
                for (int y = 0; y < height; y += squareSize) {
                    for (int x = 0; x < width; x += squareSize) {
                        Color color = (x / squareSize % 2 == 0) ^ (y / squareSize % 2 == 0) ? color1 : color2;
                        using (Brush brush = new SolidBrush(color)) {
                            g.FillRectangle(brush, x, y, squareSize, squareSize);
                        }
                    }
                }
            }
            return bmp;
        }

        internal static Bitmap GetChannelToneAdjusted(Bitmap img, int channel, float toneVal) {
            float normalized = 2 * (toneVal / 255) - 1;
            int[] channels = new int[] { 0, 0, 0 };
            if (!(channel == 3)) channels[channel] = 1;
            return ApplyColorTransform(img, channel == 3 ? 1 : 0, normalized, channels);
        }

        internal static Bitmap GetGifFrame(Bitmap gifimage, int frameIndex) {
            if (gifimage is null) return null;
            if (frameIndex >= gifimage.GetFrameCount(FrameDimension.Time))
                throw new ArgumentOutOfRangeException("frameIndex");
            gifimage.SelectActiveFrame(FrameDimension.Time, frameIndex);
            Bitmap frame = new Bitmap(gifimage);
            return frame;
        }

        internal static List<Bitmap> GetGifFrames(Bitmap gifImage) {
            List<Bitmap> frames = new List<Bitmap>();
            //using (Image gifImage = Image.FromFile(filepath)) {
            int frameCount = gifImage.GetFrameCount(FrameDimension.Time);
            for (int i = 0; i < frameCount; i++) {
                gifImage.SelectActiveFrame(FrameDimension.Time, i);
                Bitmap frame = new Bitmap(gifImage.Width, gifImage.Height);
                using (Graphics g = Graphics.FromImage(frame)) {
                    g.DrawImage(gifImage, new Rectangle(0, 0, frame.Width, frame.Height));
                }
                frames.Add(frame);
            //}
            }
            return frames;
        }

        internal static int GetFrameDelay(Bitmap gifImage) {
            gifImage.SelectActiveFrame(FrameDimension.Time, 0);
            try {
                foreach (PropertyItem prop in gifImage.PropertyItems) {
                    if (prop.Id == 0x5100) // Frame delay property ID
                        return BitConverter.ToInt32(prop.Value, 0) * 10; // Convert from 1/100s to ms
                }
                return 50; // default delay
            }
            catch (Exception) {
                return 50;
            }
        }

        [HandleProcessCorruptedStateExceptions]
        internal static int? GetColorCountUnsafe(Bitmap currentImage) {
            if (currentImage is null) return null;
            Bitmap original = new Bitmap(currentImage);
            // HashSet to keep track of unique colors
            HashSet<Color> uniqueColors = new HashSet<Color>();
            // Lock the bitmap's bits
            BitmapData originalData = original.LockBits(
                new Rectangle(0, 0, original.Width, original.Height),
                ImageLockMode.ReadOnly,
                original.PixelFormat
            );
            // Get the pointer to the first pixel data
            IntPtr ptr = originalData.Scan0;
            int bytesPerPixel = Image.GetPixelFormatSize(original.PixelFormat) / 8;
            int stride = originalData.Stride;
            int width = original.Width;
            int height = original.Height;
            unsafe {
                byte* scan0 = (byte*)ptr.ToPointer();
                for (int y = 0; y < height; y++) {
                    byte* row = scan0 + (y * stride);
                    for (int x = 0; x < width; x++) {
                        // Get the color components
                        byte blue = row[x * bytesPerPixel];
                        byte green = row[x * bytesPerPixel + 1];
                        byte red = row[x * bytesPerPixel + 2];
                        byte alpha = bytesPerPixel == 4 ? row[x * bytesPerPixel + 3] : (byte)255;
                        Color color = Color.FromArgb(alpha, red, green, blue);
                        // Add the color to the HashSet
                        uniqueColors.Add(color);
                    }
                }
            }
            original.UnlockBits(originalData);
            int ret = uniqueColors.Count;
            original?.Dispose();
            uniqueColors.Clear();
            uniqueColors.TrimExcess();
            uniqueColors = null;
            GC.Collect();                  // Not generally recommended, but this is the only thing that works
                                           // to discard the HashSet immediately
            return ret;
        }

        internal static List<Bitmap> ConvertBitmapToBitmapList(Bitmap img, int gifLength) {
            List<Bitmap> gifFrames = new List<Bitmap>();
            for (int i = 0; i < gifLength; i++)
                gifFrames.Add((Bitmap)img.Clone());
            img?.Dispose();
            img = null;
            return gifFrames;
        }

        [HandleProcessCorruptedStateExceptions]
        internal static Bitmap ExtractChannelFromImage(dynamic currentImage, int channel) {
            if (currentImage is null) return null;
            Bitmap original = new Bitmap(currentImage);
            Bitmap grayBitmap = new Bitmap(original.Width, original.Height, PixelFormat.Format8bppIndexed);
            // Create and set the grayscale palette
            ColorPalette palette = grayBitmap.Palette;
            for (int i = 0; i < 256; i++) {
                palette.Entries[i] = Color.FromArgb(i, i, i); // Create grayscale color
            }
            grayBitmap.Palette = palette;
            // Lock the bits of the original image
            BitmapData originalData = original.LockBits(new Rectangle(0, 0, original.Width, original.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData grayData = grayBitmap.LockBits(new Rectangle(0, 0, grayBitmap.Width, grayBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            int bytesPerPixel = Image.GetPixelFormatSize(original.PixelFormat) / 8;
            int byteCount = originalData.Stride * original.Height;
            byte[] originalPixels = new byte[byteCount];
            System.Runtime.InteropServices.Marshal.Copy(originalData.Scan0, originalPixels, 0, byteCount);
            // Prepare grayscale pixel buffer
            int grayByteCount = grayData.Stride * grayBitmap.Height;
            byte[] grayPixels = new byte[grayByteCount];
            int offset = channel == 0 ? 2 : (channel == 1 ? 1 : 0);
            for (int y = 0; y < original.Height; y++) {
                for (int x = 0; x < original.Width; x++) {
                    int originalIndex = y * originalData.Stride + x * bytesPerPixel;
                    int grayIndex = y * grayData.Stride + x;
                    byte ch = originalPixels[originalIndex + offset];
                    // Set grayscale value in the new bitmap
                    grayPixels[grayIndex] = ch;
                }
            }

            // Copy the modified pixel data back to the grayscale bitmap
            System.Runtime.InteropServices.Marshal.Copy(grayPixels, 0, grayData.Scan0, grayByteCount);
            // Unlock the bits (free up held memory)
            original.UnlockBits(originalData);
            grayBitmap.UnlockBits(grayData);
            original?.Dispose();    // Always dispose objects/images implement IDisposable
            return grayBitmap;
        }

        [HandleProcessCorruptedStateExceptions]
        internal static Bitmap ExtractChannelFromImageUnsafe(Bitmap currentImage, int channel) {
            if (currentImage is null) return null;
            Bitmap original = new Bitmap(currentImage);
            Bitmap grayBitmap = new Bitmap(original.Width, original.Height, PixelFormat.Format8bppIndexed);
            ColorPalette palette = grayBitmap.Palette;
            for (int i = 0; i < 256; i++) {
                palette.Entries[i] = Color.FromArgb(i, i, i); // Create grayscale color
            }
            grayBitmap.Palette = palette;
            BitmapData originalData = original.LockBits(new Rectangle(0, 0, original.Width, original.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            BitmapData grayData = grayBitmap.LockBits(new Rectangle(0, 0, grayBitmap.Width, grayBitmap.Height), ImageLockMode.WriteOnly, PixelFormat.Format8bppIndexed);
            int offset = channel == 0 ? 2 : (channel == 1 ? 1 : 0);
            int bytesPerPixel = Image.GetPixelFormatSize(original.PixelFormat) / 8;
            int stride = originalData.Stride;
            int width = original.Width;
            int height = original.Height;

            unsafe {
                byte* originalPtr = (byte*)originalData.Scan0.ToPointer();
                byte* grayPtr = (byte*)grayData.Scan0.ToPointer();
                for (int y = 0; y < height; y++) {
                    byte* originalRow = originalPtr + (y * stride);
                    byte* grayRow = grayPtr + (y * grayData.Stride);

                    for (int x = 0; x < width; x++) {
                        byte ch = originalRow[x * bytesPerPixel + offset];
                        grayRow[x] = ch;
                    }
                }
            }

            original.UnlockBits(originalData);
            grayBitmap.UnlockBits(grayData);
            original?.Dispose();
            return grayBitmap;
        }

        [HandleProcessCorruptedStateExceptions]
        internal static Bitmap CombineActiveChannelsUnsafe(dynamic currentImage, List<int[]> channelList) {
            if (currentImage is null) return null;
            int width = currentImage.Width;
            int height = currentImage.Height;
            // Create an empty RGBA image
            Bitmap rgbaBitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            BitmapData rgbaData = rgbaBitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
            // Prepare pointers for RGBA bitmap
            unsafe {
                byte* rgbaPtr = (byte*)rgbaData.Scan0.ToPointer();
                int stride = rgbaData.Stride;
                for (int y = 0; y < height; y++) {
                    byte* rowPtr = rgbaPtr + (y * stride);
                    for (int x = 0; x < width; x++) {
                        rowPtr[x * 4 + 0] = 0; // Blue
                        rowPtr[x * 4 + 1] = 0; // Green
                        rowPtr[x * 4 + 2] = 0; // Red
                        rowPtr[x * 4 + 3] = 255; // Alpha
                    }
                }
                // Extract and combine channels based on the array
                for (int i = 0; i < 3; i++) {
                    if (channelList[i][1] == 1) {
                        Bitmap grayBitmap;
                        try {
                            grayBitmap = ExtractChannelFromImageUnsafe(currentImage, channelList[i][0]);
                        }
                        catch (Exception ex) {
                            Console.WriteLine($"Error using unsafe channel extract: {ex}");
                            grayBitmap = ExtractChannelFromImage(currentImage, i);
                        }
                        BitmapData grayData = grayBitmap.LockBits(new Rectangle(0, 0, grayBitmap.Width, grayBitmap.Height), ImageLockMode.ReadOnly, PixelFormat.Format8bppIndexed);
                        byte* grayPtr = (byte*)grayData.Scan0.ToPointer();
                        for (int y = 0; y < height; y++) {
                            byte* grayRow = grayPtr + (y * grayData.Stride);
                            byte* rowPtr = rgbaPtr + (y * stride);
                            for (int x = 0; x < width; x++) {
                                byte value = grayRow[x];
                                switch (i) {
                                    case 0: // Red
                                        rowPtr[x * 4 + 2] = value;
                                        break;
                                    case 1: // Green
                                        rowPtr[x * 4 + 1] = value;
                                        break;
                                    case 2: // Blue
                                        rowPtr[x * 4 + 0] = value;
                                        break;
                                }
                            }
                        }
                        grayBitmap.UnlockBits(grayData);
                        grayBitmap?.Dispose();
                    }
                }
            }
            rgbaBitmap.UnlockBits(rgbaData);
            return rgbaBitmap;
        }

        internal static void ApplySimpleColorTransformInPlace(Bitmap bitmap, ref byte[] rgbValues) {
            Rectangle rect = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData bmpData = bitmap.LockBits(rect, ImageLockMode.ReadWrite, bitmap.PixelFormat);
            try {
                // Get the address of the first line.
                IntPtr ptr = bmpData.Scan0;
                // Allocate the array only if it hasn't been allocated yet or if the size has changed
                int bytes = Math.Abs(bmpData.Stride) * bitmap.Height;
                if (rgbValues == null || rgbValues.Length != bytes) {
                    rgbValues = new byte[bytes];
                }
                // Copy the RGB values into the array.
                Marshal.Copy(ptr, rgbValues, 0, bytes);
                // Apply your color transformation.
                for (int i = 0; i < rgbValues.Length; i += 4) {
                    rgbValues[i + 2] = (byte)(rgbValues[i + 2] * 0.5);
                    rgbValues[i + 1] = (byte)(rgbValues[i + 1] * 0.7);
                    rgbValues[i] = (byte)(rgbValues[i] * 1.2);
                }
                // Copy the modified RGB values back to the bitmap.
                Marshal.Copy(rgbValues, 0, ptr, bytes);
            }
            finally {
                bitmap.UnlockBits(bmpData);
            }
        }

        internal static Bitmap ApplyColorTransform(Bitmap currentImage, int operation, float val, int[] channels, List<dynamic> themeMatrix=null) {
            if (currentImage is null) return null;
            bool checkNull = themeMatrix is null;
            Bitmap original = new Bitmap(currentImage);
            ColorMatrix colorMatrix = new ColorMatrix();
            switch (operation) {
                case 0:
                    colorMatrix.Matrix00 = checkNull ? 1 : themeMatrix[0];
                    colorMatrix.Matrix10 = checkNull ? 0 : themeMatrix[1];
                    colorMatrix.Matrix20 = checkNull ? 0 : themeMatrix[2];
                    colorMatrix.Matrix01 = checkNull ? 0 : themeMatrix[4];
                    colorMatrix.Matrix11 = checkNull ? 1 : themeMatrix[5];
                    colorMatrix.Matrix21 = checkNull ? 0 : themeMatrix[6];
                    colorMatrix.Matrix02 = checkNull ? 0 : themeMatrix[8];
                    colorMatrix.Matrix12 = checkNull ? 0 : themeMatrix[9];
                    colorMatrix.Matrix22 = checkNull ? 1 : themeMatrix[10];
                    colorMatrix.Matrix40 = (channels[0] == 1 ? val : 0) * (checkNull ? 1 : themeMatrix[3]);
                    colorMatrix.Matrix41 = (channels[1] == 1 ? val : 0) * (checkNull ? 1 : themeMatrix[7]);
                    colorMatrix.Matrix42 = (channels[2] == 1 ? val : 0) * (checkNull ? 1 : themeMatrix[11]);
                    colorMatrix.Matrix33 = 1;
                    colorMatrix.Matrix44 = 1;
                    break;
                case 1:
                    if (channels[0] == 0 && channels[1] == 0 && channels[2] == 0) {
                        colorMatrix.Matrix00 = checkNull ? 1 : themeMatrix[0];
                        colorMatrix.Matrix10 = checkNull ? 0 : themeMatrix[1];
                        colorMatrix.Matrix20 = checkNull ? 0 : themeMatrix[2];
                        colorMatrix.Matrix30 = checkNull ? 0 : themeMatrix[3];
                        colorMatrix.Matrix01 = checkNull ? 0 : themeMatrix[4];
                        colorMatrix.Matrix11 = checkNull ? 1 : themeMatrix[5];
                        colorMatrix.Matrix21 = checkNull ? 0 : themeMatrix[6];
                        colorMatrix.Matrix31 = checkNull ? 0 : themeMatrix[7];
                        colorMatrix.Matrix02 = checkNull ? 0 : themeMatrix[8];
                        colorMatrix.Matrix12 = checkNull ? 0 : themeMatrix[9];
                        colorMatrix.Matrix22 = checkNull ? 1 : themeMatrix[10];
                        colorMatrix.Matrix32 = checkNull ? 0 : themeMatrix[11];
                        colorMatrix.Matrix43 = val;
                        colorMatrix.Matrix44 = 1;
                    }
                    else {
                        colorMatrix.Matrix00 = checkNull ? 1 : themeMatrix[0];
                        colorMatrix.Matrix10 = checkNull ? 0 : themeMatrix[1];
                        colorMatrix.Matrix20 = checkNull ? 0 : themeMatrix[2];
                        colorMatrix.Matrix01 = checkNull ? 0 : themeMatrix[4];
                        colorMatrix.Matrix11 = checkNull ? 1 : themeMatrix[5];
                        colorMatrix.Matrix21 = checkNull ? 0 : themeMatrix[6];
                        colorMatrix.Matrix02 = checkNull ? 0 : themeMatrix[8];
                        colorMatrix.Matrix12 = checkNull ? 0 : themeMatrix[9];
                        colorMatrix.Matrix22 = checkNull ? 1 : themeMatrix[10];
                        colorMatrix.Matrix33 = 1;
                        colorMatrix.Matrix44 = 1;
                        colorMatrix.Matrix03 = (channels[0] == 1 ? val : 0);
                        colorMatrix.Matrix13 = (channels[1] == 1 ? val : 0);
                        colorMatrix.Matrix23 = (channels[2] == 1 ? val : 0);
                    }
                    break;
                case 2:
                    colorMatrix.Matrix00 = 1 * (channels[0] == 1 ? val : 0);
                    colorMatrix.Matrix10 = 0;
                    colorMatrix.Matrix20 = 0;
                    colorMatrix.Matrix01 = 0;
                    colorMatrix.Matrix11 = 1 * (channels[1] == 1 ? val : 0);
                    colorMatrix.Matrix21 = 0;
                    colorMatrix.Matrix02 = 0;
                    colorMatrix.Matrix12 = 0;
                    colorMatrix.Matrix22 = 1 * (channels[2] == 1 ? val : 0);
                    colorMatrix.Matrix30 = 0;
                    colorMatrix.Matrix31 = 0;
                    colorMatrix.Matrix32 = 0;
                    colorMatrix.Matrix33 = 1;
                    colorMatrix.Matrix44 = 1;
                    break;
                default:
                    break;
            }
            // Create an ImageAttributes object and set the color matrix
            ImageAttributes imageAttributes = new ImageAttributes();
            imageAttributes.SetColorMatrix(colorMatrix);
            Bitmap transformedImage = new Bitmap(original.Width, original.Height);
            using (Graphics g = Graphics.FromImage(transformedImage)) {
                // Draw the original image onto the new bitmap using the color matrix
                g.DrawImage(original, new Rectangle(0, 0, transformedImage.Width, transformedImage.Height),
                    0, 0, original.Width, original.Height, GraphicsUnit.Pixel, imageAttributes);
            }
            original?.Dispose();
            imageAttributes?.Dispose();
            return transformedImage;
        }

        [HandleProcessCorruptedStateExceptions]
        internal static unsafe List<Bitmap> ApplyLagAffect(dynamic gifFrames, int gifLength = 3) {
            List<Bitmap> modifiedFrames = new List<Bitmap>();
            Random rand = new Random();
            if (gifFrames is Bitmap) {
                var temp = ConvertBitmapToBitmapList(gifFrames, gifLength);
                gifFrames?.Dispose();
                gifFrames = temp;
            }
            for (int i = 0; i < gifFrames.Count; i++) {
                Bitmap currentFrame = gifFrames[i];
                Bitmap modifiedFrame = new Bitmap(currentFrame.Width, currentFrame.Height);
                Bitmap randomFrame = null;
                BitmapData currentData = null;
                BitmapData randomData = null;
                BitmapData modifiedData = null;
                try {
                    // Lock the bits of the current frame
                    currentData = currentFrame.LockBits(new Rectangle(0, 0, currentFrame.Width, currentFrame.Height),
                        ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    // Select a random frame index for blending
                    int randomFrameIndex = rand.Next(1, gifFrames.Count);
                    randomFrame = gifFrames[randomFrameIndex];
                    // Lock the bits of the random frame
                    randomData = randomFrame.LockBits(new Rectangle(0, 0, randomFrame.Width, randomFrame.Height),
                        ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    // Lock the bits of the modified frame
                    modifiedData = modifiedFrame.LockBits(new Rectangle(0, 0, modifiedFrame.Width, modifiedFrame.Height),
                        ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    byte* currentPointer = (byte*)currentData.Scan0;
                    byte* randomPointer = (byte*)randomData.Scan0;
                    byte* modifiedPointer = (byte*)modifiedData.Scan0;
                    for (int y = 0; y < currentFrame.Height; y++) {
                        for (int x = 0; x < currentFrame.Width; x++) {
                            int pixelIndex = y * currentData.Stride + x * 4;
                            // Apply blending
                            modifiedPointer[pixelIndex + 0] = (byte)((currentPointer[pixelIndex + 0] + randomPointer[pixelIndex + 0]) / 2); // B
                            modifiedPointer[pixelIndex + 1] = (byte)((currentPointer[pixelIndex + 1] + randomPointer[pixelIndex + 1]) / 2); // G
                            modifiedPointer[pixelIndex + 2] = (byte)((currentPointer[pixelIndex + 2] + randomPointer[pixelIndex + 2]) / 2); // R
                            modifiedPointer[pixelIndex + 3] = (byte)((currentPointer[pixelIndex + 3] + randomPointer[pixelIndex + 3]) / 2); // A
                        }
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error processing frame {i}: {ex.Message}");
                    // If error processing frame, use the original frame instead of the modified frame
                    modifiedFrame = (Bitmap)currentFrame.Clone();
                }
                finally {
                    // Unlock the bits in the finally block to ensure they are released
                    if (currentData != null)
                        currentFrame.UnlockBits(currentData);
                    if (randomData != null && randomFrame != null)
                        randomFrame.UnlockBits(randomData);
                    if (modifiedData != null)
                        modifiedFrame.UnlockBits(modifiedData);
                }
                modifiedFrames.Add(modifiedFrame);
            }
            return modifiedFrames;
        }

        [HandleProcessCorruptedStateExceptions]
        internal static unsafe List<Bitmap> ApplyBrokenAffect(dynamic gifFrames, int gifLength = 3) {
            List<Bitmap> modifiedFrames = new List<Bitmap>();
            Random rand = new Random();
            HashSet<(int, int)> staticBlocks = new HashSet<(int, int)>(); // To keep track of static blocks
            if (gifFrames is Bitmap) {
                var temp = ConvertBitmapToBitmapList(gifFrames, gifLength);
                gifFrames?.Dispose();
                gifFrames = temp;
            }
            for (int i = 0; i < gifFrames.Count; i++) {
                Bitmap currentFrame = gifFrames[i];
                Bitmap modifiedFrame = new Bitmap(currentFrame.Width, currentFrame.Height);
                BitmapData currentData = null;
                BitmapData modifiedData = null;
                try {
                    currentData = currentFrame.LockBits(new Rectangle(0, 0, currentFrame.Width, currentFrame.Height),
                        ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                    modifiedData = modifiedFrame.LockBits(new Rectangle(0, 0, modifiedFrame.Width, modifiedFrame.Height),
                        ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);
                    byte* currentPointer = (byte*)currentData.Scan0;
                    byte* modifiedPointer = (byte*)modifiedData.Scan0;
                    int blockSize = rand.Next(2, 30); // Size of the block to sample
                    for (int y = 0; y < currentFrame.Height; y += blockSize) {
                        for (int x = 0; x < currentFrame.Width; x += blockSize) {
                            // Determine the effective block size
                            int effectiveBlockWidth = Math.Min(blockSize, currentFrame.Width - x);
                            int effectiveBlockHeight = Math.Min(blockSize, currentFrame.Height - y);
                            var blockPosition = (x, y);
                            // Randomly decide to blend, keep static, or skip
                            double decision = rand.NextDouble();
                            if (decision <= 0.4) { // 40% chance to blend with the next frame
                                int nextFrameIndex = (i + rand.Next(1, 10)) % gifFrames.Count; // Choose a frame within the range
                                Bitmap nextFrame = gifFrames[nextFrameIndex];
                                BitmapData nextData = nextFrame.LockBits(new Rectangle(0, 0, nextFrame.Width, nextFrame.Height),
                                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                                byte* nextPointer = (byte*)nextData.Scan0;
                                // Blend the block
                                for (int by = 0; by < effectiveBlockHeight; by++) {
                                    for (int bx = 0; bx < effectiveBlockWidth; bx++) {
                                        int pixelIndex = ((y + by) * currentData.Stride) + ((x + bx) * 4);
                                        int nextPixelIndex = ((y + by) * nextData.Stride) + ((x + bx) * 4);
                                        modifiedPointer[pixelIndex + 0] = (byte)nextPointer[nextPixelIndex + 0]; // B
                                        modifiedPointer[pixelIndex + 1] = (byte)nextPointer[nextPixelIndex + 1]; // G
                                        modifiedPointer[pixelIndex + 2] = (byte)nextPointer[nextPixelIndex + 2]; // R
                                        modifiedPointer[pixelIndex + 3] = (byte)nextPointer[nextPixelIndex + 3]; // A
                                    }
                                }
                                nextFrame.UnlockBits(nextData);
                            }
                            else if (decision <= 0.1) { // Specifying a chance to keep this block static
                                // Check if this block has been made static in a previous frame
                                if (!staticBlocks.Contains(blockPosition)) {
                                    // Mark the block as static
                                    staticBlocks.Add(blockPosition);
                                    // Copy the original block into the modified frame
                                    for (int by = 0; by < effectiveBlockHeight; by++) {
                                        for (int bx = 0; bx < effectiveBlockWidth; bx++) {
                                            int pixelIndex = ((y + by) * currentData.Stride) + ((x + bx) * 4);
                                            modifiedPointer[pixelIndex + 0] = currentPointer[pixelIndex + 0]; // B
                                            modifiedPointer[pixelIndex + 1] = currentPointer[pixelIndex + 1]; // G
                                            modifiedPointer[pixelIndex + 2] = currentPointer[pixelIndex + 2]; // R
                                            modifiedPointer[pixelIndex + 3] = currentPointer[pixelIndex + 3]; // A
                                        }
                                    }
                                }
                                else {
                                    // Keep the static block unchanged in the modified frame
                                    for (int by = 0; by < effectiveBlockHeight; by++) {
                                        for (int bx = 0; bx < effectiveBlockWidth; bx++) {
                                            int pixelIndex = ((y + by) * currentData.Stride) + ((x + bx) * 4);
                                            // Copy from modifiedPointer to ensure the static block remains unchanged
                                            modifiedPointer[pixelIndex + 0] = (byte)(currentPointer[pixelIndex + 0] + modifiedPointer[pixelIndex + 0]); // B
                                            modifiedPointer[pixelIndex + 1] = (byte)(currentPointer[pixelIndex + 1] + modifiedPointer[pixelIndex + 1]); // G
                                            modifiedPointer[pixelIndex + 2] = (byte)(currentPointer[pixelIndex + 2] + modifiedPointer[pixelIndex + 2]); // R
                                            modifiedPointer[pixelIndex + 3] = (byte)(currentPointer[pixelIndex + 3] + modifiedPointer[pixelIndex + 3]); // A
                                        }
                                    }
                                }
                            }
                            else {
                                // Copy the original block if no blending occurs
                                for (int by = 0; by < effectiveBlockHeight; by++) {
                                    for (int bx = 0; bx < effectiveBlockWidth; bx++) {
                                        int pixelIndex = ((y + by) * currentData.Stride) + ((x + bx) * 4);
                                        modifiedPointer[pixelIndex + 0] = currentPointer[pixelIndex + 0]; // B
                                        modifiedPointer[pixelIndex + 1] = currentPointer[pixelIndex + 1]; // G
                                        modifiedPointer[pixelIndex + 2] = currentPointer[pixelIndex + 2]; // R
                                        modifiedPointer[pixelIndex + 3] = currentPointer[pixelIndex + 3]; // A
                                    }
                                }
                            }
                        }
                    }
                }
                catch (Exception ex) {
                    Console.WriteLine($"Error processing frame {i}: {ex.Message}");
                }
                finally {
                    if (currentData != null)
                        currentFrame.UnlockBits(currentData);
                    if (modifiedData != null)
                        modifiedFrame.UnlockBits(modifiedData);
                }
                modifiedFrames.Add(modifiedFrame);
            }
            return modifiedFrames;
        }

        [HandleProcessCorruptedStateExceptions]
        internal static List<Bitmap> ApplyGlitchEffect(dynamic gifFrames, int offset, int jitter, int gifLength = 3) { // offset, jitter > 0
            List<Bitmap> modifiedFrames = new List<Bitmap>();
            if (gifFrames is Bitmap) {
                var temp = ConvertBitmapToBitmapList(gifFrames, gifLength);
                gifFrames?.Dispose();
                gifFrames = temp;
            }
            int ctr = 0;
            double time = 0.0;
            offset = Math.Abs(offset);
            jitter = Math.Abs(jitter);
            Random rand = new Random();
            foreach (Bitmap img in gifFrames) {
                Bitmap modifiedFrame = new Bitmap(img.Width, img.Height);
                BitmapData currentData = img.LockBits(new Rectangle(0, 0, img.Width, img.Height),
                    ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
                BitmapData modifiedData = modifiedFrame.LockBits(new Rectangle(0, 0, modifiedFrame.Width, modifiedFrame.Height),
                    ImageLockMode.WriteOnly, PixelFormat.Format32bppArgb);

                unsafe {
                    byte* imgPointer = (byte*)currentData.Scan0;
                    byte* modifiedPointer = (byte*)modifiedData.Scan0;
                    // Initialize variables
                    int si = (int)Math.Floor((double)rand.Next(offset)) * 4;
                    int ir = si;
                    int ig = si;
                    int ib = si;
                    // Calculate noise properties
                    // ((x % 101) / 110001) provides around 100 values in the range 0.0 - 0.0009
                    double r_prop = ((jitter % 101f) / 110001f) * CommonUtility.PerlinNoise(time);
                    double g_prop = ((jitter % 101f) / 110001f) * CommonUtility.PerlinNoise(time + 100);
                    double b_prop = ((jitter % 101f) / 110001f) * CommonUtility.PerlinNoise(time + 150);
                    int r_max_jump = (int)Math.Floor(15 * CommonUtility.PerlinNoise(time + 200));
                    int g_max_jump = (int)Math.Floor(15 * CommonUtility.PerlinNoise(time + 250));
                    int b_max_jump = (int)Math.Floor(15 * CommonUtility.PerlinNoise(time + 300));
                    time += (1 / 30.0);

                    for (int i = 0; i < 4 * img.Width * img.Height; i += 4) {
                        try {
                            // Adjust the indices based on the properties
                            if (rand.NextDouble() < r_prop) ir -= 4 * (int)Math.Floor((double)rand.Next(r_max_jump) - r_max_jump / 2);
                            if (rand.NextDouble() < g_prop) ig -= 4 * (int)Math.Floor((double)rand.Next(g_max_jump) - g_max_jump / 2);
                            if (rand.NextDouble() < b_prop) ib -= 4 * (int)Math.Floor((double)rand.Next(b_max_jump) - b_max_jump / 2);

                            modifiedPointer[i] = imgPointer[ir];          // R
                            modifiedPointer[i + 1] = imgPointer[ig + 1];  // G
                            modifiedPointer[i + 2] = imgPointer[ib + 2];  // B
                            modifiedPointer[i + 3] = 255;                  // Alpha

                            ir += 4;
                            ir %= 4 * img.Width * img.Height;
                            ig += 4;
                            ig %= 4 * img.Width * img.Height;
                            ib += 4;
                            ib %= 4 * img.Width * img.Height;
                        }
                        catch (AccessViolationException) {
                            Console.WriteLine("Error accessing pixel value");
                            continue;
                        }
                    }
                    modifiedFrames.Add(modifiedFrame);
                }
                Console.WriteLine($"{(ctr++ * 100 / (float)gifFrames.Count):F2}%");
            }
            return modifiedFrames;
        }
    }
}
