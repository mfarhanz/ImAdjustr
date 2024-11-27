using System.Text.Json.Serialization;

namespace ImAdjustr.Internal.Static {
    public static class NumberConstants {
        //public const int
    }

    public static class ConfigSettings {
        //public const string
    }

    public static class Paths {
        public const string Logs = @"Logs";
        public const string Utility = @"Utility";
        public const string Config = @"Config";
        public const string Data = @"Data";
        public const string Temp = @"Temp";
        public const string Python = @"Python";
        public const string Scripts = @"Scripts";
        public const string Assets = @"Assets";
        public const string FontAssets = Assets + @"\Fonts";
        public const string FilterAssets = Assets + @"\Filters";
        public const string LutAssets = Assets + @"\LUTs";
        public const string TextureAssets = Assets + @"\Textures";
        public const string Linkers = Scripts + @"\linkers";
        public const string PyInternal = Scripts + @"\internal";
        public const string PyTools = Scripts + @"\tools";
        public const string PipeControl = Linkers + @"\pipe_host.py";
        public const string FlaskControl = Linkers + @"\flask_server.py";
        public const string Dispatcher = PyInternal + @"\dispatcher.py";
        public const string GifSaver = PyTools + @"\gif_saver.py";
        public const string ImageThemes = Data + @"\colorMatrix.json";
        public const string ImageKernels = Data + @"\filterMatrix.json";
        public const string ImagePalettes = Data + @"\palettes.json";
        public const string AppThemes = Config + @"\Themes.xml";
        public const string AppConfig = Config + @"\App.xml";
    }

    public static class Endpoints {
        public const string FlaskShutdown = "/shutdown";
        public const string FlaskWarmup = "/warmup";
        public const string FlaskTest = "/test";
        public const string FlaskProcessThroughPath = "/process_image_through_path";
        public const string FlaskProcessDirect = "/process_image_direct";
        public const string FlaskProcessThroughMemoryMap = "/process_image_through_mmap";
    }

    public static class ErrorConstants {
        public static string DrawError => "Error drawing current image onto canvas";
    }

    public static class MiscConstants {
        public const string MapName = @"Local\ImAdjustr_mmap_";
    }

    //internal struct PyResponse {
    //    [JsonPropertyName("output")]
    //    public string Output { get; set; }

    //    [JsonPropertyName("success")]
    //    public bool Result { get; set; }
    //}

    internal class PyResponse {
        [JsonPropertyName("output")]
        public string Output { get; set; }
        [JsonPropertyName("success")]
        public bool Result { get; set; }
    }


    public enum TRANS_METHODS {
        FLASK_DIRECT,
        FLASK_DISK,
        FLASK_MMAP,
        PIPE_DIRECT,
        PIPE_MMAP,
    };

    enum IMG_OPERATIONS {
        BLUR
    }
}