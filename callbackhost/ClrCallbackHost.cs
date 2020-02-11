using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

static class HResultError
{
    // 0x80000000 (failure bit)
    // 0x20000000 (customer-defined bit)
    // 0x04F9     (a random facility code)
    const UInt32 CommonBits = 0xA4F90000;

    public const UInt32 MethodNotFound               = CommonBits | 0x01;
    public const UInt32 AssemblyNotFound             = CommonBits | 0x02;
    public const UInt32 AssemblyMissingClrBridgeType = CommonBits | 0x03;
    public const UInt32 RequestedNonStaticMethod     = CommonBits | 0x04;
    public const UInt32 RequestedGenericMethod       = CommonBits | 0x05;
    // A temporary error code until I figure out a good way to handle the delegate type problem
    public const UInt32 RequestedUnknownMethod       = CommonBits | 0x06;
}

class ClrLibRunner
{
    const String EntryName = "_clrCallbackHostEntry";
    delegate Int32 EntryDelegate(IntPtr createDelegateFuncPtr, Int32 argc, String[] argv);

    public static Int32 Main(String[] args)
    {
        if (args.Length == 0)
        {
            Console.WriteLine("Usage: ClrLibRunner.exe <NativeSharedLibrary> <Args>...");
            return 1;
        }
        String sharedLibrary = args[0];

        // TODO: might want to make this a command-line option (--dlopen or --loadlibrary)?
        // TODO: need a way to detect whether or not this mechanism is supported
        //       maybe I should use runtime reflection?
        //bool onWindows = RuntimeInformation.IsOSPlatform(OSPlatform.Windows);
        bool onWindows = Directory.Exists("C:\\Windows\\System32");

        EntryDelegate entry;
        if (onWindows)
        {
            IntPtr moduleHandle = WindowsNativeMethods.LoadLibrary(sharedLibrary);
            if (moduleHandle == IntPtr.Zero)
            {
                Console.WriteLine("Error: LoadLibrary '{0}' failed: {1}", sharedLibrary, Marshal.GetLastWin32Error());
                return 1;
            }
            IntPtr funcPtr = WindowsNativeMethods.GetProcAddress(moduleHandle, EntryName);
            if (funcPtr == IntPtr.Zero)
            {
                Int32 errorCode = Marshal.GetLastWin32Error();
                if (errorCode == WindowsNativeMethods.ERROR_PROC_NOT_FOUND)
                    Console.WriteLine("Error: library '{0}' is missing function '{1}'", sharedLibrary, EntryName);
                else
                    Console.WriteLine("Error: GetProcAddress '{0}' failed: {1}", EntryName, errorCode);
                return 1;
            }
            entry = (EntryDelegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(EntryDelegate));
        }
        else
        {
            const uint RTLD_NOW = 2;
            IntPtr moduleHandle = LinuxNativeMethods.dlopen(sharedLibrary, RTLD_NOW);
            if (moduleHandle == IntPtr.Zero)
            {
                Console.WriteLine("Error: dlopen '{0}' failed: {1}", sharedLibrary, LinuxNativeMethods.dlerror());
                return 1;
            }
            IntPtr funcPtr = LinuxNativeMethods.dlsym(moduleHandle, EntryName);
            if (funcPtr == IntPtr.Zero)
            {
                Console.WriteLine("Error: dlsym '{0}' failed: {1}", EntryName, LinuxNativeMethods.dlerror());
                return 1;
            }
            entry = (EntryDelegate)Marshal.GetDelegateForFunctionPointer(funcPtr, typeof(EntryDelegate));
        }
        IntPtr createDelegateFuncPtr = Marshal.GetFunctionPointerForDelegate(new CreateDelegateDelegate(CreateDelegate));
        return entry(createDelegateFuncPtr, args.Length, args);
    }

    delegate uint CreateDelegateDelegate(String assemblyName, String methodName, ref IntPtr outFuncAddr);

    // Returns: HRESULT on error
    static uint CreateDelegate(String assemblyName, String methodName, ref IntPtr outFuncAddr)
    {
        //Console.WriteLine("[DEBUG] assemblyName={0} methodName={1}", assemblyName, methodName);Console.Out.Flush();
        Assembly assembly;

        try { assembly = Assembly.Load(assemblyName); }
        catch (FileNotFoundException) { return HResultError.AssemblyNotFound; }
        Debug.Assert(assembly != null); // I don't think Assembly.Load will ever return null;

        //Console.WriteLine("[DEBUG] assembly.FullName is {0}", assembly.FullName);
        //Console.WriteLine("[DEBUG] assembly.Location is {0}", assembly.Location);
        //Console.Out.Flush();

        Type type = assembly.GetType("ClrBridge");
        if (type == null)
            return HResultError.AssemblyMissingClrBridgeType;

        MethodInfo method = type.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static);
        if (method == null)
            return HResultError.MethodNotFound;

        // we purposefully disallow this to keep this interface the same with the coreclr interface
        if (!method.IsStatic) return HResultError.RequestedNonStaticMethod;
        if (method.IsGenericMethod) return HResultError.RequestedGenericMethod;

        ParameterInfo[] methodParams = method.GetParameters();
        Type[] delegateParamTypes = new Type[methodParams.Length + 1];
        for(Int32 i = 0; i < methodParams.Length; i++)
        {
            delegateParamTypes[i] = methodParams[i].ParameterType;
        }
        delegateParamTypes[methodParams.Length] = method.ReturnType;
        Type delegateType = GetDelegateType(method);
        if (delegateType == null)
            return HResultError.RequestedUnknownMethod;

        Delegate dg = method.CreateDelegate(delegateType);
        outFuncAddr = Marshal.GetFunctionPointerForDelegate(dg);
        return 0;
    }

    static Type GetDelegateType(MethodInfo method)
    {
        // HACK: right now my .NET compiler doesn't have System.Linq.Expressions.dll which
        //       can be used to dynamically create Delegate types, so for now I'm just hardcoding the
        //       delegate types below.
        //ParameterInfo[] methodParams = method.GetParameters();
        //Type[] delegateParamTypes = new Type[methodParams.Length + 1];
        //for(Int32 i = 0; i < methodParams.Length; i++)
        //{
        //    delegateParamTypes[i] = methodParams[i].ParameterType;
        //}
        //delegateParamTypes[methodParams.Length] = method.ReturnType;
        //return System.Linq.Expression.GetDelegateType(delegateParamTypes);

        // HACK: remove the code below once we support dynamic deleate types
        // NOTE: if one of these is wrong, we'll get a .NET exception right away
        //       with "method arguments are incompatible" rather than an odd error later when
        //       the method is called from native code
        return ClrBridgeDelegateTypes.TryGetDelegateType(method.Name);
    }
}

static class LinuxNativeMethods
{
    [DllImport("libdl.so")]
    public static extern IntPtr dlopen(String filename, uint flags);

    [DllImport("libdl.so")]
    public static extern String dlerror();

    [DllImport("libdl.so")]
    public static extern IntPtr dlsym(IntPtr handle, String symbol);
}

static class WindowsNativeMethods
{
    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi)]
    public static extern IntPtr LoadLibrary(String dllToLoad);

    public const UInt32 ERROR_PROC_NOT_FOUND = 127;

    [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Ansi, ExactSpelling = true)]
    public static extern IntPtr GetProcAddress(IntPtr module, String name);

    [DllImport("kernel32.dll", SetLastError = true)]
    public static extern bool FreeLibrary(IntPtr module);
}
