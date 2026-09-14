// KO: 복제물만 표시 보호하며 삭제된 엔티티는 되살리지 않습니다. 보호 태그는 세이브에 저장됩니다.
// EN: Protects copies only and never revives deleted entities. The protection tag is serialized into the save.
// Override prevention and LOD refresh adapted from yenyang/Anarchy (MIT); see NOTICE.md.
using System;
using System.Linq;
using System.Reflection;
using Colossal.Serialization.Entities;
using Game;
using Game.Common;
using Game.Prefabs;
using Game.Rendering;
using Unity.Collections;
using Unity.Entities;
namespace CopyIt {
    // Persisted only on copies made by this mod; never revive Deleted objects.
    public struct CopyVisibilityProtected:IComponentData,IEmptySerializable {}
    public partial class CopyVisibilitySystem:GameSystemBase {
        private EntityQuery overridden,culled;
        private int frames;
        protected override void OnCreate(){
            base.OnCreate();
            overridden=GetEntityQuery(ComponentType.ReadOnly<CopyVisibilityProtected>(),ComponentType.ReadOnly<Overridden>(),ComponentType.Exclude<Deleted>());
            culled=GetEntityQuery(ComponentType.ReadOnly<CopyVisibilityProtected>(),ComponentType.ReadOnly<CullingInfo>(),ComponentType.Exclude<Game.Tools.Temp>(),ComponentType.Exclude<Deleted>());
        }
        protected override void OnUpdate(){
            if(!Mod.Ready)return;
            Dependency.Complete();overridden.CompleteDependency();
            if(!overridden.IsEmptyIgnoreFilter){
                using(var entities=overridden.ToEntityArray(Allocator.Temp)){
                    foreach(var e in entities){EntityManager.RemoveComponent<Overridden>(e);EntityManager.AddComponent<Updated>(e);EntityManager.AddComponent<BatchesUpdated>(e);}
                    Diagnostics.Event("visibility.restore",$"count={entities.Length}");
                }
            }
        }
        internal void RefreshCulled(){
            if(!Mod.Ready)return;
            if(++frames<60)return;frames=0;
            if(culled.IsEmptyIgnoreFilter)return;
            var camera=World.GetOrCreateSystemManaged<Game.Rendering.CameraUpdateSystem>();
            if(!camera.TryGetLODParameters(out var lod)||camera.activeCameraController==null)return;
            var rendering=World.GetOrCreateSystemManaged<RenderingSystem>();
            var batches=World.GetOrCreateSystemManaged<BatchDataSystem>();
            var parameters=RenderingUtils.CalculateLodParameters(batches.GetLevelOfDetail(rendering.frameLod,camera.activeCameraController),lod);
            culled.CompleteDependency();int count=0;
            using(var entities=culled.ToEntityArray(Allocator.Temp))foreach(var e in entities){
                var info=EntityManager.GetComponentData<CullingInfo>(e);
                if(info.m_PassedCulling!=0)continue;
                float distance=RenderingUtils.CalculateMinDistance(info.m_Bounds,lod.cameraPosition,camera.activeViewer.forward,parameters);
                if(RenderingUtils.CalculateLod(distance*distance,parameters)<=info.m_MinLod)continue;
                EntityManager.AddComponent<Updated>(e);count++;
            }
            if(count>0)Diagnostics.Event("visibility.refresh",$"count={count}");
        }
    }
    public partial class CopyVisibilityRefreshSystem:GameSystemBase {
        private CopyVisibilitySystem visibility;
        protected override void OnCreate(){base.OnCreate();visibility=World.GetOrCreateSystemManaged<CopyVisibilitySystem>();}
        protected override void OnUpdate(){visibility.RefreshCulled();}
    }
    public partial class CopyTool {
        private MethodInfo anarchyProtect;
        private bool checkedAnarchy;
        private void ProtectCopy(Entity entity,Item item){
            if(item.Network!=null||item.Surface!=null||EntityManager.HasComponent<BuildingData>(item.Prefab)||(!item.Decal&&!EntityManager.HasComponent<Game.Objects.Static>(entity)))return;
            bool added=!EntityManager.HasComponent<CopyVisibilityProtected>(entity);
            if(added)EntityManager.AddComponent<CopyVisibilityProtected>(entity);
            if(EntityManager.HasComponent<Overridden>(entity)){
                EntityManager.RemoveComponent<Overridden>(entity);EntityManager.AddComponent<Updated>(entity);Touch(entity);
            }
            if(added)Diagnostics.Event("visibility.protect",$"entity={entity} prefab={item.Prefab} decal={item.Decal}");
            if(EntityManager.HasComponent<Game.Tools.Temp>(entity))return;
            if(!checkedAnarchy){
                checkedAnarchy=true;
                var bridge=AppDomain.CurrentDomain.GetAssemblies().Select(a=>a.GetType("Anarchy.Bridge.AnarchyBridge",false)).FirstOrDefault(t=>t!=null);
                anarchyProtect=bridge?.GetMethod("TryAddAnarchyComponent",BindingFlags.Public|BindingFlags.Static,null,new[]{typeof(Entity)},null);
                Diagnostics.Event("visibility.anarchy.bridge",anarchyProtect==null?"unavailable; Copy It protection active":"available");
            }
            if(anarchyProtect!=null)try{
                var result=anarchyProtect.Invoke(null,new object[]{entity});Diagnostics.Event("visibility.anarchy.add",$"entity={entity} result={result}");
            }catch(Exception error){Diagnostics.Failure("visibility.anarchy.bridge.failed",error);anarchyProtect=null;}
        }
    }
}
