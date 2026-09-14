// KO: 네트워크의 곡선·노드·구성을 스냅샷하고 게임 생성 결과의 연결 관계를 검증합니다.
// EN: Snapshots network curves, nodes and composition, then verifies the generated topology.
using System;
using System.Collections.Generic;
using Colossal.Mathematics;
using Game.Common;
using Game.Prefabs;
using Game.Tools;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using EditorContainer=Game.Tools.EditorContainer;
using ObjectTransform=Game.Objects.Transform;
namespace CopyIt
{
    public partial class CopyTool
    {
        internal sealed class NetworkSnapshot
        {
            internal Bezier4x3 Curve;
            internal Entity Start,End; internal float3 StartPosition,EndPosition;
            internal Game.Net.Upgraded? Upgrade;
            internal bool PreserveRoadState;
            internal float2 Elevation,StartElevation,EndElevation;
        }
        private Entity[] previewNetworks=Array.Empty<Entity>();
        private string lastNetworkIssue="";
        private string placementBlockReason="미리보기 생성 대기";
        private void NetworkIssue(string reason){if(lastNetworkIssue==reason)return;lastNetworkIssue=reason;Diagnostics.Event("network.validation",reason);}
        private readonly List<Entity> networkDefinitions=new List<Entity>();
        private void ClearNetworkDefinitions(){foreach(var e in networkDefinitions)if(EntityManager.Exists(e))EntityManager.DestroyEntity(e);networkDefinitions.Clear();previewNetworks=Array.Empty<Entity>();}
        private void CacheNetworkPreview(){
            bool any=false;foreach(var item in clipboard)if(item.Network!=null){any=true;break;}
            if(!any){previewNetworks=Array.Empty<Entity>();return;}
            netTemps.CompleteDependency();using(var entities=netTemps.ToEntityArray(Allocator.Temp))previewNetworks=entities.ToArray();
        }
        private bool ContainerMatches(Entity entity,Item item){
            if(!item.Container.HasValue)return !EntityManager.HasComponent<EditorContainer>(entity);
            if(!EntityManager.HasComponent<EditorContainer>(entity))return false;
            var a=item.Container.Value;var b=EntityManager.GetComponentData<EditorContainer>(entity);
            return a.m_Prefab==b.m_Prefab&&math.distancesq(a.m_Scale,b.m_Scale)<.000001f&&math.abs(a.m_Intensity-b.m_Intensity)<.0001f;
        }
        private float3 SnapshotTransformForLog(Entity entity)=>EntityManager.HasComponent<Game.Areas.Area>(entity)&&EntityManager.HasBuffer<Game.Areas.Node>(entity)?SurfaceCenter(entity):EntityManager.HasComponent<Game.Net.Curve>(entity)?MathUtils.Position(EntityManager.GetComponentData<Game.Net.Curve>(entity).m_Bezier,.5f):EntityManager.GetComponentData<ObjectTransform>(entity).m_Position;
        private bool IsNetwork(Entity entity)
        {
            if(!Live(entity)||EntityManager.HasComponent<Owner>(entity)||!EntityManager.HasComponent<Game.Net.Edge>(entity)||!EntityManager.HasComponent<Game.Net.Curve>(entity)||!EntityManager.HasComponent<PrefabRef>(entity))return false;
            var edge=EntityManager.GetComponentData<Game.Net.Edge>(entity);
            if(!Live(edge.m_Start)||!Live(edge.m_End)||!EntityManager.HasComponent<Game.Net.Node>(edge.m_Start)||!EntityManager.HasComponent<Game.Net.Node>(edge.m_End))return false;
            var prefab=EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
            if(!EntityManager.HasComponent<NetData>(prefab))return false;
            if(EntityManager.HasComponent<EditorContainer>(entity))return EntityManager.HasComponent<NetLaneData>(EntityManager.GetComponentData<EditorContainer>(entity).m_Prefab);
            return EntityManager.HasComponent<RoadData>(prefab)||EntityManager.HasComponent<TrackData>(prefab)||EntityManager.HasComponent<PathwayData>(prefab);
        }
        private int NetworkKind(Entity entity)=>EntityManager.HasComponent<EditorContainer>(entity)?16:EntityManager.HasComponent<RoadData>(EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab)?8:64;
        private bool MeshIsDecal(Entity mesh)=>EntityManager.HasComponent<MeshData>(mesh)&&(EntityManager.GetComponentData<MeshData>(mesh).m_State&MeshFlags.Decal)!=0;
        private bool IsDecal(Entity entity)
        {
            if(EntityManager.HasComponent<EditorContainer>(entity))return MeshIsDecal(EntityManager.GetComponentData<EditorContainer>(entity).m_Prefab);
            if(!EntityManager.HasComponent<PrefabRef>(entity))return false;
            var prefab=EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab;
            if(MeshIsDecal(prefab))return true;
            if(!EntityManager.HasBuffer<SubMesh>(prefab))return false;
            var meshes=EntityManager.GetBuffer<SubMesh>(prefab,true);
            // A building with a decorative decal is still a building.
            if(EntityManager.HasComponent<BuildingData>(prefab)||EntityManager.HasComponent<TreeData>(prefab))return false;
            for(int i=0;i<meshes.Length;i++)if(MeshIsDecal(meshes[i].m_SubMesh))return true;
            return false;
        }
        private ObjectTransform SnapshotTransform(Entity entity)
        {
            if(IsSurface(entity))return new ObjectTransform(SurfaceCenter(entity),quaternion.identity);
            if(IsNetwork(entity))return new ObjectTransform(MathUtils.Position(EntityManager.GetComponentData<Game.Net.Curve>(entity).m_Bezier,.5f),quaternion.identity);
            return EntityManager.GetComponentData<ObjectTransform>(entity);
        }
        private NetworkSnapshot SnapshotNetwork(Entity entity)
        {
            var edge=EntityManager.GetComponentData<Game.Net.Edge>(entity);
            var curve=EntityManager.GetComponentData<Game.Net.Curve>(entity).m_Bezier;
            foreach(var p in CurvePoints(curve))if(!math.all(math.isfinite(p)))throw new InvalidOperationException("Non-finite network curve");
            if(MathUtils.Length(curve)<.05f)throw new InvalidOperationException("Degenerate network curve");

            var result=new NetworkSnapshot{Curve=curve,Start=edge.m_Start,End=edge.m_End,StartPosition=EntityManager.GetComponentData<Game.Net.Node>(edge.m_Start).m_Position,EndPosition=EntityManager.GetComponentData<Game.Net.Node>(edge.m_End).m_Position};
            if(!math.all(math.isfinite(result.StartPosition))||!math.all(math.isfinite(result.EndPosition)))throw new InvalidOperationException("Non-finite network node");
            // KO: 지상/고가/지하 속성은 월드 좌표와 별개입니다. 새 지형과의 차이로 덮어쓰지 않습니다.
            // EN: Ground/elevated/tunnel state is separate from world coordinates; never replace it with destination clearance.
            result.PreserveRoadState=EntityManager.HasComponent<RoadData>(EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab)&&!EntityManager.HasComponent<EditorContainer>(entity);
            if(result.PreserveRoadState){
                // Elevation is the native network state, not world Y minus terrain height.
                float2 State(Entity e)=>EntityManager.HasComponent<Game.Net.Elevation>(e)?EntityManager.GetComponentData<Game.Net.Elevation>(e).m_Elevation:float2.zero;
                result.Elevation=State(entity);result.StartElevation=State(edge.m_Start);result.EndElevation=State(edge.m_End);
                if(!math.all(math.isfinite(result.Elevation))||!math.all(math.isfinite(result.StartElevation))||!math.all(math.isfinite(result.EndElevation)))throw new InvalidOperationException("Non-finite road elevation state");
                Diagnostics.Event("copy.road.state",$"entity={entity} fixed=true elevation={result.Elevation} start={result.StartElevation} end={result.EndElevation}");
            }
            if(EntityManager.HasComponent<Game.Net.Upgraded>(entity))result.Upgrade=EntityManager.GetComponentData<Game.Net.Upgraded>(entity);
            Diagnostics.Event("copy.network.snapshot",$"entity={entity} start={result.Start} end={result.End} curve={curve} nodeStart={result.StartPosition} nodeEnd={result.EndPosition}");
            return result;
        }
        private static IEnumerable<float3> CurvePoints(Bezier4x3 curve){yield return curve.a;yield return curve.b;yield return curve.c;yield return curve.d;}
        private float3 MapPoint(float3 source)
        {
            var offset=SelectionMath.Rotate(source.x-anchor.x,source.z-anchor.z,angle);
            var point=new float3(previewAnchor.x+offset.X,PlacementMath.Height(source.y,minY,previewAnchor.y,Absolute),previewAnchor.z+offset.Y);
            if(GroundSnap)point.y=GroundAt(point);return point;
        }
        private float3 MapNode(Item item,bool start)=>MapPoint(start?item.Network.StartPosition:item.Network.EndPosition);
        private Bezier4x3 MappedCurve(Item item){
            var c=item.Network.Curve;var mapped=new Bezier4x3(MapPoint(c.a),MapPoint(c.b),MapPoint(c.c),MapPoint(c.d));
            if(GroundSnap){
                float h1=GroundAt(MathUtils.Position(mapped,1f/3f)),h2=GroundAt(MathUtils.Position(mapped,2f/3f));
                var heights=PlacementMath.FitTerrainCurve(mapped.a.y,h1,h2,mapped.d.y);
                mapped.b.y=heights.Item1;mapped.c.y=heights.Item2;
            }
            return mapped;
        }
        private Bezier4x3 GeneratedCurve(Item item)
        {
            // GenerateEdgesSystem canonicalizes endpoints, and can rebuild straight edges.
            var curve=MappedCurve(item);
            bool hasGeometry=EntityManager.HasComponent<NetGeometryData>(item.Prefab);
            var geometry=hasGeometry?EntityManager.GetComponentData<NetGeometryData>(item.Prefab):default;
            var start=MapNode(item,true);var end=MapNode(item,false);
            if(!hasGeometry||(geometry.m_Flags&Game.Net.GeometryFlags.StrictNodes)!=0){curve.a=start;curve.d=end;}
            else {curve.a.y=start.y;curve.d.y=end.y;}
            if((geometry.m_Flags&Game.Net.GeometryFlags.StraightEdges)!=0)return Game.Net.NetUtils.StraightCurve(curve.a,curve.d,geometry.m_Hanging);
            return curve;
        }
        private void CreateNetworkDefinition(Item item)
        {
            var curve=MappedCurve(item);var terrain=m_TerrainSystem.GetHeightData();
            float Elevation(float3 point)=>point.y-Game.Simulation.TerrainUtils.SampleHeight(ref terrain,point);
            var shared=CoursePosFlags.IsLeft|CoursePosFlags.IsRight|CoursePosFlags.FreeHeight;
            var course=new NetCourse{
                m_Curve=curve,m_Length=MathUtils.Length(curve),m_FixedIndex=-1,
                m_StartPosition=new CoursePos{m_Position=MapNode(item,true),m_Rotation=Game.Net.NetUtils.GetNodeRotation(math.normalizesafe(curve.b-curve.a,math.normalizesafe(curve.d-curve.a,new float3(0,0,1)))),m_CourseDelta=0,m_ParentMesh=0,m_Elevation=item.Network.PreserveRoadState?item.Network.StartElevation:new float2(Elevation(MapNode(item,true))),m_Flags=shared|CoursePosFlags.IsFirst},
                m_EndPosition=new CoursePos{m_Position=MapNode(item,false),m_Rotation=Game.Net.NetUtils.GetNodeRotation(math.normalizesafe(curve.d-curve.c,math.normalizesafe(curve.d-curve.a,new float3(0,0,1)))),m_CourseDelta=1,m_ParentMesh=0,m_Elevation=item.Network.PreserveRoadState?item.Network.EndElevation:new float2(Elevation(MapNode(item,false))),m_Flags=shared|CoursePosFlags.IsLast},
                m_Elevation=item.Network.PreserveRoadState?item.Network.Elevation:new float2(Elevation(curve.a),Elevation(curve.d))
            };
            // Course endpoint entities and all source/owner links remain null. Native generation
            // merges newly created equal-position endpoints; topology is checked before apply.
            var definition=EntityManager.CreateEntity(typeof(CreationDefinition),typeof(CopyNetworkCourse),typeof(Updated));
            networkDefinitions.Add(definition);
            EntityManager.SetComponentData(definition,new CreationDefinition{m_Prefab=item.Prefab,m_SubPrefab=item.Container?.m_Prefab??Entity.Null,m_RandomSeed=item.AppearanceSeed?.m_Seed??item.Seed.GetRandom(0).NextInt(),m_Flags=CreationFlags.SubElevation});
            EntityManager.SetComponentData(definition,new CopyNetworkCourse{Value=course});
            if(item.Network.Upgrade.HasValue)EntityManager.AddComponentData(definition,item.Network.Upgrade.Value);
        }
        private bool NetworkMatches(Entity entity,Item item)
        {
            if(!EntityManager.Exists(entity)||!EntityManager.HasComponent<Game.Net.Curve>(entity)||!EntityManager.HasComponent<Game.Net.Edge>(entity)||!EntityManager.HasComponent<PrefabRef>(entity)||EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab!=item.Prefab)return false;
            if(item.Container.HasValue&&(!EntityManager.HasComponent<EditorContainer>(entity)||EntityManager.GetComponentData<EditorContainer>(entity).m_Prefab!=item.Container.Value.m_Prefab))return false;
            if(!NetworkUpgradeMatches(entity,item))return false;
            if(item.Network.PreserveRoadState){
                var state=EntityManager.HasComponent<Game.Net.Elevation>(entity)?EntityManager.GetComponentData<Game.Net.Elevation>(entity).m_Elevation:float2.zero;
                if(math.any(math.abs(state-item.Network.Elevation)>.0001f))return false;
            }
            var a=EntityManager.GetComponentData<Game.Net.Curve>(entity).m_Bezier;var b=GeneratedCurve(item);
            return math.distancesq(a.a,b.a)<.0001f&&math.distancesq(a.b,b.b)<.0001f&&math.distancesq(a.c,b.c)<.0001f&&math.distancesq(a.d,b.d)<.0001f;
        }
        private Entity FindNetworkPreview(Item item,HashSet<Entity> used)
        {
            foreach(var e in previewNetworks){
                var temp=EntityManager.GetComponentData<Temp>(e);
                if(!used.Contains(e)&&temp.m_Original==Entity.Null&&(temp.m_Flags&TempFlags.Create)!=0&&NetworkMatches(e,item))return e;
            }
            placementBlockReason="도로 미리보기의 곡선·높이·업그레이드가 일치하지 않습니다.";
            NetworkIssue($"source={item.Source} prefab={PrefabName(item.Container?.m_Prefab??item.Prefab)} no exact new curve/height/upgrade match; temporaryEdges={previewNetworks.Length}");
            return Entity.Null;
        }
        private static string UpgradeText(CompositionFlags value)=>$"general={value.m_General};left={value.m_Left};right={value.m_Right}";
        private bool NetworkUpgradeMatches(Entity entity,Item item)
        {
            var expected=item.Network.Upgrade?.m_Flags??default(CompositionFlags);
            var actual=EntityManager.HasComponent<Game.Net.Upgraded>(entity)?EntityManager.GetComponentData<Game.Net.Upgraded>(entity).m_Flags:default(CompositionFlags);
            if(actual==expected)return true;
            // CompositionSelectSystem removes obsolete edge flags after evaluating the
            // new junction/elevation. Definitions still carry every source upgrade.
            // Accept native removal only; never accept flags absent from the source.
            return (actual&~expected)==default(CompositionFlags)
                &&EntityManager.HasComponent<Game.Net.Composition>(entity)
                &&EntityManager.GetComponentData<Game.Net.Composition>(entity).m_Edge!=Entity.Null;
        }
        private void LogNetworkCandidates()
        {
            foreach(var item in clipboard){
                if(item.Network==null)continue;
                var expected=GeneratedCurve(item);
                Diagnostics.Event("network.expected.upgrade",$"source={item.Source} flags={UpgradeText(item.Network.Upgrade?.m_Flags??default(CompositionFlags))}");
                Diagnostics.Event("network.expected",$"source={item.Source} prefab={item.Prefab} geometry={(EntityManager.HasComponent<NetGeometryData>(item.Prefab)?EntityManager.GetComponentData<NetGeometryData>(item.Prefab).m_Flags:0)} a={expected.a} b={expected.b} c={expected.c} d={expected.d}");
            }
            foreach(var entity in previewNetworks){
                if(!EntityManager.Exists(entity))continue;
                var curve=EntityManager.GetComponentData<Game.Net.Curve>(entity).m_Bezier;
                var edge=EntityManager.GetComponentData<Game.Net.Edge>(entity);var temp=EntityManager.GetComponentData<Temp>(entity);
                Diagnostics.Event("network.candidate.upgrade",$"entity={entity} flags={(EntityManager.HasComponent<Game.Net.Upgraded>(entity)?UpgradeText(EntityManager.GetComponentData<Game.Net.Upgraded>(entity).m_Flags):"none")}");
                Diagnostics.Event("network.candidate",$"entity={entity} prefab={EntityManager.GetComponentData<PrefabRef>(entity).m_Prefab} original={temp.m_Original} flags={temp.m_Flags} start={edge.m_Start} end={edge.m_End} a={curve.a} b={curve.b} c={curve.c} d={curve.d}");
            }
        }
        private bool ValidateNetworkTopology(List<Entity> roots,bool temporary)
        {
            var mapping=new NodeMapping<Entity>();
            var copiedEdges=new HashSet<Entity>(roots);
            for(int i=0;i<roots.Count;i++){
                var item=clipboard[i];if(item.Network==null)continue;
                if(!EntityManager.HasComponent<Game.Net.Edge>(roots[i]))return false;
                var edge=EntityManager.GetComponentData<Game.Net.Edge>(roots[i]);
                if(!Node(item.Network.Start,edge.m_Start,MapNode(item,true))||!Node(item.Network.End,edge.m_End,MapNode(item,false))){placementBlockReason="도로의 새 교차로 연결 관계가 원본과 다릅니다.";NetworkIssue($"source={item.Source} new-only topology mismatch");return false;}
            }
            return true;
            bool Node(Entity source,Entity target,float3 position){
                // NodeAlignSystem recomputes non-standalone junction positions from the new
                // connected curves. Curve matching and the bijection below constrain geometry.
                if(!EntityManager.HasComponent<Game.Net.Node>(target)||!math.all(math.isfinite(EntityManager.GetComponentData<Game.Net.Node>(target).m_Position)))return false;
                if(EntityManager.HasComponent<Game.Net.Standalone>(target)&&math.distancesq(EntityManager.GetComponentData<Game.Net.Node>(target).m_Position,position)>=.0001f)return false;
                if(target==source||!EntityManager.Exists(target)||EntityManager.HasComponent<Deleted>(target))return false;
                if(temporary&&(!EntityManager.HasComponent<Temp>(target)||EntityManager.GetComponentData<Temp>(target).m_Original!=Entity.Null||(EntityManager.GetComponentData<Temp>(target).m_Flags&TempFlags.Create)==0))return false;
                if(EntityManager.HasBuffer<Game.Net.ConnectedEdge>(target)){
                    var connected=EntityManager.GetBuffer<Game.Net.ConnectedEdge>(target,true);
                    for(int i=0;i<connected.Length;i++)if(!copiedEdges.Contains(connected[i].m_Edge)&&!EntityManager.HasComponent<Deleted>(connected[i].m_Edge))return false;
                }
                return mapping.Add(source,target);
            }
        }
    }
}



