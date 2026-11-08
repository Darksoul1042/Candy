// Misma política que Tests/Dump/Program.cs; la salida debe ser idéntica.
const C = require('./core.js');
const { P, SK, Booster } = C;
const inv = { [Booster.Hammer]: 3, [Booster.FreeSwap]: 2, [Booster.ObstacleRemover]: 2, [Booster.ExtraMoves]: 2, [Booster.SpecialMaker]: 2 };
const out = [];
for (let l = 1; l <= C.LEVEL_COUNT; l++) {
  const cfg = C.getLevel(l); cfg.moves = 500; cfg.targetScore = Math.floor(2147483647 / 4);
  const s = new C.GameSession(cfg, inv);
  const w = cfg.width, h = cfg.height; let acc = 0, rej = 0, steps = 0;
  for (let i = 0; i < 60 && s.state === 'Playing'; i++) {
    let r;
    if (i % 10 === 5) r = s.useHammer(P(i % w, (i * 3) % h));
    else if (i % 10 === 8) r = s.useObstacleRemover(P((i * 5) % w, (i * 7) % h));
    else if (i % 12 === 11) r = s.useExtraMoves();
    else if (i % 15 === 13) r = s.useSpecialMaker(P(i % w, (i * 2) % h), SK.Bomb);
    else if (i % 17 === 16) r = s.useFreeSwap(P(i % (w - 1), i % h), P((i % (w - 1)) + 1, i % h));
    else {
      const all = C.findAllMoves(s.board); if (all.length === 0) break;
      const m = all[(i * 7 + 3) % all.length]; r = s.trySwap(m.from, m.to);
    }
    if (r.accepted) acc++; else rej++;
    steps += r.steps.length;
  }
  const b = s.board; let d = '';
  for (let y = b.height - 1; y >= 0; y--) { for (let x = 0; x < b.width; x++) { const p = P(x, y); d += `${b.get(p)}.${b.special(p)}.${b.hp(p)}.${b.jelly(p)} `; } d += '|'; }
  const obj = s.objectives.map((o) => `${o.current}/${o.target}`).join(',');
  const iv = [0, 1, 2, 3, 4].map((k) => s.count(k)).join(',');
  out.push(`L${l} score=${s.score} moves=${s.movesLeft} acc=${acc} rej=${rej} steps=${steps} obj=${obj} inv=${iv} board=${d}`);
}
console.log(out.join('\n'));
