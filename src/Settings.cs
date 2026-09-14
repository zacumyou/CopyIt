// KO: 게임 단축키 설정과 한국어/영어 옵션 설명을 등록합니다.
// EN: Registers native key bindings and Korean/English option descriptions.
using System.Collections.Generic;
using Colossal;
using Colossal.IO.AssetDatabase;
using Game.Input;
using Game.Modding;
using Game.Settings;

namespace CopyIt
{
    [FileLocation("CopyIt")]
    [SettingsUIMouseAction("Select",ActionType.Button,rebindOptions:RebindOptions.None,modifierOptions:ModifierOptions.Ignore)]
    public sealed class Settings : ModSetting
    {
        public Settings(IMod mod):base(mod) {}
        [SettingsUIKeyboardBinding(BindingKeyboard.C,"Copy",ctrl:true)]
        [SettingsUISection("Main","Keys")]
        public ProxyBinding CopyKey {get;set;}
        [SettingsUIMouseBinding("Select")]
        [SettingsUIBindingMimic(InputManager.kToolMap,"Apply")]
        [SettingsUIHidden]
        public ProxyBinding SelectKey {get;set;}
        [SettingsUISection("Main","Info")]
        public string LogFolder => Diagnostics.PathName ?? "Game Logs/CopyIt";
        public override void SetDefaults() {}
    }
    internal sealed class Locale : IDictionarySource
    {
        private readonly Settings settings; private readonly bool ko;
        internal Locale(Settings settings,bool ko) {this.settings=settings;this.ko=ko;}
        public IEnumerable<KeyValuePair<string,string>> ReadEntries(IList<IDictionaryEntryError> errors,Dictionary<string,int> indexCounts)
        {
            return new Dictionary<string,string> {
                [settings.GetSettingsLocaleID()]="Copy It",
                [settings.GetBindingMapLocaleID()]="Copy It",
                [settings.GetBindingKeyLocaleID(nameof(Settings.CopyKey))]=ko?"선택 항목 복사":"Copy selection",
                [settings.GetBindingKeyLocaleID(nameof(Settings.SelectKey))]=ko?"선택":"Select",
                [settings.GetOptionTabLocaleID("Main")]=ko?"복사":"Copy",
                [settings.GetOptionGroupLocaleID("Keys")]=ko?"단축키":"Keyboard",
                [settings.GetOptionGroupLocaleID("Info")]=ko?"진단":"Diagnostics",
                [settings.GetOptionLabelLocaleID(nameof(Settings.CopyKey))]=ko?"선택 항목 복사":"Copy selection",
                [settings.GetOptionDescLocaleID(nameof(Settings.CopyKey))]=ko?"어디서든 Copy It을 열고, 선택 후 다시 누르면 복사 미리보기를 시작합니다.":"Open Copy It from gameplay; press again to copy the selection.",
                [settings.GetOptionLabelLocaleID(nameof(Settings.LogFolder))]=ko?"이벤트 로그":"Event log",
                [settings.GetOptionDescLocaleID(nameof(Settings.LogFolder))]=ko?"UTC 시각, 선택, 복사, 배치, 오류를 기록합니다.":"Records UTC timestamps, selection, copy, placement and errors."
            };
        }
        public void Unload() {}
    }
}

