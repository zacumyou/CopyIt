using System;
using System.Collections.Generic;
using System.Reflection;

namespace CopyIt
{
    // Optional public API: no Move It DLL dependency and no access to its mutable selection.
    internal static class MoveItCompatibility
    {
        internal static bool TrySnapshot<T>(object activeTool,out T[] entities,out string error)
        {
            entities=Array.Empty<T>();error=null;
            try {
                var property=activeTool?.GetType().GetProperty("SelectedEntities",BindingFlags.Public|BindingFlags.Instance);
                if(property==null||property.GetIndexParameters().Length!=0){error="SelectedEntities API unavailable";return false;}
                if(!(property.GetValue(activeTool) is IEnumerable<T> selection)){error="SelectedEntities API type changed";return false;}
                entities=new List<T>(new HashSet<T>(selection)).ToArray();
                return true;
            }
            catch(Exception exception){error=exception.GetType().Name;return false;}
        }
    }
}
