// KO: 데칼의 지형 높이와 경사를 미리보기 갱신 시 계산하고 캐시합니다.
// EN: Computes and caches decal terrain height and slope when rebuilding the preview.
using Game.Prefabs;
using Game.Simulation;
using Unity.Mathematics;
namespace CopyIt {
    public partial class CopyTool {
        private struct DecalHeightSampler:IHeightSampler {
            internal TerrainHeightData Data;
            public float Height(float3 p)=>TerrainUtils.SampleHeight(ref Data,p);
        }
        private void PrepareDecalPlacement(Item item){
            if(!item.Decal)return;
            var p=CalculatePosition(item);item.DecalPosition=p;
            var rotation=math.mul(quaternion.RotateY(math.radians(angle)),item.Transform.m_Rotation);
            var heading=math.forward(rotation);heading.y=0;
            rotation=quaternion.LookRotationSafe(math.normalizesafe(heading,new float3(0,0,1)),math.up());
            float2 lo=new float2(-.5f),hi=new float2(.5f);
            if(item.Container.HasValue&&EntityManager.HasComponent<MeshData>(item.Container.Value.m_Prefab)){
                var container=item.Container.Value;var b=EntityManager.GetComponentData<MeshData>(container.m_Prefab).m_Bounds;
                lo=math.min(b.min.xz*container.m_Scale.xz,b.max.xz*container.m_Scale.xz);
                hi=math.max(b.min.xz*container.m_Scale.xz,b.max.xz*container.m_Scale.xz);
            }else if(EntityManager.HasComponent<ObjectGeometryData>(item.Prefab)){
                var b=EntityManager.GetComponentData<ObjectGeometryData>(item.Prefab).m_Bounds;lo=b.min.xz;hi=b.max.xz;
            }
            var sampler=new DecalHeightSampler{Data=m_TerrainSystem.GetHeightData()};
            bool aligned=SlopeMath.TryAlign(ref sampler,p,rotation,lo,hi,out var fitted);
            item.DecalRotation=aligned?fitted:rotation;
            Diagnostics.Event("preview.decal.terrain",$"source={item.Source} position={p} aligned={aligned} rotation={item.DecalRotation}");
        }
    }
}
