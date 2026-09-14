// KO: 공간 검색으로 후보를 좁히고 화면상의 기준점으로 선택합니다. 스포이드 목록과 유형 필터는 함께 적용합니다.
// EN: Prunes candidates with spatial trees and tests projected origins; asset and category filters apply together.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Game.Common;
using Game.Prefabs;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
using ObjectTransform=Game.Objects.Transform;
namespace CopyIt
{
    public partial class CopyTool
    {
        private readonly HashSet<Entity> regionPreview=new HashSet<Entity>();
        private readonly HashSet<Entity> loggedPreview=new HashSet<Entity>();
        private readonly List<float3> polygonWorld=new List<float3>();
        private readonly List<Point2> polygonScreen=new List<Point2>();
        private float nextRegionUpdate;
        private bool regionAdditive,regionCommitting;
        private Vector2 regionEnd;
        private float3 polygonCursor;
        private bool polygonCursorValid,polygonCloseSnapped;
        private Matrix4x4 lastRegionView;
        private Vector2 lastRegionMouse;
        private int lastRegionFilters=-1,lastRegionVertices=-1;
        internal bool RegionActive=>pressed&&Box||polygonWorld.Count>0||regionCommitting;
        internal int PolygonCount=>polygonWorld.Count;
        internal int PreviewCount=>regionPreview.Count;
        internal bool Searching=>false;
        protected override void OnDestroy(){ResetRegion();base.OnDestroy();}
        private void SetSelectionMode(int mode){if(Copying)return;Eyedropper=false;ResetRegion();pressed=false;hover=Entity.Null;SelectionMode=mode;Status=mode==0?"선택할 오브젝트를 클릭하세요.":"오브젝트 기준점이 영역 안에 있으면 선택합니다.";SyncHighlights();}
        private void ResetRegion()
        {
            regionPreview.Clear();loggedPreview.Clear();polygonWorld.Clear();polygonScreen.Clear();
            regionCommitting=false;polygonCursorValid=false;polygonCloseSnapped=false;nextRegionUpdate=0;lastRegionFilters=-1;lastRegionVertices=-1;
        }
        private void BeginRegion(){sampleCandidates.Clear();lastRegionFilters=-1;nextRegionUpdate=0;Diagnostics.Event("selection.region.begin",$"mode={SelectionMode} search=static-quadtree predicate=origin-inside");}
        private void ProjectPolygon(bool cursor)
        {
            polygonScreen.Clear();var camera=Camera.main;if(camera==null)return;
            foreach(var point in polygonWorld){var p=camera.WorldToScreenPoint(point);if(p.z<=0){polygonScreen.Clear();return;}polygonScreen.Add(new Point2(p.x,p.y));}
            if(cursor&&polygonCursorValid&&!polygonCloseSnapped){var p=camera.WorldToScreenPoint(polygonCursor);if(p.z>0)polygonScreen.Add(new Point2(p.x,p.y));}
        }
        private bool EvaluateRegion()
        {
            var camera=Camera.main;if(camera==null)return false;
            regionPreview.Clear();
            var rect=new Rect2(pressPosition.x,pressPosition.y,regionEnd.x,regionEnd.y);
            if(SelectionMode==2){
                ProjectPolygon(!regionCommitting);
                if(polygonScreen.Count<3){SyncHighlights();return true;}
                rect=new Rect2(polygonScreen.Min(p=>p.X),polygonScreen.Min(p=>p.Y),polygonScreen.Max(p=>p.X),polygonScreen.Max(p=>p.Y));
            }
            var clock=Stopwatch.StartNew();
            var search=World.GetOrCreateSystemManaged<Game.Objects.SearchSystem>();
            var tree=search.GetStaticSearchTree(true,out var dependency);dependency.Complete();
            var iterator=new RegionIterator(this,camera,rect);
            tree.Iterate(ref iterator);
            if((Filters&88)!=0){
                var networks=World.GetOrCreateSystemManaged<Game.Net.SearchSystem>().GetNetSearchTree(true,out var netDependency);
                netDependency.Complete();networks.Iterate(ref iterator);
            }
            if((Filters&128)!=0){
                var surfaces=World.GetOrCreateSystemManaged<Game.Areas.SearchSystem>().GetSearchTree(true,out var surfaceDependency);surfaceDependency.Complete();
                var areaIterator=new SurfaceRegionIterator{Region=iterator,Seen=new HashSet<Entity>()};surfaces.Iterate(ref areaIterator);iterator=areaIterator.Region;
            }
            // No structural ECS writes until traversal ends.
            SyncHighlights();
            if(regionCommitting||!loggedPreview.SetEquals(regionPreview)){
                loggedPreview.Clear();loggedPreview.UnionWith(regionPreview);
                Diagnostics.Event("selection.region.preview",$"mode={SelectionMode} count={regionPreview.Count} nodes={iterator.Nodes} leaves={iterator.Leaves} ms={clock.Elapsed.TotalMilliseconds:F3} predicate=origin-inside entities={string.Join(",",regionPreview)}");
            }
            return true;
        }
        private struct RegionIterator : Colossal.Collections.INativeQuadTreeIterator<Entity,QuadTreeBoundsXZ>, Colossal.Collections.IUnsafeQuadTreeIterator<Entity,QuadTreeBoundsXZ>
        {
            private CopyTool tool;
            private Camera camera;
            private Rect2 rectangle;
            private SelectionPlane left,right,bottom,top,near,far;
            internal int Nodes,Leaves;
            internal RegionIterator(CopyTool owner,Camera view,Rect2 rect){
                tool=owner;camera=view;rectangle=rect;Nodes=Leaves=0;
                var matrix=view.projectionMatrix*view.worldToCameraMatrix;
                var x=matrix.GetRow(0);var y=matrix.GetRow(1);var w=matrix.GetRow(3);
                left=Plane(x-w*(rect.Left/Screen.width*2-1));right=Plane(w*(rect.Right/Screen.width*2-1)-x);
                bottom=Plane(y-w*(rect.Bottom/Screen.height*2-1));top=Plane(w*(rect.Top/Screen.height*2-1)-y);
                var forward=view.transform.forward;var position=view.transform.position;
                near=new SelectionPlane(forward.x,forward.y,forward.z,-Vector3.Dot(forward,position)-view.nearClipPlane);
                far=new SelectionPlane(-forward.x,-forward.y,-forward.z,Vector3.Dot(forward,position)+view.farClipPlane);
            }
            private static SelectionPlane Plane(Vector4 p)=>new SelectionPlane(p.x,p.y,p.z,p.w);
            private static bool PlaneOverlaps(SelectionPlane p,float3 c,float3 e)=>p.Overlaps(c.x,c.y,c.z,e.x,e.y,e.z);
            public bool Intersect(QuadTreeBoundsXZ bounds){
                Nodes++;
                if(tool.regionPreview.Count+(tool.regionAdditive?tool.selected.Count:0)>=Limit)return false;
                var center=(bounds.m_Bounds.min+bounds.m_Bounds.max)*0.5f;
                var extent=(bounds.m_Bounds.max-bounds.m_Bounds.min)*0.5f;
                return PlaneOverlaps(left,center,extent)&&PlaneOverlaps(right,center,extent)&&PlaneOverlaps(bottom,center,extent)&&PlaneOverlaps(top,center,extent)&&PlaneOverlaps(near,center,extent)&&PlaneOverlaps(far,center,extent);
            }
            public void Iterate(QuadTreeBoundsXZ bounds,Entity entity){
                Leaves++;
                if(tool.regionPreview.Count+(tool.regionAdditive?tool.selected.Count:0)>=Limit||!tool.Live(entity))return;
                bool network=tool.IsNetwork(entity);
                if(!network&&!tool.IsSurface(entity)&&!tool.EntityManager.HasComponent<ObjectTransform>(entity))return;
                var world=tool.SnapshotTransform(entity).m_Position;
                var screen=camera.WorldToScreenPoint(world);
                if(screen.z<camera.nearClipPlane||screen.z>camera.farClipPlane)return;
                var point=new Point2(screen.x,screen.y);
                if(!rectangle.Contains(point)||(tool.SelectionMode==2&&!SelectionMath.Contains(tool.polygonScreen,point)))return;
                if(tool.regionAdditive&&tool.selected.Contains(entity))return;
                if(tool.Eligible(entity))tool.regionPreview.Add(entity);
            }
        }
        private void CommitRegion()
        {
            if(!EvaluateRegion())return;if(!regionAdditive)selected.Clear();
            foreach(var entity in regionPreview)if(Eligible(entity)&&selected.Count<Limit)selected.Add(entity);
            Diagnostics.Event("selection.region.commit",$"mode={SelectionMode} count={selected.Count}");
            ResetRegion();pressed=false;hover=Entity.Null;SyncHighlights();Status=$"{selected.Count}개 선택 · Ctrl+C 복사";
        }
        internal void FinishPolygon()
        {
            if(SelectionMode!=2||polygonWorld.Count<3||regionCommitting)return;
            ProjectPolygon(false);
            if(!SelectionMath.SimplePolygon(polygonScreen)){Status="선이 교차하지 않는 다각형으로 그려주세요.";Diagnostics.Event("polygon.invalid");return;}
            regionCommitting=true;polygonCursorValid=false;
            CommitRegion();
        }
        private void UpdateSelection(bool hit,Entity entity,RaycastHit ray)
        {
            selected.RemoveWhere(e=>!Eligible(e,false));
            var mouse=(Vector2)Game.Input.InputManager.instance.mousePosition;
            bool shift=Keyboard.current!=null&&Keyboard.current.shiftKey.isPressed;
            if(Eyedropper){
                hover=hit?Resolve(ray.m_HitEntity,false):Entity.Null;if(hover==Entity.Null&&hit)hover=Resolve(entity,false);
                if(!IncludeUnderground&&IsUndergroundNetwork(hover))hover=Entity.Null;
                var decal=PickDecal(mouse,true);if(decal!=Entity.Null)hover=decal;
                if(hover==Entity.Null)hover=PickSampleNetwork(mouse);
                if(applyAction.WasPressedThisFrame()&&hover!=Entity.Null){
                    var prefab=EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(hover).m_Prefab;
                    var sub=EntityManager.HasComponent<Game.Tools.EditorContainer>(hover)?EntityManager.GetComponentData<Game.Tools.EditorContainer>(hover).m_Prefab:Entity.Null;
                    bool added=samples.Add(prefab,sub);
                    if(sampleCandidates.Count>0){
                        selected.Clear();foreach(var candidate in sampleCandidates)if(Eligible(candidate)&&selected.Count<Limit)selected.Add(candidate);
                    }
                    PlaySelectionSound();Status=$"에셋 {samples.Count}종 지정 · 계속 추가하거나 선택 방식 버튼으로 완료";
                    Diagnostics.Event("selection.eyedropper",$"added={added} prefab={prefab} subPrefab={sub} assets={samples.Count} selected={selected.Count}");
                }
                SyncHighlights();return;
            }

            if(regionCommitting){CommitRegion();return;}
            if(SelectionMode==2)
            {
                polygonCursorValid=hit;polygonCursor=ray.m_HitPosition;
                if(hit){var terrain=m_TerrainSystem.GetHeightData();polygonCursor.y=Game.Simulation.TerrainUtils.SampleHeight(ref terrain,polygonCursor);}
                var first=polygonWorld.Count>0&&Camera.main!=null?Camera.main.WorldToScreenPoint(polygonWorld[0]):Vector3.zero;
                // Tight entry radius, slightly wider release radius prevents edge flicker.
                float snapRadius=(polygonCloseSnapped?12f:8f)*Screen.height/1080f;
                bool snap=hit&&polygonWorld.Count>=3&&first.z>0&&Vector2.Distance(mouse,(Vector2)first)<=snapRadius;
                if(snap!=polygonCloseSnapped)Diagnostics.Event("polygon.close.snap",snap?"enter":"leave");
                polygonCloseSnapped=snap;
                if(snap)polygonCursor=polygonWorld[0];
                if(applyAction.WasPressedThisFrame()&&hit)
                {
                    if(polygonCloseSnapped){FinishPolygon();return;}
                    if(polygonWorld.Count==0){BeginRegion();regionAdditive=shift;}
                    if(polygonWorld.Count<32){polygonWorld.Add(polygonCursor);Diagnostics.Event("polygon.vertex",polygonCursor.ToString());}
                    else Status="최대 32개 꼭짓점입니다. 첫 점을 클릭하거나 Enter로 마치세요.";
                }
                if(Keyboard.current!=null&&Keyboard.current.enterKey.wasPressedThisFrame){FinishPolygon();return;}
            }
            else
            {
                hover=hit?Resolve(ray.m_HitEntity):Entity.Null;if(hover==Entity.Null&&hit)hover=Resolve(entity);
                if(!pressed){var decal=PickDecal(mouse);if(decal!=Entity.Null)hover=decal;}
                if(hover!=lastHover){Diagnostics.Event("selection.hover",hover.ToString());lastHover=hover;}
                if(applyAction.WasPressedThisFrame())
                {sampleCandidates.Clear();pressed=true;pressPosition=mouse;pressEntity=hover;regionAdditive=shift;if(Box)BeginRegion();}
                if(pressed){regionEnd=mouse;}
                if(pressed&&applyAction.WasReleasedThisFrame())
                {
                    if(Box&&Vector2.Distance(mouse,pressPosition)>5){pressed=false;regionCommitting=true;CommitRegion();return;}
                    pressed=false;if(!shift)selected.Clear();
                    if(Eligible(pressEntity)){if(shift&&selected.Contains(pressEntity))selected.Remove(pressEntity);else if(selected.Count<Limit)selected.Add(pressEntity);}
                    ResetRegion();Status=$"{selected.Count}개 선택 · Ctrl+C 복사";Diagnostics.Event("selection.changed",string.Join(",",selected));
                }
            }
            if(RegionActive)
            {
                var camera=Camera.main;
                if(camera!=null){
                    var view=camera.projectionMatrix*camera.worldToCameraMatrix;
                    bool changed=lastRegionFilters!=Filters||lastRegionVertices!=polygonWorld.Count||lastRegionView!=view||lastRegionMouse!=mouse;
                    if(changed&&UnityEngine.Time.realtimeSinceStartup>=nextRegionUpdate&&EvaluateRegion()){
                        lastRegionView=view;lastRegionMouse=mouse;lastRegionFilters=Filters;lastRegionVertices=polygonWorld.Count;
                        nextRegionUpdate=UnityEngine.Time.realtimeSinceStartup+1f/30f;
                    }
                }
            }
            SyncHighlights();
        }
        internal string PolygonPath()
        {
            if(SelectionMode!=2||polygonWorld.Count==0)return "";
            ProjectPolygon(!regionCommitting);
            return string.Join(";",polygonScreen.Select(p=>string.Format(CultureInfo.InvariantCulture,"{0},{1}",p.X/Screen.width*100,(Screen.height-p.Y)/Screen.height*100)));
        }
    }
}



