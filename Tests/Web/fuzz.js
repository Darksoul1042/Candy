// QA: fuzz del núcleo con invariantes. Política aleatoria incluyendo jugadas inválidas, boosters en celdas arbitrarias.
const C=require('../../Web/core.js');const {P,SK,Booster}=C;
let seed=12345;const rnd=n=>{seed=(seed*1103515245+12345)&0x7fffffff;return seed%n};
const fails=new Map();const bad=(k,m)=>{if(!fails.has(k))fails.set(k,[]);if(fails.get(k).length<3)fails.get(k).push(m)};
function snap(b){return Array.from(b.cells).join()+'|'+Array.from(b.specials).join()+'|'+Array.from(b.hpv).join()}
// replay de pasos sobre copia: debe igualar el tablero final (igual que el test C#)
function replay(b0,steps){const b=b0.clone();const g=new Map();
 for(const st of steps){
  if(st.kind==='Swap')b.swap(st.a,st.b);
  else if(st.kind==='SwapBack'){/* no cambia el modelo: el swap inválido no se aplicó */ }
  else if(st.kind==='Clear'){for(const p of st.cleared)b.clearCell(p);for(const c of st.created)b.setSpecial(c.pos,c.kind)}
  else if(st.kind==='Fall'){const t=st.moves.map(m=>[m,b.get(m.from),b.special(m.from),b.hp(m.from)]);for(const m of st.moves)b.clearCell(m.from);for(const m of st.moves){b.set(m.to,m.type);b.setSpecial(m.to,m.special);b.setHp(m.to,m.hp)}}
  else if(st.kind==='Spawn'){for(const m of st.moves){b.set(m.to,m.type);b.setSpecial(m.to,0);b.setHp(m.to,0)}}
  else if(st.kind==='Shuffle'){const t=st.moves.map(m=>[m.to,m.type,m.special]);for(const[p,ty,sp]of t){b.set(p,ty);b.setSpecial(p,sp)}}
 }return b}
let actions=0,runs=0;
for(let l=1;l<=30;l++)for(let run=0;run<15;run++){
 const cfg=C.getLevel(l);if(run%3===0){cfg.targetScore=1e9}// alargar partidas
 const inv={0:5,1:5,2:5,3:5,4:5};const s=new C.GameSession(cfg,inv);runs++;
 const W=cfg.width,H=cfg.height;let prevScore=0;
 for(let i=0;i<120&&s.state==='Playing';i++){
  const before=s.board.clone(),ml=s.movesLeft;let r,kind;
  const k=rnd(14);
  const rp=()=>P(rnd(W),rnd(H)), nb=()=>{const a=rp();const d=[[1,0],[-1,0],[0,1],[0,-1]][rnd(4)];return[a,P(a.x+d[0],a.y+d[1])]};
  try{
  if(k===0){kind='hammer';r=s.useHammer(rp())}
  else if(k===1){kind='remover';r=s.useObstacleRemover(rp())}
  else if(k===2){kind='extra';r=s.useExtraMoves()}
  else if(k===3){kind='maker';r=s.useSpecialMaker(rp(),[1,2,3,4,0,9][rnd(6)])}
  else if(k===4){kind='free';const[a,b]=nb();r=s.useFreeSwap(a,b)}
  else if(k<=6){kind='randswap';const[a,b]=nb();r=s.trySwap(a,b)}
  else if(k===7){kind='oob';r=s.trySwap(P(-1,0),P(0,0));r=s.useHammer(P(99,99))}
  else{kind='goodswap';const m=C.findAllMoves(s.board);if(!m.length){bad('nomoves-while-playing',`L${l}`);break}const mv=m[rnd(m.length)];r=s.trySwap(mv.from,mv.to)}
  }catch(e){bad('EXCEPTION '+kind,`L${l} i${i}: ${e.message}`);break}
  actions++;
  const b=s.board,tag=`L${l} i${i} ${kind}`;
  if(s.movesLeft<0)bad('movesLeft<0',tag);
  if(!Number.isFinite(s.score)||s.score<prevScore)bad('score no monótono/NaN',tag);prevScore=s.score;
  for(const k2 in s.boosters)if(s.boosters[k2]<0)bad('inventario<0',tag);
  if(!b.isFull())bad('tablero con huecos',tag);
  if(s.state==='Playing'){
   if(C.findMatches(b).length)bad('matches residuales',tag);
   if(!C.hasMove(b))bad('sin movimientos y sin barajar',tag);
  }
  for(let x=0;x<W;x++)for(let y=0;y<H;y++){const t=b.at(x,y);if(t>C.Board.prototype.constructor.length+10&&false){} if(t>cfg.typeCount)bad('tipo fuera de rango',tag);
   const p=P(x,y);if(t<=0&&b.special(p)!==0)bad('especial sobre obstáculo',tag);if(t===C.STONE&&b.hp(p)!==0)bad('piedra con hp',tag);if(t===C.CRATE&&b.hp(p)<=0)bad('caja sin hp',tag);if(b.hp(p)<0||b.jelly(p)<0)bad('hp/jelly negativo',tag)}
  if(r&&r.accepted&&r.steps.length&&kind!=='maker'&&kind!=='extra'){const rb=replay(before,r.steps);
   // el replay parte del tablero previo a la acción; hammer/remover/freeSwap modifican fuera de los pasos (hp/jelly), comparar solo tipo+especial
   const A=Array.from(rb.cells).join()+'|'+Array.from(rb.specials).join(),B2=Array.from(b.cells).join()+'|'+Array.from(b.specials).join();
   if(A!==B2&&kind!=='remover')bad('replay != tablero final',tag)}
  if(r&&!r.accepted&&kind==='randswap'&&(snap(before)!==snap(s.board)||s.movesLeft!==ml))bad('swap rechazado modificó estado',tag);
  if(r&&r.accepted&&kind==='randswap'&&s.movesLeft!==ml-1)bad('swap aceptado no gastó 1 movimiento',tag);
  if(r&&r.accepted&&(kind==='hammer'||kind==='remover'||kind==='free'||kind==='maker')&&s.movesLeft!==ml)bad('booster gastó movimientos',tag);
 }
}
console.log('runs',runs,'acciones',actions);
if(!fails.size)console.log('FUZZ: sin violaciones de invariantes');else for(const[k,v]of fails)console.log('FALLO',k,'->',v.join(' ; '));
