using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using PetaloPop.Core;

namespace PetaloPop.Game
{
    /// <summary>Representa el tablero en pantalla y reproduce los pasos que devuelve GameSession.</summary>
    public sealed class BoardView : MonoBehaviour
    {
        public float CellSize = 1f;

        private AnimationManager anim;
        private Fx fx;
        private AudioManager audio;
        private TileView[,] tiles;
        private SpriteRenderer[,] jelly;
        private readonly Stack<TileView> pool = new Stack<TileView>();
        private Transform tileRoot;
        private Vector2 origin;
        private int width, height;

        public Vector2 WorldSize => new Vector2(width * CellSize, height * CellSize);
        public Vector2 Center { get; private set; }

        public void Build(Board b, AnimationManager animation, Fx effects, AudioManager sound, Vector2 center)
        {
            anim = animation; fx = effects; audio = sound;
            width = b.Width;
            height = b.Height;
            Center = center;
            origin = center - new Vector2((width - 1) * CellSize * 0.5f, (height - 1) * CellSize * 0.5f);

            for (int i = transform.childCount - 1; i >= 0; i--) Destroy(transform.GetChild(i).gameObject);
            pool.Clear();

            // Panel de fondo y máscara que oculta las fichas que nacen por encima del tablero.
            var bg = new GameObject("Panel");
            bg.transform.SetParent(transform, false);
            var bgSr = bg.AddComponent<SpriteRenderer>();
            bgSr.sprite = SpriteFactory.Square();
            bgSr.color = new Color(0.05f, 0.14f, 0.20f, 0.85f);
            bgSr.sortingOrder = 0;
            bg.transform.position = new Vector3(center.x, center.y, 0);
            bg.transform.localScale = new Vector3(width * CellSize + 0.3f, height * CellSize + 0.3f, 1);

            var mask = new GameObject("Mask");
            mask.transform.SetParent(transform, false);
            var sm = mask.AddComponent<SpriteMask>();
            sm.sprite = SpriteFactory.Square();
            mask.transform.position = new Vector3(center.x, center.y, 0);
            mask.transform.localScale = new Vector3(width * CellSize, height * CellSize, 1);

            tileRoot = new GameObject("Tiles").transform;
            tileRoot.SetParent(transform, false);

            tiles = new TileView[width, height];
            jelly = new SpriteRenderer[width, height];
            for (int x = 0; x < width; x++)
                for (int y = 0; y < height; y++)
                {
                    var p = new Position(x, y);
                    var t = Spawn(b[p], b.SpecialAt(p), b.Hp(p));
                    t.transform.position = CellToWorld(p);
                    tiles[x, y] = t;

                    var j = new GameObject("Jelly").AddComponent<SpriteRenderer>();
                    j.sprite = SpriteFactory.Jelly();
                    j.sortingOrder = 1;
                    j.transform.SetParent(transform, false);
                    j.transform.position = CellToWorld(p);
                    j.transform.localScale = Vector3.one * CellSize;
                    jelly[x, y] = j;
                    SetJelly(p, b.Jelly(p));
                }
        }

        private void SetJelly(Position p, int layers)
        {
            var j = jelly[p.X, p.Y];
            j.enabled = layers > 0;
            j.color = new Color(1f, 1f, 1f, layers >= 2 ? 1f : 0.75f);
        }

        public Vector3 CellToWorld(Position p) =>
            new Vector3(origin.x + p.X * CellSize, origin.y + p.Y * CellSize, 0f);

        public bool TryWorldToCell(Vector3 world, out Position cell)
        {
            int x = Mathf.RoundToInt((world.x - origin.x) / CellSize);
            int y = Mathf.RoundToInt((world.y - origin.y) / CellSize);
            cell = new Position(x, y);
            return x >= 0 && y >= 0 && x < width && y < height;
        }

        public TileView TileAt(Position p) => tiles[p.X, p.Y];

        private TileView Spawn(int type, SpecialKind special, int hp)
        {
            TileView t = pool.Count > 0 ? pool.Pop() : new GameObject("Tile").AddComponent<TileView>();
            t.Init(type, special, hp, tileRoot);
            return t;
        }

        private void Release(TileView t)
        {
            t.gameObject.SetActive(false);
            pool.Push(t);
        }

        public IEnumerator PlaySteps(List<Step> steps)
        {
            foreach (var s in steps)
            {
                switch (s.Kind)
                {
                    case StepKind.Swap:
                    case StepKind.SwapBack:
                        if (s.Kind == StepKind.SwapBack && audio != null) audio.Invalid();
                        else if (audio != null) audio.Swap();
                        yield return PlaySwap(s.A, s.B);
                        break;
                    case StepKind.Clear:
                        yield return PlayClear(s);
                        break;
                    case StepKind.Fall:
                    case StepKind.Spawn:
                    case StepKind.Shuffle:
                        yield return PlayMoves(s);
                        break;
                }
            }
        }

        private IEnumerator PlaySwap(Position a, Position b)
        {
            TileView ta = tiles[a.X, a.Y], tb = tiles[b.X, b.Y];
            tiles[a.X, a.Y] = tb;
            tiles[b.X, b.Y] = ta;
            var list = new List<IEnumerator>
            {
                anim.MoveTo(ta.transform, CellToWorld(b), 0.16f),
                anim.MoveTo(tb.transform, CellToWorld(a), 0.16f)
            };
            yield return anim.Parallel(list);
        }

        private IEnumerator PlayClear(Step s)
        {
            if (audio != null)
            {
                if (s.Activated.Count > 0 || s.Created.Count > 0) audio.Special();
                if (s.Cleared.Count > 0) audio.Match(s.Cascade);
            }

            // Golpes a obstáculos: gelatina (capa del fondo) y vida de hielo/cajas que sobreviven.
            foreach (var h in s.Hits)
            {
                if (h.Kind == ObstacleKind.Jelly) SetJelly(h.Pos, h.Remaining);
                else if (tiles[h.Pos.X, h.Pos.Y] != null) { tiles[h.Pos.X, h.Pos.Y].SetHp(h.Remaining); fx.Burst(CellToWorld(h.Pos), new Color(0.85f, 0.95f, 1f), 3); }
            }

            // Destellos de especiales activados.
            foreach (var a in s.Activated)
            {
                var pos = CellToWorld(a.Pos);
                switch (a.Kind)
                {
                    case SpecialKind.LineH: fx.Flash(new Vector3(Center.x, pos.y, 0), new Vector2(width * CellSize, 0.35f)); break;
                    case SpecialKind.LineV: fx.Flash(new Vector3(pos.x, Center.y, 0), new Vector2(0.35f, height * CellSize)); break;
                    case SpecialKind.Bomb: fx.Flash(pos, new Vector2(3f * CellSize, 3f * CellSize)); break;
                    case SpecialKind.ColorBurst: fx.Flash(Center, new Vector2(width * CellSize, height * CellSize), 0.35f); break;
                }
            }

            var list = new List<IEnumerator>();
            var removed = new List<TileView>();
            foreach (var c in s.Cleared)
            {
                var t = tiles[c.X, c.Y];
                tiles[c.X, c.Y] = null;
                if (t == null) continue;
                removed.Add(t);
                fx.Burst(CellToWorld(c), t.BurstColor, 4);
                list.Add(anim.ScaleTo(t.transform, 0f, 0.2f));
            }
            foreach (var c in s.Created)
            {
                var t = tiles[c.Pos.X, c.Pos.Y];
                if (t == null) continue;
                t.SetSpecial(c.Kind);
                list.Add(Pop(t.transform));
            }
            if (list.Count > 0) yield return anim.Parallel(list);
            foreach (var t in removed) Release(t);
        }

        private IEnumerator Pop(Transform t)
        {
            yield return anim.ScaleTo(t, 1.35f, 0.12f);
            yield return anim.ScaleTo(t, 1f, 0.12f);
        }

        private IEnumerator PlayMoves(Step s)
        {
            // Se toman primero todas las fichas de origen y luego se asignan los destinos (movimientos simultáneos).
            var moving = new List<TileView>(s.Moves.Count);
            foreach (var m in s.Moves)
            {
                TileView t;
                if (s.Kind == StepKind.Spawn)
                {
                    t = Spawn(m.Type, m.Special, m.Hp);
                    t.transform.position = CellToWorld(m.From);
                }
                else
                {
                    t = tiles[m.From.X, m.From.Y];
                }
                moving.Add(t);
            }
            if (s.Kind != StepKind.Spawn)
                foreach (var m in s.Moves) tiles[m.From.X, m.From.Y] = null;

            var list = new List<IEnumerator>(s.Moves.Count);
            for (int i = 0; i < s.Moves.Count; i++)
            {
                var m = s.Moves[i];
                tiles[m.To.X, m.To.Y] = moving[i];
                float dist = Mathf.Abs(m.From.Y - m.To.Y) + Mathf.Abs(m.From.X - m.To.X);
                float dur = s.Kind == StepKind.Shuffle ? 0.4f : Mathf.Min(0.1f + 0.05f * dist, 0.45f);
                list.Add(anim.MoveTo(moving[i].transform, CellToWorld(m.To), dur));
            }
            yield return anim.Parallel(list);
        }

        public IEnumerator Pulse(Position a, Position b)
        {
            var ta = tiles[a.X, a.Y]; var tb = tiles[b.X, b.Y];
            if (ta == null || tb == null) yield break;
            var list = new List<IEnumerator> { anim.ScaleTo(ta.transform, 1.2f, 0.25f), anim.ScaleTo(tb.transform, 1.2f, 0.25f) };
            yield return anim.Parallel(list);
            list = new List<IEnumerator> { anim.ScaleTo(ta.transform, 1f, 0.25f), anim.ScaleTo(tb.transform, 1f, 0.25f) };
            yield return anim.Parallel(list);
        }
    }
}
