using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class FloatingText : MonoBehaviour
{
    public TextFadeMode fadeMode;
    public float fadeSpeed = 1f;
    public float fadeRange = 2f;

    private TextMesh text;
    private Vector3 fadeDirection;
    private float _elapsed;
    private Vector3 _baseScale;

    private void OnEnable()
    {
        text = GetComponent<TextMesh>();
        GetComponent<MeshRenderer>().sortingOrder = 1000;
        _elapsed = 0f;
        // Capture the intended scale (the handler sets localScale before activating us).
        _baseScale = transform.localScale;

        SetFadeDirection();
        StartCoroutine(PopScale());
        StartCoroutine(FadeTextToZeroAlpha());
        StartCoroutine(FadeInDirection());
    }

    // Quick scale overshoot so numbers "punch" in rather than just appearing.
    private IEnumerator PopScale()
    {
        const float dur = 0.16f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.deltaTime;
            float p = t / dur;
            float s = p < 0.55f
                ? Mathf.Lerp(0.4f, 1.18f, p / 0.55f)
                : Mathf.Lerp(1.18f, 1f, (p - 0.55f) / 0.45f);
            transform.localScale = _baseScale * s;
            yield return null;
        }
        transform.localScale = _baseScale;
    }

    private void OnDisable()
    {
        StopAllCoroutines();
    }

    void Update ()
    {
        _elapsed += Time.deltaTime;

        // Hard lifetime cap: deactivate after fade should be complete plus a small buffer
        float maxLifetime = (1f / Mathf.Max(fadeSpeed, 0.01f)) + 0.5f;
		if (text.color.a <= 0f || _elapsed >= maxLifetime)
        {
            gameObject.SetActive(false);
        }
	}

    private void SetFadeDirection()
    {
        switch (fadeMode)
        {
            default:
            case TextFadeMode.FadeNormal:
                fadeDirection = Vector3.zero;
                break;
            case TextFadeMode.FadeUp:
                fadeDirection = Vector3.up;
                break;
            case TextFadeMode.FadeDown:
                fadeDirection = Vector3.down;
                break;
            case TextFadeMode.FadeLeft:
                fadeDirection = Vector3.left;
                break;
            case TextFadeMode.FadeRight:
                fadeDirection = Vector3.right;
                break;
        }
    }

    public IEnumerator FadeTextToZeroAlpha()
    {
        text.color = new Color(text.color.r, text.color.g, text.color.b, 1);
        while (text.color.a > 0.0f)
        {
            text.color = new Color(text.color.r, text.color.g, text.color.b, text.color.a - (Time.deltaTime * fadeSpeed));
            yield return null;
        }
    }

    public IEnumerator FadeTextToFullAlpha()
    {
        text.color = new Color(text.color.r, text.color.g, text.color.b, 0);
        while (text.color.a < 1.0f)
        {
            text.color = new Color(text.color.r, text.color.g, text.color.b, text.color.a + (Time.deltaTime * fadeSpeed));
            yield return null;
        }
    }

    public IEnumerator FadeInDirection()
    {
        while (text.color.a > 0.0f)
        {
            transform.position += fadeDirection * Time.deltaTime * fadeRange;
            yield return null;
        }
    }
}
