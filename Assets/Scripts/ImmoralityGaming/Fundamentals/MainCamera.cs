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

    private void Update()
    {
        Drag();
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
