// Pétalo Pop — núcleo del juego en JavaScript. Port 1:1 de Assets/Scripts/Core (C#); se verifica contra C# con Tests/diff.
(function (root) {
  'use strict';
  const EMPTY = 0, CRATE = -1, STONE = -2;
  const SK = { None: 0, LineH: 1, LineV: 2, Bomb: 3, ColorBurst: 4 };
  const Obj = { CollectType: 0, ClearJelly: 1, BreakFrozen: 2, DestroyCrates: 3 };
  const Booster = { Hammer: 0, FreeSwap: 1, ObstacleRemover: 2, ExtraMoves: 3, SpecialMaker: 4 };
  const P = (x, y) => ({ x, y });
  const key = (p) => p.y * 64 + p.x;

  class PosSet {
    constructor() { this.m = new Map(); }
    add(p) { const k = key(p); if (this.m.has(k)) return false; this.m.set(k, p); return true; }
    has(p) { return this.m.has(key(p)); }
    delete(p) { return this.m.delete(key(p)); }
    get size() { return this.m.size; }
    values() { return Array.from(this.m.values()); }
    unionWith(list) { for (const p of list) this.add(p); }
    exceptWith(list) { for (const p of list) this.delete(p); }
  }

  class GameRng {
    constructor(seed) {
      this.s = BigInt.asUintN(64, BigInt(seed >>> 0) * 0x9E3779B97F4A7C15n + 0x1234567n);
      this.nextU32();
    }
    clone() { const r = Object.create(GameRng.prototype); r.s = this.s; return r; }
    nextU32() {
      this.s = BigInt.asUintN(64, this.s + 0x9E3779B97F4A7C15n);
      let z = this.s;
      z = BigInt.asUintN(64, (z ^ (z >> 30n)) * 0xBF58476D1CE4E5B9n);
      z = BigInt.asUintN(64, (z ^ (z >> 27n)) * 0x94D049BB133111EBn);
      return Number(BigInt.asUintN(32, (z ^ (z >> 31n)) >> 32n));
    }
    next(max) { return this.nextU32() % max; }
    nextRange(min, max) { return min + this.next(max - min); }
  }

  class Board {
    constructor(w, h) {
      this.width = w; this.height = h;
      const n = w * h;
      this.cells = new Int32Array(n); this.specials = new Uint8Array(n); this.hpv = new Int32Array(n); this.jellyv = new Int32Array(n);
    }
    i(p) { return p.x * this.height + p.y; }
    get(p) { return this.cells[this.i(p)]; }
    set(p, v) { this.cells[this.i(p)] = v; }
    at(x, y) { return this.cells[x * this.height + y]; }
    special(p) { return this.specials[this.i(p)]; }
    setSpecial(p, k) { this.specials[this.i(p)] = k; }
    hp(p) { return this.hpv[this.i(p)]; }
    setHp(p, v) { this.hpv[this.i(p)] = v; }
    jelly(p) { return this.jellyv[this.i(p)]; }
    setJelly(p, v) { this.jellyv[this.i(p)] = v; }
    inBounds(p) { return p.x >= 0 && p.y >= 0 && p.x < this.width && p.y < this.height; }
    isMovable(p) { return this.get(p) > 0 && this.hp(p) === 0; }
    swap(a, b) {
      const ia = this.i(a), ib = this.i(b);
      for (const arr of [this.cells, this.specials, this.hpv]) { const t = arr[ia]; arr[ia] = arr[ib]; arr[ib] = t; }
    }
    clearCell(p) { const k = this.i(p); this.cells[k] = EMPTY; this.specials[k] = SK.None; this.hpv[k] = 0; }
    clone() {
      const c = new Board(this.width, this.height);
      c.cells.set(this.cells); c.specials.set(this.specials); c.hpv.set(this.hpv); c.jellyv.set(this.jellyv);
      return c;
    }
    isFull() { return !this.cells.includes(EMPTY); }
    countSpecials() { let n = 0; for (const s of this.specials) if (s !== SK.None) n++; return n; }
    countWhere(f) { let n = 0; for (let x = 0; x < this.width; x++) for (let y = 0; y < this.height; y++) if (f(P(x, y))) n++; return n; }
  }
  const adjacent = (a, b) => Math.abs(a.x - b.x) + Math.abs(a.y - b.y) === 1;

  // ---------------------------------------------------------------- líneas
  function findMatches(b) {
    const res = [];
    const scan = (horizontal, fixed, length) => {
      const get = (i) => (horizontal ? b.at(i, fixed) : b.at(fixed, i));
      let runStart = 0;
      for (let i = 1; i <= length; i++) {
        const prev = get(i - 1), cur = i < length ? get(i) : -1;
        if (cur === prev && prev > 0) continue;
        if (prev > 0 && i - runStart >= 3) {
          const m = { type: prev, horizontal, cells: [] };
          for (let k = runStart; k < i; k++) m.cells.push(horizontal ? P(k, fixed) : P(fixed, k));
          res.push(m);
        }
        runStart = i;
      }
    };
    for (let y = 0; y < b.height; y++) scan(true, y, b.width);
    for (let x = 0; x < b.width; x++) scan(false, x, b.height);
    return res;
  }
  function countDir(b, p, dx, dy, t) { let n = 0, x = p.x + dx, y = p.y + dy; while (b.inBounds(P(x, y)) && b.at(x, y) === t) { n++; x += dx; y += dy; } return n; }
  function isPartOfMatch(b, p) {
    const t = b.get(p); if (t <= 0) return false;
    if (1 + countDir(b, p, -1, 0, t) + countDir(b, p, 1, 0, t) >= 3) return true;
    return 1 + countDir(b, p, 0, -1, t) + countDir(b, p, 0, 1, t) >= 3;
  }

  // ---------------------------------------------------------------- especiales
  function isComboSwap(b, a, c) {
    const ka = b.special(a), kc = b.special(c);
    if (ka === SK.None && kc === SK.None) return false;
    if (ka === SK.ColorBurst || kc === SK.ColorBurst) return true;
    return ka !== SK.None && kc !== SK.None;
  }
  function newOutcome() {
    return { cleared: new PosSet(), forceClear: new PosSet(), created: [], activated: [], hits: [],
      stats: { clearedByType: new Map(), jellyCleared: 0, frozenBroken: 0, cratesDestroyed: 0, stonesRemoved: 0 } };
  }
  function addRow(b, set, y) { if (y < 0 || y >= b.height) return; for (let x = 0; x < b.width; x++) if (b.at(x, y) !== EMPTY) set.add(P(x, y)); }
  function addCol(b, set, x) { if (x < 0 || x >= b.width) return; for (let y = 0; y < b.height; y++) if (b.at(x, y) !== EMPTY) set.add(P(x, y)); }
  function addArea(b, set, c, r) {
    for (let x = c.x - r; x <= c.x + r; x++) for (let y = c.y - r; y <= c.y + r; y++) if (b.inBounds(P(x, y)) && b.at(x, y) !== EMPTY) set.add(P(x, y));
  }
  function effect(b, p, k) {
    const set = new PosSet();
    if (k === SK.LineH) addRow(b, set, p.y);
    else if (k === SK.LineV) addCol(b, set, p.x);
    else if (k === SK.Bomb) addArea(b, set, p, 1);
    else if (k === SK.ColorBurst) { const t = b.get(p); for (let x = 0; x < b.width; x++) for (let y = 0; y < b.height; y++) if (b.at(x, y) === t) set.add(P(x, y)); }
    return set.values();
  }
  function expand(b, o, pre, protect) {
    const queue = o.cleared.values();
    const done = new PosSet(); done.unionWith(pre.values());
    for (let qi = 0; qi < queue.length; qi++) {
      const p = queue[qi], k = b.special(p);
      if (k === SK.None || done.has(p) || protect.has(p)) continue;
      if (b.get(p) > 0 && b.hp(p) > 0 && !o.forceClear.has(p)) continue;
      done.add(p);
      o.activated.push({ pos: p, kind: k });
      for (const e of effect(b, p, k)) if (o.cleared.add(e)) queue.push(e);
    }
  }
  function overlaps(l1, l2) { for (const p of l1) for (const q of l2) if (p.x === q.x && p.y === q.y) return true; return false; }
  function pickSpawn(b, comp, cells, a, c, kind) {
    const cand = [];
    if (a && cells.has(a)) cand.push(a);
    if (c && cells.has(c)) cand.push(c);
    if (kind === SK.Bomb) {
      for (const h of comp.filter((m) => m.horizontal)) for (const v of comp.filter((m) => !m.horizontal)) for (const p of h.cells) if (v.cells.some((q) => q.x === p.x && q.y === p.y)) cand.push(p);
    }
    let longest = comp[0]; for (const m of comp) if (m.cells.length > longest.cells.length) longest = m;
    cand.push(longest.cells[Math.floor(longest.cells.length / 2)]);
    cand.push(...cells.values());
    for (const p of cand) if (b.special(p) === SK.None) return p;
    return null;
  }
  function fromMatches(b, matches, swapA, swapB) {
    const o = newOutcome();
    let comps = [];
    for (const m of matches) {
      const touching = comps.filter((c) => c.some((x) => overlaps(x.cells, m.cells)));
      const merged = [m];
      for (const t of touching) { merged.push(...t); comps = comps.filter((c) => c !== t); }
      comps.push(merged);
    }
    const protect = new PosSet();
    for (const comp of comps) {
      const cells = new PosSet();
      for (const m of comp) cells.unionWith(m.cells);
      o.cleared.unionWith(cells.values());
      const maxRun = Math.max(...comp.map((m) => m.cells.length));
      const hasH = comp.some((m) => m.horizontal), hasV = comp.some((m) => !m.horizontal);
      let kind = SK.None;
      if (maxRun >= 5) kind = SK.ColorBurst;
      else if (hasH && hasV) kind = SK.Bomb;
      else if (maxRun === 4) kind = comp.find((m) => m.cells.length === 4).horizontal ? SK.LineV : SK.LineH;
      if (kind === SK.None) continue;
      const pos = pickSpawn(b, comp, cells, swapA, swapB, kind);
      if (!pos) continue;
      o.created.push({ pos, type: comp[0].type, kind });
      protect.add(pos);
    }
    o.cleared.exceptWith(protect.values());
    expand(b, o, new PosSet(), protect);
    o.cleared.exceptWith(protect.values());
    return o;
  }
  function fromSwapCombo(b, a, c) {
    if (!isComboSwap(b, a, c)) return null;
    const o = newOutcome(), pre = new PosSet();
    const ka = b.special(a), kc = b.special(c);
    if (ka === SK.ColorBurst && kc === SK.ColorBurst) {
      for (let x = 0; x < b.width; x++) for (let y = 0; y < b.height; y++) if (b.at(x, y) !== EMPTY) o.cleared.add(P(x, y));
      o.activated.push({ pos: a, kind: ka }); o.activated.push({ pos: c, kind: kc });
      pre.add(a); pre.add(c);
    } else if (ka === SK.ColorBurst || kc === SK.ColorBurst) {
      const burst = ka === SK.ColorBurst ? a : c, other = ka === SK.ColorBurst ? c : a;
      const ko = b.special(other), t = b.get(other);
      o.cleared.add(burst);
      o.activated.push({ pos: burst, kind: SK.ColorBurst });
      pre.add(burst);
      for (let x = 0; x < b.width; x++) for (let y = 0; y < b.height; y++) {
        const p = P(x, y);
        if (b.get(p) !== t) continue;
        o.cleared.add(p);
        if (ko !== SK.None) b.setSpecial(p, ko);
      }
    } else {
      const lineA = ka === SK.LineH || ka === SK.LineV, lineC = kc === SK.LineH || kc === SK.LineV;
      if (lineA && lineC) { addRow(b, o.cleared, c.y); addCol(b, o.cleared, c.x); }
      else if (ka === SK.Bomb && kc === SK.Bomb) addArea(b, o.cleared, c, 2);
      else for (let d = -1; d <= 1; d++) { addRow(b, o.cleared, c.y + d); addCol(b, o.cleared, c.x + d); }
      o.cleared.add(a); o.cleared.add(c);
      o.activated.push({ pos: a, kind: ka }); o.activated.push({ pos: c, kind: kc });
      pre.add(a); pre.add(c);
    }
    expand(b, o, pre, new PosSet());
    return o;
  }

  // ---------------------------------------------------------------- obstáculos
  function addType(stats, t) { stats.clearedByType.set(t, (stats.clearedByType.get(t) || 0) + 1); }
  function filterObstacles(b, o) {
    const original = o.cleared.values();
    const crateHit = new PosSet();
    const DX = [1, -1, 0, 0], DY = [0, 0, 1, -1];
    for (const p of original) for (let i = 0; i < 4; i++) {
      const n = P(p.x + DX[i], p.y + DY[i]);
      if (b.inBounds(n) && b.get(n) === CRATE) crateHit.add(n);
    }
    const remove = new PosSet(), destroyed = [];
    for (const p of original) {
      const t = b.get(p), force = o.forceClear.has(p);
      if (t === STONE) o.stats.stonesRemoved++;
      else if (t === CRATE) {
        if (force) { o.stats.cratesDestroyed++; crateHit.delete(p); }
        else { remove.add(p); crateHit.add(p); }
      } else if (t > 0 && b.hp(p) > 0 && !force) {
        const hp = b.hp(p) - 1; b.setHp(p, hp);
        o.hits.push({ pos: p, kind: 'Frozen', remaining: hp });
        if (hp === 0) o.stats.frozenBroken++;
        remove.add(p);
      }
    }
    for (const p of crateHit.values()) {
      if (b.get(p) !== CRATE) continue;
      const hp = b.hp(p) - 1; b.setHp(p, hp);
      o.hits.push({ pos: p, kind: 'Crate', remaining: hp });
      if (hp <= 0) { destroyed.push(p); o.stats.cratesDestroyed++; }
    }
    o.cleared.exceptWith(remove.values());
    for (const p of destroyed) o.cleared.add(p);
    for (const p of [...o.cleared.values(), ...o.created.map((c) => c.pos)]) {
      const j = b.jelly(p);
      if (j <= 0) continue;
      b.setJelly(p, j - 1);
      o.hits.push({ pos: p, kind: 'Jelly', remaining: j - 1 });
      if (j - 1 === 0) o.stats.jellyCleared++;
    }
    for (const p of o.cleared.values()) if (b.get(p) > 0) addType(o.stats, b.get(p));
  }

  // ---------------------------------------------------------------- gravedad
  function applyGravity(b) {
    const moves = [];
    for (let x = 0; x < b.width; x++) {
      let write = 0;
      for (let y = 0; y < b.height; y++) {
        const from = P(x, y), t = b.get(from);
        if (t === EMPTY) continue;
        if (y !== write) {
          const to = P(x, write), sp = b.special(from), hp = b.hp(from);
          b.clearCell(from); b.set(to, t); b.setSpecial(to, sp); b.setHp(to, hp);
          moves.push({ from, to, type: t, special: sp, hp });
        }
        write++;
      }
    }
    return moves;
  }
  function refill(b, source) {
    const spawns = [];
    for (let x = 0; x < b.width; x++) {
      let k = 0;
      for (let y = 0; y < b.height; y++) {
        if (b.at(x, y) !== EMPTY) continue;
        const t = source.next(x);
        b.set(P(x, y), t);
        spawns.push({ from: P(x, b.height + k), to: P(x, y), type: t, special: SK.None, hp: 0 });
        k++;
      }
    }
    return spawns;
  }

  // ---------------------------------------------------------------- movimientos y generación
  function isValidSwap(b, a, c) {
    if (!b.inBounds(a) || !b.inBounds(c) || !adjacent(a, c)) return false;
    if (!b.isMovable(a) || !b.isMovable(c)) return false;
    if (isComboSwap(b, a, c)) return true;
    if (b.get(a) === b.get(c)) return false;
    b.swap(a, c);
    const ok = isPartOfMatch(b, a) || isPartOfMatch(b, c);
    b.swap(a, c);
    return ok;
  }
  function findAllMoves(b) {
    const list = [];
    for (let x = 0; x < b.width; x++) for (let y = 0; y < b.height; y++) {
      const p = P(x, y), right = P(x + 1, y), up = P(x, y + 1);
      if (isValidSwap(b, p, right)) list.push({ from: p, to: right });
      if (isValidSwap(b, p, up)) list.push({ from: p, to: up });
    }
    return list;
  }
  function hasMove(b) {
    for (let x = 0; x < b.width; x++) for (let y = 0; y < b.height; y++) {
      const p = P(x, y);
      if (isValidSwap(b, p, P(x + 1, y)) || isValidSwap(b, p, P(x, y + 1))) return true;
    }
    return false;
  }
  function validateRows(rows, cfg, name) {
    if (!rows) return;
    if (rows.length !== cfg.height) throw new Error(name + ': filas');
    for (const r of rows) if (r.length !== cfg.width) throw new Error(name + ': columnas');
  }
  function pickWithoutMatch(b, x, y, typeCount, rng) {
    const allowed = [];
    for (let t = 1; t <= typeCount; t++) {
      const h = x >= 2 && b.at(x - 1, y) === t && b.at(x - 2, y) === t;
      const v = y >= 2 && b.at(x, y - 1) === t && b.at(x, y - 2) === t;
      if (!h && !v) allowed.push(t);
    }
    return allowed[rng.next(allowed.length)];
  }
  function createBoard(cfg, rng) {
    validateRows(cfg.tileRows, cfg, 'tileRows'); validateRows(cfg.jellyRows, cfg, 'jellyRows');
    for (let attempt = 0; attempt < 1000; attempt++) {
      const b = new Board(cfg.width, cfg.height);
      for (let y = 0; y < cfg.height; y++) for (let x = 0; x < cfg.width; x++) b.set(P(x, y), pickWithoutMatch(b, x, y, cfg.typeCount, rng));
      for (let row = 0; row < cfg.height; row++) for (let x = 0; x < cfg.width; x++) {
        const p = P(x, cfg.height - 1 - row);
        if (cfg.tileRows) {
          switch (cfg.tileRows[row][x]) {
            case 'f': b.setHp(p, 1); break;
            case 'F': b.setHp(p, 2); break;
            case 'c': b.set(p, CRATE); b.setHp(p, 1); break;
            case 'C': b.set(p, CRATE); b.setHp(p, 2); break;
            case 'K': b.set(p, CRATE); b.setHp(p, 3); break;
            case 'S': b.set(p, STONE); break;
          }
        }
        if (cfg.jellyRows) { const ch = cfg.jellyRows[row][x]; if (ch >= '1' && ch <= '9') b.setJelly(p, ch.charCodeAt(0) - 48); }
      }
      if (hasMove(b)) return b;
    }
    throw new Error('No se pudo generar un tablero válido.');
  }
  function tryReshuffle(b, rng) {
    const pos = [];
    for (let y = 0; y < b.height; y++) for (let x = 0; x < b.width; x++) { const p = P(x, y); if (b.isMovable(p)) pos.push(p); }
    const n = pos.length; if (n < 3) return null;
    const types = pos.map((p) => b.get(p)), specs = pos.map((p) => b.special(p));
    for (let attempt = 0; attempt < 500; attempt++) {
      const perm = Array.from({ length: n }, (_, i) => i);
      for (let i = n - 1; i > 0; i--) { const j = rng.next(i + 1); [perm[i], perm[j]] = [perm[j], perm[i]]; }
      const trial = b.clone();
      for (let i = 0; i < n; i++) { trial.set(pos[i], types[perm[i]]); trial.setSpecial(pos[i], specs[perm[i]]); }
      if (findMatches(trial).length > 0 || !hasMove(trial)) continue;
      const moves = [];
      for (let i = 0; i < n; i++) {
        b.set(pos[i], types[perm[i]]); b.setSpecial(pos[i], specs[perm[i]]);
        moves.push({ from: pos[perm[i]], to: pos[i], type: types[perm[i]], special: specs[perm[i]], hp: 0 });
      }
      return moves;
    }
    return null;
  }

  // ---------------------------------------------------------------- objetivos, inventario, puntuación
  function makeObjectives(specs, initial) {
    const items = [];
    for (const s of specs || []) {
      let target = s.count;
      if (target <= 0) {
        if (s.kind === Obj.ClearJelly) target = initial.countWhere((p) => initial.jelly(p) > 0);
        else if (s.kind === Obj.BreakFrozen) target = initial.countWhere((p) => initial.get(p) > 0 && initial.hp(p) > 0);
        else if (s.kind === Obj.DestroyCrates) target = initial.countWhere((p) => initial.get(p) === CRATE);
      }
      items.push({ spec: s, target, current: 0 });
    }
    return items;
  }
  const calcPoints = (count, cascade) => count * 10 * Math.min(cascade, 5);
  const starsFor = (score, target) => (score >= target * 2 ? 3 : score >= Math.trunc((target * 3) / 2) ? 2 : score >= target ? 1 : 0);

  // ---------------------------------------------------------------- sesión de juego
  class GameSession {
    constructor(config, boosters) {
      this.config = config;
      this.rng = new GameRng(config.seed);
      this.board = createBoard(config, this.rng);
      this.source = { next: () => this.rng.nextRange(1, config.typeCount + 1) };
      this.score = 0; this.movesLeft = config.moves;
      this.objectives = makeObjectives(config.objectives, this.board);
      this.boosters = boosters ? Object.assign({}, boosters) : {};
      this.state = 'Playing';
    }
    get stars() { return starsFor(this.score, this.config.targetScore); }
    objectivesDone() { return this.objectives.every((i) => i.current >= i.target); }
    count(k) { return this.boosters[k] || 0; }
    consume(k) { if (this.count(k) <= 0) return false; this.boosters[k] = this.count(k) - 1; return true; }
    hint() { const m = findAllMoves(this.board); return m.length ? m[0] : null; }
    _result(accepted) { return { accepted, steps: [], pointsGained: 0, maxCascade: 0, state: this.state }; }

    trySwap(a, b) {
      const r = this._result(false);
      if (this.state !== 'Playing' || !this.board.inBounds(a) || !this.board.inBounds(b) || !adjacent(a, b)) return r;
      if (!this.board.isMovable(a) || !this.board.isMovable(b)) return r;
      if (!isValidSwap(this.board, a, b)) { r.steps.push({ kind: 'Swap', a, b }, { kind: 'SwapBack', a, b }); return r; }
      this.movesLeft--;
      this.board.swap(a, b);
      r.accepted = true; r.steps.push({ kind: 'Swap', a, b });
      this._resolve(r, fromSwapCombo(this.board, a, b), a, b);
      return r;
    }
    _boosterStart(k) { return { r: this._result(false), ok: this.state === 'Playing' && this.count(k) > 0 }; }
    useHammer(p) {
      const { r, ok } = this._boosterStart(Booster.Hammer);
      if (!ok || !this.board.inBounds(p) || this.board.get(p) === EMPTY) return r;
      this.consume(Booster.Hammer);
      const o = newOutcome(); o.cleared.add(p); o.forceClear.add(p);
      expand(this.board, o, new PosSet(), new PosSet());
      r.accepted = true; this._resolve(r, o, null, null); return r;
    }
    useFreeSwap(a, b) {
      const { r, ok } = this._boosterStart(Booster.FreeSwap);
      const bd = this.board;
      if (!ok || !bd.inBounds(a) || !bd.inBounds(b) || !adjacent(a, b) || !bd.isMovable(a) || !bd.isMovable(b)) return r;
      this.consume(Booster.FreeSwap);
      bd.swap(a, b); r.accepted = true; r.steps.push({ kind: 'Swap', a, b });
      this._resolve(r, fromSwapCombo(bd, a, b), a, b); return r;
    }
    useObstacleRemover(p) {
      const { r, ok } = this._boosterStart(Booster.ObstacleRemover);
      const bd = this.board;
      if (!ok || !bd.inBounds(p)) return r;
      const t = bd.get(p);
      const frozen = t > 0 && bd.hp(p) > 0, blocker = t === CRATE || t === STONE, jelly = bd.jelly(p) > 0;
      if (!frozen && !blocker && !jelly) return r;
      this.consume(Booster.ObstacleRemover);
      const o = newOutcome();
      if (blocker) { o.cleared.add(p); o.forceClear.add(p); }
      else if (frozen) { bd.setHp(p, 0); o.hits.push({ pos: p, kind: 'Frozen', remaining: 0 }); o.stats.frozenBroken++; }
      if (!blocker && jelly) { bd.setJelly(p, 0); o.hits.push({ pos: p, kind: 'Jelly', remaining: 0 }); o.stats.jellyCleared++; }
      r.accepted = true; this._resolve(r, o, null, null); return r;
    }
    useExtraMoves() {
      const { r, ok } = this._boosterStart(Booster.ExtraMoves);
      if (!ok) return r;
      this.consume(Booster.ExtraMoves); this.movesLeft += 5; r.accepted = true; return r;
    }
    useSpecialMaker(p, kind) {
      const { r, ok } = this._boosterStart(Booster.SpecialMaker);
      const bd = this.board;
      if (!ok || kind === SK.None || !bd.inBounds(p) || !bd.isMovable(p) || bd.special(p) !== SK.None) return r;
      this.consume(Booster.SpecialMaker);
      bd.setSpecial(p, kind);
      r.steps.push({ kind: 'Clear', cascade: 0, points: 0, cleared: [], created: [{ pos: p, type: bd.get(p), kind }], activated: [], hits: [] });
      r.accepted = true; return r;
    }

    _resolve(result, first, swapA, swapB) {
      let cascade = 0, outcome = first;
      while (cascade < 100) {
        if (!outcome) {
          const matches = findMatches(this.board);
          if (matches.length === 0) break;
          outcome = fromMatches(this.board, matches, cascade === 0 ? swapA : null, cascade === 0 ? swapB : null);
        }
        cascade++;
        this._apply(result, outcome, cascade);
        outcome = null;
        const falls = applyGravity(this.board);
        if (falls.length > 0) result.steps.push({ kind: 'Fall', moves: falls });
        const spawns = refill(this.board, this.source);
        if (spawns.length > 0) result.steps.push({ kind: 'Spawn', moves: spawns });
      }
      result.maxCascade = cascade;
      this._updateState();
      if (this.state === 'Playing' && !hasMove(this.board)) {
        const moves = tryReshuffle(this.board, this.rng);
        if (moves) result.steps.push({ kind: 'Shuffle', moves });
      }
      result.state = this.state;
    }
    _apply(result, o, cascade) {
      filterObstacles(this.board, o);
      const points = calcPoints(o.cleared.size + o.created.length, cascade);
      this.score += points; result.pointsGained += points;
      for (const it of this.objectives) {
        const s = it.spec.kind;
        if (s === Obj.CollectType) it.current += o.stats.clearedByType.get(it.spec.type) || 0;
        else if (s === Obj.ClearJelly) it.current += o.stats.jellyCleared;
        else if (s === Obj.BreakFrozen) it.current += o.stats.frozenBroken;
        else if (s === Obj.DestroyCrates) it.current += o.stats.cratesDestroyed;
      }
      const step = { kind: 'Clear', cascade, points, cleared: [], created: o.created, activated: o.activated, hits: o.hits };
      for (const p of o.cleared.values()) { step.cleared.push(p); this.board.clearCell(p); }
      for (const c of o.created) this.board.setSpecial(c.pos, c.kind);
      result.steps.push(step);
    }
    _updateState() {
      if (this.score >= this.config.targetScore && this.objectivesDone()) this.state = 'Won';
      else if (this.movesLeft <= 0) this.state = 'Lost';
    }
  }

  // ---------------------------------------------------------------- catálogo de 30 niveles
  const COUNT = 30;
  const PPM = [700, 700, 420, 420, 520];
  const roundHalfEven = (v) => { const f = Math.floor(v), d = v - f; if (d < 0.5) return f; if (d > 0.5) return f + 1; return f % 2 === 0 ? f : f + 1; };
  const round100 = (v) => roundHalfEven(v / 100.0) * 100;
  const moves150 = (m) => round100(m * 150);
  const emptyGrid = (w, h) => Array.from({ length: h }, () => Array(w).fill('.'));
  const toRows = (g) => g.map((r) => r.join(''));
  function place(g, rng, ch, count, minRow, maxRow) {
    const w = g[0].length; let placed = 0, guard = 0;
    while (placed < count && guard++ < 1000) {
      const r = rng.nextRange(minRow, maxRow + 1), x = rng.next(w);
      if (g[r][x] !== '.') continue;
      g[r][x] = ch; placed++;
    }
  }
  function jellyPattern(w, h, j) {
    const g = emptyGrid(w, h), rows = 2 + (j % 3);
    for (let k = 0; k < rows; k++) for (let x = 0; x < w; x++) g[h - 1 - k][x] = (j >= 3 && k === 0 && x > 1 && x < w - 2) ? '2' : '1';
    if (j === 2) for (let x = 0; x < w; x += 2) g[h - 1 - rows][x] = '1';
    return toRows(g);
  }
  function scatter(w, h, rng, a, ca, b, cb) { const g = emptyGrid(w, h); place(g, rng, a, ca, 2, h - 1); place(g, rng, b, cb, 2, h - 1); return toRows(g); }
  function topCrates(w, h, rng, count, kind) { const g = emptyGrid(w, h); place(g, rng, kind, count, 0, 1); return toRows(g); }
  function overlay(base, top) { return base.map((row, r) => row.split('').map((ch, x) => (ch === '.' ? top[r][x] : ch)).join('')); }
  function putStones(rows, count) {
    const o = rows.slice(); let placed = 0;
    for (let x = 0; x < o[0].length && placed < count; x += 3) {
      const chars = o[0].split('');
      if (chars[x] !== '.') continue;
      chars[x] = 'S'; o[0] = chars.join(''); placed++;
    }
    return o;
  }
  function getLevel(level) {
    if (level < 1 || level > COUNT) throw new RangeError('nivel');
    const i = level - 1, tier = Math.floor(i / 5), j = i % 5;
    const size = tier === 0 && j < 2 ? 7 : tier === 0 && j < 4 ? 8 : 9;
    const c = { width: size, height: size, typeCount: tier === 0 && j < 2 ? 4 : tier >= 4 ? 6 : 5, seed: 7000 + level * 31,
      moves: 25, targetScore: 1500, tileRows: null, jellyRows: null, objectives: [] };
    const rng = new GameRng(c.seed);
    switch (tier) {
      case 0: c.moves = 20 + j * 2; c.targetScore = round100(c.moves * PPM[j] * (0.35 + 0.08 * j)); break;
      case 1: c.moves = 24 + j * 2; c.targetScore = moves150(c.moves);
        c.objectives.push({ kind: Obj.CollectType, type: (j % c.typeCount) + 1, count: 45 + j * 10 }); break;
      case 2: c.moves = 26 + j * 3; c.targetScore = moves150(c.moves); c.jellyRows = jellyPattern(c.width, c.height, j);
        c.objectives.push({ kind: Obj.ClearJelly, type: 0, count: 0 }); break;
      case 3: c.moves = 28 + j * 3; c.targetScore = moves150(c.moves);
        c.tileRows = scatter(c.width, c.height, rng, 'f', 5 + j * 2, j >= 3 ? 'F' : 'f', j >= 3 ? 3 : 0);
        c.objectives.push({ kind: Obj.BreakFrozen, type: 0, count: 0 }); break;
      case 4: c.moves = 32 + j * 2; c.targetScore = round100(c.moves * 120);
        c.tileRows = topCrates(c.width, c.height, rng, 5 + j, j >= 2 ? 'C' : 'c');
        c.objectives.push({ kind: Obj.DestroyCrates, type: 0, count: 0 }); break;
      default: {
        c.moves = 50 + j * 3; c.targetScore = round100(c.moves * 80);
        c.jellyRows = jellyPattern(c.width, c.height, 1);
        let rows = topCrates(c.width, c.height, rng, 4, 'c');
        rows = overlay(rows, scatter(c.width, c.height, rng, 'f', 6, 'F', j));
        rows = putStones(rows, 2);
        c.tileRows = rows;
        c.objectives.push({ kind: Obj.ClearJelly, type: 0, count: 0 }, { kind: Obj.BreakFrozen, type: 0, count: 0 }, { kind: Obj.DestroyCrates, type: 0, count: 0 });
        if (j >= 2) c.objectives.push({ kind: Obj.CollectType, type: 1, count: 30 + j * 4 });
      }
    }
    return c;
  }

  root.PetaloCore = { EMPTY, CRATE, STONE, SK, Obj, Booster, P, GameRng, Board, GameSession, getLevel, LEVEL_COUNT: COUNT, findAllMoves, hasMove, findMatches, isValidSwap, starsFor };
  if (typeof module !== 'undefined' && module.exports) module.exports = root.PetaloCore;
})(typeof window !== 'undefined' ? window : globalThis);
