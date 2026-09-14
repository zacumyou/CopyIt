// KO: 게임 업데이트 단계에 각 시스템을 등록합니다. 다른 게임 DLL에서는 안전하게 중지합니다.
// EN: Registers systems in native update phases; an unreviewed game DLL fails closed.
using System;
using System.IO;
using System.Security.Cryptography;
using Colossal.IO.AssetDatabase;
using Game;
using Game.Modding;
using Game.SceneFlow;
using Unity.Entities;

namespace CopyIt
{
    public sealed class Mod : IMod
    {
        internal static Settings Options;
        internal static bool Ready;
        internal const string TestedGameHash = "721E7E17BF74299AA2B988C1BD07E90874BB8BC72D263229500C4BF639E7E4EE";
        public void OnLoad(UpdateSystem updateSystem)
        {
            Diagnostics.Start();
            Options = new Settings(this);
            AssetDatabase.global.LoadSettings("CopyIt", Options, new Settings(this));
            Options.RegisterKeyBindings(); Options.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US",new Locale(Options,false));
            GameManager.instance.localizationManager.AddSource("ko-KR",new Locale(Options,true));
            try
            {
                string hash;
                using(var sha=SHA256.Create()) using(var stream=File.OpenRead(typeof(Game.Tools.ToolSystem).Assembly.Location))
                    hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
                Diagnostics.Event("game.compatibility", hash);
                if(hash!=TestedGameHash) throw new InvalidOperationException("Game DLL changed; Copy It needs compatibility review.");
                updateSystem.UpdateAt<CopyVisibilityRefreshSystem>(SystemUpdatePhase.ToolUpdate);
                updateSystem.UpdateBefore<CopyVisibilitySystem>(SystemUpdatePhase.ModificationEnd);
                updateSystem.UpdateAt<CopyTool>(SystemUpdatePhase.ToolUpdate);
                updateSystem.UpdateBefore<CopyNetworkHideSystem,Game.Tools.CourseSplitSystem>(SystemUpdatePhase.PostTool);
                updateSystem.UpdateBefore<CopyNetworkPublishSystem,Game.Tools.GenerateNodesSystem>(SystemUpdatePhase.Modification1);
                updateSystem.UpdateBefore<CopyDefinitionSystem,Game.Tools.GenerateObjectsSystem>(SystemUpdatePhase.Modification1);
                updateSystem.UpdateAt<CopyInputSystem>(SystemUpdatePhase.UIUpdate);
                updateSystem.UpdateAt<CopyUI>(SystemUpdatePhase.UIUpdate);
                Ready=true;
                Diagnostics.Event("mod.loaded");
            }
            catch(Exception e) { Diagnostics.Failure("mod.load.failed", e); }
        }
        public void OnDispose()
        {
            World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<CopyTool>()?.Deactivate();
            World.DefaultGameObjectInjectionWorld?.GetExistingSystemManaged<CopyInputSystem>()?.Release();
            Ready=false; Options?.UnregisterInOptionsUI(); Diagnostics.Stop();
        }
    }
}

