const { chromium } = require('playwright');
const URL=require('url').pathToFileURL(require('path').join(__dirname,'../../Web/test.html')).href;
const res=[];const ok=(n,c,d)=>{res.push((c?'OK    ':'FALLA ')+n+(d?'  ['+d+']':''))};
(async()=>{
 const br=await chromium.launch();
 async function page(init){const ctx=await br.newContext({viewport:{width:390,height:844},hasTouch:true});const pg=await ctx.newPage();pg.errs=[];
  pg.on('pageerror',e=>pg.errs.push(e.message));pg.on('console',m=>{if(m.type()==='error'&&!/fonts|ERR_|net::/.test(m.text()))pg.errs.push(m.text())});
  if(init)await pg.addInitScript(init);await pg.goto(URL);return pg}
 const cell=(pg,p)=>pg.evaluate(p=>{const g=__petalo,r=document.getElementById('cv').getBoundingClientRect(),W=g.S.board.width,H=g.S.board.height;return{x:r.left+(p.x+.5)/W*r.width,y:r.top+(H-1-p.y+.5)/H*r.height}},p);
 const idle=pg=>pg.waitForFunction(()=>!__petalo.busy,{timeout:20000});
 const start=async(pg,l)=>{await pg.evaluate(l=>{__petalo.save.unlocked=30;__petalo.startLevel(l)},l);await pg.click('#iplay')};

 // 1. Atrás durante una animación
 { const pg=await page();await start(pg,1);
   const m=await pg.evaluate(()=>__petalo.S.hint());const a=await cell(pg,m.from),b=await cell(pg,m.to);
   await pg.touchscreen.tap(a.x,a.y);await pg.touchscreen.tap(b.x,b.y);await pg.waitForTimeout(200);
   await pg.click('#back');await pg.waitForTimeout(2500);
   ok('Atrás durante animación sin excepción',pg.errs.length===0,pg.errs.join('|'));
   const menuVisible=await pg.evaluate(()=>!document.getElementById('menu').hidden&&document.getElementById('game').hidden);ok('  y queda en el menú',menuVisible);
   // y se puede empezar otro nivel sin estado corrupto
   await pg.click('.lv:nth-child(1)');await pg.click('#iplay');const busy=await pg.evaluate(()=>__petalo.busy);ok('  nivel nuevo no queda bloqueado (busy=false)',!busy);
 }
 // 2. Temporizador de fin de partida tras salir al menú
 { const pg=await page();await start(pg,1);
   await pg.evaluate(()=>{__petalo.S.config.targetScore=10});
   const m=await pg.evaluate(()=>__petalo.S.hint());const a=await cell(pg,m.from),b=await cell(pg,m.to);
   await pg.touchscreen.tap(a.x,a.y);await pg.touchscreen.tap(b.x,b.y);await idle(pg);
   await pg.click('#back');await pg.click('.lv:nth-child(2)');await pg.waitForTimeout(700);
   const endShown=await pg.evaluate(()=>!document.getElementById('end').hidden);ok('Panel de fin no aparece sobre otro nivel',!endShown);
 }
 // 3. Victoria: estrellas, desbloqueo, premio, guardado; nivel 30 sin «Siguiente»
 { const pg=await page();await start(pg,1);await pg.evaluate(()=>{__petalo.S.config.targetScore=10});
   const m=await pg.evaluate(()=>__petalo.S.hint());const a=await cell(pg,m.from),b=await cell(pg,m.to);
   await pg.touchscreen.tap(a.x,a.y);await pg.touchscreen.tap(b.x,b.y);await idle(pg);await pg.waitForTimeout(500);
   const r=await pg.evaluate(()=>({st:__petalo.S.state,end:!document.getElementById('end').hidden,title:document.getElementById('etitle').textContent,stars:document.getElementById('stars').textContent,sv:JSON.parse(localStorage.getItem('petalopop.web.v1')),nextHidden:document.getElementById('enext').hidden}));
   ok('Victoria detectada y panel visible',r.st==='Won'&&r.end,r.title+' '+r.stars);ok('Estrellas guardadas y nivel 2 desbloqueado',r.sv.unlocked>=2&&r.sv.stars[0]>=1,'unlocked='+r.sv.unlocked);
   await pg.click('#enext');const lv=await pg.evaluate(()=>document.getElementById('ltitle').textContent);ok('Botón Siguiente lleva al nivel 2',lv==='Nivel 2',lv);
   await pg.click('#iplay');
   await start(pg,30);await pg.evaluate(()=>{__petalo.S.config.targetScore=1;__petalo.S.objectives.forEach(o=>o.target=0)});
   const m2=await pg.evaluate(()=>__petalo.S.hint());const a2=await cell(pg,m2.from),b2=await cell(pg,m2.to);await pg.touchscreen.tap(a2.x,a2.y);await pg.touchscreen.tap(b2.x,b2.y);await idle(pg);await pg.waitForTimeout(500);
   ok('Nivel 30 ganado: sin botón Siguiente',await pg.evaluate(()=>document.getElementById('enext').hidden));
   ok('Sin errores en flujo de victoria',pg.errs.length===0,pg.errs.join('|'));
 }
 // 4. Derrota
 { const pg=await page();await start(pg,1);await pg.evaluate(()=>{__petalo.S.movesLeft=1});
   const m=await pg.evaluate(()=>__petalo.S.hint());const a=await cell(pg,m.from),b=await cell(pg,m.to);await pg.touchscreen.tap(a.x,a.y);await pg.touchscreen.tap(b.x,b.y);await idle(pg);await pg.waitForTimeout(500);
   const r=await pg.evaluate(()=>({st:__petalo.S.state,t:document.getElementById('etitle').textContent,boDisabled:[...document.querySelectorAll('.bo')].every(b=>b.disabled)}));
   ok('Derrota: panel y boosters desactivados',r.st==='Lost'&&r.boDisabled,r.t);
   await pg.click('#eretry');const r2=await pg.evaluate(()=>({m:__petalo.S.movesLeft,s:__petalo.S.state,end:document.getElementById('end').hidden}));ok('Reintentar reinicia el nivel',r2.s==='Playing'&&r2.m===20&&r2.end,JSON.stringify(r2));
 }
 // 5. Spam de entradas durante animación
 { const pg=await page();await start(pg,3);const mv0=await pg.evaluate(()=>__petalo.S.movesLeft);
   await pg.evaluate(async()=>{const g=__petalo,m=g.S.hint();const p=g.run(()=>g.S.trySwap(m.from,m.to));for(let i=0;i<30;i++){const mm=g.S.hint();if(mm)g.run(()=>g.S.trySwap(mm.from,mm.to))}await p});
   await idle(pg);const r=await pg.evaluate(()=>({moves:__petalo.S.movesLeft,full:__petalo.S.board.isFull()}));
   ok('30 jugadas simultáneas: solo se procesa 1 (busy)',r.moves===mv0-1,'moves '+r.moves+' de '+mv0);
   ok('  sin errores',pg.errs.length===0,pg.errs.join('|'));
 }
 // 6. localStorage corrupto / no disponible
 { const pg=await page(()=>{localStorage.setItem('petalopop.web.v1','{{{no json')});ok('JSON corrupto: arranca con guardado por defecto',await pg.evaluate(()=>__petalo.save.unlocked===1)&&pg.errs.length===0,pg.errs.join('|'))}
 { const pg=await page(()=>{localStorage.setItem('petalopop.web.v1',JSON.stringify({unlocked:5,stars:'x'}))});ok('Guardado con forma inválida (stars no es array): no revienta y se normaliza',pg.errs.length===0&&await pg.evaluate(()=>__petalo.save.stars.length===30),pg.errs.join('|'))}
 { const pg=await page(()=>{Object.defineProperty(window,'localStorage',{get(){throw new Error('SecurityError')}})});
   await start(pg,1);const m=await pg.evaluate(()=>__petalo.S.hint());const a=await cell(pg,m.from),b=await cell(pg,m.to);await pg.touchscreen.tap(a.x,a.y);await pg.touchscreen.tap(b.x,b.y);await idle(pg);
   ok('localStorage bloqueado: el juego sigue funcionando',pg.errs.length===0&&await pg.evaluate(()=>__petalo.S.score>0),pg.errs.join('|'))}
 { const pg=await page(()=>{localStorage.setItem('petalopop.web.v1',JSON.stringify({unlocked:9999,stars:[9,9],best:[],inv:{0:-5}}))});
   const r=await pg.evaluate(()=>({u:__petalo.save.unlocked,inv:JSON.stringify(__petalo.save.inv),btn:document.querySelectorAll('.lv').length}));ok('Guardado manipulado (unlocked=9999, inventario negativo) se sanea',r.u<=30&&!/-/.test(r.inv),JSON.stringify(r))}
 // 7. Redimensionar durante animación / girar
 { const pg=await page();await start(pg,2);await pg.evaluate(()=>{const g=__petalo,m=g.S.hint();g.run(()=>g.S.trySwap(m.from,m.to))});await pg.waitForTimeout(100);
   await pg.setViewportSize({width:844,height:390});await pg.waitForTimeout(100);await pg.setViewportSize({width:390,height:844});await idle(pg);
   const r=await pg.evaluate(()=>{const c=document.getElementById('cv');return{w:c.width,h:c.height,cw:c.clientWidth}});ok('Girar durante animación: canvas coherente',r.w===r.h&&r.cw>0&&pg.errs.length===0,JSON.stringify(r)+pg.errs.join('|'))}
 // 8. Toque fuera del tablero / arrastre fuera / multitáctil
 { const pg=await page();await start(pg,1);const c=await cell(pg,{x:0,y:0});
   await pg.mouse.move(c.x,c.y);await pg.mouse.down();await pg.mouse.move(c.x-300,c.y,{steps:5});await pg.mouse.up();await idle(pg);
   const m1=await pg.evaluate(()=>__petalo.S.movesLeft);ok('Arrastrar fuera del borde no gasta jugada ni falla',m1===20&&pg.errs.length===0,'moves='+m1+pg.errs.join('|'));
   await pg.mouse.move(c.x,c.y);await pg.mouse.down();await pg.mouse.move(c.x+10,c.y+2,{steps:2});await pg.mouse.up();
   ok('Movimiento corto (<umbral) se trata como toque',pg.errs.length===0) }
 // 9. Boosters en celdas inválidas no gastan inventario
 { const pg=await page();await start(pg,1);
   const r=await pg.evaluate(async()=>{const g=__petalo,P=g.C.P;const i0=JSON.stringify(g.S.boosters);
    await g.run(()=>g.S.useObstacleRemover(P(2,2)));await g.run(()=>g.S.useSpecialMaker(P(2,2),g.C.SK.Bomb));await g.run(()=>g.S.useSpecialMaker(P(2,2),g.C.SK.Bomb));
    return{i0,i1:JSON.stringify(g.S.boosters)}});
   ok('Limpiar sobre casilla normal no consume; Bomba sobre bomba no consume 2.ª vez',JSON.parse(r.i1)[2]===2&&JSON.parse(r.i1)[4]===1,r.i1)}
 // 10. Armar booster y salir al menú / reiniciar
 { const pg=await page();await start(pg,1);await pg.click('.bo:nth-child(1)');await pg.click('#back');await start(pg,1);
   const on=await pg.evaluate(()=>document.querySelectorAll('.bo.on').length);ok('Booster armado no sobrevive a un nivel nuevo',on===0,'armados='+on)}
 // 11. Doble clic rápido en nivel
 { const pg=await page();await pg.click('.lv:nth-child(1)',{clickCount:2});await pg.waitForTimeout(200);ok('Doble clic en un nivel sin errores',pg.errs.length===0,pg.errs.join('|'))}
 console.log(res.join('\n'));await br.close()})();
