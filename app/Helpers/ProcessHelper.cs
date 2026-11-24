using System.Security.Principal;

namespace GHelper.Helpers
{
    public static class ProcessHelper
    {
        private static readonly Lazy<bool> _isSystem = new Lazy<bool>(() =>
        {
            using var identity = WindowsIdentity.GetCurrent();
            return identity.IsSystem;
        }, LazyThreadSafetyMode.ExecutionAndPublication);

        public static bool IsRunningAsSystem() => _isSystem.Value;
    }
}
