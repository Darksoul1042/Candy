using UnityEngine;
using UnityEngine.EventSystems;
using PetaloPop.Core;

namespace PetaloPop.Game
{
    /// <summary>Entrada táctil/ratón: deslizar sobre una ficha o tocar dos fichas adyacentes.</summary>
    public sealed class InputController : MonoBehaviour
    {
        private GameManager game;
        private BoardView view;
        private Camera cam;

        private bool pressing;
        private Position pressCell;
        private Vector2 pressScreen;
        private bool hasSelection;
        private Position selected;

        public void Init(GameManager g, BoardView v, Camera c) { game = g; view = v; cam = c; }

        private void Update()
        {
            if (game == null || game.Busy) { pressing = false; return; }

            if (Input.GetMouseButtonDown(0))
            {
                if (OverUi()) { pressing = false; return; } // un toque sobre un botón no debe contar sobre el tablero
                var world = cam.ScreenToWorldPoint(Input.mousePosition);
                pressing = view.TryWorldToCell(world, out pressCell);
                pressScreen = Input.mousePosition;
            }
            else if (pressing && Input.GetMouseButton(0))
            {
                Vector2 delta = (Vector2)Input.mousePosition - pressScreen;
                float threshold = Screen.height * 0.025f;
                if (delta.magnitude >= threshold)
                {
                    Position target = pressCell;
                    if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) target = new Position(pressCell.X + (delta.x > 0 ? 1 : -1), pressCell.Y);
                    else target = new Position(pressCell.X, pressCell.Y + (delta.y > 0 ? 1 : -1));
                    pressing = false;
                    ClearSelection();
                    game.RequestSwap(pressCell, target);
                }
            }
            else if (pressing && Input.GetMouseButtonUp(0))
            {
                pressing = false;
                OnTap(pressCell);
            }
        }

        private static bool OverUi()
        {
            var es = EventSystem.current;
            if (es == null) return false;
            return Input.touchCount > 0 ? es.IsPointerOverGameObject(Input.GetTouch(0).fingerId) : es.IsPointerOverGameObject();
        }

        private void OnTap(Position cell)
        {
            if (game.PendingBooster.HasValue && game.PendingBooster.Value != BoosterKind.FreeSwap)
            {
                game.RequestBoosterTap(cell);
                return;
            }
            if (hasSelection && selected.IsAdjacentTo(cell))
            {
                var a = selected;
                ClearSelection();
                game.RequestSwap(a, cell);
                return;
            }
            ClearSelection();
            selected = cell;
            hasSelection = true;
            var t = view.TileAt(cell);
            if (t != null) t.SetSelected(true);
        }

        private void ClearSelection()
        {
            if (!hasSelection) return;
            var t = view.TileAt(selected);
            if (t != null) t.SetSelected(false);
            hasSelection = false;
        }
    }
}
