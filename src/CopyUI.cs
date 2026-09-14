// KO: UI 바인딩은 도구 상태를 읽고 명령만 전달합니다. 실제 선택과 배치는 C# 도구가 수행합니다.
// EN: Bindings expose tool state and route commands; selection and placement remain in the C# tool.
using System;
using Colossal.Serialization.Entities;
using Colossal.UI.Binding;
using Game;
using Game.Input;
using UnityEngine;

namespace CopyIt
{
    public partial class CopyUI : Game.UI.UISystemBase
    {
        private ValueBinding<bool> includeUnderground, active, copying, absolute, box, canPlace, searching, compact, eyedropper, assetFilter, groundSnap;
        private ValueBinding<int> count, filters, selectionMode, polygonCount, previewCount;
        private ValueBinding<float> angle;
        private ValueBinding<string> status, rectangle, polygon, assetFilterName;
        private CopyTool tool;
        public override GameMode gameMode => GameMode.Game;
        protected override void OnCreate()
        {
            base.OnCreate();tool=World.GetOrCreateSystemManaged<CopyTool>();
            AddBinding(includeUnderground=new ValueBinding<bool>("CopyIt","includeUnderground",false));
            AddBinding(groundSnap=new ValueBinding<bool>("CopyIt","groundSnap",false));
            AddBinding(assetFilter=new ValueBinding<bool>("CopyIt","assetFilter",false));
            AddBinding(assetFilterName=new ValueBinding<string>("CopyIt","assetFilterName",""));
            AddBinding(eyedropper=new ValueBinding<bool>("CopyIt","eyedropper",false));
            AddBinding(compact=new ValueBinding<bool>("CopyIt","compact",false));
            AddBinding(selectionMode=new ValueBinding<int>("CopyIt","selectionMode",0));
            AddBinding(polygonCount=new ValueBinding<int>("CopyIt","polygonCount",0));
            AddBinding(previewCount=new ValueBinding<int>("CopyIt","previewCount",0));
            AddBinding(searching=new ValueBinding<bool>("CopyIt","searching",false));
            AddBinding(polygon=new ValueBinding<string>("CopyIt","polygon",""));
            AddBinding(angle=new ValueBinding<float>("CopyIt","angle",0));
            AddBinding(active=new ValueBinding<bool>("CopyIt","active",false));
            AddBinding(copying=new ValueBinding<bool>("CopyIt","copying",false));
            AddBinding(absolute=new ValueBinding<bool>("CopyIt","absolute",false));
            AddBinding(box=new ValueBinding<bool>("CopyIt","box",false));
            AddBinding(canPlace=new ValueBinding<bool>("CopyIt","canPlace",false));
            AddBinding(count=new ValueBinding<int>("CopyIt","count",0));
            AddBinding(filters=new ValueBinding<int>("CopyIt","filters",255));
            AddBinding(status=new ValueBinding<string>("CopyIt","status",""));
            AddBinding(rectangle=new ValueBinding<string>("CopyIt","rectangle",""));
            AddBinding(new TriggerBinding<string>("CopyIt","command",value=>
            {
                try {tool.Command(value);} catch(Exception e){Diagnostics.Failure("ui.command.failed",e);}
            }));
            AddBinding(new TriggerBinding<string>("CopyIt","diagnostic",value=>Diagnostics.Event("ui.event",(value??"").Substring(0,Math.Min(500,(value??"").Length)))));
        }
        protected override void OnGamePreload(Purpose purpose,GameMode mode)
        {
            tool.ClearState();Diagnostics.Event("world.preload",purpose+" "+mode);
            base.OnGamePreload(purpose,mode);
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();
            includeUnderground.Update(tool.IncludeUnderground);
            selectionMode.Update(tool.SelectionMode);polygonCount.Update(tool.PolygonCount);previewCount.Update(tool.PreviewCount);searching.Update(tool.Searching);polygon.Update(tool.PolygonPath());angle.Update(tool.Angle);
            groundSnap.Update(tool.GroundSnap);assetFilter.Update(tool.AssetFilterActive);assetFilterName.Update(tool.AssetFilterName);eyedropper.Update(tool.Eyedropper);compact.Update(tool.Compact);active.Update(tool.Active);copying.Update(tool.Copying);absolute.Update(tool.Absolute);box.Update(tool.Box);
            canPlace.Update(tool.CanPlace);count.Update(tool.Copying?tool.ClipboardCount:tool.Count);filters.Update(tool.Filters);status.Update(tool.Status);
            if(tool.Dragging)
            {
                var a=tool.DragStart;var b=(Vector2)InputManager.instance.mousePosition;
                rectangle.Update(string.Format(System.Globalization.CultureInfo.InvariantCulture,"{0},{1},{2},{3}",
                    Mathf.Min(a.x,b.x)/Screen.width*100,(Screen.height-Mathf.Max(a.y,b.y))/Screen.height*100,
                    Mathf.Abs(a.x-b.x)/Screen.width*100,Mathf.Abs(a.y-b.y)/Screen.height*100));
            }
            else rectangle.Update("");
        }
    }
}


