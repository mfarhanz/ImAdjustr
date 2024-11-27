using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System;
using System.Text;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Diagnostics;
using ImAdjustr.Internal.Controllers;
using ImAdjustr.Internal.Static;

namespace ImAdjustr.Internal.Services {
    internal class FlaskControl : TransmissionHandler {
        private BackendController control;
        internal FlaskControl(BackendController controller) { control = controller; }

        internal override async Task<(bool, bool)> ProcessImage(Bitmap bitmap, params object[] parameters) {
            if (control.batchProcessing) {
                int attempt = 0;
                while (attempt < 3) {
                    var result = await TryProcess(() => ProcessThroughMemoryMap(bitmap, parameters));
                    if (result.success) return result;
                    await Task.Delay(100);
                    attempt++;
                }
                return (false, false);
            }
            else {
                var result = await TryProcess(() => ProcessThroughMemoryMap(bitmap, parameters));
                if (result.success) return result;
                result = await TryProcess(() => ProcessDirect(bitmap, parameters));
                if (result.success) return result;
                return await TryProcess(() => ProcessThroughDisk(bitmap, parameters));
            }
        }

        internal async Task<bool> Shutdown(HttpClient client) {
            try {
                HttpResponseMessage response = await client.PostAsync($"http://localhost:{control.serverPort}{Endpoints.FlaskShutdown}", null);
                if (response.IsSuccessStatusCode) return true;
                else {
                    try {
                        int serverPid = int.Parse(await response.Content.ReadAsStringAsync());
                        using (Process flaskProcess = Process.GetProcessById(serverPid)) flaskProcess.Kill();
                        return true;
                    }
                    catch (Exception) {
                        return false;
                    }
                }                
            }
            catch (Exception) {
                return false;
            }
        }

        private async Task<HttpResponseMessage> SendData(HttpClient client, string endpoint, dynamic data) {
            HttpResponseMessage response = null;
            StringContent content = null;
            try {
                var json = JsonSerializer.Serialize(data);
                content = new StringContent(json, Encoding.UTF8, "application/json");
                response = await client.PostAsync(endpoint, content);
                response.EnsureSuccessStatusCode();
                return response;
            }
            catch (Exception) {
                return response;
            }
            finally {
                content?.Dispose();
            }
        }

        private async Task<bool> ProcessDirect(Bitmap bitmap, params object[] parameters) {
            try {
                int bytesPerPixel = bitmap.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4;
                (int bmpWidth, int bmpHeight) = (bitmap.Width, bitmap.Height);
                byte[] imageData = control.MarshalBitmapBufferCopy(1, bmpWidth, bmpHeight, bytesPerPixel, bitmap: bitmap);
                var data = new { image = imageData, width = bmpWidth, height = bmpHeight, bytes_per_pixel = bytesPerPixel, parameters = parameters };
                using (HttpResponseMessage response = await SendData(control.flaskClient, $"http://localhost:{control.serverPort}{Endpoints.FlaskProcessDirect}", data))
                using (var content = response.Content) {
                    if (response is null) return false;
                    if (response.IsSuccessStatusCode) {
                        byte[] processedImageData = await content.ReadAsByteArrayAsync();
                        Bitmap processedBitmap = control.MarshalBitmapBufferCopy(0, bmpWidth, bmpHeight, bytesPerPixel, buffer: processedImageData);
                        control.UpdateBitmap(ref bitmap, ref processedBitmap);
                        control.DisposeResources(processedBitmap);
                    }
                    else {
                        var pythonResponse = JsonSerializer.Deserialize<PyResponse>(await content.ReadAsStringAsync());
                        control.logger.Error(pythonResponse.Output);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex) {
                control.logger.Error("Error processing image via flask (direct)", ex);
                return false;
            }
        }

        private async Task<bool> ProcessThroughDisk(Bitmap bitmap, params object[] parameters) {
            try {
                string inputFile = Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                string tempPath = Path.Combine(control.imageCacheFolder, inputFile.Substring(0, inputFile.Length - 2) + ".bmp");
                bitmap.Save(tempPath, ImageFormat.Bmp);
                var data = new { input_path = tempPath, parameters = parameters };
                using (HttpResponseMessage response = await SendData(control.flaskClient, $"http://localhost:{control.serverPort}{Endpoints.FlaskProcessThroughPath}", data))
                using (var content = response.Content)
                using (response) {
                    PyResponse pythonResponse = JsonSerializer.Deserialize<PyResponse>(await content.ReadAsStringAsync());
                    if (response is null) return false;
                    if (response.IsSuccessStatusCode) {
                        string outputPath = pythonResponse.Output;
                        Bitmap processedBitmap = new Bitmap(outputPath);
                        control.UpdateBitmap(ref bitmap, ref processedBitmap);
                        await control.DiskUsageLimiter(control.diskReadCounter, control.diskWriteCounter, processedBitmap.Width * processedBitmap.Height * 3);
                        control.DisposeResources(processedBitmap);
                        File.Delete(outputPath);
                        File.Delete(tempPath);
                    }
                    else {
                        control.logger.Error(pythonResponse.Output);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex) {
                control.logger.Error("Error processing image via flask (disk)", ex);
                return false;
            }
        }

        private async Task<bool> ProcessThroughMemoryMap(Bitmap bitmap, params object[] parameters) {
            try {
                int bytesPerPixel = bitmap.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4;
                (int bmpWidth, int bmpHeight, int bmpSize) = (bitmap.Width, bitmap.Height, bitmap.Width * bitmap.Height * bytesPerPixel);
                byte[] imageData = control.MarshalBitmapBufferCopy(1, bmpWidth, bmpHeight, bytesPerPixel, bitmap: bitmap);
                // Write data to memory-mapped file
                (MemoryMappedFile mmf, string mmfName) = (null, null);
                if (control.batchProcessing) {
                    mmfName = MiscConstants.MapName + Convert.ToBase64String(Guid.NewGuid().ToByteArray());
                    mmfName = mmfName.Substring(0, mmfName.Length - 2);
                    mmf = MemoryMappedFile.CreateNew(mmfName, bmpSize);
                }       // creating new map takes at max <0.2ms, negligible
                else mmf = MemoryMappedFile.CreateNew(MiscConstants.MapName, bmpSize);
                using (mmf)
                using (var accessor = mmf.CreateViewAccessor(0, bmpSize)) {
                    control.FastBufferBlockCopy(imageData, accessor, 1);
                    var data = new { map_name = control.batchProcessing ? mmfName : MiscConstants.MapName, width = bmpWidth, height = bmpHeight, bytes_per_pixel = bytesPerPixel, parameters = parameters };
                    HttpResponseMessage response = await SendData(control.flaskClient, $"http://localhost:{control.serverPort}{Endpoints.FlaskProcessThroughMemoryMap}", data);
                    if (response is null) return false;
                    if (response.IsSuccessStatusCode) {
                        using (response) {
                            byte[] processedImageData = new byte[bmpSize];
                            control.FastBufferBlockCopy(processedImageData, accessor, 0);
                            Bitmap processedBitmap = control.MarshalBitmapBufferCopy(0, bmpWidth, bmpHeight, bytesPerPixel, buffer: processedImageData);
                            control.UpdateBitmap(ref bitmap, ref processedBitmap);
                            control.DisposeResources(processedBitmap);
                        }
                    }
                    else {
                        var pythonResponse = JsonSerializer.Deserialize<PyResponse>(await response.Content.ReadAsStringAsync());
                        control.logger.Error(pythonResponse.Output);
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex) {
                control.logger.Error("Error processing image via flask (mmap)", ex);
                return false;
            }
        }
    }
}
