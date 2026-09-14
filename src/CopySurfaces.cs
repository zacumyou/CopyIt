// KO: 표면은 Transform 대신 꼭짓점 버퍼를 사용합니다. 마지막에 첫 점을 반복해야 게임이 닫힌 면으로 인식합니다.
// EN: Surfaces use node buffers instead of Transform. Repeating the first node closes the area in native generation.
using System;
using System.Collections.Generic;
using Colossal.Collections;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using AreaNode=Game.Areas.Node;
namespace CopyIt {
    public partial class CopyTool {
        private EntityQuery surfaceTemps;
        private readonly List<Entity> surfaceDefinitions=new List<Entity>();
        private bool IsSurface(Entity e)=>Live(e)&&!EntityManager.HasComponent<Owner>(e)&&EntityManager.HasComponent<Game.Areas.Area>(e)&&EntityManager.HasBuffer<AreaNode>(e)&&EntityManager.HasComponent<PrefabRef>(e)&&EntityManager.HasComponent<SurfaceData>(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab)&&EntityManager.GetBuffer<AreaNode>(e,true).Length>=3;
        private float3 SurfaceCenter(Entity e){
            var nodes=EntityManager.GetBuffer<AreaNode>(e,true);float3 sum=0;foreach(var n in nodes)sum+=n.m_Position;return sum/nodes.Length;
        }
        private AreaNode[] SnapshotSurface(Entity e){
            var nodes=EntityManager.GetBuffer<AreaNode>(e,true);
            if(nodes.Length>2048)throw new InvalidOperationException("표면 하나의 꼭짓점은 최대 2048개까지 복사할 수 있습니다.");
            var result=nodes.ToNativeArray(Allocator.Temp);try {
                foreach(var n in result)if(!math.all(math.isfinite(n.m_Position)))throw new InvalidOperationException("Non-finite surface node");
                Diagnostics.Event("copy.surface.snapshot",$"source={e} vertices={nodes.Length}");return result.ToArray();
            }finally{result.Dispose();}
        }
        private void ClearSurfaceDefinitions(){foreach(var e in surfaceDefinitions)if(EntityManager.Exists(e))EntityManager.DestroyEntity(e);surfaceDefinitions.Clear();}
        private void CreateSurfaceDefinition(Item item){
            var e=EntityManager.CreateEntity(typeof(CreationDefinition),typeof(Updated));surfaceDefinitions.Add(e);
            EntityManager.SetComponentData(e,new CreationDefinition{m_Prefab=item.Prefab,m_RandomSeed=item.AppearanceSeed?.m_Seed??item.Seed.GetRandom(0).NextInt()});
            var nodes=EntityManager.AddBuffer<AreaNode>(e);
            foreach(var source in item.Surface){var p=MapPoint(source.m_Position);nodes.Add(new AreaNode(p,0));}
            // Native GenerateAreasSystem recognizes this closing node and marks the area complete.
            nodes.Add(nodes[0]);
        }
        private bool SurfaceMatches(Entity entity,Item item){
            if(!EntityManager.Exists(entity)||!EntityManager.HasComponent<PrefabRef>(entity)||EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab!=item.Prefab||!EntityManager.HasBuffer<AreaNode>(entity))return false;
            var nodes=EntityManager.GetBuffer<AreaNode>(entity,true);if(nodes.Length!=item.Surface.Length)return false;
            for(int i=0;i<nodes.Length;i++)if(math.distancesq(nodes[i].m_Position,MapPoint(item.Surface[i].m_Position))>.0001f)return false;
            return EntityManager.HasComponent<Game.Areas.Area>(entity)&&(EntityManager.GetComponentData<Game.Areas.Area>(entity).m_Flags&Game.Areas.AreaFlags.Complete)!=0;
        }
        private Entity FindSurfacePreview(Item item,HashSet<Entity> used){
            surfaceTemps.CompleteDependency();using(var entities=surfaceTemps.ToEntityArray(Allocator.Temp))foreach(var e in entities){
                var t=EntityManager.GetComponentData<Temp>(e);
                if(!used.Contains(e)&&t.m_Original==Entity.Null&&(t.m_Flags&TempFlags.Create)!=0&&SurfaceMatches(e,item))return e;
            }
            placementBlockReason="표면 미리보기의 꼭짓점·높이가 일치하지 않습니다.";return Entity.Null;
        }
        private struct SurfaceRegionIterator:INativeQuadTreeIterator<Game.Areas.AreaSearchItem,QuadTreeBoundsXZ>,IUnsafeQuadTreeIterator<Game.Areas.AreaSearchItem,QuadTreeBoundsXZ>{
            internal RegionIterator Region;
            internal HashSet<Entity> Seen;
            public bool Intersect(QuadTreeBoundsXZ bounds)=>Region.Intersect(bounds);
            public void Iterate(QuadTreeBoundsXZ bounds,Game.Areas.AreaSearchItem item){if(Seen.Add(item.m_Area))Region.Iterate(bounds,item.m_Area);}
        }
    }
}
