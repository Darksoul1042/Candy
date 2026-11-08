using System;
using System.Collections.Generic;
using System.Linq;

namespace PetaloPop.Core
{
    /// <summary>
    /// Orquesta una partida: valida acciones, resuelve cadenas (limpiar, gravedad, relleno), aplica obstáculos,
    /// especiales, objetivos y potenciadores, y devuelve los pasos para que la vista los anime. No depende de Unity.
    /// </summary>
    public sealed class GameSession
    {
        private const int MaxCascades = 100;
        public const int ExtraMovesAmount = 5;

        public Board Board { get; private set; }
        public LevelConfig Config { get; }
        public ScoreManager Scoring { get; private set; }
        public MoveManager Moves { get; private set; }
        public ObjectiveManager Objectives { get; private set; }
        public BoosterManager Boosters { get; private set; }
        public GameState State { get; private set; } = GameState.Playing;
        public int Score => Scoring.Score;

        private ITileSource source;
        private GameRng rng;
        private readonly bool randomSource;

        public GameSession(LevelConfig config) : this(config, null, null, null) { }

        public GameSession(LevelConfig config, Board board, ITileSource tileSource, BoosterManager boosters = null)
        {
            Config = config;
            rng = new GameRng(config.Seed);
            Board = board ?? BoardGenerator.Create(config, rng);
            randomSource = tileSource == null;
            source = tileSource ?? new RandomTileSource(rng, config.TypeCount);
            Scoring = new ScoreManager(config.TargetScore);
            Moves = new MoveManager(config.Moves);
            Objectives = new ObjectiveManager(config.Objectives, Board);
            Boosters = boosters ?? new BoosterManager();
        }

        private GameSession(GameSession o, int? reseed)
        {
            Config = o.Config;
            rng = reseed.HasValue ? new GameRng(reseed.Value) : o.rng.Clone();
            Board = o.Board.Clone();
            randomSource = true;
            source = new RandomTileSource(rng, Config.TypeCount);
            Scoring = o.Scoring.Clone();
            Moves = o.Moves.Clone();
            Objectives = o.Objectives.Clone();
            Boosters = o.Boosters.Clone();
            State = o.State;
        }

        /// <summary>Copia independiente (para bots y simulaciones). Solo con fuente de fichas aleatoria.</summary>
        public GameSession Clone()
        {
            if (!randomSource) throw new InvalidOperationException("Solo se puede clonar con fuente de fichas aleatoria.");
            return new GameSession(this, null);
        }

        /// <summary>Copia con otro azar futuro: para que un bot simule sin "ver" las fichas que van a caer.</summary>
        public GameSession CloneReseeded(int seed)
        {
            if (!randomSource) throw new InvalidOperationException("Solo se puede clonar con fuente de fichas aleatoria.");
            return new GameSession(this, seed);
        }

        public bool TryGetHint(out Position a, out Position b) => MoveFinder.TryFindMove(Board, out a, out b);

        // ---------------------------------------------------------------- Intercambio normal
        public MoveResult TrySwap(Position a, Position b)
        {
            var result = new MoveResult { State = State };
            if (State != GameState.Playing || !Board.InBounds(a) || !Board.InBounds(b) || !a.IsAdjacentTo(b)) return result;
            if (!Board.IsMovable(a) || !Board.IsMovable(b)) return result;

            if (!MoveFinder.IsValidSwap(Board, a, b))
            {
                // Intercambio adyacente sin combinación: se anima ida y vuelta, no cuesta movimiento.
                result.Steps.Add(new Step { Kind = StepKind.Swap, A = a, B = b });
                result.Steps.Add(new Step { Kind = StepKind.SwapBack, A = a, B = b });
                return result;
            }

            Moves.Consume();
            Board.Swap(a, b);
            result.Accepted = true;
            result.Steps.Add(new Step { Kind = StepKind.Swap, A = a, B = b });
            Resolve(result, SpecialTileSystem.FromSwapCombo(Board, a, b), a, b);
            return result;
        }

        // ---------------------------------------------------------------- Potenciadores
        public MoveResult UseHammer(Position p)
        {
            var r = BoosterStart(BoosterKind.Hammer, out bool ok);
            if (!ok || !Board.InBounds(p) || Board[p] == Board.Empty) return Reject(r, BoosterKind.Hammer, false);
            Boosters.TryConsume(BoosterKind.Hammer);
            var o = new ClearOutcome();
            o.Cleared.Add(p);
            o.ForceClear.Add(p);
            SpecialTileSystem.Expand(Board, o, new HashSet<Position>(), new HashSet<Position>());
            r.Accepted = true;
            Resolve(r, o, null, null);
            return r;
        }

        public MoveResult UseFreeSwap(Position a, Position b)
        {
            var r = BoosterStart(BoosterKind.FreeSwap, out bool ok);
            if (!ok || !Board.InBounds(a) || !Board.InBounds(b) || !a.IsAdjacentTo(b) || !Board.IsMovable(a) || !Board.IsMovable(b))
                return Reject(r, BoosterKind.FreeSwap, false);
            Boosters.TryConsume(BoosterKind.FreeSwap);
            Board.Swap(a, b);
            r.Accepted = true;
            r.Steps.Add(new Step { Kind = StepKind.Swap, A = a, B = b });
            Resolve(r, SpecialTileSystem.FromSwapCombo(Board, a, b), a, b);
            return r;
        }

        public MoveResult UseObstacleRemover(Position p)
        {
            var r = BoosterStart(BoosterKind.ObstacleRemover, out bool ok);
            if (!ok || !Board.InBounds(p)) return Reject(r, BoosterKind.ObstacleRemover, false);
            int t = Board[p];
            bool frozen = t > 0 && Board.Hp(p) > 0, blocker = t == Board.Crate || t == Board.Stone, jelly = Board.Jelly(p) > 0;
            if (!frozen && !blocker && !jelly) return Reject(r, BoosterKind.ObstacleRemover, false);
            Boosters.TryConsume(BoosterKind.ObstacleRemover);

            var o = new ClearOutcome();
            if (blocker) { o.Cleared.Add(p); o.ForceClear.Add(p); }
            else if (frozen)
            {
                Board.SetHp(p, 0);
                o.Hits.Add(new Hit { Pos = p, Kind = ObstacleKind.Frozen, Remaining = 0 });
                o.Stats.FrozenBroken++;
            }
            if (!blocker && jelly)
            {
                Board.SetJelly(p, 0);
                o.Hits.Add(new Hit { Pos = p, Kind = ObstacleKind.Jelly, Remaining = 0 });
                o.Stats.JellyCleared++;
            }
            r.Accepted = true;
            Resolve(r, o, null, null);
            return r;
        }

        public MoveResult UseExtraMoves()
        {
            var r = BoosterStart(BoosterKind.ExtraMoves, out bool ok);
            if (!ok) return Reject(r, BoosterKind.ExtraMoves, false);
            Boosters.TryConsume(BoosterKind.ExtraMoves);
            Moves.Add(ExtraMovesAmount);
            r.Accepted = true;
            return r;
        }

        public MoveResult UseSpecialMaker(Position p, SpecialKind kind)
        {
            var r = BoosterStart(BoosterKind.SpecialMaker, out bool ok);
            if (!ok || kind == SpecialKind.None || !Board.InBounds(p) || !Board.IsMovable(p) || Board.SpecialAt(p) != SpecialKind.None)
                return Reject(r, BoosterKind.SpecialMaker, false);
            Boosters.TryConsume(BoosterKind.SpecialMaker);
            Board.SetSpecial(p, kind);
            var step = new Step { Kind = StepKind.Clear, Cascade = 0 };
            step.Created.Add(new SpecialSpawn { Pos = p, Type = Board[p], Kind = kind });
            r.Steps.Add(step);
            r.Accepted = true;
            return r;
        }

        private MoveResult BoosterStart(BoosterKind k, out bool ok)
        {
            ok = State == GameState.Playing && Boosters.Count(k) > 0;
            return new MoveResult { State = State };
        }

        private static MoveResult Reject(MoveResult r, BoosterKind _, bool __) { r.Accepted = false; r.Steps.Clear(); return r; }

        // ---------------------------------------------------------------- Resolución común
        private void Resolve(MoveResult result, ClearOutcome first, Position? swapA, Position? swapB)
        {
            int cascade = 0;
            var outcome = first;
            while (cascade < MaxCascades)
            {
                if (outcome == null)
                {
                    var matches = MatchDetector.FindMatches(Board);
                    if (matches.Count == 0) break;
                    outcome = SpecialTileSystem.FromMatches(Board, matches, cascade == 0 ? swapA : null, cascade == 0 ? swapB : null);
                }
                cascade++;
                ApplyOutcome(result, outcome, cascade);
                outcome = null;

                var falls = GravitySystem.ApplyGravity(Board);
                if (falls.Count > 0) result.Steps.Add(new Step { Kind = StepKind.Fall, Moves = falls });
                var spawns = GravitySystem.Refill(Board, source);
                if (spawns.Count > 0) result.Steps.Add(new Step { Kind = StepKind.Spawn, Moves = spawns });
            }
            result.MaxCascade = cascade;

            UpdateState();
            if (State == GameState.Playing && !MoveFinder.HasMove(Board))
            {
                var moves = BoardGenerator.TryReshuffle(Board, rng);
                if (moves != null) result.Steps.Add(new Step { Kind = StepKind.Shuffle, Moves = moves });
            }
            result.State = State;
        }

        private void ApplyOutcome(MoveResult result, ClearOutcome o, int cascade)
        {
            ObstacleSystem.Filter(Board, o);

            int points = ScoreManager.Calculate(o.Cleared.Count + o.Created.Count, cascade);
            Scoring.Add(points);
            result.PointsGained += points;
            Objectives.Apply(o.Stats);

            var step = new Step { Kind = StepKind.Clear, Cascade = cascade, Points = points, Created = o.Created, Activated = o.Activated, Hits = o.Hits };
            foreach (var p in o.Cleared) { step.Cleared.Add(p); Board.ClearCell(p); }
            foreach (var c in o.Created) Board.SetSpecial(c.Pos, c.Kind);
            result.Steps.Add(step);
        }

        private void UpdateState()
        {
            bool scoreOk = Scoring.Score >= Config.TargetScore;
            if (scoreOk && Objectives.AllDone) State = GameState.Won;
            else if (!Moves.HasMoves) State = GameState.Lost;
        }
    }
}
