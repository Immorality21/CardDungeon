using UnityEngine;

public class SpriteAnimator : MonoBehaviour
{
    private SpriteRenderer _renderer;
    private Sprite[] _frames;
    private float _frameDuration;
    private float _timer;
    private int _frame;

    public void Initialize(Sprite[] frames, float fps)
    {
        _renderer = GetComponent<SpriteRenderer>();
        _frames = frames;
        _frameDuration = 1f / fps;

        if (_frames.Length > 0)
            _renderer.sprite = _frames[0];
    }

    private void Update()
    {
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