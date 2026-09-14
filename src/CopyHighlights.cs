// KO: 실제 소유 관계를 확인한 하위 오브젝트에만 강조를 확장합니다.
// EN: Expands highlights only to descendants with verified ownership.
using System.Collections.Generic;
using System.Linq;
using Colossal.Mathematics;
using Game.Common;
using Game.Objects;
using Game.Prefabs;
using Game.Rendering;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using Color=UnityEngine.Color;
using SubObject=Game.Objects.SubObject;
using ObjectTransform=Game.Objects.Transform;
namespace CopyIt
{
    public partial class CopyTool
    {
        private readonly Dictionary<Entity,Entity[]> highlightChildren=new Dictionary<Entity,Entity[]>();
        private void ExpandHighlightTargets(HashSet<Entity> wanted)
        {
            foreach(var stale in highlightChildren.Keys.Where(e=>!wanted.Contains(e)).ToArray())highlightChildren.Remove(stale);
            foreach(var root in wanted.ToArray())
            {
                if(!Live(root))continue;
                if(!highlightChildren.TryGetValue(root,out var children))
                {
                    var visited=new HashSet<Entity>{root};var queue=new List<Entity>{root};int next=0;
                    while(next<queue.Count&&visited.Count<4096)
                    {
                        var parent=queue[next++];if(!EntityManager.HasBuffer<SubObject>(parent))continue;
                        var buffer=EntityManager.GetBuffer<SubObject>(parent,true);
                        for(int i=0;i<buffer.Length&&visited.Count<4096;i++){
                            var child=buffer[i].m_SubObject;
                            if(!Live(child)||!EntityManager.HasComponent<Owner>(child)||EntityManager.GetComponentData<Owner>(child).m_Owner!=parent||!visited.Add(child))continue;
                            queue.Add(child);
                        }
                    }
                    visited.Remove(root);children=visited.ToArray();highlightChildren[root]=children;
                    var prefab=EntityManager.HasComponent<PrefabRef>(root)?EntityManager.GetComponentData<PrefabRef>(root).m_Prefab:Entity.Null;
                    string geometry=EntityManager.HasComponent<ObjectGeometryData>(prefab)?EntityManager.GetComponentData<ObjectGeometryData>(prefab).m_Layers.ToString():"none";
                    Diagnostics.Event("highlight.structure",$"root={root} prefab={PrefabName(prefab)} children={children.Length} layers={geometry} boundary=enabled");
                }
                foreach(var child in children)if(Live(child))wanted.Add(child);
            }
        }
        private void DrawSelectionBoundaries()
        {
            if(!Active||Copying)return;
            var roots=new HashSet<Entity>(selected);roots.UnionWith(regionPreview);
            if(roots.Count==0)return;
            var renderer=World.GetOrCreateSystemManaged<OverlayRenderSystem>();
            var buffer=renderer.GetBuffer(out var dependency);dependency.Complete();
            var color=new Color(.48f,.85f,1f,1f);
            foreach(var root in roots)
            {
                if(IsSurface(root)){
                    var nodes=EntityManager.GetBuffer<Game.Areas.Node>(root,true);
                    for(int i=0;i<nodes.Length;i++)buffer.DrawLine(color,color,0,OverlayRenderSystem.StyleFlags.Projected,new Line3.Segment(nodes[i].m_Position,nodes[(i+1)%nodes.Length].m_Position),.2f,default);
                    continue;
                }
                if(IsNetwork(root)){
                    buffer.DrawCurve(color,EntityManager.GetComponentData<Game.Net.Curve>(root).m_Bezier,.3f);
                    continue;
                }
                if(!Live(root)||!EntityManager.HasComponent<ObjectTransform>(root)||!EntityManager.HasComponent<PrefabRef>(root))continue;
                var prefab=EntityManager.GetComponentData<PrefabRef>(root).m_Prefab;
                if(!EntityManager.HasComponent<ObjectGeometryData>(prefab))continue;
                var geometry=EntityManager.GetComponentData<ObjectGeometryData>(prefab);
                var transform=EntityManager.GetComponentData<ObjectTransform>(root);
                var lo=geometry.m_Bounds.min;var hi=geometry.m_Bounds.max;
                if(EntityManager.HasComponent<Game.Tools.EditorContainer>(root)){
                    var container=EntityManager.GetComponentData<Game.Tools.EditorContainer>(root);
                    if(EntityManager.HasComponent<MeshData>(container.m_Prefab)){
                        var mesh=EntityManager.GetComponentData<MeshData>(container.m_Prefab);
                        var a=mesh.m_Bounds.min*container.m_Scale;var b=mesh.m_Bounds.max*container.m_Scale;lo=math.min(a,b);hi=math.max(a,b);
                    }
                }
                if(!math.all(math.isfinite(lo))||!math.all(math.isfinite(hi)))continue;
                bool circular=(geometry.m_Flags&GeometryFlags.Circular)!=0;
                if(!circular&&EntityManager.HasComponent<BuildingData>(prefab)){
                    var lot=EntityManager.GetComponentData<BuildingData>(prefab).m_LotSize;
                    if(math.all(lot>0)){lo.xz=-new float2(lot.x,lot.y)*4f;hi.xz=-lo.xz;}
                }
                float width=math.clamp(math.max(hi.x-lo.x,hi.z-lo.z)*.008f,.12f,.4f);
                float3 Position(float x,float z)=>transform.m_Position+math.rotate(transform.m_Rotation,new float3(x,lo.y+.15f,z));
                if(circular){
                    buffer.DrawCircle(color,Color.clear,width,OverlayRenderSystem.StyleFlags.Projected,new float2(0,1),Position((lo.x+hi.x)*.5f,(lo.z+hi.z)*.5f),math.max(hi.x-lo.x,hi.z-lo.z)+width);
                }else{
                    var a=Position(lo.x,lo.z);var b=Position(hi.x,lo.z);var c=Position(hi.x,hi.z);var d=Position(lo.x,hi.z);
                    buffer.DrawLine(color,color,0,OverlayRenderSystem.StyleFlags.Projected,new Line3.Segment(a,b),width,default);
                    buffer.DrawLine(color,color,0,OverlayRenderSystem.StyleFlags.Projected,new Line3.Segment(b,c),width,default);
                    buffer.DrawLine(color,color,0,OverlayRenderSystem.StyleFlags.Projected,new Line3.Segment(c,d),width,default);
                    buffer.DrawLine(color,color,0,OverlayRenderSystem.StyleFlags.Projected,new Line3.Segment(d,a),width,default);
                }
            }
            renderer.AddBufferWriter(default);
        }
    }
}

