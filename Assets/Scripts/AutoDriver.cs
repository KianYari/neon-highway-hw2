using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

// Test helper. Only runs when the game is started with the environment variable NEON_AUTODRIVE set.
// It drives the car with a simple AI, saves screenshots to NEON_SHOTS and prints some stats.
// With NEON_AUTODRIVE=ui it instead pushes fake keyboard and mouse events through the Input System
// to check that the buttons and the key bindings work.
public class AutoDriver : MonoBehaviour
{
    GameManager gm;
    string shotDir;
    bool steering = true;
    float playSeconds;
    bool uiTest;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Init()
    {
        string flag = System.Environment.GetEnvironmentVariable("NEON_AUTODRIVE");
        if (string.IsNullOrEmpty(flag)) return;

        GameObject go = new GameObject("AutoDriver");
        DontDestroyOnLoad(go);
        AutoDriver d = go.AddComponent<AutoDriver>();
        d.uiTest = flag == "ui";
        float.TryParse(flag, out d.playSeconds);
        d.shotDir = System.Environment.GetEnvironmentVariable("NEON_SHOTS");
        if (!string.IsNullOrEmpty(d.shotDir)) Directory.CreateDirectory(d.shotDir);
    }

    void Start()
    {
        gm = GameManager.Instance;
        StartCoroutine(uiTest ? UiTest() : Run());
    }

    IEnumerator Click(string buttonName)
    {
        GameObject go = GameObject.Find(buttonName);
        if (go == null) { Debug.Log("[UITEST] missing button " + buttonName); yield break; }

        Vector3[] corners = new Vector3[4];
        ((RectTransform)go.transform).GetWorldCorners(corners);
        Vector2 pos = (corners[0] + corners[2]) * 0.5f;

        Mouse mouse = Mouse.current;
        InputSystem.QueueStateEvent(mouse, new MouseState { position = pos });
        yield return new WaitForSecondsRealtime(0.15f);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = pos }.WithButton(MouseButton.Left));
        yield return new WaitForSecondsRealtime(0.1f);
        InputSystem.QueueStateEvent(mouse, new MouseState { position = pos });
        yield return new WaitForSecondsRealtime(0.3f);
    }

    IEnumerator Press(Key key)
    {
        Keyboard kb = Keyboard.current;
        InputSystem.QueueStateEvent(kb, new KeyboardState(key));
        yield return new WaitForSecondsRealtime(0.1f);
        InputSystem.QueueStateEvent(kb, new KeyboardState());
        yield return new WaitForSecondsRealtime(0.4f);
    }

    int failures;

    void Check(string what, bool ok)
    {
        if (!ok) failures++;
        Debug.Log("[UITEST] " + (ok ? "ok   " : "FAIL ") + what);
    }

    IEnumerator UiTest()
    {
        steering = false;
        yield return new WaitForSecondsRealtime(2f);
        Check("starts in menu", gm.State == GameState.Menu);

        yield return Click("StartButton");
        Check("Start button starts the game", gm.State == GameState.Playing);

        yield return Press(Key.A);
        Check("A moves to the left lane", gm.player.Lane == 0);
        yield return Press(Key.LeftArrow);
        Check("left arrow stops at the edge", gm.player.Lane == 0);
        yield return Press(Key.D);
        Check("D moves to the middle lane (lane " + gm.player.Lane + ")", gm.player.Lane == 1);
        yield return Press(Key.RightArrow);
        Check("right arrow moves to the right lane (lane " + gm.player.Lane + ")", gm.player.Lane == 2);

        yield return Press(Key.Escape);
        Check("Escape pauses", gm.State == GameState.Paused && Time.timeScale == 0f);
        yield return Click("ResumeButton");
        Check("Resume button resumes", gm.State == GameState.Playing && Time.timeScale == 1f);

        yield return new WaitForSeconds(3f);
        yield return Press(Key.P);
        Check("P pauses", gm.State == GameState.Paused);
        float distBefore = gm.Distance;
        yield return Click("RestartButton");
        Check("Restart button restarts", gm.State == GameState.Playing && gm.Distance < distBefore);

        yield return Press(Key.Escape);
        yield return Click("MenuButton");
        Check("Main menu button goes to the menu", gm.State == GameState.Menu);

        yield return Press(Key.Enter);
        Check("Enter starts from the menu", gm.State == GameState.Playing);

        Debug.Log("[UITEST] done, failures=" + failures);
        Application.Quit();
    }

    IEnumerator Shot(string name)
    {
        yield return new WaitForEndOfFrame();
        if (string.IsNullOrEmpty(shotDir)) yield break;
        ScreenCapture.CaptureScreenshot(Path.Combine(shotDir, name + ".png"));
        yield return null;
    }

    IEnumerator Run()
    {
        yield return new WaitForSecondsRealtime(2.5f);
        yield return Shot("1_main_menu");

        gm.StartGame();
        Log("started");
        yield return new WaitForSeconds(9f);
        yield return Shot("2_gameplay");
        yield return new WaitForSeconds(6f);
        yield return Shot("3_gameplay_b");
        for (int i = 0; i < 4; i++)
        {
            yield return new WaitForSeconds(0.7f);
            yield return Shot("3_sky_" + i);
        }

        gm.Pause();
        yield return new WaitForSecondsRealtime(0.4f);
        yield return Shot("4_pause");
        gm.Resume();

        float end = Time.time + playSeconds;
        float nextLog = Time.time + 5f;
        while (Time.time < end && gm.State == GameState.Playing)
        {
            if (Time.time > nextLog)
            {
                nextLog = Time.time + 5f;
                Log("playing");
            }
            yield return null;
        }
        Log("autopilot done");

        // Now stop steering and let the car crash into things.
        steering = false;
        bool shotHit = false;
        int lives = gm.player.Lives;
        float timeout = Time.time + 60f;
        while (gm.State == GameState.Playing && Time.time < timeout)
        {
            if (!shotHit && gm.player.Lives < lives)
            {
                shotHit = true;
                yield return new WaitForSeconds(0.1f);
                yield return Shot("5_hit");
            }
            yield return null;
        }

        yield return new WaitForSecondsRealtime(1.5f);
        yield return Shot("6_game_over");
        Log("game over state=" + gm.State);
        Application.Quit();
    }

    void Log(string tag)
    {
        Debug.Log("[AutoDriver] " + tag + " t=" + Time.time.ToString("F1") + " state=" + gm.State + " level=" + gm.Level
                  + " speed=" + gm.WorldSpeed.ToString("F1") + " dist=" + gm.Distance.ToString("F0") + " score=" + gm.Score
                  + " coins=" + gm.Coins + " lives=" + gm.player.Lives + " fuel=" + gm.player.Fuel.ToString("F0")
                  + " active=" + Interactable.Active.Count
                  + " fps=" + (1f / Mathf.Max(Time.smoothDeltaTime, 0.0001f)).ToString("F0"));
    }

    float nextThink;

    void Update()
    {
        if (!steering || gm.State != GameState.Playing || Time.time < nextThink) return;
        nextThink = Time.time + 0.08f;

        PlayerCar player = gm.player;
        float[] value = new float[GameManager.LaneCount];
        for (int lane = 0; lane < value.Length; lane++) value[lane] = 80f;

        foreach (Interactable i in Interactable.Active)
        {
            float z = i.transform.position.z;
            if (z < -3f || z > 80f) continue;

            if (i is Obstacle) value[i.Lane] = Mathf.Min(value[i.Lane], z);
            else if (i is FuelCan && player.Fuel < 70f) value[i.Lane] += 40f;
            else if (i is Coin) value[i.Lane] += 1.5f;
        }
        value[player.Lane] += 6f;

        int best = player.Lane;
        for (int lane = 0; lane < value.Length; lane++)
            if (value[lane] > value[best]) best = lane;

        if (best == player.Lane) return;
        int next = player.Lane + (best > player.Lane ? 1 : -1);

        // Only move if the neighbouring lane is clear close to the car.
        bool clear = true;
        foreach (Interactable i in Interactable.Active)
        {
            if (!(i is Obstacle) || i.Lane != next) continue;
            float z = i.transform.position.z;
            if (z > -4f && z < 14f) clear = false;
        }
        if (clear) player.SetLane(next);
    }
}
