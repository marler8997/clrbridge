using System;

public static class GenerateClrBridgeCode
{
    static readonly String[] PrimitiveTypes = new String[] {
        "Boolean",
        "Byte",
        "SByte",
        "UInt16",
        "Int16",
        "UInt32",
        "Int32",
        "UInt64",
        "Int64",
        "Char",
        "String",
        "Single",
        "Double",
        "Decimal",
        "Object",
    };
    public static void Main(String[] args)
    {
        Console.WriteLine("using System;");
        Console.WriteLine("using System.Reflection;");
        Console.WriteLine("using System.Runtime.InteropServices;");
        Console.WriteLine("");
        Console.WriteLine("public static partial class ClrBridge");
        Console.WriteLine("{");
        foreach (String type in PrimitiveTypes)
        {
            Console.WriteLine("    public static IntPtr Box{0}({0} value)", type);
            Console.WriteLine("    {");
            Console.WriteLine("        return GCHandle.ToIntPtr(GCHandle.Alloc((Object)value));");
            Console.WriteLine("    }");
            Console.WriteLine("    public static void CallStatic{0}(IntPtr methodPtr, {0} value)", type);
            Console.WriteLine("    {");
            Console.WriteLine("        MethodInfo method = (MethodInfo)GCHandle.FromIntPtr(methodPtr).Target;");
            Console.WriteLine("        method.Invoke(null, new Object[] {value});");
            Console.WriteLine("    }");
            if (type == "Object")
                continue;
            Console.WriteLine("    public static void ArraySet{0}(IntPtr arrayPtr, Int32 index, {0} value)", type);
            Console.WriteLine("    {");
            Console.WriteLine("        Array array = (Array)GCHandle.FromIntPtr(arrayPtr).Target;");
            Console.WriteLine("        array.SetValue(value, index);");
            Console.WriteLine("    }");
        }
        Console.WriteLine("}");
     }
}
