const { chromium } = require('playwright');
(async()=>{const br=await chromium.launch({args:['--enable-precise-memory-info']});
 const ctx=await br.newContext({viewport:{width:390,height:844},deviceScaleFactor:2,hasTouch:true});const pg=await ctx.newPage();const errs=[];
 pg.on('pageerror',e=>errs.push(e.message));
 await pg.goto(require('url').pathToFileURL(require('path').join(__dirname,'../../Web/test.html')).href);
 await pg.evaluate(()=>{__petalo.save.unlocked=30;__petalo.startLevel(30)});await pg.click('#iplay');
 // cpu throttle 4x ~ móvil gama media
 const cdp=await ctx.newCDPSession(pg);await cdp.send('Emulation.setCPUThrottlingRate',{rate:4});
 const r=await pg.evaluate(async()=>{
  const g=__petalo;const frames=[];let last=performance.now(),run=true;
  (function loop(t){if(!run)return;frames.push(t-last);last=t;requestAnimationFrame(loop)})(last);
  g.S.config.targetScore=1e9;g.S.movesLeft=1000;
  const heap0=performance.memory.usedJSHeapSize;let n=0,heapMid=0;
  const t0=performance.now();
  for(let i=0;i<120;i++){const m=g.S.hint();if(!m)break;await g.run(()=>g.S.trySwap(m.from,m.to));n++;if(i===20)heapMid=performance.memory.usedJSHeapSize}
  run=false;const dt=performance.now()-t0;
  frames.sort((a,b)=>a-b);const med=frames[frames.length>>1],p95=frames[Math.floor(frames.length*.95)],max=frames[frames.length-1];
  return{moves:n,secs:(dt/1000).toFixed(1),frames:frames.length,medMs:med.toFixed(1),p95Ms:p95.toFixed(1),maxMs:max.toFixed(1),heapStartMB:(heap0/1e6).toFixed(1),heapAt20MB:(heapMid/1e6).toFixed(1),heapEndMB:(performance.memory.usedJSHeapSize/1e6).toFixed(1)}});
 console.log('9x9, CPU 4x más lento, 120 jugadas animadas:',JSON.stringify(r));
 // coste del núcleo por jugada (sin animar)
 const c=await pg.evaluate(()=>{const g=__petalo,C=g.C;const s=new C.GameSession(C.getLevel(30),{});s.config.targetScore=1e9;s.movesLeft=1e6;let t=performance.now(),n=0,worst=0;for(let i=0;i<300&&s.state==='Playing';i++){const m=s.hint();if(!m)break;const a=performance.now();s.trySwap(m.from,m.to);worst=Math.max(worst,performance.now()-a);n++}return{n,avgMs:((performance.now()-t)/n).toFixed(2),worstMs:worst.toFixed(1)}});
 console.log('núcleo por jugada (con CPU 4x):',JSON.stringify(c));
 console.log('errores:',errs.length?errs.join('|'):'ninguno');await br.close()})();
