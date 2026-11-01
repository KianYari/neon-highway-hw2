using UnityEngine;

// Third-person camera behind the car. Also does the shake and the speed FOV.
[RequireComponent(typeof(Camera))]
public class CameraFollow : MonoBehaviour
{
    public Transform target;
    public Vector3 playOffset = new Vector3(0f, 3.9f, -8.5f);
    public Vector3 menuOffset = new Vector3(4.2f, 1.5f, -6.2f);
    public Vector3 lookAtOffset = new Vector3(0f, 1.6f, 28f);
    public float minFov = 62f;
    public float maxFov = 84f;

    Camera cam;
    Vector3 currentOffset;
    float shakeMagnitude;
    float shakeEnd;
    float shakeDuration = 1f;

    void Awake()
    {
        cam = GetComponent<Camera>();
        currentOffset = menuOffset;
    }

    public void Shake(float magnitude, float duration)
    {
        shakeMagnitude = magnitude;
        shakeDuration = duration;
        shakeEnd = Time.time + duration;
    }

    void LateUpdate()
    {
        GameManager gm = GameManager.Instance;
        bool inMenu = gm.State == GameState.Menu;

        Vector3 wanted = inMenu ? menuOffset : playOffset;
        currentOffset = Vector3.Lerp(currentOffset, wanted, 1f - Mathf.Exp(-3f * Time.unscaledDeltaTime));

        Vector3 anchor = target.position;
        float followX = anchor.x * 0.65f;
        Vector3 pos = new Vector3(followX, 0f, 0f) + currentOffset;

        if (shakeEnd > Time.time)
        {
            float strength = shakeMagnitude * ((shakeEnd - Time.time) / shakeDuration);
            pos += Random.insideUnitSphere * (strength * 0.35f);
        }

        transform.position = pos;
        Vector3 look = new Vector3(anchor.x * 0.3f, 0f, 0f) + (inMenu ? new Vector3(0f, 0.8f, 0f) : lookAtOffset);
        transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);

        float targetFov = Mathf.Lerp(minFov, maxFov, gm.Speed01);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, targetFov, 1f - Mathf.Exp(-2f * Time.unscaledDeltaTime));
    }
}
