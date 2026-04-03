using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;

public class GameHandler : MonoBehaviour
{
    private static GameHandler instance;
    public Mesh quadMesh;
    public Material walkingSpriteSheetMaterial;

    public int FrameCount;
    public float FrameTimerMax = 0.1f;
    public int AmountEntity;
    private void Awake()
    {
        instance = this;
    }
    private void Start()
    {

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype entityArchetype = entityManager.CreateArchetype(typeof(LocalTransform), typeof(SpriteSheetAnimationData));

        NativeArray<Entity> entityArray = new NativeArray<Entity>(AmountEntity, Allocator.Temp);
        entityManager.CreateEntity(entityArchetype, entityArray);

        foreach (Entity entity in entityArray)
        {
            float x = UnityEngine.Random.Range(-5f, 5f);
            float y = UnityEngine.Random.Range(-2.5f, 2.5f);



            entityManager.SetComponentData(entity,
                new LocalTransform
                {
                    Position = new float3(x, y, 0)
                }
            );



            entityManager.SetComponentData(entity,
                new SpriteSheetAnimationData
                {
                    currentFrame = UnityEngine.Random.Range(0, FrameCount),
                    frameCount = FrameCount,
                    frameTimer = 0,
                    frameTimerMax = FrameTimerMax
                }
            );
        }

        entityArray.Dispose();
    }

    public static GameHandler GetInstance()
    {
        return instance;
    }

}
