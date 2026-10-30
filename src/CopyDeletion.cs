// KO: 게임 불도저의 정의 생성기를 호출하고 범위를 검증한 뒤 기본 적용 절차로 삭제합니다.
// EN: Use the native bulldozer definition generator, validate its scope, then apply natively.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;

namespace CopyIt
{
    public partial class CopyTool
    {
        private int deleteStage, deleteFrames;
        private readonly HashSet<Entity> deleteTargets = new HashSet<Entity>();
        private readonly HashSet<Entity> deleteDependents = new HashSet<Entity>();
        internal bool Deleting => deleteStage != 0;

        private void RequestDeletion()
        {
            if(Copying || Deleting || pressed || regionCommitting || polygonWorld.Count>0 || selected.Count==0)return;
            deleteTargets.Clear();
            foreach(var e in selected)
            {
                if(!Eligible(e,false) || EntityManager.HasComponent<Game.Objects.OutsideConnection>(e))
                {Status=L10n.Message("CopyIt.Text.32A203E169");deleteTargets.Clear();return;}
                deleteTargets.Add(e);
            }
            ResetRegion();Eyedropper=false;hover=Entity.Null;
            deleteStage=1;deleteFrames=0;Status=L10n.Message("CopyIt.Text.0EC08C7562", (deleteTargets.Count));
            Diagnostics.Event("deletion.request",$"count={deleteTargets.Count}");
        }

        // KO: UI 콜백에서는 예약만 합니다. ECB 사용은 ToolUpdate 안에서만 허용됩니다.
        // EN: UI only queues the request. Run command-buffer work exclusively in ToolUpdate.
        private bool UpdateDeletion()
        {
            if(!Deleting)return false;
            if(deleteStage==3)
            {
                int remaining=deleteTargets.Count(Live),total=deleteTargets.Count;
                if(remaining>0 && ++deleteFrames<8)return true;
                Status=remaining==0?L10n.Message("CopyIt.Text.E172DB15E6", (total)):L10n.Message("CopyIt.Text.B48672794D", (total-remaining), (total));
                Diagnostics.Event("deletion.result",$"requested={total} remaining={remaining}");
                if(remaining<total && !sound.IsEmptyIgnoreFilter)
                    World.GetOrCreateSystemManaged<Game.Audio.AudioManager>().PlayUISound(sound.GetSingleton<ToolUXSoundSettingsData>().m_BulldozeSound);
                selected.RemoveWhere(e=>!Live(e));sampleCandidates.Clear();SyncHighlights();
                deleteStage=0;deleteTargets.Clear();return true;
            }
            if(cancelAction.WasPressedThisFrame()){AbortDeletion(L10n.Message("CopyIt.Text.00E70F2681"));return true;}
            if(deleteTargets.Any(e=>!Live(e))){AbortDeletion(L10n.Message("CopyIt.Text.01C59A6A3A"));return true;}
            if(deleteStage==1)
            {
                CollectDeleteDependents();BuildDeleteDefinitions();deleteStage=2;deleteFrames=0;return true;
            }
            if(++deleteFrames<3)return true;
            var found=new HashSet<Entity>();
            using(var entities=allTemps.ToEntityArray(Allocator.Temp))
                foreach(var e in entities)
                {
                    var t=EntityManager.GetComponentData<Temp>(e);
                    if((t.m_Flags&TempFlags.Delete)==0)continue;
                    if(deleteTargets.Contains(t.m_Original))found.Add(t.m_Original);
                    else if(!AllowedDeleteChild(t.m_Original))
                    {Diagnostics.Event("deletion.outOfScope",$"temp={e} flags={t.m_Flags} "+DeleteEntityDetails(t.m_Original));AbortDeletion(L10n.Message("CopyIt.Text.606FC33F8B"));return true;}
                }
            if(found.Count!=deleteTargets.Count || !GetAllowApply())
            {Diagnostics.Event("deletion.validation",$"expected={deleteTargets.Count} found={found.Count} allowApply={GetAllowApply()}");AbortDeletion(L10n.Message("CopyIt.Text.23525559A3"));return true;}
            foreach(var e in deleteTargets)Diagnostics.Event("deletion.apply",e.ToString());
            applyMode=ApplyMode.Apply;
            DestroyDefinitions(GetDefinitionQuery(),m_ToolOutputBarrier,default).Complete();
            deleteStage=3;deleteFrames=0;Status=L10n.Message("CopyIt.Text.EC445FA6D5");return true;
        }

        private void CollectDeleteDependents()
        {
            EntityManager.CompleteAllTrackedJobs();
            deleteDependents.Clear();
            var queue=new List<Entity>(deleteTargets);
            // KO: Owner 역참조가 없는 내부 경로도 부모의 명시적 SubNet 목록으로 추적합니다.
            // EN: Internal paths may lack a reverse Owner; follow the parent's explicit child buffers.
            for(int cursor=0;cursor<queue.Count;cursor++)
            {
                if(cursor>131072)throw new InvalidOperationException("Deletion graph traversal limit exceeded");
                var e=queue[cursor];
                if(e==Entity.Null||!EntityManager.Exists(e)||!deleteDependents.Add(e))continue;
                if(deleteDependents.Count>32768)throw new InvalidOperationException("Deletion dependency graph exceeds 32768 entities");
                if(EntityManager.HasBuffer<Game.Net.SubNet>(e))
                    foreach(var item in EntityManager.GetBuffer<Game.Net.SubNet>(e,true))queue.Add(item.m_SubNet);
                if(EntityManager.HasBuffer<Game.Objects.SubObject>(e))
                    foreach(var item in EntityManager.GetBuffer<Game.Objects.SubObject>(e,true))queue.Add(item.m_SubObject);
                if(EntityManager.HasBuffer<Game.Areas.SubArea>(e))
                    foreach(var item in EntityManager.GetBuffer<Game.Areas.SubArea>(e,true))queue.Add(item.m_Area);
            }
            Diagnostics.Event("deletion.dependencies",$"roots={deleteTargets.Count} graph={deleteDependents.Count}");
            foreach(var e in deleteDependents)
                if(EntityManager.HasComponent<Game.Net.Edge>(e)||EntityManager.HasComponent<Game.Net.Node>(e))
                    Diagnostics.Event("deletion.dependentNetwork",DeleteEntityDetails(e));
        }

        private bool AllowedDeleteChild(Entity e) => DeletionScope.Allows(e,deleteDependents.Contains,
            x=>EntityManager.Exists(x)&&EntityManager.HasComponent<Owner>(x)?EntityManager.GetComponentData<Owner>(x).m_Owner:Entity.Null,
            IsDeletedJunction,IsTouchedJunction);

        private bool IsTouchedJunction(Entity node) => CheckJunction(node,false);
        private bool IsDeletedJunction(Entity node) => CheckJunction(node,true);
        private bool CheckJunction(Entity node,bool requireAll)
        {
            if(!EntityManager.Exists(node)||!EntityManager.HasComponent<Game.Net.Node>(node)||!EntityManager.HasBuffer<Game.Net.ConnectedEdge>(node))return false;
            bool found=false;
            foreach(var item in EntityManager.GetBuffer<Game.Net.ConnectedEdge>(node,true))
            {
                if(!EntityManager.Exists(item.m_Edge)||!EntityManager.HasComponent<Game.Net.Edge>(item.m_Edge))continue;
                var edge=EntityManager.GetComponentData<Game.Net.Edge>(item.m_Edge);
                // KO/EN: 간접 연결 항목은 고립 판단에서 제외 / Ignore entries which do not terminate at this node.
                if(edge.m_Start!=node&&edge.m_End!=node)continue;
                bool deleting=deleteDependents.Contains(item.m_Edge);
                if(requireAll&&!deleting)return false;
                if(deleting)found=true;
            }
            return found;
        }

        private string DeleteEntityDetails(Entity e)
        {
            string result="entity="+e;
            for(int i=0;i<16&&e!=Entity.Null&&EntityManager.Exists(e);i++)
            {
                result+=$" [level={i} entity={e} node={EntityManager.HasComponent<Game.Net.Node>(e)} edge={EntityManager.HasComponent<Game.Net.Edge>(e)}";
                if(EntityManager.HasComponent<PrefabRef>(e))result+=" prefab="+PrefabName(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab);
                result+="]";
                if(!EntityManager.HasComponent<Owner>(e))break;
                e=EntityManager.GetComponentData<Owner>(e).m_Owner;
            }
            return result;
        }
        private void AbortDeletion(string reason)
        {
            Diagnostics.Event("deletion.cancel",$"stage={deleteStage} frames={deleteFrames} reason={reason}");
            applyMode=ApplyMode.Clear;
            DestroyDefinitions(GetDefinitionQuery(),m_ToolOutputBarrier,default).Complete();
            deleteStage=0;deleteTargets.Clear();Status=reason;
        }

        private object DeleteComponentLookup<T>() where T:unmanaged,IComponentData => GetComponentLookup<T>(true);
        private object DeleteBufferLookup<T>() where T:unmanaged,IBufferElementData => GetBufferLookup<T>(true);

        private void BuildDeleteDefinitions()
        {
            EntityManager.CompleteAllTrackedJobs();
            // KO: 비공개 API는 Mod의 Game.dll 해시 검사 아래에서만 사용하며 모든 필드를 검사합니다.
            // EN: This private API is protected by Mod's exact Game.dll hash guard; reject unknown fields.
            var type=typeof(BulldozeToolSystem).GetNestedType("CreateDefinitionsJob",BindingFlags.NonPublic);
            if(type==null)throw new NotSupportedException("Native bulldozer generator unavailable");
            object job=Activator.CreateInstance(type);
            using(var points=new NativeList<ControlPoint>(deleteTargets.Count,Allocator.Temp))
            using(var commands=new EntityCommandBuffer(Allocator.Temp))
            {
                foreach(var e in deleteTargets)points.Add(new ControlPoint{m_OriginalEntity=e});
                foreach(var field in type.GetFields(BindingFlags.Public|BindingFlags.Instance))
                {
                    object value;
                    if(field.Name=="m_EditorMode")value=false;
                    else if(field.Name=="m_Mode")value=BulldozeToolSystem.Mode.MainElements;
                    else if(field.Name=="m_State")value=Enum.Parse(field.FieldType,"Default");
                    else if(field.Name=="m_ControlPoints")value=points;
                    else if(field.Name=="m_CommandBuffer")value=commands;
                    else if(field.FieldType.IsGenericType && (field.FieldType.GetGenericTypeDefinition()==typeof(ComponentLookup<>) || field.FieldType.GetGenericTypeDefinition()==typeof(BufferLookup<>)))
                    {
                        string method=field.FieldType.GetGenericTypeDefinition()==typeof(ComponentLookup<>)?nameof(DeleteComponentLookup):nameof(DeleteBufferLookup);
                        value=typeof(CopyTool).GetMethod(method,BindingFlags.NonPublic|BindingFlags.Instance).MakeGenericMethod(field.FieldType.GetGenericArguments()[0]).Invoke(this,null);
                    }
                    else throw new NotSupportedException("Unknown bulldozer field: "+field.Name);
                    field.SetValue(job,value);
                }
                type.GetMethod("Execute",Type.EmptyTypes).Invoke(job,null);
                // KO/EN: 생성 실패 시 명령 버퍼를 버려 원본을 보존 / Discard the buffer on generation failure.
                commands.Playback(EntityManager);
            }
            Diagnostics.Event("deletion.preview",$"count={deleteTargets.Count}");
        }
    }
}
