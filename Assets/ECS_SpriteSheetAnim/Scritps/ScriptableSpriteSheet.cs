using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "SpriteSheetData", menuName = "GameData/SpriteSheetData")]
public class ScriptableSpriteSheet : ScriptableObject
{
    public List<EnemyAnimConfig> enemyAnimConfigs;
}
[Serializable]
public class EnemyAnimConfig
{
    public Texture2D texture;
    public Sprite[] sprites;
    public float frameTimerMax;
}