using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PetaloPop.Game
{
    /// <summary>Efectos de partículas ligeros con pooling (sin ParticleSystem): fragmentos al eliminar y destellos de especiales.</summary>
    public sealed class Fx : MonoBehaviour
    {
        private readonly Stack<SpriteRenderer> pool = new Stack<SpriteRenderer>();
        private Transform root;

        private void Awake() { root = new GameObject("Fx").transform; root.SetParent(transform, false); }

        private SpriteRenderer Get()
        {
            SpriteRenderer r;
            if (pool.Count > 0) r = pool.Pop();
            else
            {
                r = new GameObject("fx").AddComponent<SpriteRenderer>();
                r.sprite = SpriteFactory.Square();
                r.sortingOrder = 10;
                r.transform.SetParent(root, false);
            }
            r.gameObject.SetActive(true);
            return r;
        }

        public void Burst(Vector3 pos, Color color, int count = 5)
        {
            for (int i = 0; i < count; i++)
            {
                float ang = (i + Random.value) / count * Mathf.PI * 2f;
                var dir = new Vector3(Mathf.Cos(ang), Mathf.Sin(ang), 0f);
                StartCoroutine(Piece(pos, dir * Random.Range(0.5f, 1.1f), color, Random.Range(0.1f, 0.2f)));
            }
        }

        private IEnumerator Piece(Vector3 pos, Vector3 vel, Color color, float size)
        {
            var r = Get();
            r.color = color;
            r.transform.position = pos;
            r.transform.localScale = Vector3.one * size;
            const float life = 0.4f;
            for (float t = 0; t < life; t += Time.deltaTime)
            {
                r.transform.position = pos + vel * t + Vector3.down * (2.2f * t * t);
                var c = color; c.a = 1f - t / life;
                r.color = c;
                yield return null;
            }
            r.gameObject.SetActive(false);
            pool.Push(r);
        }

        /// <summary>Barra o anillo que se desvanece (activación de líneas y bombas).</summary>
        public void Flash(Vector3 pos, Vector2 size, float duration = 0.25f)
        {
            StartCoroutine(FlashRoutine(pos, size, duration));
        }

        private IEnumerator FlashRoutine(Vector3 pos, Vector2 size, float duration)
        {
            var r = Get();
            r.transform.position = pos;
            for (float t = 0; t < duration; t += Time.deltaTime)
            {
                float k = t / duration;
                r.transform.localScale = new Vector3(size.x, size.y * (1f - 0.6f * k), 1f);
                r.color = new Color(1f, 1f, 1f, 0.8f * (1f - k));
                yield return null;
            }
            r.gameObject.SetActive(false);
            pool.Push(r);
        }
    }
}
