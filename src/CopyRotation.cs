// KO: 그룹 중심은 커서를 따라가며 회전은 원본 자세에 더해집니다. 입력 누적과 실제 배치값을 분리합니다.
// EN: The group pivot follows the cursor; rotation is relative to the source pose. Input accumulation is separate from placement.
using Game.Input;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.InputSystem;
namespace CopyIt
{
    public partial class CopyTool
    {
        private float angle,dragStartAngle,dragStartX;
        private bool rotating;

        internal float Angle=>angle;
        internal void Shortcut()
        {
            if(GizmoHeld)return;
            Diagnostics.Event("shortcut.copy",Active?"copy selection":"enter selection tool");
            if(!Active)Activate();else shortcutRequested=true;
        }
        private quaternion Rotation(Item item)=>item.Decal?item.DecalRotation:math.mul(GizmoHeld?gizmoRotation:quaternion.RotateY(math.radians(angle)),item.Transform.m_Rotation);
        private void ResetRotation(){ResetRotationDrag();angle=0;}
        private void ResetRotationDrag(){if(rotating){Diagnostics.Event("rotation.drag.end",angle.ToString());}rotating=false;}
        private void SetAngle(float value,string input)
        {
            value=SelectionMath.NormalizeAngle(value);
            if(math.abs(value-angle)<0.0001f)return;
            angle=value;dirty=true;placeRequested=false;CanPlace=false;
            Diagnostics.Event("rotation.changed",$"input={input} degrees={angle}");
        }
        private void HandleRotation()
        {
            var mouse=Mouse.current;if(mouse==null)return;
            if(mouse.rightButton.wasPressedThisFrame && previewExists)
            {rotating=true;dragStartAngle=angle;dragStartX=InputManager.instance.mousePosition.x;placeRequested=false;Diagnostics.Event("rotation.drag.begin",angle.ToString());}
            if(rotating)
            {
                // Yaw follows the right drag while the group center follows the terrain cursor.
                SetAngle(dragStartAngle+(InputManager.instance.mousePosition.x-dragStartX)*1080f/Mathf.Max(1,Screen.height)*0.4f,"right-drag");
                if(mouse.rightButton.wasReleasedThisFrame || !mouse.rightButton.isPressed)ResetRotationDrag();
            }
        }
        private bool HandleCancel()
        {
            if(!UnityEngine.Application.isFocused || InputManager.instance.hasInputFieldFocus)return false;
            bool escape=Keyboard.current!=null&&Keyboard.current.escapeKey.wasPressedThisFrame;
            if(Copying){if(escape){CancelCopy();return true;}return false;}
            if(escape || cancelAction.WasPressedThisFrame())
            {
                if(Eyedropper){Eyedropper=false;Status=L10n.Message("CopyIt.Text.BAD401D904");return true;}
                if(polygonWorld.Count>0)
                {if(escape)ResetRegion();else {polygonWorld.RemoveAt(polygonWorld.Count-1);regionPreview.Clear();if(polygonWorld.Count==0)ResetRegion();}SyncHighlights();Diagnostics.Event("polygon.cancel",escape?"all":"last vertex");return true;}
                if(pressed || regionCommitting){pressed=false;ResetRegion();SyncHighlights();return true;}
                if(!escape){sampleCandidates.Clear();selected.Clear();hover=Unity.Entities.Entity.Null;ResetRegion();SyncHighlights();Status=L10n.Message("CopyIt.Text.9AC3A0BEBB");Diagnostics.Event("selection.clear","right-click");return true;}
                Deactivate();return true;
            }
            return false;
        }
    }
}



