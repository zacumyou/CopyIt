const fs=require('fs'),path=require('path'),Module=require('module'),assert=require('assert/strict'),{pathToFileURL}=require('url');
const root=path.resolve(__dirname,'..'),name=path.basename(root),ui=path.join(root,'ui'),React=require(path.join(ui,'node_modules/react')),render=require(path.join(ui,'node_modules/react-dom/server')).renderToStaticMarkup,ts=require(path.join(ui,'node_modules/typescript'));
const en=require(path.join(root,'lang/en-US.json')),ko=require(path.join(root,'lang/ko-KR.json')),catalog=require(path.join(root,'localization/catalog.json'));
let checks=0;const check=(value,msg)=>{assert.ok(value,msg);checks++;};
for(const id of Object.keys(en)){check(id in ko,'Korean key parity');check(JSON.stringify([...en[id].matchAll(/\{\d+\}/g)].map(m=>m[0]).sort())===JSON.stringify([...ko[id].matchAll(/\{\d+\}/g)].map(m=>m[0]).sort()),'placeholder parity '+id);check(!/[가-힣]/.test(en[id]),'English fallback '+id);}
const file=path.join(ui,'src/text.ts'),moduleText=new Module(file,module);moduleText.filename=file;moduleText.paths=Module._nodeModulePaths(path.dirname(file));moduleText._compile(ts.transpileModule(fs.readFileSync(file,'utf8'),{compilerOptions:{module:ts.ModuleKind.CommonJS,esModuleInterop:true}}).outputText,file);
const {createText}=moduleText.exports;let dictionary=en;const t=createText((id,fallback)=>dictionary[id]??fallback);
const id=Object.keys(catalog).find(key=>catalog[key].en.includes('{0}'));const token='\x1e'+id+'|'+encodeURIComponent(en[id])+'|'+encodeURIComponent('Asset | 한글 {7}')+'\x1f';
check(t(token).includes('Asset | 한글 {7}'),'encoded argument delimiters and Unicode');dictionary=ko;check(t(token).includes('Asset | 한글 {7}'),'same idle token switches locale');
dictionary={[id]:'{0} / user translation'};check(t(token)==='Asset | 한글 {7} / user translation','external override and placeholder reorder');dictionary={};check(t(token).includes('Asset | 한글 {7}'),'missing key falls back to English');
// Cross-mod transport uses its own fallback, not a hard reference to another catalog.
check(t('\x1eOtherMod.Status|'+encodeURIComponent('Other group {0}')+'|3\x1f')==='Other group 3','cross-mod fallback');
let values={active:true,count:12,filters:255,status:token,selectionMode:1,held:true,editing:true,editable:true,anarchyAvailable:true,canPlace:true,projection:'45,40,119,-39,-77,64,0,-133',rotating:false,pivotIndex:4};
const h=React.createElement;
global.window={React,innerWidth:1280,innerHeight:800,'cs2/l10n':{useLocalization:()=>({translate:(key,fallback)=>dictionary[key]??fallback})},'cs2/api':{bindValue:(group,key,initial)=>({key,initial}),useValue:b=>values[b.key]??b.initial,trigger:()=>{}},'cs2/ui':{
 Button:p=>h('button',{'aria-label':p['aria-label'],className:[p.theme?.button,p.className].filter(Boolean).join(' '),disabled:p.disabled},p.children),FloatingButton:()=>null,
 Tooltip:p=>h('div',{title:typeof p.tooltip==='string'?p.tooltip:undefined},p.children),Panel:p=>h('section',{className:p.theme.panel},h('div',{className:p.theme.header},p.header),h('div',{className:p.theme.content},p.children))
}};
(async()=>{
 const mod=await import(pathToFileURL(path.join(root,'local',name,name+'.mjs')).href);const hooks={};mod.default({append:(key,c)=>hooks[key]=c,extend:()=>{}});
 const out=path.join(root,'tests/localization-browser');fs.mkdirSync(out,{recursive:true});
 for(const locale of ['en-US','ko-KR','override']){
  dictionary=locale==='en-US'?en:locale==='ko-KR'?ko:Object.fromEntries(Object.keys(en).map(key=>[key,'Translated '+en[key]]));
  values.status='\x1e'+id+'|'+encodeURIComponent(en[id])+'|12\x1f';
  for(const mode of name==='CopyIt'?['selection','compact','copy','polygon']:['move','rotate']){
   values.compact=mode==='compact';values.copying=mode==='copy';values.selectionMode=mode==='polygon'?2:1;values.rotating=mode==='rotate';values.polygonCount=3;values.anarchyAvailable=true;
   const html=render(h(hooks.Game));
   check(!html.includes('\x1e')&&!html.includes('\x1f'),'no transport tokens rendered');
   if(locale!=='ko-KR')check(!/[가-힣]/.test(html),'no untranslated Korean in '+name+' '+mode+' '+locale);
   if(locale==='override')check(html.includes('Translated '),'lookup overrides visible');
   const css=fs.readFileSync(path.join(root,'local',name,name+'.css'),'utf8');
   const assetHtml=html.replaceAll('coui://ui-mods/','/mods/'+name+'/');
   fs.writeFileSync(path.join(out,locale+'-'+mode+'.html'),'<!doctype html><meta charset="utf-8"><style>html{font-size:0.0925926vh}body{font:14px Arial,sans-serif;background:#657059;color:white}*{box-sizing:border-box}button{font:inherit}'+css+'</style>'+assetHtml);
  }
 }
 console.log('PASS: '+name+' '+checks+' localization checks; compiled UI, bilingual/fallback/override, idle tokens and placeholder parity. No game runtime claim.');
})().catch(e=>{console.error(e);process.exitCode=1;});
