import data from "./text-catalog.json";
export type TextTranslator=(source:string,...args:unknown[])=>string;
export type Lookup=(id:string,fallback:string)=>string|null;
const sources:Record<string,string>=data.source,english:Record<string,string>=data.en;
// Format complete messages; arguments may contain another mod's localized status token.
export function createText(lookup:Lookup):TextTranslator {
 const resolve=(source:string,args:unknown[]=[],depth=0):string=>{
  if(depth>12)return "";
  const expanded=source.replace(/\u001e([^\u001f]*)\u001f/g,(_token,body:string)=>{
   try {const [id,fallback,...values]=body.split("|");const template=lookup(id,decodeURIComponent(fallback))??decodeURIComponent(fallback);
    return format(template,values.map(value=>resolve(decodeURIComponent(value),[],depth+1)));
   }catch{return "";}
  });
  const id=sources[expanded];const template=id?(lookup(id,english[id]??expanded)??english[id]??expanded):expanded;
  return format(template,args.map(value=>resolve(String(value??""),[],depth+1)));
 };
 return (source,...args)=>resolve(source,args);
}
export function format(template:string,args:unknown[]):string {
 return template.replace(/\{(\d+)\}/g,(token,index:string)=>Number(index)<args.length?String(args[Number(index)]??""):token);
}
