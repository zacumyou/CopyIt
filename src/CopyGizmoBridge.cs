// Optional, versioned bridge. Gizmo XYZ owns input; Copy It retains snapshot,
// native preview validation, appearance preservation and batch commit ownership.
using Game.Tools;
using Unity.Mathematics;
using UnityEngine;
namespace CopyIt {
public partial class CopyTool {
    public int GizmoBridgeVersion=>1;
    public bool GizmoHeld {get;private set;}
    public float3 GizmoAnchor=>gizmoPoint;
    public float3 GizmoBoundsMin {get;private set;}
    public float3 GizmoBoundsMax {get;private set;}
    public quaternion GizmoRotation=>gizmoRotation;
    public bool GizmoReady=>GizmoHeld&&CanPlace&&!appliedLastFrame;
    public int GizmoCount=>clipboard.Count;
    public int GizmoCommits {get;private set;}
    public Unity.Entities.Entity[] GizmoLastCreated {get;private set;}=new Unity.Entities.Entity[0];
    public string GizmoStatus=>Status;
    private bool gizmoBeginRequested,gizmoCancelRequested,gizmoConfirmRequested,gizmoHasLast;
    private float3 gizmoPoint,gizmoLastPoint;
    private quaternion gizmoRotation=quaternion.identity,gizmoLastRotation;
    private float gizmoNextBuild;
    public bool RequestGizmo(){
        if(!Active||Copying||Deleting||selected.Count==0)return false;
        gizmoBeginRequested=true;Diagnostics.Event("gizmo.request",$"selected={selected.Count}");return true;
    }
    public bool SetGizmoTransform(float3 position,quaternion rotation){
        if(!GizmoHeld||appliedLastFrame||!math.all(math.isfinite(position))||!math.all(math.isfinite(rotation.value))||math.lengthsq(rotation.value)<.001f)return false;
        rotation=math.normalize(rotation);
        if(math.distancesq(position,gizmoPoint)>.000001f||math.abs(math.dot(rotation.value,gizmoRotation.value))<.9999999f){gizmoPoint=position;gizmoRotation=rotation;dirty=true;CanPlace=false;gizmoConfirmRequested=false;}
        return true;
    }
    public void ConfirmGizmo(){if(GizmoHeld&&!appliedLastFrame)gizmoConfirmRequested=true;}
    public void CancelGizmo(){if(GizmoHeld)gizmoCancelRequested=true;}
    private void ResetGizmo(){GizmoHeld=false;gizmoBeginRequested=gizmoCancelRequested=gizmoConfirmRequested=gizmoHasLast=false;gizmoRotation=quaternion.identity;}
    private void BeginGizmoIfRequested(){
        if(!gizmoBeginRequested)return;gizmoBeginRequested=false;BeginCopy();if(!Copying)return;
        GizmoHeld=true;gizmoPoint=anchor;gizmoRotation=quaternion.identity;gizmoNextBuild=0;GizmoCommits=0;gizmoHasLast=false;
        ChooseGizmoReference();
        Diagnostics.Event("gizmo.begin",$"count={clipboard.Count} pivot={anchor}");
    }
    private void UpdateGizmoPreview(){
        if(gizmoCancelRequested){CancelCopy();return;}
        var input=Game.Input.InputManager.instance;
        if(!Application.isFocused||input.hasInputFieldFocus){gizmoConfirmRequested=false;return;}
        // Escape can open the native overlay in the same frame; handle cancellation first.
        if(UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame==true){Diagnostics.Event("gizmo.cancel.escape","native tool phase");CancelCopy();return;}
        if(input.overlayActive){gizmoConfirmRequested=false;return;}
        if(dirty||!previewExists){if(UnityEngine.Time.unscaledTime>=gizmoNextBuild||gizmoConfirmRequested){BuildPreview(gizmoPoint);gizmoNextBuild=UnityEngine.Time.unscaledTime+1f/30f;}return;}
        previewFrames++;
        CanPlace=ReadyPreview(out var roots)&&previewFrames>=2;
        if(gizmoHasLast&&math.distancesq(gizmoPoint,gizmoLastPoint)<.0001f&&math.abs(math.dot(gizmoRotation.value,gizmoLastRotation.value))>.99999f)CanPlace=false;
        Status=CanPlace?L10n.Message("CopyIt.Text.0FD8A0731F", (clipboard.Count)):placementBlockReason;
        if(!gizmoConfirmRequested)return;gizmoConfirmRequested=false;
        if(!CanPlace){Diagnostics.Event("gizmo.rejected",Status);return;}
        pending.Clear();pending.AddRange(roots);
        applyMode=ApplyMode.Apply;ClearNetworkDefinitions();ClearSurfaceDefinitions();DestroyDefinitions(GetDefinitionQuery(),m_ToolOutputBarrier,default).Complete();
        appliedLastFrame=true;CanPlace=false;previewExists=false;gizmoLastPoint=gizmoPoint;gizmoLastRotation=gizmoRotation;gizmoHasLast=true;
        Diagnostics.Event("gizmo.commit",$"count={pending.Count} pivot={gizmoPoint} rotation={gizmoRotation.value}");
    }
}
}
