// KO: 게임 도구와 UI 입력 포커스를 고려하여 전역 복사 단축키를 전달합니다.
// EN: Routes the global copy shortcut while respecting tool and UI input focus.
using Game;
using Game.Input;
using UnityEngine;
using UnityEngine.InputSystem;
namespace CopyIt
{
    public partial class CopyInputSystem:Game.UI.UISystemBase
    {
        private CopyTool tool;private ProxyAction copy;private InputBarrier zoom,rotate;
        public override GameMode gameMode=>GameMode.Game;
        protected override void OnCreate()
        {
            base.OnCreate();tool=World.GetOrCreateSystemManaged<CopyTool>();copy=Mod.Options.GetAction("Copy");
            zoom=InputManager.instance.FindAction("Camera","Zoom").CreateBarrier("Copy It wheel rotation",InputManager.DeviceType.Mouse);
            rotate=InputManager.instance.FindAction("Camera","Rotate").CreateBarrier("Copy It drag rotation",InputManager.DeviceType.Mouse);
        }
        protected override void OnUpdate()
        {
            base.OnUpdate();var input=InputManager.instance;
            bool enabled=Mod.Ready&&Application.isFocused&&!input.hasInputFieldFocus&&!input.overlayActive;
            copy.shouldBeEnabled=enabled;
            if(enabled&&copy.WasPressedThisFrame())tool.Shortcut();
            bool world=enabled&&tool.Active&&tool.Copying&&input.controlOverWorld;
            // KO/EN: 복사 중 휠은 회전 전용 / Reserve mouse wheel for rotation while copying.
            zoom.blocked=world;
            rotate.blocked=world&&Mouse.current!=null&&Mouse.current.rightButton.isPressed;
        }
        internal void Release(){if(copy!=null)copy.shouldBeEnabled=false;if(zoom!=null)zoom.blocked=false;if(rotate!=null)rotate.blocked=false;}
        protected override void OnStopRunning(){Release();base.OnStopRunning();}
        protected override void OnDestroy(){Release();zoom?.Dispose();rotate?.Dispose();base.OnDestroy();}
    }
}
