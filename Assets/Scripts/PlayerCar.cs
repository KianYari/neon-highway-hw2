using UnityEngine;
using UnityEngine.InputSystem;

// The car the player drives. It stays at z = 0, the road moves past it.
public class PlayerCar : MonoBehaviour
{
    public const int MaxLives = 3;
    public const float MaxFuel = 100f;

    [Header("Input")]
    public InputActionAsset inputActions;

    [Header("Driving")]
    public float laneSmoothTime = 0.1f;
    public float invulnerableTime = 1.8f;

    [Header("Fuel")]
    public float fuelDrainBase = 2f;
    public float fuelDrainAtMaxSpeed = 1.5f;

    [Header("Visuals")]
    public Transform body;
    public Transform[] wheels;
    public Renderer[] bodyRenderers;
    public Renderer[] blinkRenderers;
    public Light underglowLight;
    public Renderer underglowRenderer;
    public Color underglowA = new Color(0f, 0.9f, 1f);
    public Color underglowB = new Color(1f, 0.1f, 0.8f);
    public ParticleSystem[] exhaust;

    [Header("Effects")]
    public PooledFx explosionFx;
    public CameraFollow cameraRig;

    public int Lives { get; private set; } = MaxLives;
    public float Fuel { get; private set; } = MaxFuel;
    public int Lane { get; private set; } = 1;
    public bool IsInvulnerable => invulnerableUntil > Time.time;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");

    InputAction leftAction;
    InputAction rightAction;
    MaterialPropertyBlock block;
    Collider hitbox;

    float x;
    float xVelocity;
    float invulnerableUntil;
    float flashUntil;
    float wheelAngle;
    float lowFuelBeepAt;
    bool destroyed;

    void Awake()
    {
        InputActionMap map = inputActions.FindActionMap("Gameplay", true);
        leftAction = map.FindAction("MoveLeft", true);
        rightAction = map.FindAction("MoveRight", true);
        block = new MaterialPropertyBlock();
        hitbox = GetComponent<Collider>();
    }

    void OnEnable()
    {
        leftAction.performed += OnLeft;
        rightAction.performed += OnRight;
    }

    void OnDisable()
    {
        leftAction.performed -= OnLeft;
        rightAction.performed -= OnRight;
    }

    void OnLeft(InputAction.CallbackContext ctx) => ChangeLane(-1);
    void OnRight(InputAction.CallbackContext ctx) => ChangeLane(1);

    void ChangeLane(int dir)
    {
        if (GameManager.Instance.State != GameState.Playing || destroyed) return;
        Lane = Mathf.Clamp(Lane + dir, 0, GameManager.LaneCount - 1);
    }

    // Used by the AI test rig, goes through the same path as the keys.
    public void SetLane(int lane)
    {
        Lane = Mathf.Clamp(lane, 0, GameManager.LaneCount - 1);
    }

    public void ResetCar()
    {
        Lives = MaxLives;
        Fuel = MaxFuel;
        Lane = 1;
        x = 0f;
        xVelocity = 0f;
        invulnerableUntil = 0f;
        flashUntil = 0f;
        destroyed = false;
        hitbox.enabled = true;

        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
        SetVisible(true);
        SetBodyEmission(Color.black);
        foreach (ParticleSystem ps in exhaust) ps.Play();
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        float dt = Time.deltaTime;
        float time = Time.time;

        if (!destroyed)
        {
            x = Mathf.SmoothDamp(x, GameManager.LaneToX(Lane), ref xVelocity, laneSmoothTime);
            transform.position = new Vector3(x, 0f, 0f);

            float roll = Mathf.Clamp(-xVelocity * 2.2f, -14f, 14f);
            float yaw = Mathf.Clamp(xVelocity * 1.6f, -12f, 12f);
            transform.rotation = Quaternion.Euler(0f, yaw, roll);
        }

        wheelAngle += gm.WorldSpeed * dt / 0.38f * Mathf.Rad2Deg;
        foreach (Transform w in wheels)
            w.localRotation = Quaternion.Euler(wheelAngle, 0f, 0f) * Quaternion.Euler(0f, 0f, 90f);

        UpdateNeon(gm, time);
        UpdateBlink(time);
        UpdateFlash(time);

        if (gm.State == GameState.Playing && !destroyed) UpdateFuel(gm, dt, time);
    }

    void UpdateFuel(GameManager gm, float dt, float time)
    {
        Fuel -= (fuelDrainBase + fuelDrainAtMaxSpeed * gm.Speed01) * dt;

        if (Fuel <= 0f)
        {
            Fuel = 0f;
            AudioManager.Instance.PlayGameOver();
            gm.GameOver();
            return;
        }

        if (Fuel < MaxFuel * 0.25f && time >= lowFuelBeepAt)
        {
            AudioManager.Instance.PlayLowFuel();
            lowFuelBeepAt = time + 1f;
        }
    }

    void UpdateNeon(GameManager gm, float time)
    {
        float speed01 = gm.Speed01;
        float pulse = 0.85f + 0.15f * Mathf.Sin(time * 9f);
        Color color = Color.Lerp(underglowA, underglowB, (Mathf.Sin(time * 0.6f) + 1f) * 0.5f);

        if (underglowLight != null)
        {
            underglowLight.color = color;
            underglowLight.intensity = Mathf.Lerp(1.5f, 9f, speed01) * pulse;
        }

        if (underglowRenderer != null)
        {
            underglowRenderer.GetPropertyBlock(block);
            block.SetColor(EmissionColor, color * Mathf.Lerp(1.5f, 7f, speed01) * pulse);
            underglowRenderer.SetPropertyBlock(block);
        }
    }

    void UpdateBlink(float time)
    {
        bool visible = !destroyed && (!IsInvulnerable || Mathf.FloorToInt(time * 10f) % 2 == 0);
        foreach (Renderer r in blinkRenderers) r.enabled = visible;
    }

    void UpdateFlash(float time)
    {
        if (destroyed) return;
        if (flashUntil > time) SetBodyEmission(Color.red * 3f * ((flashUntil - time) / 0.4f));
        else if (flashUntil != 0f)
        {
            SetBodyEmission(Color.black);
            flashUntil = 0f;
        }
    }

    void SetBodyEmission(Color color)
    {
        foreach (Renderer r in bodyRenderers)
        {
            r.GetPropertyBlock(block);
            block.SetColor(EmissionColor, color);
            r.SetPropertyBlock(block);
        }
    }

    void SetVisible(bool visible)
    {
        foreach (Renderer r in blinkRenderers) r.enabled = visible;
        if (body != null) body.gameObject.SetActive(visible);
        if (underglowLight != null) underglowLight.enabled = visible;
    }

    void OnTriggerEnter(Collider other)
    {
        Interactable item = other.GetComponentInParent<Interactable>();
        if (item != null) item.OnPlayerContact(this);
    }

    // Returns true when the hit counted.
    public bool TakeHit(Obstacle obstacle)
    {
        GameManager gm = GameManager.Instance;
        if (gm.State != GameState.Playing || destroyed || IsInvulnerable) return false;

        Lives--;
        gm.NotifyPlayerHit();
        cameraRig.Shake(0.6f, 0.4f);

        if (Lives <= 0)
        {
            Explode();
            return true;
        }

        invulnerableUntil = Time.time + invulnerableTime;
        flashUntil = Time.time + 0.4f;
        AudioManager.Instance.PlayCrash();
        return true;
    }

    void Explode()
    {
        destroyed = true;
        hitbox.enabled = false;
        SetVisible(false);
        foreach (ParticleSystem ps in exhaust) ps.Stop();

        if (explosionFx != null)
        {
            PooledFx fx = PoolManager.Instance.Get(explosionFx, transform.position + Vector3.up, Quaternion.identity);
            fx.Play(new Color(1f, 0.5f, 0.1f));
        }

        cameraRig.Shake(1.2f, 0.8f);
        AudioManager.Instance.PlayCrash();
        AudioManager.Instance.PlayGameOver();
        GameManager.Instance.GameOver();
    }

    public void AddFuel(float amount)
    {
        Fuel = Mathf.Min(MaxFuel, Fuel + amount);
    }
}
