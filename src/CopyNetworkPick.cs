// KO: 일반 레이가 노드/레인을 반환할 때 화면상의 곡선 거리로 네트워크 선택을 보완합니다.
// EN: Falls back to projected curve distance when native picking returns a node or lane.
using Colossal.Collections;
using Colossal.Mathematics;
using Game.Common;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace CopyIt {
    public partial class CopyTool {
        // Native hits can resolve to junction nodes or generated lanes, rather than an edge.
        // Search only the ray's nearby network leaves, then compare the projected curve.
        private Entity PickSampleNetwork(Vector2 mouse){
            var camera=Camera.main;if(camera==null)return Entity.Null;
            var tree=World.GetOrCreateSystemManaged<Game.Net.SearchSystem>().GetNetSearchTree(true,out var dependency);dependency.Complete();
            var iterator=new SampleNetworkIterator{Tool=this,Camera=camera,Mouse=mouse,Ray=camera.ScreenPointToRay(mouse),Best=float.MaxValue};
            tree.Iterate(ref iterator);return iterator.Result;
        }
        private struct SampleNetworkIterator:INativeQuadTreeIterator<Entity,QuadTreeBoundsXZ>,IUnsafeQuadTreeIterator<Entity,QuadTreeBoundsXZ>{
            internal CopyTool Tool;internal Camera Camera;internal Vector2 Mouse;internal Ray Ray;internal float Best;internal Entity Result;
            public bool Intersect(QuadTreeBoundsXZ bounds){var b=bounds.m_Bounds;return new Bounds((b.min+b.max)*.5f,b.max-b.min+new float3(8)).IntersectRay(Ray);}
            public void Iterate(QuadTreeBoundsXZ bounds,Entity entity){
                if(!Tool.IsNetwork(entity)||(!Tool.IncludeUnderground&&Tool.IsUndergroundNetwork(entity)))return;
                var curve=Tool.EntityManager.GetComponentData<Game.Net.Curve>(entity).m_Bezier;
                var previous=Camera.WorldToScreenPoint(curve.a);
                float threshold=12f*Screen.height/1080f;
                for(int i=1;i<=32;i++){
                    var next=Camera.WorldToScreenPoint(MathUtils.Position(curve,i/32f));
                    if(previous.z>Camera.nearClipPlane&&next.z>Camera.nearClipPlane){
                        Vector2 a=previous,b=next,delta=b-a;
                        float t=delta.sqrMagnitude<.0001f?0:Mathf.Clamp01(Vector2.Dot(Mouse-a,delta)/delta.sqrMagnitude);
                        float distance=(Mouse-(a+t*delta)).sqrMagnitude;
                        if(distance<=threshold*threshold&&distance<Best){Best=distance;Result=entity;}
                    }
                    previous=next;
                }
            }
        }
    }
}
