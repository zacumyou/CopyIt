using System.Linq;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
namespace CopyIt {
public partial class CopyTool {
    // Additive bridge capability: old Gizmo clients still use the world-space bounds.
    public int GizmoReferenceVersion=>1;
    public quaternion GizmoReferenceRotation {get;private set;}=quaternion.identity;
    public float3 GizmoReferenceBoundsMin {get;private set;}
    public float3 GizmoReferenceBoundsMax {get;private set;}
    public string GizmoReferenceName=>gizmoReference==null?"":PrefabName(gizmoReference.Container?.m_Prefab??gizmoReference.Prefab);
    private Entity lastIndividualSelection;
    private Item gizmoReference;
    private bool IsReferenceEntity(Entity entity){
        if(!EntityManager.Exists(entity)||EntityManager.HasComponent<Game.Objects.Tree>(entity)||IsNetwork(entity)||IsSurface(entity)||!EntityManager.HasComponent<Game.Prefabs.PrefabRef>(entity))return false;
        var prefab=EntityManager.GetComponentData<Game.Prefabs.PrefabRef>(entity).m_Prefab;
        if(!EntityManager.HasComponent<Game.Prefabs.ObjectGeometryData>(prefab))return false;
        var source=EntityManager.HasComponent<Game.Tools.EditorContainer>(entity)?EntityManager.GetComponentData<Game.Tools.EditorContainer>(entity).m_Prefab:prefab;
        // Pure light/VFX editor containers are effects, not meaningful orientation anchors.
        return !(World.GetOrCreateSystemManaged<Game.Prefabs.PrefabSystem>().GetPrefab<Game.Prefabs.PrefabBase>(source) is Game.Prefabs.EffectPrefab);
    }
    private bool ReferenceCandidate(Item item)=>IsReferenceEntity(item.Source)&&math.all(math.abs(ReferenceCorner(item,7).xz-ReferenceCorner(item,0).xz)>.05f);
    private float3 ReferenceCorner(Item item,int i){
        var b=EntityManager.GetComponentData<Game.Prefabs.ObjectGeometryData>(item.Prefab).m_Bounds;
        var p=new float3((i&1)==0?b.min.x:b.max.x,(i&2)==0?b.min.y:b.max.y,(i&4)==0?b.min.z:b.max.z);
        if(item.Container.HasValue)p*=item.Container.Value.m_Scale;
        return p;
    }
    private bool FlatReference(Item item){
        var size=math.abs(ReferenceCorner(item,7)-ReferenceCorner(item,0));
        return ReferenceMath.IsFlat(size);
    }
    private void ChooseGizmoReference(){
        var candidates=clipboard.Where(ReferenceCandidate).ToList();
        var chosen=candidates.FirstOrDefault(i=>i.Source==lastIndividualSelection)??candidates.OrderBy(i=>FlatReference(i)?0:1).ThenBy(i=>math.distancesq(i.Transform.m_Position,anchor)).ThenBy(i=>i.Source.Index).FirstOrDefault();
        SetReference(chosen);
    }
    private void SetReference(Item item){
        gizmoReference=item;GizmoReferenceRotation=item?.Transform.m_Rotation??quaternion.identity;
        // Bounds in the initial reference frame; group transform remains identity on entry.
        var inverse=math.inverse(GizmoReferenceRotation);
        GizmoReferenceBoundsMin=GizmoReferenceBoundsMax=GizmoBoundsMin=GizmoBoundsMax=0;
        foreach(var source in clipboard){
            IncludeReferencePoint(source.Transform.m_Position,inverse);
            if(EntityManager.HasComponent<Game.Prefabs.ObjectGeometryData>(source.Prefab))for(int j=0;j<8;j++)IncludeReferencePoint(source.Transform.m_Position+math.mul(source.Transform.m_Rotation,ReferenceCorner(source,j)),inverse);
            if(source.Network!=null)foreach(var p in CurvePoints(source.Network.Curve).Concat(new[]{source.Network.StartPosition,source.Network.EndPosition}))IncludeReferencePoint(p,inverse);
            if(source.Surface!=null)foreach(var node in source.Surface)IncludeReferencePoint(node.m_Position,inverse);
        }
        Diagnostics.Event("gizmo.reference",$"source={item?.Source} name={GizmoReferenceName} rotation={GizmoReferenceRotation.value}");
    }
    private void IncludeReferencePoint(float3 world,quaternion inverse){
        var delta=world-anchor;var p=math.mul(inverse,delta);
        GizmoBoundsMin=math.min(GizmoBoundsMin,delta);GizmoBoundsMax=math.max(GizmoBoundsMax,delta);
        GizmoReferenceBoundsMin=math.min(GizmoReferenceBoundsMin,p);GizmoReferenceBoundsMax=math.max(GizmoReferenceBoundsMax,p);
    }
    public bool PickGizmoReference(float3 origin,float3 direction){
        if(!GizmoHeld||appliedLastFrame||!math.all(math.isfinite(origin))||!math.all(math.isfinite(direction)))return false;
        Item chosen=null;float best=float.MaxValue;int rank=int.MaxValue;
        foreach(var item in clipboard.Where(ReferenceCandidate)){
            var rotation=math.mul(gizmoRotation,item.Transform.m_Rotation);var inverse=math.inverse(rotation);
            var position=gizmoPoint+math.mul(gizmoRotation,item.Transform.m_Position-anchor);
            var a=ReferenceCorner(item,0);var b=ReferenceCorner(item,7);
            var extent=math.max(math.abs(b-a),new float3(.06f))*.5f;var center=(a+b)*.5f;
            if(!ReferenceMath.Hit(math.mul(inverse,origin-position),math.mul(inverse,direction),center-extent,center+extent,out var distance))continue;
            int candidateRank=FlatReference(item)?0:1;
            if(candidateRank<rank||candidateRank==rank&&(distance<best||distance==best&&(chosen==null||item.Source.Index<chosen.Source.Index))){chosen=item;best=distance;rank=candidateRank;}
        }
        if(chosen==null)return false;
        SetReference(chosen);return true;
    }
    public float3[] GizmoReferenceCorners {
        get {
            if(!GizmoHeld||gizmoReference==null)return new float3[0];
            var item=gizmoReference;var result=new float3[8];
            for(int i=0;i<8;i++)result[i]=gizmoPoint+math.mul(gizmoRotation,item.Transform.m_Position+math.mul(item.Transform.m_Rotation,ReferenceCorner(item,i))-anchor);
            return result;
        }
    }
}
}
