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

    private void OnEnable()
    {
        text = GetComponent<TextMesh>();
        GetComponent<MeshRenderer>().sortingOrder = 1000;
        _elapsed = 0f;

        SetFadeDirection();
        StartCoroutine(FadeTextToZeroAlpha());
        StartCoroutine(FadeInDirection());
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
