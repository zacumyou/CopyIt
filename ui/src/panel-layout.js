/* Shared by independently installable city mod UI bundles. */
(function () {
  if (window.csPanelLayoutV1) return;
  const entries = new Map();
  let timer = 0;

  function choose(box, occupied, width, height, gap) {
    const limit = (n, max) => Math.max(0, Math.min(n, max));
    const xs = [box.x, 0, width - box.w], ys = [box.y, 0, height - box.h];
    for (const b of occupied) {
      xs.push(b.x - box.w - gap, b.x + b.w + gap, b.x - box.w, b.x + b.w);
      ys.push(b.y - box.h - gap, b.y + b.h + gap, b.y - box.h, b.y + b.h);
    }
    let best, rank;
    for (const rawX of xs) for (const rawY of ys) {
      const x = limit(rawX, width - box.w), y = limit(rawY, height - box.h);
      let covered = 0, header = 0, clearance = 0;
      for (const b of occupied) {
        const span = (lo, hi, start, end) => Math.max(0, Math.min(hi, end) - Math.max(lo, start));
        const w = span(x, x + box.w, b.x, b.x + b.w);
        covered += w * span(y, y + box.h, b.y, b.y + b.h);
        header += w * span(y, y + box.h, b.y, b.y + Math.min(32,b.h));
        clearance += span(x, x + box.w, b.x-gap, b.x+b.w+gap) * span(y, y+box.h, b.y-gap, b.y+b.h+gap);
      }
      const score = [covered, header, clearance, (x-box.x)**2 + (y-box.y)**2];
      const diff = rank ? score.findIndex((v,i) => Math.abs(v-rank[i]) > 0.01) : -1;
      if (!rank || (diff >= 0 && score[diff] < rank[diff])) {
        best = {x,y,w:box.w,h:box.h}; rank = score;
        if (score.every(v => v === 0)) return best;
      }
    }
    return best;
  }

  function offset(e, dx, dy) {
    if (dx === e.dx && dy === e.dy) return;
    e.dx = dx; e.dy = dy;
    const style = e.node.style;
    // Right-anchored and left-anchored panels move in opposite margin directions.
    style.marginRight = dx ? -dx + 'px' : '';
    style.marginLeft = dx ? dx + 'px' : '';
    style.marginTop = dy ? dy + 'px' : '';
  }

  function layout() {
    const width = window.innerWidth, height = window.innerHeight;
    const occupied = [];
    const gap = 12 * (parseFloat(getComputedStyle(document.documentElement).fontSize) || 1);
    for (const obstacle of document.querySelectorAll('.tlki-panel,.pynb-editor')) {
      if (getComputedStyle(obstacle).visibility === 'hidden') continue;
      const r = obstacle.getBoundingClientRect();
      if (r.width && r.height) occupied.push({x:r.left,y:r.top,w:r.width,h:r.height});
    }
    for (const [node,e] of Array.from(entries).sort((a,b)=>Number(b[1].placed || b[1].manual)-Number(a[1].placed || a[1].manual))) {
      if (!document.body.contains(node) || node.getAttribute('aria-hidden') === 'true') {
        entries.delete(node); continue;
      }
      const rect = node.getBoundingClientRect();
      if (!rect.width || !rect.height) continue;
      let left=rect.left, top=rect.top, right=rect.right, bottom=rect.bottom;
      if (e.parts) for (const part of node.querySelectorAll(e.parts)) {
        if (part.style.position === 'fixed') continue;
        const r = part.getBoundingClientRect();
        if (r.width && r.height) {
          left=Math.min(left,r.left); top=Math.min(top,r.top);
          right=Math.max(right,r.right); bottom=Math.max(bottom,r.bottom);
        }
      }
      const box = {x:left,y:top,w:right-left,h:bottom-top};
      if (e.placed || e.manual) {
        occupied.push(box);
        continue;
      }
      const next = choose(box,occupied,width,height,gap);
      offset(e,next.x-left,next.y-top);
      e.placed = true;
      occupied.push(next);
    }
    if (timer && !Array.from(entries.values()).some(e => !e.placed && !e.manual)) { clearInterval(timer); timer=0; }
  }

  window.csPanelLayoutV1 = {
    add(node, parts) {
      if (!node || entries.has(node)) return;
      entries.set(node,{node,parts,dx:0,dy:0,coords:node.style.left+'/'+node.style.top,manual:false,placed:false});
      if (!timer) timer=setInterval(layout,200);
      layout();
    },
    drag(node) {
      const e=entries.get(node);
      if (e) e.manual=true;
    },
    moved(node) {
      const e=entries.get(node);
      if (e && e.manual && node.style.left+'/'+node.style.top !== e.coords) {
        offset(e,0,0); e.coords=node.style.left+'/'+node.style.top;
      }
    }
  };
})();