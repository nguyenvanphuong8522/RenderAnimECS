using UnityEngine;

[CreateAssetMenu(fileName = "SpriteSheetData", menuName = "GameData/SpriteSheetData")]
public class ScriptableSpriteSheet : ScriptableObject
{
    public int FrameCount;
    public Texture2D Texture;
    public float FrameTimerMax;
}
