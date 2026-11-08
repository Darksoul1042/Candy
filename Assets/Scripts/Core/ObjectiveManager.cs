using System.Collections.Generic;
using System.Linq;

namespace PetaloPop.Core
{
    public enum ObjectiveKind { CollectType, ClearJelly, BreakFrozen, DestroyCrates }

    public sealed class ObjectiveSpec
    {
        public ObjectiveKind Kind;
        public int Type;   // CollectType: color
        public int Count;  // <= 0 en ClearJelly/BreakFrozen/DestroyCrates = todos los del tablero inicial
    }

    public sealed class ObjectiveProgress
    {
        public ObjectiveSpec Spec;
        public int Target;
        public int Current;
        public bool Done => Current >= Target;
    }

    public sealed class ObjectiveManager
    {
        public List<ObjectiveProgress> Items { get; } = new List<ObjectiveProgress>();

        public ObjectiveManager(IEnumerable<ObjectiveSpec> specs, Board initial)
        {
            if (specs == null) return;
            foreach (var s in specs)
            {
                int target = s.Count;
                if (target <= 0)
                {
                    switch (s.Kind)
                    {
                        case ObjectiveKind.ClearJelly: target = initial.CountWhere(p => initial.Jelly(p) > 0); break;
                        case ObjectiveKind.BreakFrozen: target = initial.CountWhere(p => initial[p] > 0 && initial.Hp(p) > 0); break;
                        case ObjectiveKind.DestroyCrates: target = initial.CountWhere(p => initial[p] == Board.Crate); break;
                    }
                }
                Items.Add(new ObjectiveProgress { Spec = s, Target = target });
            }
        }

        private ObjectiveManager() { }

        public ObjectiveManager Clone()
        {
            var c = new ObjectiveManager();
            foreach (var i in Items) c.Items.Add(new ObjectiveProgress { Spec = i.Spec, Target = i.Target, Current = i.Current });
            return c;
        }

        public void Apply(RoundStats s)
        {
            foreach (var i in Items)
            {
                switch (i.Spec.Kind)
                {
                    case ObjectiveKind.CollectType:
                        s.ClearedByType.TryGetValue(i.Spec.Type, out int n);
                        i.Current += n;
                        break;
                    case ObjectiveKind.ClearJelly: i.Current += s.JellyCleared; break;
                    case ObjectiveKind.BreakFrozen: i.Current += s.FrozenBroken; break;
                    case ObjectiveKind.DestroyCrates: i.Current += s.CratesDestroyed; break;
                }
            }
        }

        public bool AllDone => Items.All(i => i.Done);
    }
}
