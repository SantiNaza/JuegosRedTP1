using UnityEngine;

namespace TP.Config
{
    [CreateAssetMenu(menuName = "TP/Stats Config", fileName = "StatsConfig")]
    public sealed class StatsConfig : ScriptableObject
    {
        public int maxPerStat = 3;
        public int xpPerLevel = 100;
        [Tooltip("Puntos totales = nivel + basePoints (igual que el juego original: nivel 1 = 10 puntos).")]
        public int basePoints = 9;
        [Tooltip("Bonificación por punto, en el orden del enum StatType.")]
        public float[] perPoint = { 10f, 5f, 0.03f, 0.3f, 25f, 0.5f, 1f };
        public int waveClearXp = 10;
        public int victoryXp = 50;
    }
}
