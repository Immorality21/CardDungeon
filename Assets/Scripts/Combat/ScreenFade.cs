using System.Collections;
using ImmoralityGaming.Fundamentals;
using UnityEngine;

namespace Assets.Scripts.Combat
{
    /// <summary>
    /// A full-viewport colour overlay for combat framing beats: a quick <see cref="Flash"/> on
    /// victory and a lingering <see cref="FadeTo"/> tint on defeat. Auto-creates on first use (no
    /// scene wiring); a solid sprite parented to the camera (like <see cref="CombatStage"/>'s
    /// background) at a sorting order above the world so it reads as a screen effect. It sits
    /// under the UI-Toolkit panels, so the victory/death windows still draw on top.
    /// </summary>
    public class ScreenFade : SingletonBehaviour<ScreenFade>
    {
        private const int SortOrder = 1100; // above units (600), HP bars (900), floating text (1000)

        private static Sprite _solid;
        private SpriteRenderer _sr;
        private Coroutine _running;

        private void EnsureSprite()
        {
            if (_sr != null)
            {
                return;
            }

            var cam = Camera.main != null ? Camera.main : MainCamera.Camera;
            var go = new GameObject("ScreenFadeOverlay");
            _sr = go.AddComponent<SpriteRenderer>();
            _sr.sprite = Solid();
            _sr.sortingOrder = SortOrder;
            _sr.color = new Color(0f, 0f, 0f, 0f);

            if (cam != null)
            {
                go.transform.SetParent(cam.transform, false);
                float halfH = cam.orthographicSize;
                float halfW = halfH * cam.aspect;
                go.transform.localPosition = new Vector3(0f, 0f, 1f); // in front of the camera
                go.transform.localScale = new Vector3(halfW * 2f + 2f, halfH * 2f + 2f, 1f);
            }
        }

        /// <summary>Fade a colour in to <paramref name="peak"/> alpha then back out — a victory pop.</summary>
        public void Flash(Color color, float peak, float inDuration, float outDuration)
        {
            EnsureSprite();
            if (_sr == null)
            {
                return;
            }
            if (_running != null)
            {
                StopCoroutine(_running);
            }
            _running = StartCoroutine(FlashRoutine(color, peak, inDuration, outDuration));
        }

        /// <summary>Fade a colour in to <paramref name="targetAlpha"/> and hold — a defeat tint.</summary>
        public void FadeTo(Color color, float targetAlpha, float duration)
        {
            EnsureSprite();
            if (_sr == null)
            {
                return;
            }
            if (_running != null)
            {
                StopCoroutine(_running);
            }
            _running = StartCoroutine(FadeRoutine(color, targetAlpha, duration));
        }

        /// <summary>Instantly clear the overlay.</summary>
        public void Clear()
        {
            if (_running != null)
            {
                StopCoroutine(_running);
                _running = null;
            }
            if (_sr != null)
            {
                _sr.color = new Color(0f, 0f, 0f, 0f);
            }
        }

        private IEnumerator FlashRoutine(Color color, float peak, float inDuration, float outDuration)
        {
            float t = 0f;
            while (t < inDuration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(color, Mathf.Lerp(0f, peak, t / inDuration));
                yield return null;
            }
            t = 0f;
            while (t < outDuration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(color, Mathf.Lerp(peak, 0f, t / outDuration));
                yield return null;
            }
            SetAlpha(color, 0f);
            _running = null;
        }

        private IEnumerator FadeRoutine(Color color, float targetAlpha, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                SetAlpha(color, Mathf.Lerp(0f, targetAlpha, t / duration));
                yield return null;
            }
            SetAlpha(color, targetAlpha);
            _running = null;
        }

        private void SetAlpha(Color c, float a)
        {
            _sr.color = new Color(c.r, c.g, c.b, a);
        }

        private static Sprite Solid()
        {
            if (_solid != null)
            {
                return _solid;
            }
            var tex = new Texture2D(1, 1) { filterMode = FilterMode.Point };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            _solid = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            return _solid;
        }
    }
}
