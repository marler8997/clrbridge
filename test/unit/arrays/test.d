import cstring;
import clr : PrimitiveType, Decimal, TypeSpec, DotNetObject;
import clrbridge : MethodSpec;
import clrbridge.global : globalClrBridge;
import arrays;
import std.stdio;

void test()
{
    /+
    {
        auto bools = globalClrBridge.argsToArray!bool(true, false, true);
    }
    {
        auto ints = globalClrBridge.argsToArray!uint(1, 2, 3, 10, 8291);
    }
    +/
    {
        auto objs = globalClrBridge.argsToArray!DotNetObject(
            globalClrBridge.typeType,
            globalClrBridge.primitiveTypes.UInt32,
            globalClrBridge.primitiveTypes.UInt32,
            globalClrBridge.primitiveTypes.Boolean);
        writefln("objs.handle=%s", objs.handle);
        globalClrBridge.debugWriteObject(objs);
        auto obj2 = Static.PassthroughObjectArray(objs);
        writefln("obj2.handle=%s", obj2.handle);
        globalClrBridge.debugWriteObject(obj2);
    }
}
