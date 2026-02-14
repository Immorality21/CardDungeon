using UnityEngine;

public class ScreenScaler : MonoBehaviour
{
    //public float orthographicSize = 5;
    public float aspect = 1.33333f;

    protected new Camera camera => MainCamera.Camera;

    void Awake()
    {
        //camera = GetComponent<Camera>();
        float orthographicSize = camera.orthographicSize;

        camera.projectionMatrix = Matrix4x4.Ortho(
                -orthographicSize * aspect, orthographicSize * aspect,
                -orthographicSize, orthographicSize,
                camera.nearClipPlane, camera.farClipPlane);
    }

    public void Resize()
    {
        float orthographicSize = camera.orthographicSize;

        camera.projectionMatrix = Matrix4x4.Ortho(
                -orthographicSize * aspect, orthographicSize * aspect,
                -orthographicSize, orthographicSize,
                camera.nearClipPlane, camera.farClipPlane);
    }
}
