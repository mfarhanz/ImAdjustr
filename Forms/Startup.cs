using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
using ImAdjustr.Internal.Services;
using ImAdjustr.Internal.Controllers;
using ImAdjustr.Internal.Static;
using ImAdjustr.Utility;

namespace ImAdjustr.Base {
    internal static class Startup {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main() {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Process baseProcess = Process.GetProcessesByName(AppDomain.CurrentDomain.FriendlyName.Replace(".exe", ""))[0];
            DebugLogger logger = new DebugLogger(baseProcess, Path.Combine(Paths.Logs, "app.log"));
            BackendController controller = new BackendController(logger, pythonPath: FileReader.LoadXMLConfig(Paths.AppConfig)["pythonHome"]);
            Application.Run(new Editor(controller, logger));
        }
    }
}
