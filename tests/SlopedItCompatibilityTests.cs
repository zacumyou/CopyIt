using System;
using System.Reflection;
using System.Reflection.Emit;
using CopyIt;

public sealed class FakeSlopedOptions { public bool Enabled {get;set;}=true; }
internal static class SlopedItCompatibilityTests
{
    internal static void Run()
    {
        void Check(bool value,string name){if(!value)throw new Exception(name);}
        Check(!SlopedItCompatibility.IsEnabled(),"SlopedIt absent");
        var assembly=AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("SlopedIt.TestFixture"),AssemblyBuilderAccess.Run);
        var builder=assembly.DefineDynamicModule("Main").DefineType("SlopedIt.Mod",TypeAttributes.Public);
        builder.DefineField("Active",typeof(bool),FieldAttributes.Assembly|FieldAttributes.Static);
        builder.DefineField("Options",typeof(FakeSlopedOptions),FieldAttributes.Assembly|FieldAttributes.Static);
        var type=builder.CreateType();
        const BindingFlags flags=BindingFlags.Static|BindingFlags.NonPublic;
        var active=type.GetField("Active",flags);var options=type.GetField("Options",flags);
        var setting=new FakeSlopedOptions();options.SetValue(null,setting);
        Check(!SlopedItCompatibility.IsEnabled(),"loaded but inactive");
        active.SetValue(null,true);
        Check(SlopedItCompatibility.IsEnabled(),"late load discovery");
        setting.Enabled=false;
        Check(!SlopedItCompatibility.IsEnabled(),"live option disabled");
        setting.Enabled=true;
        Check(SlopedItCompatibility.IsEnabled(),"live option enabled again");
        active.SetValue(null,false);
        Check(!SlopedItCompatibility.IsEnabled(),"stopped mod");
        active.SetValue(null,true);options.SetValue(null,null);
        Check(!SlopedItCompatibility.IsEnabled(),"disposed settings");
        Check(SlopedItCompatibility.ShouldAlign(false,false),"disabled retains legacy non-snap alignment");
        Check(SlopedItCompatibility.ShouldAlign(false,true),"disabled retains legacy snap alignment");
        Check(!SlopedItCompatibility.ShouldAlign(true,false),"enabled preserves original rotation outside snap");
        Check(SlopedItCompatibility.ShouldAlign(true,true),"enabled aligns snap mode");
        Console.WriteLine("PASS: 11 SlopedIt optional integration checks (runtime fixture, no game execution).");
    }
}
