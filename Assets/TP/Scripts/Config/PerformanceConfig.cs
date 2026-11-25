using UnityEngine;

namespace TP.Config
{
    [CreateAssetMenu(menuName = "TP/Performance Config", fileName = "PerformanceConfig")]
    public sealed class PerformanceConfig : ScriptableObject
    {
        [Tooltip("Desactivar SOLO para medir la comparación contra Instantiate/Destroy constante.")]
        public bool usePooling = true;
        public int enemyPrewarmPerType = 12;
        public int bulletPrewarm = 40;
        [Tooltip("Cada cuánto recalculan el camino los enemigos (segundos). Menos = más CPU.")]
        public float enemyRepathInterval = 0.25f;
        [Tooltip("-1 = sin límite (recomendado para medir con el Profiler).")]
        public int targetFrameRate = -1;
        public bool showFpsCounter = true;
    }
}
