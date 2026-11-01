using UnityEngine;

// A stopped car with its hazard lights on. It just sits in the lane.
public class StaticCar : Obstacle
{
    public Renderer[] hazardLights;
    public Color hazardColor = new Color(1f, 0.45f, 0f);
    public float blinkRate = 2.2f;

    static readonly int EmissionColor = Shader.PropertyToID("_EmissionColor");
    MaterialPropertyBlock block;
    float phase;

    public override bool IsMoving => false;

    public override void Init(int lane)
    {
        base.Init(lane);
        phase = Random.value * 10f;
    }

    protected override void Update()
    {
        base.Update();

        if (hazardLights == null || hazardLights.Length == 0) return;
        if (block == null) block = new MaterialPropertyBlock();

        bool on = Mathf.Sin((Time.time + phase) * blinkRate * Mathf.PI) > 0f;
        Color c = on ? hazardColor * 4f : Color.black;
        foreach (Renderer r in hazardLights)
        {
            r.GetPropertyBlock(block);
            block.SetColor(EmissionColor, c);
            r.SetPropertyBlock(block);
        }
    }
}
