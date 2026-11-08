using System;

namespace PetaloPop.Core
{
    public sealed class MoveManager
    {
        public int MovesLeft { get; private set; }
        public MoveManager(int moves) { MovesLeft = moves; }

        public bool HasMoves => MovesLeft > 0;

        public void Consume()
        {
            if (MovesLeft <= 0) throw new InvalidOperationException("Sin movimientos.");
            MovesLeft--;
        }

        public void Add(int extra) => MovesLeft += extra;

        public MoveManager Clone() => new MoveManager(MovesLeft);
    }
}
