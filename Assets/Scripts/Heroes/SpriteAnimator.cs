using UnityEngine;

public class SpriteAnimator : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private Sprite[] _frames;
    private float _frameDuration;
    private float _timer;
    private int _frame;

    // A one-shot played over the loop (the hit reaction): its frames, how long each shows, and how
    // far through it is. The loop keeps its place underneath and resumes where it was.
    private Sprite[] _oneShot;
    private float _oneShotFrameDuration;
    private float _oneShotTimer;

    // Set while a one-shot ends on a held pose (the fall): no loop runs until Resume.
    private bool _holdLast;

    public void Initialize(Sprite[] frames, float fps)
    {
        _renderer = GetComponent<SpriteRenderer>();
        _frames = frames;
        _frameDuration = 1f / fps;
        _oneShot = null;

        if (_frames.Length > 0)
            _renderer.sprite = _frames[0];
    }

    /// <summary>
    /// Shows <paramref name="frames"/> once, spread over <paramref name="seconds"/>, then goes back to
    /// the loop - or to the sprite that was showing, for a unit with no loop. A second call while one
    /// is playing restarts it. Unscaled time, like the hit flash, so hit-stop does not freeze it.
    /// </summary>
    public void PlayOnce(Sprite[] frames, float seconds)
    {
        if (frames == null || frames.Length == 0 || seconds <= 0f)
        {
            return;
        }
        if (_renderer == null)
        {
            _renderer = GetComponent<SpriteRenderer>();
        }
        if (_frames == null || _frames.Length == 0)
        {
            // No loop to return to: remember the still sprite as a one-frame loop.
            _frames = new[] { _renderer.sprite };
            _frameDuration = 1f;
        }
        _oneShot = frames;
        _oneShotFrameDuration = seconds / frames.Length;
        _oneShotTimer = 0f;
        _holdLast = false;
        _renderer.sprite = frames[0];
    }

    /// <summary>
    /// <see cref="PlayOnce"/>, then stays on the last frame instead of going back to the loop - a hero
    /// falling and lying where they fell. <see cref="Resume"/> lets the loop run again.
    /// </summary>
    public void PlayOnceAndHold(Sprite[] frames, float seconds)
    {
        PlayOnce(frames, seconds);
        if (_oneShot != null)
        {
            _holdLast = true;
        }
    }

    /// <summary>Back to the loop after a held pose (a revive).</summary>
    public void Resume()
    {
        _holdLast = false;
        _oneShot = null;
        if (_renderer != null && _frames != null && _frames.Length > 0)
        {
            _renderer.sprite = _frames[_frame % _frames.Length];
        }
    }

    private void Update()
    {
        if (_oneShot != null)
        {
            _oneShotTimer += Time.unscaledDeltaTime;
            int index = Mathf.FloorToInt(_oneShotTimer / _oneShotFrameDuration);
            if (index < _oneShot.Length)
            {
                _renderer.sprite = _oneShot[index];
                return;
            }
            if (_holdLast)
            {
                _renderer.sprite = _oneShot[_oneShot.Length - 1];
                return;
            }
            _oneShot = null;
            if (_frames != null && _frames.Length > 0)
            {
                _renderer.sprite = _frames[_frame % _frames.Length];
            }
        }

        if (_frames == null || _frames.Length <= 1)
            return;

        _timer += Time.deltaTime;

        if (_timer >= _frameDuration)
        {
            _timer -= _frameDuration;
            _frame = (_frame + 1) % _frames.Length;
            _renderer.sprite = _frames[_frame];
        }
    }
}
