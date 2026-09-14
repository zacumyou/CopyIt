// KO: 게임이 임시 오브젝트를 생성하기 전에 Copy It의 루트 정의만 보정합니다.
// EN: Adjusts only Copy It root definitions before native temporary-object generation.
using Game;
using Game.Common;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace CopyIt
{
    // Runs after ToolOutputBarrier played the native creation jobs, before generation.
    // Only our clipboard roots are adjusted. Child definitions retain native ownership.
    public partial class CopyDefinitionSystem : GameSystemBase
    {
        private EntityQuery definitions;
        private CopyTool tool;
        protected override void OnCreate()
        {
            base.OnCreate();
            definitions=GetEntityQuery(ComponentType.ReadOnly<CreationDefinition>(),ComponentType.ReadWrite<ObjectDefinition>(),
                ComponentType.ReadOnly<Updated>(),ComponentType.Exclude<Deleted>());
            tool=World.GetOrCreateSystemManaged<CopyTool>();
        }
        protected override void OnUpdate()
        {
            if(!tool.Active || !tool.Copying) return;
            Dependency.Complete();definitions.CompleteDependency();
            using(var entities=definitions.ToEntityArray(Allocator.Temp))
                foreach(var entity in entities)
                {
                    var creation=EntityManager.GetComponentData<CreationDefinition>(entity);
                    if(creation.m_Original!=Entity.Null || creation.m_Owner!=Entity.Null || EntityManager.HasComponent<OwnerDefinition>(entity)) continue;
                    var definition=EntityManager.GetComponentData<ObjectDefinition>(entity);
                    if(!tool.PrepareDefinition(ref creation,ref definition))continue;
                    EntityManager.SetComponentData(entity,creation);
                    EntityManager.SetComponentData(entity,definition);
                }
        }
    }
}
