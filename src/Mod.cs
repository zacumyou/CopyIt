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
        private Locale en,ko;
        // KO/EN: 1.6.2f1 (767.21d1), reviewed native placement and bulldozer contracts.
        internal const string TestedGameHash = "AAEE15C4FA41C130ABAA1183840667E4FAAE531D67E8A9FA7536618EFFA2F86A";
        public void OnLoad(UpdateSystem updateSystem)
        {
            Diagnostics.Start();
            Ready=false;
            Options = new Settings(this);
            AssetDatabase.global.LoadSettings("CopyIt", Options, new Settings(this));
            Options.PrepareToolSettings();
            Options.RegisterKeyBindings(); Options.RegisterInOptionsUI();
            GameManager.instance.localizationManager.AddSource("en-US",en=new Locale(Options,false));
            GameManager.instance.localizationManager.AddSource("ko-KR",ko=new Locale(Options,true));
            try
            {
                string hash;
                using(var sha=SHA256.Create()) using(var stream=File.OpenRead(typeof(Game.Tools.ToolSystem).Assembly.Location))
                    hash=BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
                Diagnostics.Event("game.compatibility", hash);
                if(hash!=TestedGameHash) throw new InvalidOperationException($"Game DLL changed; Copy It targets 1.6.2f1 and needs compatibility review. Expected {TestedGameHash}, actual {hash}.");
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
            if(en!=null)GameManager.instance.localizationManager.RemoveSource("en-US",en);
            if(ko!=null)GameManager.instance.localizationManager.RemoveSource("ko-KR",ko);
            Ready=false; Options?.UnregisterInOptionsUI(); Diagnostics.Stop();
        }
    }
}

