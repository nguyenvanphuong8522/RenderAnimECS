using UnityEngine;
using Unity.Entities;
using Unity.Collections;
using Unity.Transforms;
using Unity.Mathematics;
using System.Collections.Generic;

public class GameHandler : MonoBehaviour
{
    private static GameHandler instance;
    public Material walkingSpriteSheetMaterial;
    public int AmountEntity;

    public float XRange;
    public float YRange;

    public ScriptableSpriteSheet SheetData;
    public Texture2D[] EnemyTextures;


    private void Awake()
    {
        instance = this;
        Application.targetFrameRate = 60;
    }
    private void Start()
    {

        EntityManager entityManager = World.DefaultGameObjectInjectionWorld.EntityManager;
        EntityArchetype entityArchetype = entityManager.CreateArchetype(typeof(LocalTransform), typeof(SpriteSheetAnimationData), typeof(VisibleTag), typeof(SpatialCell));

        NativeArray<Entity> entityArray = new NativeArray<Entity>(AmountEntity, Allocator.Temp);
        entityManager.CreateEntity(entityArchetype, entityArray);

        foreach (Entity entity in entityArray)
        {
            float x = UnityEngine.Random.Range(-XRange, XRange);
            float y = UnityEngine.Random.Range(-YRange, YRange);



            entityManager.SetComponentData(entity,
                new LocalTransform
                {
                    Position = new float3(x, y, 0),
                    Scale = 1
                }
            );


            int FrameCount = SheetData.FrameCount;
            float FrameTimerMax = SheetData.FrameTimerMax;
            entityManager.SetComponentData(entity,
                new SpriteSheetAnimationData
                {
                    currentFrame = UnityEngine.Random.Range(0, FrameCount),
                    frameCount = FrameCount,
                    frameTimer = 0,
                    frameTimerMax = FrameTimerMax,
                    uvWidth = (float)1f / FrameCount,
                    invFrameTimerMax = 1f / FrameTimerMax
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
