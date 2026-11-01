using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// HUD, main menu, pause menu and game over screen.
public class UIManager : MonoBehaviour
{
    [Header("Input")]
    public InputActionAsset inputActions;
    public InputSystemUIInputModule uiModule;

    [Header("Panels")]
    public GameObject mainMenuPanel;
    public GameObject hudPanel;
    public GameObject pausePanel;
    public GameObject gameOverPanel;

    [Header("HUD")]
    public Text speedText;
    public Text scoreText;
    public Text highScoreText;
    public Text levelText;
    public Text coinsText;
    public Text lowFuelText;
    public Text levelBanner;
    public Image fuelFill;
    public Image[] lifeIcons;
    public Image damageFlash;

    [Header("Menus")]
    public Text menuHighScoreText;
    public Text finalScoreText;
    public Text finalHighScoreText;
    public Text newRecordText;
    public Text gameOverReasonText;

    public Color fuelFull = new Color(0.1f, 1f, 0.55f);
    public Color fuelLow = new Color(1f, 0.15f, 0.2f);

    GameManager gm;
    float flashAlpha;
    float bannerUntil;

    void Awake()
    {
        InputActionMap ui = inputActions.FindActionMap("UI", true);
        ui.Enable();

        uiModule.actionsAsset = inputActions;
        uiModule.point = InputActionReference.Create(ui.FindAction("Point", true));
        uiModule.leftClick = InputActionReference.Create(ui.FindAction("Click", true));
        uiModule.move = InputActionReference.Create(ui.FindAction("Navigate", true));
        uiModule.submit = InputActionReference.Create(ui.FindAction("Submit", true));
        uiModule.cancel = InputActionReference.Create(ui.FindAction("Cancel", true));
        uiModule.scrollWheel = InputActionReference.Create(ui.FindAction("Scroll", true));
    }

    void Start()
    {
        gm = GameManager.Instance;
        gm.StateChanged += OnStateChanged;
        gm.LevelChanged += OnLevelChanged;
        gm.PlayerHit += OnPlayerHit;
        OnStateChanged(gm.State);
    }

    void OnDestroy()
    {
        if (gm == null) return;
        gm.StateChanged -= OnStateChanged;
        gm.LevelChanged -= OnLevelChanged;
        gm.PlayerHit -= OnPlayerHit;
    }

    void OnStateChanged(GameState state)
    {
        mainMenuPanel.SetActive(state == GameState.Menu);
        hudPanel.SetActive(state == GameState.Playing || state == GameState.Paused);
        pausePanel.SetActive(state == GameState.Paused);
        gameOverPanel.SetActive(state == GameState.GameOver);

        if (state == GameState.Menu)
            menuHighScoreText.text = "BEST  " + gm.HighScore;

        if (state == GameState.GameOver)
        {
            finalScoreText.text = gm.Score.ToString();
            finalHighScoreText.text = "BEST  " + gm.HighScore;
            newRecordText.gameObject.SetActive(gm.NewRecord);
            gameOverReasonText.text = gm.player.Fuel <= 0f ? "OUT OF FUEL" : "CAR DESTROYED";
        }

        if (state == GameState.Playing) levelBanner.gameObject.SetActive(false);
    }

    void OnLevelChanged(int level)
    {
        levelBanner.text = "LEVEL " + level;
        levelBanner.gameObject.SetActive(true);
        bannerUntil = Time.time + 2f;
        AudioManager.Instance.PlayLevelUp();
    }

    void OnPlayerHit()
    {
        flashAlpha = 0.5f;
    }

    void Update()
    {
        if (damageFlash != null)
        {
            flashAlpha = Mathf.MoveTowards(flashAlpha, 0f, Time.unscaledDeltaTime * 1.4f);
            Color c = damageFlash.color;
            c.a = flashAlpha;
            damageFlash.color = c;
        }

        if (!hudPanel.activeSelf) return;

        PlayerCar player = gm.player;
        float fuel01 = player.Fuel / PlayerCar.MaxFuel;

        speedText.text = Mathf.RoundToInt(gm.WorldSpeed * 3.6f).ToString();
        scoreText.text = gm.Score.ToString();
        highScoreText.text = "BEST " + Mathf.Max(gm.HighScore, gm.Score);
        levelText.text = "LEVEL " + gm.Level;
        coinsText.text = "x " + gm.Coins;

        fuelFill.fillAmount = fuel01;
        fuelFill.color = Color.Lerp(fuelLow, fuelFull, Mathf.Clamp01((fuel01 - 0.15f) / 0.4f));

        for (int i = 0; i < lifeIcons.Length; i++)
            lifeIcons[i].enabled = i < player.Lives;

        bool lowFuel = fuel01 < 0.25f && gm.State == GameState.Playing;
        lowFuelText.gameObject.SetActive(lowFuel && Mathf.FloorToInt(Time.time * 3f) % 2 == 0);

        if (levelBanner.gameObject.activeSelf && Time.time > bannerUntil)
            levelBanner.gameObject.SetActive(false);
    }

    // Button callbacks (wired as persistent listeners in the scene).
    public void OnStartClicked() { AudioManager.Instance.PlayClick(); gm.StartGame(); }
    public void OnResumeClicked() { AudioManager.Instance.PlayClick(); gm.Resume(); }
    public void OnRestartClicked() { AudioManager.Instance.PlayClick(); gm.Restart(); }
    public void OnMenuClicked() { AudioManager.Instance.PlayClick(); gm.ToMenu(); }

    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
