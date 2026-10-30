// KO: 게임 단축키 설정과 한국어/영어 옵션 설명을 등록합니다.
// EN: Registers native key bindings and Korean/English option descriptions.
using System.Collections.Generic;
using System.Linq;
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
        [SettingsUISection("Main","Selection")]
        public bool ResetToolSettingsOnStartup {get;set;}
        [SettingsUIHidden] public int LastSelectionMode {get;set;}
        [SettingsUIHidden] public int LastFilters {get;set;} = 255;
        [SettingsUIHidden] public bool LastIncludeUnderground {get;set;}
        [SettingsUIHidden] public int LastHeightMode {get;set;}
        [SettingsUIHidden] public bool LastCompact {get;set;}

        // KO: 초기화는 모드 시작 때만 적용하여 같은 실행 중의 최근 선택은 유지합니다.
        // EN: Reset only on mod startup, preserving choices throughout the current session.
        internal void PrepareToolSettings()
        {
            if(ResetToolSettingsOnStartup) ResetToolSettings();
            LastSelectionMode=LastSelectionMode>=0&&LastSelectionMode<=2?LastSelectionMode:0;
            LastFilters=LastFilters>=0&&LastFilters<=255?LastFilters:255;
            LastHeightMode=LastHeightMode>=0&&LastHeightMode<=2?LastHeightMode:0;
        }
        private void ResetToolSettings()
        {
            LastSelectionMode=0;LastFilters=255;LastIncludeUnderground=false;
            LastHeightMode=0;LastCompact=false;
        }
        public override void SetDefaults()
        {
            ResetToolSettingsOnStartup=false;
            ResetToolSettings();
        }
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
                [settings.GetOptionGroupLocaleID("Selection")]=ko?"도구 설정 저장":"Tool preferences",
                [settings.GetOptionLabelLocaleID(nameof(Settings.ResetToolSettingsOnStartup))]=ko?"게임 시작 시 도구 설정 초기화":"Reset tool preferences on game startup",
                [settings.GetOptionDescLocaleID(nameof(Settings.ResetToolSettingsOnStartup))]=ko?"끄면 최근 선택 방식, 선택 유형, 지하 포함 여부, 높이 모드와 패널 접힘 상태를 다음 실행에서도 복원합니다. 켜면 다음 게임 시작 시 기본값으로 초기화합니다. 선택한 오브젝트와 스포이드 에셋 목록은 저장하지 않습니다.":"When off, restore the latest selection method, type filters, underground choice, height mode and collapsed panel state next session. When on, reset these to defaults at the next game startup. Selected objects and eyedropper asset samples are not saved.",
                [settings.GetOptionLabelLocaleID(nameof(Settings.CopyKey))]=ko?"선택 항목 복사":"Copy selection",
                [settings.GetOptionDescLocaleID(nameof(Settings.CopyKey))]=ko?"어디서든 Copy It을 열고, 선택 후 다시 누르면 복사 미리보기를 시작합니다.":"Open Copy It from gameplay; press again to copy the selection.",
                [settings.GetOptionLabelLocaleID(nameof(Settings.LogFolder))]=ko?"이벤트 로그":"Event log",
                [settings.GetOptionDescLocaleID(nameof(Settings.LogFolder))]=ko?"UTC 시각, 선택, 복사, 배치, 오류를 기록합니다.":"Records UTC timestamps, selection, copy, placement and errors."
            }.Concat(Catalog.Entries(ko));
        }
        public void Unload() {}
    }
}

