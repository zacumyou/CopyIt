// KO: 원본 곡선이 새 도로 그리기 분할 과정에서 변형되지 않도록 NetCourse 공개 시점을 제어합니다.
// EN: Controls when NetCourse is exposed so the new-road splitter does not reshape copied curves.
using Game;
using Game.Common;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace CopyIt
{
    // Only Copy It owns these staged definitions. CourseSplitSystem (PostTool) is for
    // drawing new roads: it resamples height and splits segments. A copied course
    // already has authored topology. Publish it in Modification1 for native generation.
    public struct CopyNetworkCourse : IComponentData { public NetCourse Value; }

    public partial class CopyNetworkHideSystem : GameSystemBase
    {
        private EntityQuery query;
        protected override void OnCreate(){base.OnCreate();query=GetEntityQuery(ComponentType.ReadOnly<CopyNetworkCourse>(),ComponentType.ReadOnly<NetCourse>());}
        protected override void OnUpdate()
        {
            query.CompleteDependency();
            using(var entities=query.ToEntityArray(Allocator.Temp))
                foreach(var entity in entities)EntityManager.RemoveComponent<NetCourse>(entity);
        }
    }

    public partial class CopyNetworkPublishSystem : GameSystemBase
    {
        private EntityQuery query;
        protected override void OnCreate(){base.OnCreate();query=GetEntityQuery(ComponentType.ReadOnly<CopyNetworkCourse>(),ComponentType.ReadOnly<CreationDefinition>(),ComponentType.ReadOnly<Updated>(),ComponentType.Exclude<NetCourse>(),ComponentType.Exclude<Deleted>());}
        protected override void OnUpdate()
        {
            query.CompleteDependency();
            using(var entities=query.ToEntityArray(Allocator.Temp))
                foreach(var entity in entities)EntityManager.AddComponentData(entity,EntityManager.GetComponentData<CopyNetworkCourse>(entity).Value);
        }
    }
}
