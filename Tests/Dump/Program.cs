using System;
using System.Linq;
using System.Text;
using PetaloPop.Core;

// Juega partidas deterministas (política fija, sin azar propio) y vuelca un resumen del estado final.
// El port a JavaScript debe producir EXACTAMENTE las mismas líneas.
class P
{
    static Position Pt(int x, int y) => new Position(x, y);

    static string Digest(GameSession s)
    {
        var b = s.Board;
        var sb = new StringBuilder();
        for (int y = b.Height - 1; y >= 0; y--)
        {
            for (int x = 0; x < b.Width; x++)
            {
                var p = Pt(x, y);
                sb.Append(b[p]).Append('.').Append((int)b.SpecialAt(p)).Append('.').Append(b.Hp(p)).Append('.').Append(b.Jelly(p)).Append(' ');
            }
            sb.Append('|');
        }
        return sb.ToString();
    }

    static void Main()
    {
        for (int l = 1; l <= LevelCatalog.Count; l++)
        {
            var cfg = LevelCatalog.Get(l);
            cfg.Moves = 500; cfg.TargetScore = int.MaxValue / 4; // para que la partida no termine
            var s = new GameSession(cfg, null, null, Bot.StartingInventory());
            int w = cfg.Width, h = cfg.Height, accepted = 0, rejected = 0, steps = 0;
            for (int i = 0; i < 60 && s.State == GameState.Playing; i++)
            {
                MoveResult r;
                if (i % 10 == 5) r = s.UseHammer(Pt(i % w, i * 3 % h));
                else if (i % 10 == 8) r = s.UseObstacleRemover(Pt(i * 5 % w, i * 7 % h));
                else if (i % 12 == 11) r = s.UseExtraMoves();
                else if (i % 15 == 13) r = s.UseSpecialMaker(Pt(i % w, i * 2 % h), SpecialKind.Bomb);
                else if (i % 17 == 16) r = s.UseFreeSwap(Pt(i % (w - 1), i % h), Pt(i % (w - 1) + 1, i % h));
                else
                {
                    var all = MoveFinder.FindAllMoves(s.Board);
                    if (all.Count == 0) break;
                    var m = all[(i * 7 + 3) % all.Count];
                    r = s.TrySwap(m.From, m.To);
                }
                if (r.Accepted) accepted++; else rejected++;
                steps += r.Steps.Count;
            }
            string obj = string.Join(",", s.Objectives.Items.Select(o => o.Current + "/" + o.Target));
            string inv = string.Join(",", Enum.GetValues(typeof(BoosterKind)).Cast<BoosterKind>().Select(k => s.Boosters.Count(k)));
            Console.WriteLine($"L{l} score={s.Score} moves={s.Moves.MovesLeft} acc={accepted} rej={rejected} steps={steps} obj={obj} inv={inv} board={Digest(s)}");
        }
    }
}
