// Compiled UI contract tests with cs2/* stand-ins. No claim of game rendering/audio validation.
const assert = require('node:assert/strict');
const {pathToFileURL}=require('node:url');
const path=require('node:path');
const React=require('../ui/node_modules/react');
const {renderToStaticMarkup}=require('../ui/node_modules/react-dom/server');
let values={active:false,copying:false,absolute:false,box:false,count:0,filters:7,canPlace:false,status:'ready',rectangle:''};
let commands=[],buttons=[];
const labelOf=x=>x['aria-label']??(typeof x.children==='string'?x.children:x.children?.props?.children);
const NativeButton=props=>{buttons.push(props);return React.createElement('button',{disabled:props.disabled,'data-selected':props.selected},props.children);};
global.window={React,innerWidth:1920,innerHeight:1080,'cs2/api':{
    bindValue:(group,key,initial)=>({group,key,initial}),useValue:binding=>values[binding.key]??binding.initial,
    trigger:(...args)=>commands.push(args)
},'cs2/ui':{
    Button:NativeButton,FloatingButton:NativeButton,
    Panel:props=>React.createElement('section',null,props.header,props.children),
    PanelSection:props=>React.createElement('div',null,props.children),
    Tooltip:props=>props.children
}};
(async()=>{
    const mod=await import(pathToFileURL(path.resolve(__dirname,'../local/CopyIt/CopyIt.mjs')).href);
    assert.equal(mod.hasCSS,true);
    const hooks={};mod.default({append:(key,component)=>hooks[key]=component});
    assert.deepEqual(Object.keys(hooks),['GameTopRight','Game']);
    const render=()=>{buttons=[];return renderToStaticMarkup(React.createElement(hooks.Game));};
    assert.equal(render(),'');
    values.active=true;
    let html=render();assert.ok(html.includes('COPY IT'));assert.ok(html.includes('지형 기준'));assert.ok(html.includes('다각형 선택'));for(const label of ['도로','넷레인','데칼'])assert.ok(html.includes(label));
    assert.ok(!html.includes('그룹 최저'));assert.ok(!html.includes('1.5 /'));assert.ok(!html.includes('절대 Y 유지'));
    for(const button of buttons)assert.ok(button.theme?.button,'explicit button theme');
    assert.ok(!buttons.find(x=>labelOf(x)==='스포이드').disabled,'eyedropper available before selection');
    values.assetFilter=true;values.assetFilterName='Test Asset';html=render();assert.ok(html.includes('에셋 목록: Test Asset'));
    buttons.find(x=>labelOf(x)==='에셋 필터 해제').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','assetFilter.clear']);values.assetFilter=false;render();
    let copy=buttons.find(x=>labelOf(x)==='복사 · Ctrl+C');assert.equal(copy.disabled,true);
    buttons.find(x=>labelOf(x)==='지하 포함').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','underground.include']);
    buttons.find(x=>labelOf(x)==='지하 제외').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','underground.exclude']);
    values.count=2;render();buttons.find(x=>labelOf(x)==='스포이드').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','eyedropper']);copy=buttons.find(x=>labelOf(x)==='복사 · Ctrl+C');assert.equal(copy.disabled,false);
    copy.onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','copy']);
    values.copying=true;values.angle=27;html=render();assert.ok(html.includes('오브젝트 복사'));assert.ok(!html.includes('박스 선택'));assert.ok(/<b[^>]*>27\.0°<\/b>/.test(html),'angle and degree form one text node');
    const absolute=buttons.find(x=>labelOf(x)==='절대값 기준');absolute.onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','height']);
    buttons.find(x=>labelOf(x)==='지형에 붙이기').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','height.snap']);
    values.absolute=true;values.canPlace=true;html=render();assert.ok(!html.includes('1.5 /'));assert.ok(html.includes('반복 배치 가능'));
    values.copying=false;values.rectangle='10,20,30,40';html=render();assert.ok(html.includes('left:10%;top:20%;width:30%;height:40%'));
    values.selectionMode=2;values.polygonCount=3;values.polygon='10,10;30,10;20,30';html=render();assert.ok(html.includes('rotate('));
    buttons.find(x=>labelOf(x)==='선택 완료').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','finish']);
    assert.equal(buttons.find(x=>labelOf(x)==='복사 · Ctrl+C').disabled,true);
    for(const button of buttons)assert.equal(button.selectSound,undefined); // native sound default is retained
    buttons.find(x=>x['aria-label']==='패널 접기').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','compact']);
    values.compact=true;values.polygonCount=0;html=render();
    for(const label of ['COPY IT','지형 기준','절대값 기준','복사 · Ctrl+C','선택 해제','클릭 선택'])assert.ok(!html.includes(label));
    assert.equal(buttons.length,11,'eight types, mode cycle, expand, close');
    for(const label of ['건물','프롭','나무','도로','도로 외 경로','넷레인','데칼','패널 펼치기','닫기'])assert.ok(buttons.find(x=>x['aria-label']===label));
    buttons.find(x=>x['aria-label']==='선택 방식 전환').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','click']);
    values.selectionMode=0;render();buttons.find(x=>x['aria-label']==='선택 방식 전환').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','box']);
    values.selectionMode=1;render();buttons.find(x=>x['aria-label']==='선택 방식 전환').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','polygon']);
    buttons.find(x=>x['aria-label']==='표면').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','surfaces']);
    buttons.find(x=>x['aria-label']==='데칼').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','decals']);
    buttons.find(x=>x['aria-label']==='닫기').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','toggle']);
    buttons.find(x=>x['aria-label']==='패널 펼치기').onSelect();assert.deepEqual(commands.pop(),['CopyIt','command','compact']);
    assert.equal((html.match(/<img /g)||[]).length,12,'compact grip and controls use SVG images');
    console.log('PASS: compiled UI registration, CSS export, visibility, disabled copy, command routing, height modes, marquee, compact controls and native sound defaults. cs2 components mocked; no in-game validation.');
})().catch(e=>{console.error(e);process.exitCode=1;});


