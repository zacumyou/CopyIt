// Static browser preview using actual compiled CSS/module with native component stand-ins.
const fs=require('node:fs'),path=require('node:path'),{pathToFileURL}=require('node:url');
const React=require('../ui/node_modules/react'),{renderToStaticMarkup}=require('../ui/node_modules/react-dom/server');
let values={active:true,count:12,filters:7,status:'12개 선택 · Ctrl+C 복사',selectionMode:1};
const h=React.createElement;
global.window={React,innerWidth:1000,innerHeight:800,'cs2/api':{bindValue:(g,key,initial)=>({key,initial}),useValue:b=>values[b.key]??b.initial,trigger:()=>{}},'cs2/ui':{
 Button:p=>h('button',{className:[p.theme?.button,p.className].filter(Boolean).join(' '),disabled:p.disabled},p.children),FloatingButton:()=>null,Tooltip:p=>p.children,
 Panel:p=>h('section',{className:p.theme.panel},h('div',{className:p.theme.header},h('div',{className:p.theme.titleBar},h('div',{className:p.theme.title},p.header),h('button',{className:p.theme.closeButton},'×'))),h('div',{className:p.theme.content},p.children))
}};
(async()=>{
 const mod=await import(pathToFileURL(path.resolve(__dirname,'../local/CopyIt/CopyIt.mjs')).href);const hooks={};mod.default({append:(k,c)=>hooks[k]=c});
 const css=fs.readFileSync(path.resolve(__dirname,'../local/CopyIt/CopyIt.css'),'utf8');
 for(const mode of ['selection','polygon','copy','compact','prefilter']){
  values.assetFilter=mode==='prefilter';values.assetFilterName='선택한 에셋';values.count=mode==='prefilter'?0:12;values.compact=mode==='compact';values.selectionMode=mode==='polygon'?2:1;values.copying=mode==='copy';values.angle=27;values.canPlace=true;values.polygonCount=mode==='polygon'?3:0;
  const html=renderToStaticMarkup(h(hooks.Game)).replaceAll('coui://ui-mods/', '../local/CopyIt/');
  fs.writeFileSync(path.resolve(__dirname,`../research/ui-${mode}.html`),`<!doctype html><meta charset="utf-8"><style>html{font-size:1.35px}*{box-sizing:border-box}body{font:13rem "Malgun Gothic",sans-serif;margin:0;background:repeating-linear-gradient(30deg,transparent 0 110px,#63695b 111px 126px,transparent 127px 210px),linear-gradient(140deg,#64714f,#384839);height:100vh}button{font:inherit}${css}</style>${html}`);
 }
})();
