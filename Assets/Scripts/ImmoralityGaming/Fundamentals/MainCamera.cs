using ImmoralityGaming.Fundamentals;
using System;
using System.Collections;
using UnityEngine;

public class MainCamera : SingletonBehaviour<MainCamera>
{
    const string INPUT_MOUSE_SCROLLWHEEL = "Mouse ScrollWheel";
    const string INPUT_MOUSE_X = "Mouse X";
    const string INPUT_MOUSE_Y = "Mouse Y";

    const float MIN_CAM_DISTANCE = 10f;
    const float MAX_CAM_DISTANCE = 40f;

    public Camera _camera
    {
        get
        {
            return GetComponent<Camera>();
        }
    }

    [Range(2f, 15f)]
    public float moveSpeed = 5f;

    /// <summary>
    /// When false, keyboard camera panning is disabled. Combat sets this off so the arrow/WASD
    /// keys drive the battle command cursor instead of moving the (frozen) camera.
    /// </summary>
    public bool AllowManualPan = true;

    private static Camera _staticCameraInstance { get; set; }
    public static Camera Camera => _staticCameraInstance = _staticCameraInstance ?? Instance._camera;
    public static bool IsMoving { get; set; }

    private bool _movingAnimation { get; set; }

    private ScreenScaler _screenScaler { get; set; }
    private Transform _cameraTransform { get; set; }

    protected override void Awake()
    {
        base.Awake();

        _staticCameraInstance = _camera;
        _cameraTransform = _camera.transform;
        _screenScaler = GetComponent<ScreenScaler>();

        if (_screenScaler != null)
        {
            _screenScaler.Resize();
        }
    }

    public void SetCameraZoom(float amount)
    {
        if (amount < 1 || amount > 11)
        {
            return;
        }

        Camera.orthographicSize = amount;
        _screenScaler.Resize();
    }

    public void ZoomIn()
    {
        float zoom = Camera.orthographicSize - 3f * Time.deltaTime;

        SetCameraZoom(zoom);
    }

    public void ZoomOut()
    {
        float zoom = Camera.orthographicSize + 3f * Time.deltaTime;

        SetCameraZoom(zoom);
    }

    private float _shakeMagnitude;
    private float _shakeDuration;
    private float _shakeElapsed;
    private Vector3 _appliedShake;

    private float _zoomBaseSize;
    private float _zoomPunchAmount;
    private float _zoomPunchDuration;
    private float _zoomPunchElapsed;
    private bool _zoomPunching;

    /// <summary>Kick off a decaying camera shake. Applied in LateUpdate on top of the follow position.</summary>
    public void Shake(float magnitude, float duration)
    {
        _shakeMagnitude = magnitude;
        _shakeDuration = duration;
        _shakeElapsed = 0f;
    }

    /// <summary>
    /// A quick zoom-IN punch (orthographic size dips then eases back) for combat impact. Only ever
    /// zooms in, so a full-viewport combat background parented to the camera keeps covering.
    /// </summary>
    public void ZoomPunch(float amount, float duration)
    {
        if (!_zoomPunching)
        {
            _zoomBaseSize = _camera.orthographicSize;
        }
        _zoomPunchAmount = amount;
        _zoomPunchDuration = duration;
        _zoomPunchElapsed = 0f;
        _zoomPunching = true;
    }

    private void Update()
    {
        Drag();
    }

    private void LateUpdate()
    {
        // Undo last frame's shake first so it never accumulates into the follow position.
        _cameraTransform.position -= _appliedShake;
        _appliedShake = Vector3.zero;

        if (_shakeElapsed < _shakeDuration)
        {
            _shakeElapsed += Time.deltaTime;
            float damper = 1f - Mathf.Clamp01(_shakeElapsed / _shakeDuration);
            float amt = _shakeMagnitude * damper;
            _appliedShake = new Vector3(UnityEngine.Random.Range(-amt, amt), UnityEngine.Random.Range(-amt, amt), 0f);
            _cameraTransform.position += _appliedShake;
        }

        if (_zoomPunching)
        {
            _zoomPunchElapsed += Time.deltaTime;
            if (_zoomPunchElapsed >= _zoomPunchDuration)
            {
                _camera.orthographicSize = _zoomBaseSize;
                _zoomPunching = false;
            }
            else
            {
                // sin(0..π): 0 at both ends, peak dip in the middle → zoom in and back out.
                float dip = Mathf.Sin((_zoomPunchElapsed / _zoomPunchDuration) * Mathf.PI) * _zoomPunchAmount;
                _camera.orthographicSize = Mathf.Max(1f, _zoomBaseSize - dip);
            }
        }
    }

    public void MoveCamera(Vector3 direction)
    {
        _cameraTransform.position += (direction * Time.deltaTime * moveSpeed);
        ClampCamera();
    }

    private void SetMovementFlag()
    {
        IsMoving = false;
    }

    public void Drag(Vector3 direction)
    {
        _cameraTransform.position += direction * Time.deltaTime * moveSpeed;
        ClampCamera();
    }

    private void Drag()
    {
        if (!AllowManualPan)
        {
            return;
        }

        float xValue = 0f; // Input.GetAxis(INPUT_MOUSE_X);
        float yValue = 0f; // Input.GetAxis(INPUT_MOUSE_Y);

        // TODO mouse movement when edge of screen or swipe on mobile

        if (xValue < -0.1f || Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            _cameraTransform.position += (Vector3.right * Time.deltaTime * moveSpeed);
        }
        else if (xValue > 0.1f || Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            _cameraTransform.position += (Vector3.left * Time.deltaTime * moveSpeed);
        }

        if (yValue < -0.1f || Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            _cameraTransform.position += (Vector3.up * Time.deltaTime * moveSpeed);
        }
        else if (yValue > 0.1f || Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            _cameraTransform.position += (Vector3.down * Time.deltaTime * moveSpeed);
        }

        ClampCamera();
    }

    public void SetPosition(Vector2 position)
    {
        _cameraTransform.position = new Vector3(position.x, position.y, -10);
        ClampCamera();
    }

    public void MoveCameraTo(Vector2 position, float time = 0.5f, Action onReachDestination = null)
    {
        StopAllCoroutines();
        StartCoroutine(MoveCameraOverTime(new Vector3(position.x, position.y, -10), time, onReachDestination));
    }

    private void ClampCamera()
    {
        return; // TODO

        //var largestVector = PlayingField.ActivePlayingField.LargestVector2;
        //var smallestVector = PlayingField.ActivePlayingField.SmallestVector2;

        //_cameraTransform.position = new Vector3
        //{
        //    x = Mathf.Clamp(_cameraTransform.position.x, smallestVector.x, largestVector.x),
        //    y = Mathf.Clamp(_cameraTransform.position.y, smallestVector.y, largestVector.y),
        //    z = _cameraTransform.position.z
        //};
    }

    private IEnumerator MoveCameraOverTime(Vector3 targetPosition, float time, Action onReachDestination = null)
    {
        while (Time.timeScale == 0)
        {
            yield return new WaitForEndOfFrame();
        }

        _movingAnimation = true;
        Vector3 startPosition = _cameraTransform.position;
        var t = 0f;

        while (t < 1)
        {
            while (Time.timeScale == 0)
            {
                yield return new WaitForEndOfFrame();
            }

            t += Time.deltaTime / time;

            _cameraTransform.position = new Vector3(
                Mathf.SmoothStep(startPosition.x, targetPosition.x, t),
                Mathf.SmoothStep(startPosition.y, targetPosition.y, t),
                _cameraTransform.position.z);
            yield return new WaitForEndOfFrame();
        }

        onReachDestination?.Invoke();
        _movingAnimation = false;
    }
}
