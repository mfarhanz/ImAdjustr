using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.IO.MemoryMappedFiles;
using System.Threading.Tasks;
using System.Net.NetworkInformation;
using System.Collections.Generic;
using System.Threading;
using System.Linq;
using ImAdjustr.Internal.Services;
using ImAdjustr.Internal.Static;

namespace ImAdjustr.Internal.Controllers {
    internal class BackendController {

        internal int serverPort;
        internal float batchProcessProgress;
        internal bool batchProcessing;

        private PipeControl pipe;
        private FlaskControl flask;
        internal Process pipeProcess;
        private Process flaskProcess;

        internal string pythonHome;
        internal bool validated;
        private readonly string root;
        internal readonly string imageCacheFolder;
        private readonly Stopwatch debugTimer;
        internal readonly HttpClient flaskClient;
        internal readonly DebugLogger logger;
        //private readonly Process applicationProcess;
        internal readonly PerformanceCounter diskReadCounter;
        internal readonly PerformanceCounter diskWriteCounter;

        internal BackendController(DebugLogger debugLogger, string pythonPath = null) {
            batchProcessing = false;
            logger = debugLogger;
            serverPort = GetTcpPort();
            debugTimer = new Stopwatch();
            flaskClient = new HttpClient();
            flaskClient.Timeout = TimeSpan.FromSeconds(10);
            root = AppDomain.CurrentDomain.BaseDirectory;
            pythonHome = ValidatedPythonExecutablePath(pythonPath);
            imageCacheFolder = Path.Combine(root, Paths.Temp);
            if (!Directory.Exists(imageCacheFolder)) Directory.CreateDirectory(imageCacheFolder);
            diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
            diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");
            if (pythonHome != null) {
                SetEnvironmentVariables();
                validated = ValidatedPackageRequirements();
            }
            else validated = false;
        }

        private string ValidatedPythonExecutablePath(string path) {
            string _path;
            bool validated = false;
            path = path.Trim();
            if (string.IsNullOrEmpty(path)) _path = Path.Combine(root, Paths.Python);
            else _path = path;
            if (Directory.Exists(_path)) {
                string[] files = Directory.GetFiles(_path);
                string[] dirs = Directory.GetDirectories(_path);
                string[] scriptDir;
                bool pythonFound = Array.Exists(files, file => Path.GetFileName(file) == "python.exe");
                bool pipFound = Array.Exists(files, file => Path.GetFileName(file) == "pip.exe");
                bool scriptsDirFound = Array.Exists(dirs, dir => Path.GetFileName(dir) == "Scripts");
                if (scriptsDirFound) {
                    if (!pipFound || !pythonFound) {
                        scriptDir = Directory.GetFiles(Path.Combine(_path, "Scripts"));
                        if (!pythonFound) pythonFound = Array.Exists(scriptDir, file => Path.GetFileName(file) == "python.exe");    // if virtual environment
                        if (!pipFound) pipFound = Array.Exists(scriptDir, file => Path.GetFileName(file) == "pip.exe");
                    }
                }
                if (pythonFound && pipFound) {
                    pythonHome = _path;     // to test if Python runs fine with the path
                    Process checkVersion = RunPythonScript("--version");
                    checkVersion.WaitForExit();
                    string version = checkVersion.StandardOutput.ReadToEnd();
                    if (!string.IsNullOrWhiteSpace(version) && version.Contains("Python")) validated = true;
                }
                else { }        // if python path is not valid
            }
            if (validated) return _path;
            else return null;
        }

        private bool ValidatedPackageRequirements() {
            Process getPackages = RunPythonScript("-m pip list");
            getPackages.WaitForExit();
            string packages = getPackages.StandardOutput.ReadToEnd();
            List<string> required = new List<string> { "pillow", "numpy", "numba", "Flask", "waitress" };
            return required.All(package => packages.Contains(package));
        }

        private void SetEnvironmentVariables() {
            Environment.SetEnvironmentVariable("PYTHONHOME", pythonHome);
            if (Path.GetFileName(pythonHome).ToLower() == "scripts") {
                Environment.SetEnvironmentVariable("PYTHONPATH", Path.Combine(Directory.GetParent(pythonHome)?.FullName, "Lib"));
            }
            else Environment.SetEnvironmentVariable("PYTHONPATH", Path.Combine(pythonHome, "Lib"));
        }

        internal async Task SetupListeners() {
            pipe = new PipeControl(this);
            flask = new FlaskControl(this);
            EnsureListenersActive();
            if (pipeProcess == null || flaskProcess == null || pipeProcess.HasExited || flaskProcess.HasExited) {
                serverPort = GetTcpPort();
                await SetupListeners();         // TODO: might go infinite loop, test later
                return;
            }
            await Task.Delay(1000);
            try {
                var response = await flaskClient.PostAsync($"http://localhost:{serverPort}{Endpoints.FlaskWarmup}", null);
                response.EnsureSuccessStatusCode();
                if (response.IsSuccessStatusCode) logger.Info("Server connection established");
            }
            catch (Exception ex) {
                logger.Error("Server connection was not established - the first call to Flask will be delayed", ex);
            }
        }

        private void EnsureListenersActive() {
            void EnsureProcessActive(ref Process process, string path, bool isServer = false) {
                if (process == null || process.HasExited) {
                    process?.Dispose();
                    process = RunPythonScript(path, isServer);
                }
            }

            EnsureProcessActive(ref pipeProcess, Paths.PipeControl);
            EnsureProcessActive(ref flaskProcess, Paths.FlaskControl, isServer: true);
        }

        internal Process RunPythonScript(string scriptFile, bool isServer = false) {
            try {
                ProcessStartInfo startInfo = new ProcessStartInfo {
                    FileName = Path.Combine(pythonHome, "python.exe"),
                    Arguments = isServer ? $"{scriptFile} {serverPort}" : scriptFile,
                    RedirectStandardOutput = true,
                    RedirectStandardInput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                };
                dynamic currProcess = new Process { StartInfo = startInfo };
                if (currProcess == null) return null;
                currProcess.Start();
                return currProcess;
            }
            catch (Exception) {
                return null;
            }
        }

        private int GetTcpPort() {
            int port = 5000; // default base port
            bool isAvailable = false;
            IPGlobalProperties ipGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            TcpConnectionInformation[] tcpConnInfoArray = ipGlobalProperties.GetActiveTcpConnections();
            while (!isAvailable) {
                for (int i = 0; i < tcpConnInfoArray.Length; i++) {
                    if (tcpConnInfoArray[i].LocalEndPoint.Port == port) {
                        port++;
                        break;
                    }
                    if (i == tcpConnInfoArray.Length - 1) isAvailable = true;
                }
            }
            return port;
        }

        internal async Task<bool> ProcessSingle(Bitmap bitmap, params object[] parameters) {
            EnsureListenersActive();
            try {
                if (batchProcessing) {
                    var flaskResult = await flask.ProcessImage(bitmap, parameters);
                    if (flaskResult.Item2) return flaskResult.Item1;
                }
                else {
                    var pipeResult = await pipe.ProcessImage(bitmap, parameters);
                    if (pipeResult.Item2) return pipeResult.Item1;
                    return (await flask.ProcessImage(bitmap, parameters)).Item1;
                }
            }
            catch (Exception ex) {
                logger.Error("Error while processing image: ", ex);
            }
            return false;
        }

        internal async Task<float> ProcessBatch(string inDirectory, string outDirectory, params object[] parameters) {
            batchProcessing = true;
            batchProcessProgress = 0;
            string[] imageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".ico" };
            string[] files = Directory.GetFiles(inDirectory);
            List<Task<bool>> tasks = new List<Task<bool>>();
            (float fileIndex, int attempt, int filesCount) = (0, 1, files.Length);
            SemaphoreSlim logSemaphore = new SemaphoreSlim(1, 1);
            await ProcessTillSuccess(files, 3);
            return batchProcessProgress;

            async Task LogInfoAsync(string message) {
                await logSemaphore.WaitAsync();
                try {
                    logger.Info(message);
                }
                finally {
                    logSemaphore.Release();
                }
            }

            async Task ProcessTillSuccess(IEnumerable<string> filesList, int concurrency) {
                List<string> retryList = new List<string>();
                var semaphore = new SemaphoreSlim(concurrency); // Limit number of concurrent tasks
                foreach (var file in filesList) {
                    if (imageExtensions.Contains(Path.GetExtension(file).ToLower())) {
                        await semaphore.WaitAsync();
                        tasks.Add(Task.Run(async () => {
                            string fileName = Path.GetFileName(file);
                            //logger.Info($"Added {fileName} to batch queue");
                            await LogInfoAsync($"Added {fileName} to batch queue");
                            try {
                                using (Bitmap bitmap = new Bitmap(file)) {
                                    bool success = await ProcessSingle(bitmap, parameters);
                                    if (success) {
                                        string saveFile = Path.Combine(outDirectory, $"{Path.GetFileNameWithoutExtension(fileName)}_2{Path.GetExtension(fileName)}");
                                        bitmap.Save(saveFile);
                                        //logger.Info($"{fileName} saved at {saveFile}");
                                        await LogInfoAsync($"{fileName} saved at {saveFile}");
                                        batchProcessProgress = (float)Math.Round(++fileIndex * 100 / filesCount, 2);
                                    }
                                    else {
                                        retryList.Add(file);
                                        return false;
                                    }
                                }
                            }
                            catch (Exception) {
                                retryList.Add(file);
                                return false;
                            }
                            finally {
                                semaphore.Release(); // Release the slot when done
                            }
                            return true;
                        }));
                    }
                }
                try {
                    bool[] results = await Task.WhenAll(tasks);
                    if (Array.TrueForAll(results, result => result)) return; // Returns true if all tasks were successful
                    else {      // Allow a max of three attempts to process files that failed earlier
                        await LogInfoAsync($"Retrying processing:  {retryList.Count} files");
                        if (++attempt < 4) await ProcessTillSuccess(retryList, 1);
                    }
                }
                catch (Exception ex) {
                    logger.Error($"Error: {ex.Message}");
                }
                finally {
                    semaphore?.Dispose();
                    batchProcessing = false;
                }
            }
        }

        internal void UpdateBitmap(ref Bitmap original, ref Bitmap updated) {
            Rectangle rect = new Rectangle(0, 0, original.Width, original.Height);
            BitmapData updatedData = updated.LockBits(rect, ImageLockMode.ReadOnly, updated.PixelFormat);
            BitmapData originalData = original.LockBits(rect, ImageLockMode.WriteOnly, original.PixelFormat);
            unsafe {
                byte* updatedPtr = (byte*)updatedData.Scan0;
                byte* originalPtr = (byte*)originalData.Scan0;
                int bytesPerPixel = original.PixelFormat == PixelFormat.Format24bppRgb ? 3 : 4;
                long bytesToCopy = original.Width * original.Height * bytesPerPixel;
                Buffer.MemoryCopy(updatedPtr, originalPtr, bytesToCopy, bytesToCopy);
            }
            updated.UnlockBits(updatedData);
            original.UnlockBits(originalData);
        }

        internal void FastBufferBlockCopy(byte[] byteArray, MemoryMappedViewAccessor accessor, int operation) {
            unsafe {
                int arraySize = byteArray.Length;
                byte* accessorPtr = null;
                try {
                    accessor.SafeMemoryMappedViewHandle.AcquirePointer(ref accessorPtr);
                    // Use fixed to get the pointer to the processed image data
                    fixed (byte* dataPtr = byteArray) {         // read from MMAP into buffer
                        if (operation == 0) Buffer.MemoryCopy(accessorPtr, dataPtr, arraySize, arraySize);
                        else Buffer.MemoryCopy(dataPtr, accessorPtr, arraySize, arraySize); // write from buffer into MMAP
                    }
                }
                finally {
                    accessor.SafeMemoryMappedViewHandle.ReleasePointer();
                }
            }
        }

        internal dynamic MarshalBitmapBufferCopy(int mode, int width, int height, int bytesPerPixel, byte[] buffer = null, Bitmap bitmap = null) {
            Rectangle rect = new Rectangle(0, 0, width, height);
            if (mode == 1) { // write bitmap data to buffer
                byte[] bitmapData = new byte[width * height * bytesPerPixel];
                if (bitmap is null) return bitmapData;
                BitmapData bmpData = bitmap.LockBits(rect, ImageLockMode.ReadOnly, bitmap.PixelFormat);
                Marshal.Copy(bmpData.Scan0, bitmapData, 0, width * height * bytesPerPixel);
                bitmap.UnlockBits(bmpData);
                return bitmapData;
            }
            else {      // create bitmap from buffer
                PixelFormat pixelFormat = bytesPerPixel == 3 ? PixelFormat.Format24bppRgb : PixelFormat.Format32bppArgb;
                Bitmap newBitmap = new Bitmap(width, height, pixelFormat);
                if (buffer is null) return newBitmap;
                BitmapData bmpData = newBitmap.LockBits(rect, ImageLockMode.WriteOnly, pixelFormat);
                Marshal.Copy(buffer, 0, bmpData.Scan0, buffer.Length);
                newBitmap.UnlockBits(bmpData);
                return newBitmap;
            }
        }

        internal async Task DiskUsageLimiter(PerformanceCounter diskReader, PerformanceCounter diskWriter, int currentFileSize) {
            float diskReadSpeed = diskReader.NextValue() / 1024;
            float diskWriteSpeed = diskWriter.NextValue() / 1024;
            int delay = 200 + (int)(1800.0 * (currentFileSize - 6000000) / 90000000);
            if ((diskReadSpeed + diskWriteSpeed) > 20) await Task.Delay(delay);    // limit calling function
            else return;  // continue execution of calling function
        }

        internal async Task<bool> CheckServerStatus() {
            try {
                var response = await flaskClient.GetAsync($"http://localhost:{serverPort}{Endpoints.FlaskTest}");
                response.EnsureSuccessStatusCode();
                return true;
            }
            catch (Exception) {
                return false;
            }
        }

        internal void DisposeResources(params object[] items) { 
            for(int i = 0; i < items.Length; i++) {
                if (items[i] is IDisposable item) {
                    item?.Dispose();
                    item = null;
                }
            }
        }

        private void ClearDirectory(string directory, DebugLogger logger) {
            if (Directory.Exists(directory)) {
                string[] cached = Directory.GetFiles(directory);
                if (cached.Length > 0) {
                    foreach (string file in Directory.GetFiles(directory))
                        try {
                            File.Delete(file);
                        }
                        catch (Exception ex) {
                            logger.Error($"Error deleting {file}, file may be in use", ex);
                        }
                    logger.Info("Image cache directory cleared");
                }
            }
        }

        private void ClearPyRuntimes() {
            logger.Warning("Force clearing running subprocesses");
            foreach (var process in Process.GetProcessesByName("Python")) process.Kill();
        }

        private async Task<bool> ShutdownListeners() {
            try {
                if (!(await pipe.Shutdown(pipeProcess))) logger.Error("Error closing pipe process");
                else logger.Info("Pipe host disconnected");
                if (!(await flask.Shutdown(flaskClient))) logger.Error("Error shutting down Flask server");
                else {
                    logger.Info("Flask server disconnected");
                    return true;
                }
                return false;
            }
            catch (Exception ex) {
                logger.Error("Error shutting down: ", ex);
                return false;
            }
        }

        internal async Task ShutdownController() {
            ClearDirectory(imageCacheFolder, logger);
            flaskClient.CancelPendingRequests();
            if (!(await ShutdownListeners())) ClearPyRuntimes();
            debugTimer.Restart();
            while (await CheckServerStatus()) {
                await Task.Delay(200);
                if (debugTimer.Elapsed.Seconds > 6) break;
            }
            DisposeResources(flaskClient, pipeProcess);
            GC.Collect();
            logger.Info("Application session ended");
        }
    }
}
