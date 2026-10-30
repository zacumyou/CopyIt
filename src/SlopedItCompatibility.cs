using System;
using System.Reflection;

namespace CopyIt
{
    // KO: 선택적 연동. SlopedIt DLL을 필수 참조하지 않고 현재 실행/옵션 상태를 읽습니다.
    // EN: Optional integration; read live state without a required SlopedIt assembly reference.
    internal static class SlopedItCompatibility
    {
        private static int assemblyCount=-1;
        private static FieldInfo active,options;
        private static PropertyInfo enabled;
        internal static bool IsEnabled()
        {
            try
            {
                var assemblies=AppDomain.CurrentDomain.GetAssemblies();
                if(assemblyCount!=assemblies.Length)
                {
                    assemblyCount=assemblies.Length;active=null;options=null;enabled=null;
                    foreach(var assembly in assemblies)
                    {
                        var type=assembly.GetType("SlopedIt.Mod",false);
                        if(type==null)continue;
                        const BindingFlags flags=BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic;
                        active=type.GetField("Active",flags);options=type.GetField("Options",flags);
                        enabled=options?.FieldType.GetProperty("Enabled",BindingFlags.Public|BindingFlags.Instance);
                        break;
                    }
                }
                if(active?.FieldType!=typeof(bool)||enabled?.PropertyType!=typeof(bool))return false;
                var settings=options?.GetValue(null);
                return (bool)active.GetValue(null)&&settings!=null&&(bool)enabled.GetValue(settings);
            }
            catch { return false; } // Missing/changed optional API retains Copy It's original behavior.
        }
        internal static bool ShouldAlign(bool slopedItEnabled,bool groundSnap)=>!slopedItEnabled||groundSnap;
    }
}
