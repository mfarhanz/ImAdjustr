using System;
using System.IO;
using System.Drawing;
using System.Windows.Forms;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ImAdjustr.Utility {
    internal class CommonUtility {
        internal CommonUtility() { }

        public class ScriptResponse {
            [JsonPropertyName("status")]
            public string Status { get; set; }
            [JsonPropertyName("data")]
            public int[] Data { get; set; }
            [JsonPropertyName("message")]
            public string Message { get; set; }
        }

        public class ServerShutdownResponse {
            [JsonPropertyName("pid")]
            public int Pid { get; set; }
            [JsonPropertyName("status")]
            public int Status { get; set; }
            [JsonPropertyName("message")]
            public string Message { get; set; }
        }


        public static string FindPythonExecutable() {
            string[] paths = Environment.GetEnvironmentVariable("PATH").Split(';');
            foreach (string path in paths) {
                string pythonExePath = Path.Combine(path, "python.exe");
                if (File.Exists(pythonExePath)) return pythonExePath;
            }
            return null;
        }

        public static double PerlinNoise(double x) {
            // Adapted from :
            // https://github.com/accord-net/framework/blob/master/Sources/Accord.Math/AForge.Math/PerlinNoise.cs
            double SmoothedNoise(double y) {
                int yInt = (int)y;
                double yFrac = y - yInt;
                return CosineInterpolate(Noise(yInt), Noise(yInt + 1), yFrac);
            }

            double CosineInterpolate(double x1, double x2, double a) {
                double f = (1 - Math.Cos(a * Math.PI)) * 0.5;
                return x1 * (1 - f) + x2 * f;
            }

            double Noise(int y) {
                int n = (y << 13) ^ y;
                return (1.0 - ((n * (n * n * 15731 + 789221) + 1376312589) & 0x7fffffff) / 1073741824.0);
            }

            double initFrequency = 1.0;
            double initAmplitude = 1.0;
            double persistence = 0.65;
            int octaves = 4;
            double frequency = initFrequency;
            double amplitude = initAmplitude;
            double sum = 0;
            for (int i = 0; i < octaves; i++) {
                sum += SmoothedNoise(x * frequency) * amplitude;
                frequency *= 2;
                amplitude *= persistence;
            }
            return Math.Abs(sum);
        }

        // Pretty print dictionaries utility
        public static void DictPrinter<TKey, TValue>(Dictionary<TKey, TValue> dict, int indent = 0) {
            foreach (var item in dict) {
                if (item.Value is Dictionary<string, dynamic> nestedDict) {
                    Console.WriteLine($"{"".PadLeft(indent, '\t')}{item.Key}");
                    DictPrinter(nestedDict, indent + 1);
                }
                else {
                    if (item.Value is List<dynamic> lst)
                        if (lst[0] is List<dynamic>) {
                            var lst2d = new List<string>();
                            foreach (var innerList in lst) {
                                lst2d.Add($"[{string.Join(",", innerList)}]");
                            }
                            Console.WriteLine($"{"".PadLeft(indent, '\t')}{item.Key} - [{string.Join(" ", lst2d)}]");
                        }
                        else
                            Console.WriteLine($"{"".PadLeft(indent, '\t')}{item.Key} - [{string.Join(", ", item.Value)}]");
                    else
                        Console.WriteLine($"{"".PadLeft(indent, '\t')}{item.Key} - {item.Value}");
                }
            }
        }

        public static (int, int) GetAdjustedCoordsOnCanvas(int x, int y, PictureBox canvas) {   // TODO: issue when image is zoomed
            // Get the scaled size of the image in the PictureBox
            Size imageSize = canvas.Image.Size;
            Size canvasSize = canvas.ClientSize;
            float scaleX = (float)canvasSize.Width / imageSize.Width;
            float scaleY = (float)canvasSize.Height / imageSize.Height;
            float scale = Math.Min(scaleX, scaleY);
            int scaledWidth = (int)(imageSize.Width * scale);
            int scaledHeight = (int)(imageSize.Height * scale);
            // Calculate the offset to center the image in the PictureBox
            int offsetX = (canvasSize.Width - scaledWidth) / 2;
            int offsetY = (canvasSize.Height - scaledHeight) / 2;
            // Adjust the mouse coordinates to get the correct pixel position in the image
            int imgX = (int)((x - offsetX) / scale);
            int imgY = (int)((y - offsetY) / scale);
            return (imgX, imgY);
        }
    }
}
