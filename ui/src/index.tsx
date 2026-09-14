// KO: 화면은 바인딩을 표시하고 명령을 보냅니다. 엔티티를 직접 다루지 않습니다.
// EN: UI renders bindings and sends commands; it never mutates game entities directly.
import React, { useEffect, useState, useRef } from "react";
import { ModRegistrar } from "cs2/modding";
import { bindValue, trigger, useValue } from "cs2/api";
import { Button, FloatingButton, Panel, Tooltip } from "cs2/ui";
import icon from "./copy.svg";
import pathsIcon from "./paths.svg";
import eyedropperIcon from "./eyedropper.svg";
import gripIcon from "./grip.svg";
import buildingIcon from "./building.svg";
import propIcon from "./prop.svg";
import treeIcon from "./tree.svg";
import roadIcon from "./road.svg";
import laneIcon from "./lane.svg";
import surfaceIcon from "./surface.svg";
import decalIcon from "./decal.svg";
import expandIcon from "./expand.svg";
import collapseIcon from "./collapse.svg";
import closeIcon from "./close.svg";
const typeIcons=[buildingIcon,propIcon,treeIcon,roadIcon,pathsIcon,laneIcon,decalIcon,surfaceIcon];
const typeLabels=["건물","프롭","나무","도로","도로 외 경로","넷레인","데칼","표면"];
const typeBits=[1,2,4,8,64,16,32,128];
const typeCommands=["buildings","props","trees","roads","paths","netlanes","decals","surfaces"];
import clickIcon from "./click.svg";
import boxIcon from "./box.svg";
import polygonIcon from "./polygon.svg";
import groundSnapIcon from "./ground-snap.svg";
import terrainIcon from "./terrain.svg";
import absoluteIcon from "./absolute.svg";
import clearIcon from "./clear.svg";
import finishIcon from "./finish.svg";
import styles from "./copy.module.scss";
const underground$=bindValue<boolean>("CopyIt","includeUnderground",false);
const groundSnap$=bindValue<boolean>("CopyIt","groundSnap",false);
const assetFilter$=bindValue<boolean>("CopyIt","assetFilter",false),assetFilterName$=bindValue<string>("CopyIt","assetFilterName","");
const eyedropper$=bindValue<boolean>("CopyIt","eyedropper",false);
const compact$=bindValue<boolean>("CopyIt","compact",false);
const active$=bindValue<boolean>("CopyIt","active",false), copying$=bindValue<boolean>("CopyIt","copying",false);
const absolute$=bindValue<boolean>("CopyIt","absolute",false), canPlace$=bindValue<boolean>("CopyIt","canPlace",false);
const count$=bindValue<number>("CopyIt","count",0), filters$=bindValue<number>("CopyIt","filters",255);
const mode$=bindValue<number>("CopyIt","selectionMode",0), vertices$=bindValue<number>("CopyIt","polygonCount",0);
const preview$=bindValue<number>("CopyIt","previewCount",0), searching$=bindValue<boolean>("CopyIt","searching",false);
const status$=bindValue<string>("CopyIt","status",""), rectangle$=bindValue<string>("CopyIt","rectangle","");
const polygon$=bindValue<string>("CopyIt","polygon",""), angle$=bindValue<number>("CopyIt","angle",0);
const command=(value:string)=>trigger("CopyIt","command",value);
const buttonTheme={button:styles.button};
const panelTheme={panel:styles.panel,header:styles.header,titleBar:styles.titleBar,title:styles.title,iconSpace:styles.iconSpace,content:styles.panelContent,closeButton:styles.closeButton};
const labelIcons:Record<string,string>={"건물":buildingIcon,"프롭":propIcon,"나무":treeIcon,"도로":roadIcon,"도로 외 경로":pathsIcon,"스포이드":eyedropperIcon,"넷레인":laneIcon,"데칼":decalIcon,"표면":surfaceIcon,"클릭 선택":clickIcon,"박스 선택":boxIcon,"다각형 선택":polygonIcon,"지형 기준":terrainIcon,"지형에 붙이기":groundSnapIcon,"절대값 기준":absoluteIcon,"복사 · Ctrl+C":icon,"선택 해제":clearIcon,"선택 완료":finishIcon,"복사 취소 · Esc":closeIcon};
const ButtonLabel=({children}:{children:React.ReactNode})=><span className={styles.buttonLabel}>{typeof children==="string"&&labelIcons[children]&&<img className={styles.labelIcon} src={labelIcons[children]} alt=""/>}<span>{children}</span></span>;
const Choice=({selected,children,onSelect,disabled=false,selectionMethod=false}:{selected?:boolean;children:React.ReactNode;onSelect:()=>void;disabled?:boolean;selectionMethod?:boolean})=><Button style={{fontFamily:'"Noto Sans KR"'}} theme={buttonTheme} className={selected?(selectionMethod?styles.methodSelected:styles.selected):undefined} selected={selected} disabled={disabled} aria-label={typeof children==="string"?children:undefined} onSelect={onSelect}><ButtonLabel>{children}</ButtonLabel></Button>;
const CopyButton=()=>{const active=useValue(active$);return <Tooltip tooltip="Copy It · Ctrl+C"><FloatingButton src={icon} selected={active} onSelect={()=>command("toggle")} /></Tooltip>;};
function PolygonOverlay({path}:{path:string}) {
    if(!path)return null;
    const points=path.split(";").map(p=>p.split(",").map(Number));
    const width=window.innerWidth,height=window.innerHeight;
    return <div className={styles.overlay}>{points.map((p,i)=>{
        const next=points[(i+1)%points.length],dx=(next[0]-p[0])*width/100,dy=(next[1]-p[1])*height/100;
        return <React.Fragment key={i}><div className={styles.edge} style={{left:p[0]+"%",top:p[1]+"%",width:Math.hypot(dx,dy)+"px",transform:`rotate(${Math.atan2(dy,dx)*180/Math.PI}deg)`}} /><div className={i===0?styles.firstPoint:styles.point} style={{left:p[0]+"%",top:p[1]+"%"}} /></React.Fragment>;
    })}</div>;
}
const CopyPanel=()=>{
    const underground=useValue(underground$);
    const groundSnap=useValue(groundSnap$);
    const eyedropper=useValue(eyedropper$),assetFilter=useValue(assetFilter$),assetName=useValue(assetFilterName$);
    const [position,setPosition]=useState<{x:number;y:number}|null>(null);
    const [dragging,setDragging]=useState(false);
    const drag=useRef<{x:number;y:number;left:number;top:number;width:number;height:number}|null>(null);
    // KO/EN: 게임 입력과 패널 드래그가 겹치지 않도록 이벤트를 차단 / Isolate panel dragging from world input.
    const beginDrag=(e:React.MouseEvent)=>{
        if(e.button!==0)return;
        const panel=(e.currentTarget as HTMLElement).closest('[data-copy-panel]') as HTMLElement;
        if(!panel)return;
        const rect=panel.getBoundingClientRect();
        drag.current={x:e.clientX,y:e.clientY,left:rect.left,top:rect.top,width:rect.width,height:rect.height};
        setPosition({x:rect.left,y:rect.top});setDragging(true);e.preventDefault();e.stopPropagation();
        trigger("CopyIt","diagnostic","panel.drag.begin");
    };
    useEffect(()=>{
        if(!dragging)return;
        const move=(e:MouseEvent)=>{const d=drag.current;if(d)setPosition({x:Math.max(0,Math.min(window.innerWidth-d.width,d.left+e.clientX-d.x)),y:Math.max(0,Math.min(window.innerHeight-40,d.top+e.clientY-d.y))});};
        const end=()=>{drag.current=null;setDragging(false);trigger("CopyIt","diagnostic","panel.drag.end");};
        window.addEventListener('mousemove',move);window.addEventListener('mouseup',end);window.addEventListener('blur',end);
        return()=>{window.removeEventListener('mousemove',move);window.removeEventListener('mouseup',end);window.removeEventListener('blur',end);};
    },[dragging]);
    const dragStyle:React.CSSProperties={...(position?{left:position.x,top:position.y,right:'auto'}:{}),...(dragging?{transform:'scale(0.97)',backgroundColor:'#23292e'}:{})};

    const compact=useValue(compact$);
    const active=useValue(active$),copying=useValue(copying$),absolute=useValue(absolute$),mode=useValue(mode$);
    const count=useValue(count$),filters=useValue(filters$),canPlace=useValue(canPlace$),status=useValue(status$);
    const rectangle=useValue(rectangle$),polygon=useValue(polygon$),vertices=useValue(vertices$),preview=useValue(preview$),searching=useValue(searching$),angle=useValue(angle$);
    useEffect(()=>{trigger("CopyIt","diagnostic","UI mounted 0.5.6");},[]);
    if(!active)return null;
    const rect=rectangle.split(",").map(Number);
    return <>
        {dragging&&<div className={styles.dragShield}/>}
        {!copying&&rectangle&&<div className={styles.marquee} style={{left:rect[0]+"%",top:rect[1]+"%",width:rect[2]+"%",height:rect[3]+"%"}} />}
        {!copying&&<PolygonOverlay path={polygon}/>}
        {compact?<div data-copy-panel="true" style={dragStyle} className={styles.compact} role="region" aria-label="Copy It 유형 선택"><span className={styles.dragHandle} onMouseDown={beginDrag} title="드래그하여 이동"><img src={gripIcon} alt="패널 이동" draggable={false}/></span>
            <Tooltip tooltip={`현재: ${["클릭 선택","박스 선택","다각형 선택"][mode]} · 클릭하면 ${["클릭 선택","박스 선택","다각형 선택"][(mode+1)%3]}`}><Button theme={buttonTheme} className={styles.methodSelected} disabled={copying} aria-label="선택 방식 전환" onSelect={()=>command(["click","box","polygon"][(mode+1)%3])}><img className={styles.typeIcon} src={[clickIcon,boxIcon,polygonIcon][mode]} alt=""/></Button></Tooltip>
            {typeLabels.map((label,index)=><Tooltip key={label} tooltip={label}><Button theme={buttonTheme} className={filters&typeBits[index]?styles.selected:undefined} selected={!!(filters&typeBits[index])} disabled={copying} aria-label={label} onSelect={()=>command(typeCommands[index])}><img className={styles.typeIcon} src={typeIcons[index]} alt=""/></Button></Tooltip>)}
            <Tooltip tooltip="패널 펼치기"><Button theme={buttonTheme} className={styles.expandButton} aria-label="패널 펼치기" onSelect={()=>command("compact")}><img className={styles.typeIcon} src={expandIcon} alt=""/></Button></Tooltip>
            <Tooltip tooltip="닫기"><Button theme={buttonTheme} className={styles.compactClose} aria-label="닫기" onSelect={()=>command("toggle")}><img className={styles.typeIcon} src={closeIcon} alt=""/></Button></Tooltip>
        </div>:<Panel data-copy-panel="true" style={dragStyle} onMouseDown={e=>{const target=e.target as HTMLElement;if(target.closest(`.${styles.header}`)&&!target.closest("button,[role=button]"))beginDrag(e);}} theme={panelTheme} header={<div className={styles.headerRow}><img className={styles.dragGrip} src={gripIcon} alt="" aria-hidden="true" draggable={false}/><div className={styles.headerText}><div className={styles.eyebrow}>COPY IT</div><div className={styles.heading}>{copying?"오브젝트 복사":"오브젝트 선택"}</div></div><Tooltip tooltip="패널 접기"><Button theme={buttonTheme} className={styles.foldButton} aria-label="패널 접기" onSelect={()=>command("compact")}><img className={styles.typeIcon} src={collapseIcon} alt=""/></Button></Tooltip></div>} onClose={()=>command("toggle")}>
            <div className={styles.content}>
                <div className={styles.summary}><span>{copying?"복사할 오브젝트":"선택한 오브젝트"}</span><span className={styles.count}><span>{count}</span><span className={styles.unit}>개</span></span></div>
                {!copying&&<div className={styles.selectionTools}>
                    <span className={styles.toolCaption}>에셋 선택 도구</span>
                    <Tooltip tooltip="스포이드: 에셋들을 차례로 클릭해 추가 · 선택 방식 버튼으로 완료"><Button theme={buttonTheme} className={`${styles.eyedropperButton} ${eyedropper?styles.methodSelected:""}`} selected={eyedropper} aria-label="스포이드" onSelect={()=>command("eyedropper")}><img className={styles.typeIcon} src={eyedropperIcon} alt=""/></Button></Tooltip>
                </div>}
                {!copying&&assetFilter&&<div className={styles.assetFilterBar}><Tooltip tooltip={assetName}><span className={styles.assetName}>에셋 목록: {assetName}</span></Tooltip><Tooltip tooltip="에셋 필터 해제"><Button theme={buttonTheme} className={styles.eyedropperButton} aria-label="에셋 필터 해제" onSelect={()=>command("assetFilter.clear")}><img className={styles.typeIcon} src={closeIcon} alt=""/></Button></Tooltip></div>}
                {!copying&&<div className={styles.section}>
                    <div className={`${styles.row} ${styles.selectionMethods}`}>{["클릭 선택","박스 선택","다각형 선택"].map((label,index)=><Choice key={label} selectionMethod selected={mode===index} onSelect={()=>command(["click","box","polygon"][index])}>{label}</Choice>)}</div>
                    <div className={styles.row}>{["건물","프롭","나무"].map((label,index)=><Choice key={label} selected={!!(filters&typeBits[index])} onSelect={()=>command(["buildings","props","trees"][index])}>{label}</Choice>)}</div>
                    <div className={styles.row}>{[3,4].map(index=><Choice key={typeLabels[index]} selected={!!(filters&typeBits[index])} onSelect={()=>command(typeCommands[index])}>{typeLabels[index]}</Choice>)}</div><div className={styles.row}>{[5,6,7].map(index=><Choice key={typeLabels[index]} selected={!!(filters&typeBits[index])} onSelect={()=>command(typeCommands[index])}>{typeLabels[index]}</Choice>)}</div>
                    <div className={styles.row}><Choice selected={underground} onSelect={()=>command("underground.include")}>지하 포함</Choice><Choice selected={!underground} onSelect={()=>command("underground.exclude")}>지하 제외</Choice></div>
                    <p className={styles.hint}>도로·경로·넷레인 복사: 실험 기능 · 최대 64구간</p>
                    <p className={styles.hint}>{mode===2?"클릭으로 꼭짓점 추가 · 첫 점 / Enter로 완료":"Shift로 선택 추가 · 최대 256개"}</p>
                    {mode===2&&<div className={styles.polygonActions}><span>{vertices} / 32점 · 우클릭 한 점 취소</span><Button style={{fontFamily:'"Noto Sans KR"'}} theme={buttonTheme} disabled={vertices<3} onSelect={()=>command("finish")}><ButtonLabel>선택 완료</ButtonLabel></Button></div>}
                    {(rectangle||polygon||searching)&&<p className={styles.live}>{searching?"후보 검색 중 · ":""}영역 안 {preview}개 강조</p>}
                </div>}
                <div className={styles.section}><div className={styles.row}><Choice selected={!absolute&&!groundSnap} onSelect={()=>command("height.terrain")}>지형 기준</Choice><Choice selected={absolute} onSelect={()=>!absolute&&command("height")}>절대값 기준</Choice></div><div className={styles.row}><Choice selected={groundSnap} onSelect={()=>command("height.snap")}>지형에 붙이기</Choice></div></div>
                {copying&&<div className={styles.rotation}><span>회전 <b className={styles.angleValue}>{`${angle.toFixed(1)}°`}</b></span><p className={styles.hint}>우클릭 드래그 · 휠 0.5°</p></div>}
                <div className={styles.actions}>{copying?<Button style={{fontFamily:'"Noto Sans KR"'}} theme={buttonTheme} onSelect={()=>command("cancel")}><ButtonLabel>복사 취소 · Esc</ButtonLabel></Button>:<><Button style={{fontFamily:'"Noto Sans KR"'}} theme={buttonTheme} className={styles.primary} disabled={count===0||vertices>0||searching} onSelect={()=>command("copy")}><ButtonLabel>복사 · Ctrl+C</ButtonLabel></Button><Button style={{fontFamily:'"Noto Sans KR"'}} theme={buttonTheme} disabled={count===0&&vertices===0} onSelect={()=>command("clear")}><ButtonLabel>선택 해제</ButtonLabel></Button></>}</div>
                {copying&&<p className={styles.live}>{canPlace?"클릭하여 배치 · 반복 배치 가능":"위치를 지정하세요"}</p>}
                <p className={styles.status}>{status}</p>
            </div>
        </Panel>}
    </>;
};
const register:ModRegistrar=registry=>{registry.append("GameTopRight",CopyButton);registry.append("Game",CopyPanel);};
export default register;








