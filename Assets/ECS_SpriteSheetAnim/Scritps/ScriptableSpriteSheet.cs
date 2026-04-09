using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

[CreateAssetMenu(fileName = "SpriteSheetData", menuName = "GameData/SpriteSheetData")]
public class ScriptableSpriteSheet : ScriptableObject
{
    public List<EnemyAnimConfig> enemyAnimConfigs;
}

// --- DÀNH CHO CẤU HÌNH TRÊN INSPECTOR ---
[Serializable]
public class AnimSequence
{
    public string animName; // Ví dụ: "Idle", "Run"
    public float frameTimerMax = 0.1f; // Tốc độ chạy của Anim này
    public Sprite[] sprites;
}

[Serializable]
public class EnemyAnimConfig
{
    public int atlasIndex;
    public AnimSequence[] sequences; // Thay vì 1 mảng Sprite, giờ là mảng các Sequence
}

public struct SequenceBlob
{
    public int frameCount;
    public float frameTimerMax;
    public BlobArray<float4> uvs;
    public BlobArray<float2> sizes;
}

public struct EnemyAnimationsBlob
{
    public BlobArray<SequenceBlob> sequences;
}

