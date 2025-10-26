using MelonLoader.Bootstrap.RuntimeHandlers.Il2Cpp;
using MelonLoader.Bootstrap.RuntimeHandlers.Mono;
using MelonLoader.Bootstrap.Utils;
using System.Runtime.InteropServices;

namespace MelonLoader.Bootstrap
{
    internal static partial class ModuleSymbolRedirect
    {
        private static bool _runtimeInitialised;

#if LINUX || OSX
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
#endif
#if WINDOWS
        [UnmanagedFunctionPointer(CallingConvention.StdCall)]
#endif
        private delegate nint DetourFn(nint handle, nint symbol);
        private static readonly DetourFn DetourDelegate = SymbolDetour;

        private static PltNativeHook<DetourFn>? nativeHook;

        internal static void Attach()
        {
            IntPtr detourPtr = Marshal.GetFunctionPointerForDelegate(DetourDelegate);

#if LINUX || OSX
            nativeHook = PltNativeHook<DetourFn>.RedirectUnityPlayer("dlsym", detourPtr);
#endif

#if WINDOWS
            nativeHook = PltNativeHook<DetourFn>.RedirectUnityPlayer("GetProcAddress", detourPtr);
#endif

            nativeHook?.Attach();
        }

        internal static void Detach()
        {
            nativeHook?.Detach();
            nativeHook = null;
        }

        private static nint SymbolDetour(nint handle, nint symbol)
        {
            nint originalSymbolAddress = nativeHook?.Trampoline(handle, symbol) ?? 0;

            string? symbolName = Marshal.PtrToStringAnsi(symbol);
            if (string.IsNullOrEmpty(symbolName)
                || string.IsNullOrWhiteSpace(symbolName))
                return originalSymbolAddress;

            //MelonDebug.Log($"Looking for Symbol {symbolName}");
            if (!MonoHandler.SymbolRedirects.TryGetValue(symbolName, out var redirect)
                && !Il2CppHandler.SymbolRedirects.TryGetValue(symbolName, out redirect))
                return originalSymbolAddress;

            if (!_runtimeInitialised)
            {
                MelonDebug.Log("Init");
                redirect.InitMethod(handle);
                if (!LoaderConfig.Current.Loader.CapturePlayerLogs)
                    ConsoleHandler.ResetHandles();
            }
            _runtimeInitialised = true;

            MelonDebug.Log($"Redirecting {symbolName}");
            return redirect.detourPtr;
        }
    }
}
