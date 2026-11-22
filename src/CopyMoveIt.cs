using System;
using System.Collections.Generic;
using Unity.Entities;

namespace CopyIt
{
    public partial class CopyTool
    {
        private Entity[] moveItSelection;
        private string moveItError;

        private void EnterFromMoveIt()
        {
            // Capture before switching tools; consume only after previous-tool temp cleanup.
            var previous=m_ToolSystem.activeTool;
            bool captured=MoveItCompatibility.TrySnapshot<Entity>(previous,out var snapshot,out var error);
            Activate();
            moveItSelection=captured?snapshot:Array.Empty<Entity>();
            moveItError=captured?null:error;
        }
        private void ImportMoveItSelection()
        {
            var snapshot=moveItSelection;moveItSelection=null;
            if(moveItError!=null){Diagnostics.Event("moveit.selection.failed",moveItError);moveItError=null;Status=L10n.Message("CopyIt.MoveIt.Unavailable");return;}
            if(snapshot.Length==0)return;
            var imported=new HashSet<Entity>();
            foreach(var entity in snapshot){
                // Never silently expand nodes/control points into unrelated roads or drop unsupported entries.
                var root=Resolve(entity,false);
                if(root==Entity.Null){Status=L10n.Message("CopyIt.MoveIt.Unsupported");Diagnostics.Event("moveit.selection.unsupported",entity.ToString());return;}
                imported.Add(root);
            }
            if(imported.Count>Limit){Status=L10n.Message("CopyIt.MoveIt.Limit");return;}
            selected.Clear();selected.UnionWith(imported);SyncHighlights();
            Diagnostics.Event("moveit.selection.imported",$"selected={snapshot.Length} roots={imported.Count}");
            BeginCopy();
        }
    }
}
