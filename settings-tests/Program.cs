using System;
using System.Text.Json;
using CopyIt;

// Exercises the production preference properties and startup policy without launching Unity.
// JSON round trips model persisted data; the game's AssetDatabase still needs an in-game check.
void Check(bool value,string name) { if(!value)throw new Exception(name); }
Settings Reload(Settings value) {
    using var document=JsonDocument.Parse(JsonSerializer.Serialize(value));
    var loaded=new Settings(null);
    foreach(var property in typeof(Settings).GetProperties()) {
        if(!property.CanWrite)continue;
        var saved=document.RootElement.GetProperty(property.Name);
        if(property.PropertyType==typeof(int))property.SetValue(loaded,saved.GetInt32());
        if(property.PropertyType==typeof(bool))property.SetValue(loaded,saved.GetBoolean());
    }
    loaded.PrepareToolSettings();return loaded;
}
var defaults=new Settings(null);
Check(!defaults.ResetToolSettingsOnStartup&&defaults.LastFilters==255,"first install defaults");
for(int mode=0;mode<3;mode++)for(int filters=0;filters<256;filters++)for(int height=0;height<3;height++) {
    var loaded=Reload(new Settings(null) {LastSelectionMode=mode,LastFilters=filters,
        LastHeightMode=height,LastIncludeUnderground=true,LastCompact=true});
    Check(loaded.LastSelectionMode==mode&&loaded.LastFilters==filters&&loaded.LastHeightMode==height&&
        loaded.LastIncludeUnderground&&loaded.LastCompact,"remember all choices including empty filter");
}
var reset=new Settings(null) {ResetToolSettingsOnStartup=true,LastSelectionMode=2,LastFilters=0,
    LastHeightMode=2,LastIncludeUnderground=true,LastCompact=true};
var next=Reload(reset);
Check(next.ResetToolSettingsOnStartup&&next.LastSelectionMode==0&&next.LastFilters==255&&
    next.LastHeightMode==0&&!next.LastIncludeUnderground&&!next.LastCompact,"reset on next startup");
reset.ResetToolSettingsOnStartup=false;
Check(Reload(reset).LastSelectionMode==2,"disable reset preserves latest choice");
var invalid=Reload(new Settings(null){LastSelectionMode=-1,LastFilters=256,LastHeightMode=3});
Check(invalid.LastSelectionMode==0&&invalid.LastFilters==255&&invalid.LastHeightMode==0,"invalid stored data");
reset.SetDefaults();
Check(!reset.ResetToolSettingsOnStartup&&reset.LastFilters==255&&reset.LastSelectionMode==0,"restore defaults");
Console.WriteLine("PASS: 2304 preference round trips, startup reset, opt-out, defaults and invalid values.");

// Minimal game UI stubs; no game persistence or Unity behavior is claimed by these tests.
namespace Colossal {
    public interface IDictionaryEntryError {}
    public interface IDictionarySource {
        System.Collections.Generic.IEnumerable<System.Collections.Generic.KeyValuePair<string,string>> ReadEntries(System.Collections.Generic.IList<IDictionaryEntryError> errors,System.Collections.Generic.Dictionary<string,int> counts);
        void Unload();
    }
}
namespace Colossal.IO.AssetDatabase {
    public sealed class FileLocationAttribute:Attribute {public FileLocationAttribute(string value){}}
}
namespace Game.Modding { public interface IMod {} }
namespace Game.Input {
    public enum ActionType {Button} public enum RebindOptions {None} public enum ModifierOptions {Ignore}
    public enum BindingKeyboard {C} public class ProxyBinding {}
    public static class InputManager {public const string kToolMap="Tool";}
}
namespace Game.Settings {
    public class ModSetting {
        public ModSetting(Game.Modding.IMod mod){} public virtual void SetDefaults(){}
        public string GetSettingsLocaleID()=>"Settings";public string GetBindingMapLocaleID()=>"Map";
        public string GetBindingKeyLocaleID(string s)=>"Key"+s;public string GetOptionTabLocaleID(string s)=>"Tab"+s;
        public string GetOptionGroupLocaleID(string s)=>"Group"+s;public string GetOptionLabelLocaleID(string s)=>"Label"+s;
        public string GetOptionDescLocaleID(string s)=>"Desc"+s;
    }
    public class SettingsUIMouseActionAttribute:Attribute {public SettingsUIMouseActionAttribute(string s,Game.Input.ActionType a,Game.Input.RebindOptions rebindOptions,Game.Input.ModifierOptions modifierOptions){}}
    public class SettingsUIKeyboardBindingAttribute:Attribute {public SettingsUIKeyboardBindingAttribute(Game.Input.BindingKeyboard b,string s,bool ctrl){}}
    public class SettingsUISectionAttribute:Attribute {public SettingsUISectionAttribute(string a,string b){}}
    public class SettingsUIMouseBindingAttribute:Attribute {public SettingsUIMouseBindingAttribute(string s){}}
    public class SettingsUIBindingMimicAttribute:Attribute {public SettingsUIBindingMimicAttribute(string a,string b){}}
    public class SettingsUIHiddenAttribute:Attribute {}
}
namespace CopyIt {
    internal static class Diagnostics {internal static string PathName=>null;}
}
