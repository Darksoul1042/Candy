using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PetaloPop.Game
{
    /// <summary>Tweens sencillos basados en corrutinas. Permite esperar varias animaciones en paralelo.</summary>
    public sealed class AnimationManager : MonoBehaviour
    {
        public static float EaseOutCubic(float t) { t = 1f - t; return 1f - t * t * t; }
        public static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f, c3 = c1 + 1f;
            float p = t - 1f;
            return 1f + c3 * p * p * p + c1 * p * p;
        }

        public IEnumerator MoveTo(Transform t, Vector3 to, float duration)
        {
            if (t == null) yield break;
            Vector3 from = t.position;
            for (float e = 0f; e < duration; e += Time.deltaTime)
            {
                if (t == null) yield break;
                t.position = Vector3.LerpUnclamped(from, to, EaseOutCubic(e / duration));
                yield return null;
            }
            if (t != null) t.position = to;
        }

        public IEnumerator ScaleTo(Transform t, float to, float duration)
        {
            if (t == null) yield break;
            float from = t.localScale.x;
            for (float e = 0f; e < duration; e += Time.deltaTime)
            {
                if (t == null) yield break;
                t.localScale = Vector3.one * Mathf.LerpUnclamped(from, to, EaseOutBack(Mathf.Clamp01(e / duration)));
                yield return null;
            }
            if (t != null) t.localScale = Vector3.one * to;
        }

        /// <summary>Lanza todas las corrutinas a la vez y espera a que terminen todas.</summary>
        public IEnumerator Parallel(List<IEnumerator> routines)
        {
            int running = routines.Count;
            foreach (var r in routines) StartCoroutine(Wrap(r, () => running--));
            while (running > 0) yield return null;
        }

        private static IEnumerator Wrap(IEnumerator inner, System.Action done)
        {
            yield return inner;
            done();
        }
    }
}
