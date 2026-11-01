using UnityEngine;

// One piece of road. Its pivot is at the start (near end) of the piece.
// Every time it is taken from the pool the buildings on the sides are randomised.
public class RoadSegment : MonoBehaviour
{
    public const float Length = 24f;

    public Transform[] leftBuildings;
    public Transform[] rightBuildings;
    public Renderer[] buildingRenderers;
    public Vector2 windowTileSize = new Vector2(12f, 12f);

    static readonly int BaseMapST = Shader.PropertyToID("_BaseMap_ST");
    MaterialPropertyBlock block;

    void OnEnable()
    {
        Randomize();
    }

    void Randomize()
    {
        if (block == null) block = new MaterialPropertyBlock();

        Place(leftBuildings, -1f, 0);
        Place(rightBuildings, 1f, leftBuildings.Length);
    }

    void Place(Transform[] buildings, float side, int rendererOffset)
    {
        float slot = Length / buildings.Length;
        for (int i = 0; i < buildings.Length; i++)
        {
            float width = Random.Range(8f, slot + 4f);
            float depth = Random.Range(8f, 14f);
            float height = Random.value < 0.15f ? Random.Range(34f, 50f) : Random.Range(8f, 26f);
            float x = side * (17f + depth * 0.5f + Random.Range(0f, 14f));
            float z = slot * (i + 0.5f) + Random.Range(-1.5f, 1.5f);

            Transform b = buildings[i];
            b.localPosition = new Vector3(x, height * 0.5f - 0.3f, z);
            b.localScale = new Vector3(depth, height, width);

            Renderer r = buildingRenderers[rendererOffset + i];
            r.GetPropertyBlock(block);
            block.SetVector(BaseMapST, new Vector4(width / windowTileSize.x, height / windowTileSize.y, Random.value, 0f));
            r.SetPropertyBlock(block);
        }
    }
}
