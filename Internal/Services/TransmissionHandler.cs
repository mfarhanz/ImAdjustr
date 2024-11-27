using System;
using System.Drawing;
using System.Threading.Tasks;

namespace ImAdjustr.Internal.Services {
    internal abstract class TransmissionHandler {
        internal abstract Task<(bool, bool)> ProcessImage(Bitmap bitmap, params object[] parameters);

        protected async Task<(bool result, bool success)> TryProcess(Func<Task<bool>> operation) {
            try {
                return (await operation(), true);
            }
            catch {
                return (false, false);
            }
        }
    }
}
