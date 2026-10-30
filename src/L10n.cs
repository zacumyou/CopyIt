using System;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace CopyIt {
// Preserve keys/arguments until UI render, so language switches also update idle status text.
internal static class L10n {
 internal static string ForLog(string value)=>Regex.Replace(value??"",@"\x1e([^\x1f]*)\x1f",match=>{
  try {var parts=match.Groups[1].Value.Split('|');var template=Uri.UnescapeDataString(parts[1]);
   return Regex.Replace(template,@"\{(\d+)\}",m=>int.TryParse(m.Groups[1].Value,out var n)&&n+2<parts.Length?ForLog(Uri.UnescapeDataString(parts[n+2])):m.Value);
  }catch{return "[localized message]";}
 });
 internal static string Message(string id,params object[] args){
  var text=new StringBuilder("\u001e").Append(id).Append('|').Append(Uri.EscapeDataString(Catalog.English[id]));
  foreach(var arg in args)text.Append('|').Append(Uri.EscapeDataString(Convert.ToString(arg,CultureInfo.InvariantCulture)??""));
  return text.Append('\u001f').ToString();
 }
}
}
