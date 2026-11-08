using UnityEngine;
using PetaloPop.Core;

namespace PetaloPop.Game
{
    public sealed class TileView : MonoBehaviour
    {
        public int Type { get; private set; }
        private SpriteRenderer sr, overlay, ice;

        public void Init(int type, SpecialKind special, int hp, Transform parent)
        {
            if (sr == null)
            {
                sr = Make("Body", this.gameObject, 2, true);
                overlay = Make("Special", new GameObject("Special"), 3, false);
                ice = Make("Ice", new GameObject("Ice"), 4, false);
                overlay.transform.SetParent(transform, false);
                ice.transform.SetParent(transform, false);
            }
            Type = type;
            sr.sprite = SpriteFactory.Tile(type);
            sr.color = Color.white;
            transform.SetParent(parent, false);
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);
            SetSpecial(special);
            SetHp(hp);
        }

        private static SpriteRenderer Make(string name, GameObject go, int order, bool useSelf)
        {
            var r = go.AddComponent<SpriteRenderer>();
            r.sortingOrder = order;
            r.maskInteraction = SpriteMaskInteraction.VisibleInsideMask;
            return r;
        }

        public void SetSpecial(SpecialKind k)
        {
            overlay.enabled = k != SpecialKind.None;
            if (k != SpecialKind.None) overlay.sprite = SpriteFactory.Overlay(k);
        }

        /// <summary>Vida: en fichas de color = capas de hielo; en cajas = capas restantes (tono más claro cuanto menos quedan).</summary>
        public void SetHp(int hp)
        {
            if (Type > 0)
            {
                ice.enabled = hp > 0;
                if (hp > 0) { ice.sprite = SpriteFactory.Ice(); ice.color = new Color(1f, 1f, 1f, hp >= 2 ? 1f : 0.7f); }
            }
            else
            {
                ice.enabled = false;
                if (Type == Board.Crate) sr.color = Color.Lerp(new Color(1.35f, 1.25f, 1.1f), Color.white, Mathf.Clamp01((hp - 1) / 2f));
            }
        }

        public void SetSelected(bool selected) => transform.localScale = Vector3.one * (selected ? 1.15f : 1f);

        public Color BurstColor => Type > 0 ? SpriteFactory.ColorOf(Type) : new Color(0.6f, 0.45f, 0.3f);
    }
}
