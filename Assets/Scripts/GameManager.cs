using System;
using UnityEngine;
using UnityEngine.InputSystem;

public enum GameState { Menu, Playing, Paused, GameOver }

// Singleton that owns the game state, the speed of the world, the score and the high score.
[DefaultExecutionOrder(-100)]
public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public const string HighScoreKey = "HighScore";
    public const int LaneCount = 3;
    public const float LaneWidth = 3.2f;

    [Header("Speed (m/s)")]
    public float menuSpeed = 12f;
    public float baseSpeed = 22f;
    public float speedPerLevel = 2.5f;
    public float maxSpeed = 56f;
    public float acceleration = 3f;

    [Header("Progression")]
    public float metersPerLevel = 300f;
    public int coinScore = 50;

    [Header("References")]
    public InputActionAsset inputActions;
    public PlayerCar player;
    public RoadGenerator road;
    public ObstacleSpawner spawner;
    public FireworksManager fireworks;

    public GameState State { get; private set; } = GameState.Menu;
    public float WorldSpeed { get; private set; }
    public float Distance { get; private set; }
    public int Coins { get; private set; }
    public int Level { get; private set; } = 1;
    public int HighScore { get; private set; }
    public bool NewRecord { get; private set; }
    public int Score => Mathf.FloorToInt(Distance) + Coins * coinScore;
    public float Difficulty01 => Mathf.Clamp01((Level - 1) / 12f);
    public float Speed01 => Mathf.Clamp01(WorldSpeed / maxSpeed);

    public event Action<GameState> StateChanged;
    public event Action<int> LevelChanged;
    public event Action PlayerHit;

    InputAction pauseAction;
    InputAction confirmAction;

    public static float LaneToX(int lane) => (lane - 1) * LaneWidth;

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        HighScore = PlayerPrefs.GetInt(HighScoreKey, 0);

        InputActionMap map = inputActions.FindActionMap("Gameplay", true);
        pauseAction = map.FindAction("Pause", true);
        confirmAction = map.FindAction("Confirm", true);
    }

    void OnEnable()
    {
        inputActions.FindActionMap("Gameplay", true).Enable();
        pauseAction.performed += OnPausePressed;
        confirmAction.performed += OnConfirmPressed;
    }

    void OnDisable()
    {
        pauseAction.performed -= OnPausePressed;
        confirmAction.performed -= OnConfirmPressed;
    }

    void Start()
    {
        spawner.Prewarm();
        fireworks.Prewarm();
        PoolManager.Instance.Prewarm(player.explosionFx.gameObject, 1);
        player.ResetCar();
        road.ResetRoad();
        SetState(GameState.Menu);
    }

    void Update()
    {
        float dt = Time.deltaTime;
        float target = 0f;
        float rate = acceleration;

        switch (State)
        {
            case GameState.Menu:
                target = menuSpeed;
                break;
            case GameState.Playing:
                Distance += WorldSpeed * dt;
                int level = 1 + Mathf.FloorToInt(Distance / metersPerLevel);
                if (level != Level)
                {
                    Level = level;
                    LevelChanged?.Invoke(Level);
                }
                target = Mathf.Min(maxSpeed, baseSpeed + (Level - 1) * speedPerLevel);
                break;
            case GameState.GameOver:
                rate = 30f;
                break;
        }

        WorldSpeed = Mathf.MoveTowards(WorldSpeed, target, rate * dt);
    }

    void OnPausePressed(InputAction.CallbackContext ctx)
    {
        if (State == GameState.Playing) Pause();
        else if (State == GameState.Paused) Resume();
    }

    void OnConfirmPressed(InputAction.CallbackContext ctx)
    {
        if (State == GameState.Menu) StartGame();
        else if (State == GameState.GameOver) Restart();
    }

    void SetState(GameState state)
    {
        State = state;
        Time.timeScale = state == GameState.Paused ? 0f : 1f;
        AudioListener.pause = state == GameState.Paused;
        StateChanged?.Invoke(state);
    }

    void ResetRun()
    {
        Distance = 0f;
        Coins = 0;
        Level = 1;
        NewRecord = false;
        player.ResetCar();
        spawner.ResetSpawner(true);
    }

    public void StartGame()
    {
        ResetRun();
        WorldSpeed = menuSpeed;
        SetState(GameState.Playing);
    }

    public void Restart() => StartGame();

    public void Pause()
    {
        if (State == GameState.Playing) SetState(GameState.Paused);
    }

    public void Resume()
    {
        if (State == GameState.Paused) SetState(GameState.Playing);
    }

    public void ToMenu()
    {
        spawner.ResetSpawner(false);
        player.ResetCar();
        Distance = 0f;
        Coins = 0;
        Level = 1;
        SetState(GameState.Menu);
    }

    public void GameOver()
    {
        if (State != GameState.Playing) return;

        if (Score > HighScore)
        {
            HighScore = Score;
            NewRecord = true;
        }
        SaveHighScore();
        SetState(GameState.GameOver);
    }

    public void AddCoin() => Coins++;

    public void NotifyPlayerHit() => PlayerHit?.Invoke();

    void SaveHighScore()
    {
        PlayerPrefs.SetInt(HighScoreKey, HighScore);
        PlayerPrefs.Save();
    }

    void OnApplicationQuit()
    {
        if (Score > HighScore) HighScore = Score;
        SaveHighScore();
    }
}
