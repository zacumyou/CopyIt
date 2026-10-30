// KO: 선택→클립보드→임시 미리보기→확정 배치의 상태를 관리합니다. 원본 엔티티는 직접 복제/수정하지 않습니다.
// EN: Owns selection, clipboard and preview states. Explicit deletion uses the native bulldozer pipeline.
using System;
using System.Collections.Generic;
using System.Linq;
using Colossal.Collections;
using Game;
using Game.Common;
using Game.Input;
using Game.Objects;
using Game.Prefabs;
using Game.Rendering;
using Game.Simulation;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using EditorContainer=Game.Tools.EditorContainer;
using ObjectTransform = Game.Objects.Transform;

namespace CopyIt
{
    public partial class CopyTool : ObjectToolBaseSystem
    {
        internal const int Limit = 256;
        internal sealed class Item
        {
            internal Entity Source, Prefab;
            internal ObjectTransform Transform;
            internal RandomSeed Seed;
            internal Tree? Tree;
            internal PseudoRandomSeed? AppearanceSeed;
            internal CustomMeshColor[] Colors;
            internal Game.Areas.Node[] Surface; internal bool Decal; internal quaternion DecalRotation; internal float3 DecalPosition;
            internal NetworkSnapshot Network; internal EditorContainer? Container;
        }
        private readonly HashSet<Entity> selected = new HashSet<Entity>();
        private readonly HashSet<Entity> highlights = new HashSet<Entity>();
        private readonly List<Item> clipboard = new List<Item>();
        private readonly List<Entity> pending = new List<Entity>();
        private EntityQuery temps, netTemps, allTemps, sound;
        private bool shortcutRequested;
        private bool clearPreviousToolPreview;
        private float3 anchor, previewAnchor;
        private float minY;
        private bool previewExists, pressed, dirty, appliedLastFrame;
        private bool placeRequested;
        private int previewFrames;
        private Entity lastHover;
        private Vector2 pressPosition;
        private Entity pressEntity, hover;
        internal bool Copying {get; private set;}
        internal bool GroundSnap {get;private set;}
        internal bool Absolute {get; private set;}
        internal bool Compact {get; private set;}
        internal int SelectionMode {get; private set;}
        internal bool Box => SelectionMode==1;
        private readonly FilterClicks filterClicks=new FilterClicks();
        internal bool Eyedropper {get;private set;}
        private readonly AssetSamples<Entity> samples=new AssetSamples<Entity>();
        private readonly HashSet<Entity> sampleCandidates=new HashSet<Entity>();
        internal bool AssetFilterActive=>samples.Count>0;
        internal string AssetFilterName=>string.Join(" · ",samples.Entries.Select(p=>PrefabName(p.Item2!=Entity.Null?p.Item2:p.Item1)));
        private bool MatchesSample(Entity e)=>EntityManager.HasComponent<PrefabRef>(e)&&samples.Contains(EntityManager.GetComponentData<PrefabRef>(e).m_Prefab,EntityManager.HasComponent<EditorContainer>(e)?EntityManager.GetComponentData<EditorContainer>(e).m_Prefab:Entity.Null);
        private void ToggleEyedropper(){
            if(Copying)return;
            Eyedropper=!Eyedropper;
            if(Eyedropper&&sampleCandidates.Count==0)sampleCandidates.UnionWith(selected);
            ResetRegion();pressed=false;hover=Entity.Null;
            Status=Eyedropper?L10n.Message("CopyIt.Text.DC7E9F0280"):L10n.Message("CopyIt.Text.04673003E3");
        }

        internal int Filters {get; private set;} = 255;
        internal int Count => selected.Count;
        internal int ClipboardCount => clipboard.Count;
        internal bool Active => m_ToolSystem.activeTool == this;
        internal string Status {get; private set;} = L10n.Message("CopyIt.Text.F6EA647C2C");
        internal bool CanPlace {get; private set;}
        internal bool Dragging => pressed && Box;
        internal Vector2 DragStart => pressPosition;
        public override string toolID => "CopyIt";
        public override PrefabBase GetPrefab() => null;
        public override bool TrySetPrefab(PrefabBase prefab) => false;

        protected override void OnCreate()
        {
            base.OnCreate();
            RestoreToolSettings();
            temps = GetEntityQuery(ComponentType.ReadOnly<Temp>(),ComponentType.ReadOnly<ObjectTransform>(),ComponentType.ReadOnly<PrefabRef>(),ComponentType.Exclude<Deleted>(),ComponentType.Exclude<Owner>());
            netTemps=GetEntityQuery(ComponentType.ReadOnly<Temp>(),ComponentType.ReadOnly<Game.Net.Edge>(),ComponentType.ReadOnly<Game.Net.Curve>(),ComponentType.ReadOnly<PrefabRef>(),ComponentType.Exclude<Deleted>(),ComponentType.Exclude<Owner>());
            surfaceTemps=GetEntityQuery(ComponentType.ReadOnly<Temp>(),ComponentType.ReadOnly<Game.Areas.Area>(),ComponentType.ReadOnly<Game.Areas.Node>(),ComponentType.ReadOnly<PrefabRef>(),ComponentType.Exclude<Deleted>(),ComponentType.Exclude<Owner>());
            allTemps = GetEntityQuery(ComponentType.ReadOnly<Temp>(),ComponentType.Exclude<Deleted>());
            sound = GetEntityQuery(ComponentType.ReadOnly<ToolUXSoundSettingsData>());
        }
        internal void Activate()
        {
            if (!Mod.Ready) return;
            bool previousUnderground=m_ToolSystem.activeTool!=null&&m_ToolSystem.activeTool.requireUnderground;
            m_ToolSystem.activeTool = this;
            SetUnderground(previousUnderground);
        }
        internal void Deactivate()
        {
            if (Active) {var next=World.GetOrCreateSystemManaged<DefaultToolSystem>();next.SetUnderground(undergroundView);m_ToolSystem.activeTool=next;}
        }
        public override void InitializeRaycast()
        {
            base.InitializeRaycast();
            m_ToolRaycastSystem.typeMask = (Copying || (SelectionMode==2&&!Eyedropper)) ? TypeMask.Terrain : TypeMask.StaticObjects | TypeMask.Terrain | TypeMask.Net | TypeMask.Lanes | TypeMask.Areas;
            m_ToolRaycastSystem.areaTypeMask=(Eyedropper||(Filters&128)!=0)?Game.Areas.AreaTypeMask.Surfaces:Game.Areas.AreaTypeMask.None;
            m_ToolRaycastSystem.collisionMask = CollisionMask.OnGround | CollisionMask.Overground | CollisionMask.ExclusiveGround | (IncludeUnderground?CollisionMask.Underground:0);
            if(undergroundView&&!Copying&&!(SelectionMode==2&&!Eyedropper))m_ToolRaycastSystem.collisionMask=CollisionMask.Underground;
            m_ToolRaycastSystem.raycastFlags &= ~RaycastFlags.IgnoreSecondary;
            m_ToolRaycastSystem.raycastFlags |= RaycastFlags.Decals | RaycastFlags.Markers;
            m_ToolRaycastSystem.netLayerMask=Game.Net.Layer.TrainTrack|Game.Net.Layer.TramTrack|Game.Net.Layer.SubwayTrack|Game.Net.Layer.Road|Game.Net.Layer.Fence|Game.Net.Layer.Pathway|Game.Net.Layer.LaneEditor;
        }
        protected override void OnStartRunning()
        {
            applyActionOverride=Mod.Options.GetAction("Select");
            base.OnStartRunning();
            clearPreviousToolPreview=true;
            applyAction.shouldBeEnabled = true; cancelAction.shouldBeEnabled = true;

            Diagnostics.Event("tool.open");
        }
        protected override void OnStopRunning()
        {
            Dependency.Complete();

            try { ClearState(); Diagnostics.Event("tool.close"); }
            finally { base.OnStopRunning(); applyActionOverride=null; }
        }
// KO: 도구 종료는 ECB 허용 구간이 아닐 수 있으므로 자신의 정의만 동기 정리합니다.
        // EN: Tool shutdown may be outside the ECB window; synchronously clear owned definitions only.
        internal void ClearState()
        {
            bool ownsPreview=Copying || previewExists || Deleting;
            deleteStage=0;deleteTargets.Clear();
            ClearNetworkDefinitions();ClearSurfaceDefinitions();
            if (pending.Count > 0) VerifyPlacement();
            ResetGizmo();
            samples.Clear();sampleCandidates.Clear();filterClicks.Reset();Eyedropper=false;ResetRegion(); ResetRotation(); shortcutRequested=false;
            pressed=false; Copying=false; CanPlace=false; previewExists=false; appliedLastFrame=false;placeRequested=false;
            clipboard.Clear(); selected.Clear(); hover=Entity.Null; SyncHighlights();
            applyMode=ApplyMode.Clear;
            if(ownsPreview)
            {
                // ToolSystem stops the previous tool in MainLoop, BEFORE ToolUpdate
                // enables ToolOutputBarrier. Do not request an ECB or defer a global
                // definition query into the incoming tool's update.
                var definitions=GetDefinitionQuery();
                definitions.CompleteDependency();
                int count=definitions.CalculateEntityCount();
                EntityManager.DestroyEntity(definitions);
                Diagnostics.Event("preview.cleanup",$"immediate definitions={count}");
            }
        }
        internal void Command(string command)
        {
            Diagnostics.Event("ui.command",command);
            if(command!="buildings"&&command!="props"&&command!="trees"&&command!="roads"&&command!="paths"&&command!="netlanes"&&command!="decals"&&command!="surfaces")filterClicks.Reset();
            if(command=="toggle") { if(Active) Deactivate(); else Activate(); return; }
            if(!Active) return;
            if(Deleting && command!="compact")return;
            switch(command)
            {
                case "underground.include": SetUndergroundSelection(true);break;
                case "underground.exclude": SetUndergroundSelection(false);break;
                case "compact": Compact=!Compact; break;
                case "copy": BeginCopy(); break;
                case "delete": RequestDeletion();break;
                case "cancel": CancelCopy(); break;
                case "clear": if(!Copying) {sampleCandidates.Clear();Eyedropper=false;ResetRegion();selected.Clear(); SyncHighlights(); Status=L10n.Message("CopyIt.Text.9AC3A0BEBB");} break;
                case "height.snap": GroundSnap=true;Absolute=false;dirty=true;placeRequested=false;CanPlace=false;Diagnostics.Event("height.mode","terrain.per-object");break;
                case "height.terrain": GroundSnap=false;Absolute=false;dirty=true;placeRequested=false;CanPlace=false;Diagnostics.Event("height.mode","terrain.group.minimum");break;
                case "height": GroundSnap=false;Absolute=!Absolute; dirty=true; placeRequested=false;CanPlace=false; Diagnostics.Event("height.mode",Absolute?"absolute":"terrain.group.minimum"); break;
                case "click": SetSelectionMode(0); break;
                case "box": SetSelectionMode(1); break;
                case "polygon": SetSelectionMode(2); break;
                case "finish": FinishPolygon(); break;
                case "buildings": ToggleFilter(1); break;
                case "props": ToggleFilter(2); break;
                case "trees": ToggleFilter(4); break;
                case "paths": ToggleFilter(64); break;
                case "eyedropper": ToggleEyedropper();break;
                case "assetFilter.clear": samples.Clear();sampleCandidates.Clear();Eyedropper=false;ResetRegion();pressed=false;Status=L10n.Message("CopyIt.Text.22D93553CC");Diagnostics.Event("selection.assetFilter.clear");break;
                case "roads": ToggleFilter(8); break;
                case "netlanes": ToggleFilter(16); break;
                case "surfaces": ToggleFilter(128);break;
                case "decals": ToggleFilter(32); break;
            }
            SaveToolSettings();
        }
        private void RestoreToolSettings()
        {
            var options=Mod.Options;
            if(options==null)return;
            SelectionMode=options.LastSelectionMode;Filters=options.LastFilters;
            IncludeUnderground=options.LastIncludeUnderground;
            GroundSnap=options.LastHeightMode==2;Absolute=options.LastHeightMode==1;
            Compact=options.LastCompact;
            Status=SelectionMode==0?L10n.Message("CopyIt.Text.F6EA647C2C"):L10n.Message("CopyIt.Text.25331ED127");
        }
        // KO: 실제 설정 변경만 즉시 저장하여 정상 종료 여부와 관계없이 복원합니다.
        // EN: Save actual preference changes immediately, without relying on clean shutdown.
        private void SaveToolSettings()
        {
            var options=Mod.Options;
            if(options==null)return;
            int heightMode=GroundSnap?2:Absolute?1:0;
            if(options.LastSelectionMode==SelectionMode&&options.LastFilters==Filters&&
                options.LastIncludeUnderground==IncludeUnderground&&options.LastHeightMode==heightMode&&
                options.LastCompact==Compact)return;
            options.LastSelectionMode=SelectionMode;options.LastFilters=Filters;
            options.LastIncludeUnderground=IncludeUnderground;options.LastHeightMode=heightMode;
            options.LastCompact=Compact;
            try { options.ApplyAndSave(); }
            catch(Exception e) { Diagnostics.Failure("settings.save.failed",e); }
        }
        private void ToggleFilter(int bit)
        {
            if(Copying)return;
            Filters=filterClicks.Apply(Filters,bit,(double)System.Diagnostics.Stopwatch.GetTimestamp()/System.Diagnostics.Stopwatch.Frequency,255,out bool doubleClick);
            Diagnostics.Event(doubleClick?(Filters==bit?"filter.solo":"filter.except"):"filter.toggle",$"type={bit} filters={Filters}");
        }
        private void PlaySelectionSound()
        {
            if(sound.IsEmptyIgnoreFilter)return;
            var clip=sound.GetSingleton<ToolUXSoundSettingsData>().m_SelectEntitySound;
            if(clip==Entity.Null)return;
            World.GetOrCreateSystemManaged<Game.Audio.AudioManager>().PlayUISound(clip);
            Diagnostics.Event("selection.sound",$"source=eyedropper count=1 clip={clip}");
        }
        private bool Live(Entity e) => e!=Entity.Null && EntityManager.Exists(e) && !EntityManager.HasComponent<Deleted>(e) && !EntityManager.HasComponent<Temp>(e);
        private bool Eligible(Entity e,bool filter=true)
        {
            if(filter&&!MatchesView(e))return false;
            if(filter&&!IncludeUnderground&&IsUndergroundNetwork(e))return false;
            if(filter&&AssetFilterActive&&(!Live(e)||!MatchesSample(e)))return false;
            if(Live(e)&&!EntityManager.HasComponent<Owner>(e)&&EntityManager.HasComponent<ObjectTransform>(e)&&EntityManager.HasComponent<PrefabRef>(e)&&IsDecal(e))return !filter||(Filters&32)!=0;
            if(IsSurface(e))return !filter||(Filters&128)!=0;
            if(IsNetwork(e))return !filter||(Filters&NetworkKind(e))!=0;
            if(!Live(e) || !EntityManager.HasComponent<ObjectTransform>(e) || !EntityManager.HasComponent<Game.Objects.Static>(e) || !EntityManager.HasComponent<PrefabRef>(e) || EntityManager.HasComponent<Owner>(e)) return false;
            var prefab=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab;
            if(!EntityManager.Exists(prefab) || !EntityManager.HasComponent<ObjectData>(prefab) || EntityManager.HasComponent<NetObjectData>(prefab) || EntityManager.HasComponent<BuildingExtensionData>(prefab) || EntityManager.HasComponent<Attached>(e) || (EntityManager.HasComponent<Game.Tools.EditorContainer>(e)&&!IsDecal(e))) return false;
            int kind=IsDecal(e)?32:EntityManager.HasComponent<Game.Buildings.Building>(e)?1:EntityManager.HasComponent<Tree>(e)?4:2;
            return !filter || (Filters&kind)!=0;
        }
        private Entity Resolve(Entity entity,bool filter=true)
        {
            for(int i=0;i<16 && Live(entity);i++)
            {
                if(IsNetwork(entity))return Eligible(entity,filter)?entity:Entity.Null;
                if(EntityManager.HasComponent<Owner>(entity)) { entity=EntityManager.GetComponentData<Owner>(entity).m_Owner; continue; }
                return Eligible(entity,filter)?entity:Entity.Null;
            }
            return Entity.Null;
        }
        private void BeginCopy()
        {
            Eyedropper=false;
            if(Deleting || Copying || pressed || regionCommitting || polygonWorld.Count>0 || selected.Count==0) return;
            if(selected.Count(IsNetwork)>64){Status=L10n.Message("CopyIt.Text.08065C0267");Diagnostics.Event("copy.network.limit",selected.Count.ToString());return;}
            int surfaceVertices=0;
            foreach(var e in selected)if(IsSurface(e))surfaceVertices+=EntityManager.GetBuffer<Game.Areas.Node>(e,true).Length;
            if(surfaceVertices>8192){Status=L10n.Message("CopyIt.Text.EC86ED9694");Diagnostics.Event("copy.surface.limit",surfaceVertices.ToString());return;}
            clipboard.Clear();lastNetworkIssue="";
            foreach(var e in selected.OrderBy(e=>e.Index))
            {
                if(!Eligible(e,false)) { Status=L10n.Message("CopyIt.Text.9529A9C534"); clipboard.Clear(); Diagnostics.Event("copy.rejected",e.ToString()); return; }
                var item=new Item {Source=e, Prefab=EntityManager.GetComponentData<PrefabRef>(e).m_Prefab,
                    Transform=SnapshotTransform(e), Seed=RandomSeed.Next()};
                if(!math.all(math.isfinite(item.Transform.m_Position)) || !math.all(math.isfinite(item.Transform.m_Rotation.value)))
                {clipboard.Clear();Status=L10n.Message("CopyIt.Text.2EF99D6A15");Diagnostics.Event("copy.rejected",e+" non-finite transform");return;}
                if(EntityManager.HasComponent<Tree>(e)) item.Tree=EntityManager.GetComponentData<Tree>(e);
                if(EntityManager.HasComponent<PseudoRandomSeed>(e)) item.AppearanceSeed=EntityManager.GetComponentData<PseudoRandomSeed>(e);
                if(EntityManager.HasBuffer<CustomMeshColor>(e) && EntityManager.IsComponentEnabled<CustomMeshColor>(e))
                { using(var colors=EntityManager.GetBuffer<CustomMeshColor>(e,true).ToNativeArray(Allocator.Temp)) item.Colors=colors.ToArray(); }
                if(IsNetwork(e))item.Network=SnapshotNetwork(e);
                if(IsSurface(e))item.Surface=SnapshotSurface(e);
                item.Decal=item.Surface==null&&item.Network==null&&IsDecal(e);
                if(EntityManager.HasComponent<EditorContainer>(e)){item.Container=EntityManager.GetComponentData<EditorContainer>(e);if(!math.all(math.isfinite(item.Container.Value.m_Scale))||!math.isfinite(item.Container.Value.m_Intensity))throw new InvalidOperationException("Non-finite container scale/intensity");}
                clipboard.Add(item);
            }
            float3 lo=clipboard[0].Transform.m_Position, hi=lo;
            foreach(var item in clipboard) {lo=math.min(lo,item.Transform.m_Position);hi=math.max(hi,item.Transform.m_Position);}
            foreach(var item in clipboard)if(item.Network!=null)foreach(var p in CurvePoints(item.Network.Curve).Concat(new[]{item.Network.StartPosition,item.Network.EndPosition})){lo=math.min(lo,p);hi=math.max(hi,p);}
            foreach(var item in clipboard)if(item.Surface!=null)foreach(var node in item.Surface){lo=math.min(lo,node.m_Position);hi=math.max(hi,node.m_Position);}
            ResetRotation(); ResetRegion();
            minY=lo.y; anchor=(lo+hi)*0.5f; anchor.y=minY;
            GizmoBoundsMin=lo-anchor;GizmoBoundsMax=hi-anchor;
            Copying=true; dirty=true; previewExists=false; placeRequested=false;hover=Entity.Null; SyncHighlights();
            Status=L10n.Message("CopyIt.Text.6A4538F5AA");
            Diagnostics.Event("copy.begin",$"count={clipboard.Count} anchor={anchor} absolute={Absolute} groundSnap={GroundSnap}");
            foreach(var item in clipboard) Diagnostics.Event("copy.snapshot",$"source={item.Source} prefab={item.Prefab} name={PrefabName(item.Container?.m_Prefab??item.Prefab)} position={item.Transform.m_Position} rotation={item.Transform.m_Rotation}");
        }
        private string PrefabName(Entity prefab)
        {
            try {return World.GetOrCreateSystemManaged<PrefabSystem>().GetPrefab<PrefabBase>(prefab).name;}
            catch(Exception) {return prefab.ToString();}
        }
        private void CancelCopy()
        {
            if(!Copying) {Deactivate();return;}
            ResetGizmo(); ResetRotation();
            Copying=false; clipboard.Clear(); pressed=false; previewExists=false; CanPlace=false;placeRequested=false;
            ClearNetworkDefinitions();ClearSurfaceDefinitions();
            applyMode=ApplyMode.Clear; DestroyDefinitions(GetDefinitionQuery(),m_ToolOutputBarrier,default).Complete();
            Diagnostics.Event("copy.cancel"); Status=L10n.Message("CopyIt.Text.04673003E3");
        }
        protected override JobHandle OnUpdate(JobHandle deps)
        {
            deps.Complete();
            try { UpdateTool(); DrawSelectionBoundaries(); }
            catch(Exception error)
            {
                Diagnostics.Failure("tool.exception",error); ClearState(); Status=L10n.Message("CopyIt.Text.038C6B5369");
            }
            return default;
        }
        private void UpdateTool()
        {
            requireUnderground=undergroundView;
            applyMode=ApplyMode.None;
            // KO: 이전 도구의 호버용 Temp.Select를 첫 ToolUpdate에서 정리합니다.
            // EN: Clear the previous tool's hover temps in the first valid ToolUpdate phase.
            // Keep Clear for this whole frame; selection/preview work starts next frame.
            if(clearPreviousToolPreview)
            {
                DestroyDefinitions(GetDefinitionQuery(),m_ToolOutputBarrier,default).Complete();
                applyMode=ApplyMode.Clear;
                clearPreviousToolPreview=false;
                Diagnostics.Event("tool.previousPreview.clear");
                return;
            }
            if(UpdateDeletion())return;
            if(appliedLastFrame)
            {
                var gizmoCreated=GizmoHeld?pending.ToArray():null;
                bool verified=VerifyPlacement();if(verified&&GizmoHeld){GizmoCommits++;GizmoLastCreated=gizmoCreated;}appliedLastFrame=false;previewExists=false;dirty=true;
                if(!verified) {CancelCopy();Status=L10n.Message("CopyIt.Text.FC9DA44BE9");return;}
            }
            BeginGizmoIfRequested();
            if(GizmoHeld){UpdateGizmoPreview();return;}
            if(HandleCancel())return;
            if(!m_HasFocus) {ResetRegion();ResetRotationDrag();pressed=false; CanPlace=false;placeRequested=false;return;}
            if(shortcutRequested) {shortcutRequested=false;BeginCopy();}
            if(!Game.Input.InputManager.instance.controlOverWorld) {ResetRotationDrag(); if(pressed){pressed=false;ResetRegion();} CanPlace=false;placeRequested=false;return;}
            bool hit=GetRaycastResult(out Entity entity,out RaycastHit ray);
            if(Copying)
            {
                RefreshSlopedItState();
                HandleRotation();
                if(!hit) { if(previewExists) ClearPreview(); return; }
                var point=ray.m_HitPosition;


                var terrain=m_TerrainSystem.GetHeightData();
                point.y=TerrainUtils.SampleHeight(ref terrain,point);
                if(!math.all(math.isfinite(point))) { ClearPreview();return; }
                if(placeRequested) point=previewAnchor;
                if(!rotating && applyAction.WasPressedThisFrame()) placeRequested=true;
                if(dirty || !previewExists || math.distancesq(point,previewAnchor)>0.000001f)
                {
                    BuildPreview(point); return; // validate the generated preview on the next tool frame
                }
                previewFrames++;
                CanPlace=ReadyPreview(out var roots);
                if(placeRequested && previewFrames>=2)
                {
                    placeRequested=false;
                    if(!CanPlace) { Status=L10n.Message("CopyIt.Text.7A1D649994")+placementBlockReason; Diagnostics.Event("placement.rejected",Status);LogNetworkCandidates();PlaySound(false);return; }
                    pending.Clear();pending.AddRange(roots);
                    Diagnostics.Event("placement.begin",$"count={pending.Count} anchor={previewAnchor} absolute={Absolute} groundSnap={GroundSnap}");
                    foreach(var root in roots) Diagnostics.Event("placement.pending",$"entity={root} transform={SnapshotTransformForLog(root)}");
                    applyMode=ApplyMode.Apply;
                    ClearNetworkDefinitions();ClearSurfaceDefinitions();
                    DestroyDefinitions(GetDefinitionQuery(),m_ToolOutputBarrier,default).Complete();
                    appliedLastFrame=true;CanPlace=false;previewExists=false;
                }
                return;
            }
            UpdateSelection(hit,entity,ray);
        }
        private float GroundAt(float3 position)
        {
            var terrain=m_TerrainSystem.GetHeightData();
            return TerrainUtils.SampleHeight(ref terrain,position);
        }
        private float3 Position(Item item)=>item.Decal?item.DecalPosition:CalculatePosition(item);
        private float3 CalculatePosition(Item item)
        {
            if(GizmoHeld)return previewAnchor+math.mul(gizmoRotation,item.Transform.m_Position-anchor);
            var offset=SelectionMath.Rotate(item.Transform.m_Position.x-anchor.x,item.Transform.m_Position.z-anchor.z,angle);
            var position=new float3(previewAnchor.x+offset.X,0,previewAnchor.z+offset.Y);
            position.y=(GroundSnap||item.Decal)?GroundAt(position):PlacementMath.Height(item.Transform.m_Position.y,minY,previewAnchor.y,Absolute);
            return position;
        }
        internal bool PrepareDefinition(ref CreationDefinition creation,ref ObjectDefinition definition)
        {
            if(!previewExists)return false;
            foreach(var item in clipboard)
            {
                if(item.Network!=null||item.Surface!=null)continue;
                if(creation.m_Prefab!=item.Prefab || math.distancesq(definition.m_Position,Position(item))>=0.0001f)continue;
                // A nonnegative parent mesh emits Elevation without OnGround. GroundHeightSystem
                // then preserves this world position instead of resampling each object's footprint.
                definition.m_ParentMesh=0;
                if(GizmoHeld){definition.m_Rotation=Rotation(item);definition.m_LocalRotation=Rotation(item);}
                if(item.Decal){definition.m_Rotation=item.DecalRotation;definition.m_LocalRotation=item.DecalRotation;}
                if(item.Container.HasValue){creation.m_SubPrefab=item.Container.Value.m_Prefab;definition.m_Scale=item.Container.Value.m_Scale;definition.m_Intensity=item.Container.Value.m_Intensity;}
                if(item.AppearanceSeed.HasValue)creation.m_RandomSeed=item.AppearanceSeed.Value.m_Seed;
                if(item.Tree.HasValue)
                {
                    var tree=item.Tree.Value;
                    switch(tree.m_State & (TreeState.Teen|TreeState.Adult|TreeState.Elderly|TreeState.Dead))
                    {
                        case TreeState.Teen: definition.m_Age=0.1f+tree.m_Growth/1706.6666f;break;
                        case TreeState.Adult: definition.m_Age=0.25f+tree.m_Growth/731.4286f;break;
                        case TreeState.Elderly: definition.m_Age=0.6f+tree.m_Growth/731.4286f;break;
                        case TreeState.Dead: definition.m_Age=0.95f+tree.m_Growth/5120f;break;
                        default: definition.m_Age=tree.m_Growth/2560f;break;
                    }
                }
                return true;
            }
            return false;
        }
        private void BuildPreview(float3 point)
        {
            ClearNetworkDefinitions();ClearSurfaceDefinitions();
            DestroyDefinitions(GetDefinitionQuery(),m_ToolOutputBarrier,default).Complete();
            applyMode=ApplyMode.Clear; previewAnchor=point;CanPlace=false;
            previewFrames=0;
            var terrain=m_TerrainSystem.GetHeightData();
            var city=World.GetOrCreateSystemManaged<Game.City.CityConfigurationSystem>();
            foreach(var item in clipboard)
            {
                if(!EntityManager.Exists(item.Prefab)) throw new InvalidOperationException("Clipboard prefab unloaded");
                if(item.Network!=null){CreateNetworkDefinition(item);continue;}
                if(item.Surface!=null){CreateSurfaceDefinition(item);continue;}
                PrepareDecalPlacement(item);
                var position=Position(item);
                using(var points=new NativeList<ControlPoint>(1,Allocator.TempJob))
                {
                    points.Add(new ControlPoint { m_Position=position,m_HitPosition=position,m_Rotation=Rotation(item),
                        m_Elevation=position.y-TerrainUtils.SampleHeight(ref terrain,position) });
                    CreateDefinitions(item.Prefab,item.Container?.m_Prefab??Entity.Null,Entity.Null,Entity.Null,Entity.Null,Entity.Null,city.defaultTheme,
                        points,default,false,city.leftHandTraffic,false,false,0,0,1,0,0,item.Seed,Snap.Upright,Game.Tools.AgeMask.Mature,false,
                        overrides:default(PlacementOverrides),inputDeps:default).Complete();
                }
            }
            Diagnostics.Event(previewExists?"preview.update":"preview.begin",$"count={clipboard.Count} anchor={point} absolute={Absolute} groundSnap={GroundSnap}");
            previewExists=true;dirty=false;
        }
        private void ClearPreview()
        {
            ClearNetworkDefinitions();ClearSurfaceDefinitions();
            applyMode=ApplyMode.Clear; DestroyDefinitions(GetDefinitionQuery(),m_ToolOutputBarrier,default).Complete();
            previewExists=false; CanPlace=false;placeRequested=false; Diagnostics.Event("preview.clear","pointer has no valid terrain hit");
        }
// KO: 모든 루트와 기존 도시 변경 여부를 검증한 뒤에만 확정합니다.
        // EN: Commit only after every root and possible edits to the existing city are validated.
        private bool ReadyPreview(out List<Entity> roots)
        {
            placementBlockReason=L10n.Message("CopyIt.Text.20FA405019");
            roots=new List<Entity>(); temps.CompleteDependency();
            CacheNetworkPreview();
            using(var entities=temps.ToEntityArray(Allocator.Temp))
            {
                var used=new HashSet<Entity>();
                foreach(var item in clipboard)
                {
                    if(item.Network!=null){var net=FindNetworkPreview(item,used);if(net==Entity.Null)return false;used.Add(net);roots.Add(net);continue;}
                    if(item.Surface!=null){var surface=FindSurfacePreview(item,used);if(surface==Entity.Null)return false;used.Add(surface);roots.Add(surface);continue;}
                    Entity match=Entity.Null;
                    foreach(var entity in entities)
                    {
                        if(used.Contains(entity) || EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab!=item.Prefab)continue;
                        var temp=EntityManager.GetComponentData<Temp>(entity);
                        if(temp.m_Original!=Entity.Null || (temp.m_Flags&TempFlags.Create)==0)continue;
                        var transform=EntityManager.GetComponentData<ObjectTransform>(entity);
                        if(math.distancesq(transform.m_Position,Position(item))<0.0001f && math.abs(math.dot(transform.m_Rotation.value,Rotation(item).value))>0.9999f && ContainerMatches(entity,item)) {match=entity;break;}
                    }
                    if(match==Entity.Null) {placementBlockReason=L10n.Message("CopyIt.Text.B6CFF5C6A1");return false;}
                    used.Add(match); roots.Add(match);ApplyAppearance(match,item);
                }
                // Do not approve a native operation that would remove or relocate existing objects.
                allTemps.CompleteDependency();
                using(var everyTemp=allTemps.ToEntityArray(Allocator.Temp)) foreach(var entity in everyTemp)
                {
                    var t=EntityManager.GetComponentData<Temp>(entity);
                    if(previewNetworks.Length>0 && t.m_Original!=Entity.Null && (EntityManager.HasComponent<Game.Net.Edge>(entity)||EntityManager.HasComponent<Game.Net.Node>(entity)) && !UnchangedNetworkReference(entity,t))
                    {
                        placementBlockReason=L10n.Message("CopyIt.Text.F60301C3C7");
                        NetworkIssue($"existing network changed: entity={entity} original={t.m_Original} flags={t.m_Flags}");
                        return false;
                    }
                    if(t.m_Original!=Entity.Null && (t.m_Flags&(TempFlags.Delete|TempFlags.Modify|TempFlags.Replace))!=0) {placementBlockReason=L10n.Message("CopyIt.Text.F23EA9C557");return false;}
                }
            }
            m_ErrorQuery.CompleteDependency();
            if(!m_ErrorQuery.IsEmptyIgnoreFilter){placementBlockReason=L10n.Message("CopyIt.Text.8773C036EE");return false;}
            return roots.Count==clipboard.Count && ValidateNetworkTopology(roots,true);
        }
        private bool VerifyPlacement()
        {
            int applied=0, exactCount=0;
            for(int i=0;i<pending.Count;i++)
            {
                var entity=pending[i];
                if(!Live(entity)) {Diagnostics.Event("placement.unconfirmed",entity.ToString());continue;}
                if(i<clipboard.Count) ApplyAppearance(entity,clipboard[i]);
                applied++;
                if(i<clipboard.Count&&clipboard[i].Network!=null){bool valid=NetworkMatches(entity,clipboard[i]);if(valid)exactCount++;Diagnostics.Event(valid?"placement.network.created":"placement.network.mismatch",entity.ToString());continue;}
                if(i<clipboard.Count&&clipboard[i].Surface!=null){bool valid=SurfaceMatches(entity,clipboard[i]);if(valid)exactCount++;Diagnostics.Event(valid?"placement.surface.created":"placement.surface.mismatch",entity.ToString());continue;}
                var position=EntityManager.GetComponentData<ObjectTransform>(entity).m_Position;
                var transform=EntityManager.GetComponentData<ObjectTransform>(entity);
                bool exact=i<clipboard.Count && EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab==clipboard[i].Prefab && math.distancesq(position,Position(clipboard[i]))<0.0001f && math.abs(math.dot(transform.m_Rotation.value,Rotation(clipboard[i]).value))>0.9999f && ContainerMatches(entity,clipboard[i]);
                if(exact)exactCount++;
                Diagnostics.Event(exact?"placement.created":"placement.height.mismatch",$"entity={entity} position={position}");
            }
            Status=L10n.Message("CopyIt.Text.2FEE64A9B3", (applied), (pending.Count));
            bool success=applied==pending.Count && exactCount==pending.Count && ValidateNetworkTopology(pending,false);
            Diagnostics.Event(success?"placement.complete":"placement.incomplete",Status+" exact="+exactCount);pending.Clear();
            if(success)PlaySound(true);
            return success;
        }
        private void ApplyAppearance(Entity entity,Item item)
        {
            ProtectCopy(entity,item);
            bool changed=false;
            if(item.Tree.HasValue && EntityManager.HasComponent<Tree>(entity))
            {
                var tree=EntityManager.GetComponentData<Tree>(entity);
                if(tree.m_State!=item.Tree.Value.m_State || tree.m_Growth!=item.Tree.Value.m_Growth)
                {EntityManager.SetComponentData(entity,item.Tree.Value);changed=true;}
            }
            if(item.AppearanceSeed.HasValue && EntityManager.HasComponent<PseudoRandomSeed>(entity) && EntityManager.GetComponentData<PseudoRandomSeed>(entity).m_Seed!=item.AppearanceSeed.Value.m_Seed)
            {EntityManager.SetComponentData(entity,item.AppearanceSeed.Value);changed=true;}
            if(item.Colors!=null && EntityManager.HasBuffer<CustomMeshColor>(entity))
            {
                var colors=EntityManager.GetBuffer<CustomMeshColor>(entity);
                bool differs=colors.Length!=item.Colors.Length || !EntityManager.IsComponentEnabled<CustomMeshColor>(entity);
                if(!differs)for(int i=0;i<colors.Length;i++) if(!colors[i].Equals(item.Colors[i])) {differs=true;break;}
                if(differs) {colors.Clear();foreach(var color in item.Colors) colors.Add(color); EntityManager.SetComponentEnabled<CustomMeshColor>(entity,true);changed=true;}
            }
            if(changed)Touch(entity);
        }
        private void PlaySound(bool success)
        {
            if(sound.IsEmptyIgnoreFilter) return;
            var data=sound.GetSingleton<ToolUXSoundSettingsData>();
            bool buildings=clipboard.Any(item=>EntityManager.HasComponent<BuildingData>(item.Prefab));
            bool networks=clipboard.Any(item=>item.Network!=null);
            var clip=success?(buildings?data.m_PlaceBuildingSound:networks?data.m_NetBuildSound:data.m_PlacePropSound):data.m_PlaceBuildingFailSound;
            if(clip==Entity.Null)clip=success?data.m_PlacePropSound:data.m_PlaceBuildingFailSound;
            if(clip==Entity.Null){Diagnostics.Event("placement.sound.missing");return;}
            World.GetOrCreateSystemManaged<Game.Audio.AudioManager>().PlayUISound(clip);
            Diagnostics.Event("placement.sound",$"success={success} count=1 clip={clip} groupObjects={clipboard.Count}");
        }
        private void SyncHighlights()
        {
            var wanted=new HashSet<Entity>(selected);
            wanted.UnionWith(regionPreview);
            if(hover!=Entity.Null && !RegionActive) wanted.Add(hover);
            ExpandHighlightTargets(wanted);
            foreach(var e in highlights.ToArray())
                if(!wanted.Contains(e) || !Live(e))
                {
                    if(EntityManager.Exists(e)) {EntityManager.RemoveComponent<Highlighted>(e);Touch(e);}
                    highlights.Remove(e);
                }
            foreach(var e in wanted)
                if(Live(e) && !EntityManager.HasComponent<Highlighted>(e))
                {EntityManager.AddComponent<Highlighted>(e);Touch(e);highlights.Add(e);}
        }
        private void Touch(Entity e) { if(!EntityManager.HasComponent<BatchesUpdated>(e))EntityManager.AddComponent<BatchesUpdated>(e); }
    }
}







