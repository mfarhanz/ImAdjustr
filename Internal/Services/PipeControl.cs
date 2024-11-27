using System;
using System.Linq;
using System.Drawing;
using System.Drawing.Imaging;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.IO.MemoryMappedFiles;
using ImAdjustr.Internal.Controllers;
using ImAdjustr.Internal.Static;

namespace ImAdjustr.Internal.Services {
    internal class PipeControl : TransmissionHandler {
        private BackendController control;
        internal PipeControl(BackendController controller) { control = controller; }

        internal override async Task<(bool, bool)> ProcessImage(Bitmap bitmap, params object[] parameters) {
            var result = await TryProcess(() => ProcessDirect(bitmap, parameters));
            if (result.success) return result;
            return await TryProcess(() => ProcessThroughMemoryMap(bitmap, parameters));
        }

        internal async Task<bool> Shutdown(Process pipeProcess) {
            if (pipeProcess is null || pipeProcess.HasExited) return true;
            try {
                await SendData(pipeProcess, "SHTD");
                if (pipeProcess.HasExited) return true;
                else
                    try {
                        pipeProcess.Kill();
                        return true;
                    }
                    catch (Exception) {
                        return false;
                    }
            }
            catch (Exception) {
                return false;
            }
        }

        private async Task<bool> SendData(Process pipeProcess, params dynamic[] data) {
            async Task WriteToPipe(byte[] bytes, int size) {
                await pipeProcess.StandardInput.BaseStream.WriteAsync(bytes, 0, size);
            }

            bool success;
            try {
                foreach (var param in data) {
                    if (param is int) {
                        await WriteToPipe(new byte[] { 0x01 }, 1);  // Type byte for int
                        await WriteToPipe(BitConverter.GetBytes(param), 4);
                    }
                    else if (param is float) {
                        await WriteToPipe(new byte[] { 0x02 }, 1);  // Type byte for float
                        await WriteToPipe(BitConverter.GetBytes((float)param), 4);
                    }
                    else if (param is double) {
                        await WriteToPipe(new byte[] { 0x03 }, 1);  // Type byte for double
                        await WriteToPipe(BitConverter.GetBytes((double)param), 8);
                    }
                    else if (param is bool) {
                        await WriteToPipe(new byte[] { 0x04 }, 1);  // Type byte for bool
                        await WriteToPipe(new byte[] { (byte)((bool)param ? 1 : 0) }, 1);
                    }
                    else if (param is char) {
                        await WriteToPipe(new byte[] { 0x05 }, 1);  // Type byte for char
                        await WriteToPipe(new byte[] { (byte)param }, 1);
                    }
                    else if (param is byte[] || param is string) {
                        byte[] parameterBytes = param is string ? Encoding.UTF8.GetBytes(param) : param;
                        await WriteToPipe(new byte[] { (byte)(param is string ? 0x06 : 0x07) }, 1);    // Type byte for string or byte[]
                        await WriteToPipe(BitConverter.GetBytes(parameterBytes.Length), 4);
                        await WriteToPipe(parameterBytes, parameterBytes.Length);
                    }
                }
            }
            catch (Exception) {
                return false;
            }
            finally {
                await pipeProcess.StandardInput.BaseStream.FlushAsync();
                int flag = await pipeProcess.StandardOutput.BaseStream.ReadAsync(new byte[1], 0, 1);
                success = flag == 1 ? true : false;
            }
            return success;
        }

        private async Task<bool> ProcessDirect(Bitmap bitmap, params object[] parameters) {
            try {
                int bytesPerPixel = bitmap.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4;
                (int bmpWidth, int bmpHeight, int bmpSize) = (bitmap.Width, bitmap.Height, bitmap.Width * bitmap.Height * bytesPerPixel);
                byte[] imageData = control.MarshalBitmapBufferCopy(1, bmpWidth, bmpHeight, bytesPerPixel, bitmap: bitmap);
                int parameterCount = 4 + parameters.Length;
                var data = new object[] { "DIRECT", parameterCount, bmpWidth, bmpHeight, bytesPerPixel, imageData }.Concat(parameters).ToArray();
                bool success = await SendData(control.pipeProcess, data);
                if (success) {
                    // TODO: instead of using old bmpsize, get new piped size first and then read image of this size
                    byte[] processedImageData = new byte[bmpSize];
                    await control.pipeProcess.StandardOutput.BaseStream.ReadAsync(processedImageData, 0, bmpSize);
                    Bitmap processedBitmap = control.MarshalBitmapBufferCopy(0, bmpWidth, bmpHeight, bytesPerPixel, buffer: processedImageData);
                    control.UpdateBitmap(ref bitmap, ref processedBitmap);
                    control.DisposeResources(processedBitmap);
                }
                else {
                    control.logger.Error("No data returned from pipe");
                    return false;
                }
                return true;
            }
            catch (Exception ex) {
                control.logger.Error("Error processing image via pipe stream (direct)", ex);
                return false;
            }
        }

        private async Task<bool> ProcessThroughMemoryMap(Bitmap bitmap, params object[] parameters) {
            try {
                int bytesPerPixel = bitmap.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4;
                (int bmpWidth, int bmpHeight, int bmpSize) = (bitmap.Width, bitmap.Height, bitmap.Width * bitmap.Height * bytesPerPixel);
                byte[] imageData = control.MarshalBitmapBufferCopy(1, bmpWidth, bmpHeight, bytesPerPixel, bitmap: bitmap);
                MemoryMappedFile mmf = MemoryMappedFile.CreateNew(MiscConstants.MapName, bmpSize); // creating new map takes at max <0.2ms, negligible
                using (mmf)
                using (var accessor = mmf.CreateViewAccessor(0, bmpSize)) {
                    long capacity = accessor.Capacity;
                    control.FastBufferBlockCopy(imageData, accessor, 1);
                    int parameterCount = 4 + parameters.Length;
                    var data = new object[] { "MMAP", parameterCount, MiscConstants.MapName, bmpWidth, bmpHeight, bytesPerPixel }.Concat(parameters).ToArray();
                    bool success = await SendData(control.pipeProcess, data);
                    if (success) {
                        byte[] processedImageData = new byte[bmpSize];
                        control.FastBufferBlockCopy(processedImageData, accessor, 0);
                        Bitmap processedBitmap = control.MarshalBitmapBufferCopy(0, bmpWidth, bmpHeight, bytesPerPixel, buffer: processedImageData);
                        control.UpdateBitmap(ref bitmap, ref processedBitmap);
                        control.DisposeResources(processedBitmap);
                    }
                    else {
                        control.logger.Error("No data returned from pipe");
                        return false;
                    }
                }
                return true;
            }
            catch (Exception ex) {
                control.logger.Error("Error processing image via pipe stream (mmap)", ex);
                return false;
            }
        }
    }
}
