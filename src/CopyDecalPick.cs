// KO: 얇은 데칼도 잡히도록 메시의 로컬 경계에서 선택 레이를 검사합니다.
// EN: Tests the picking ray against local mesh bounds so thin decals remain selectable.
using Colossal.Collections;
using Game.Common;
using Game.Prefabs;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using ObjectTransform=Game.Objects.Transform;
namespace CopyIt
{
    public partial class CopyTool
    {
        private Entity PickDecal(Vector2 mouse,bool ignoreFilters=false)
        {
            if((!ignoreFilters&&(Filters&32)==0)||Camera.main==null)return Entity.Null;
            var tree=World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>().GetStaticSearchTree(true,out var dependency);dependency.Complete();
            var iterator=new DecalPickIterator{Tool=this,IgnoreFilters=ignoreFilters,Ray=Camera.main.ScreenPointToRay(mouse),Distance=float.MaxValue,Result=Entity.Null};
            tree.Iterate(ref iterator);return iterator.Result;
        }
        private struct DecalPickIterator:INativeQuadTreeIterator<Entity,QuadTreeBoundsXZ>,IUnsafeQuadTreeIterator<Entity,QuadTreeBoundsXZ>
        {
            internal CopyTool Tool;internal bool IgnoreFilters;internal Ray Ray;internal float Distance;internal Entity Result;
            public bool Intersect(QuadTreeBoundsXZ bounds){
                var b=bounds.m_Bounds;var box=new Bounds((b.min+b.max)*.5f,b.max-b.min+new float3(.1f,.5f,.1f));
                return box.IntersectRay(Ray,out var d)&&d<=Distance;
            }
            public void Iterate(QuadTreeBoundsXZ bounds,Entity entity){
                if(!Tool.Live(entity)||Tool.EntityManager.HasComponent<Owner>(entity)||!Tool.EntityManager.HasComponent<ObjectTransform>(entity)||!Tool.IsDecal(entity)||!Tool.Eligible(entity,!IgnoreFilters))return;
                var prefab=Tool.EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
                float3 lo,hi;
                if(Tool.EntityManager.HasComponent<Game.Tools.EditorContainer>(entity)){
                    var c=Tool.EntityManager.GetComponentData<Game.Tools.EditorContainer>(entity);
                    if(!Tool.EntityManager.HasComponent<MeshData>(c.m_Prefab))return;
                    var b=Tool.EntityManager.GetComponentData<MeshData>(c.m_Prefab).m_Bounds;
                    var a=b.min*c.m_Scale;var z=b.max*c.m_Scale;lo=math.min(a,z);hi=math.max(a,z);
                }else{
                    if(!Tool.EntityManager.HasComponent<ObjectGeometryData>(prefab))return;
                    var b=Tool.EntityManager.GetComponentData<ObjectGeometryData>(prefab).m_Bounds;lo=b.min;hi=b.max;
                }
                var t=Tool.EntityManager.GetComponentData<ObjectTransform>(entity);var inverse=math.inverse(t.m_Rotation);
                var local=new Ray(math.rotate(inverse,(float3)Ray.origin-t.m_Position),math.rotate(inverse,(float3)Ray.direction));
                var box=new Bounds((lo+hi)*.5f,math.max(hi-lo,new float3(.02f,.2f,.02f)));
                if(box.IntersectRay(local,out var distance)&&distance<Distance){Distance=distance;Result=entity;}
            }
        }
    }
}
