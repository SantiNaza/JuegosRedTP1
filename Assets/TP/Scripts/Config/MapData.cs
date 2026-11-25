using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace TP.Config
{
    /// <summary>Datos del mapa generados por el editor a partir de la textura (mismos colores que el LevelGenerator original).</summary>
    [CreateAssetMenu(menuName = "TP/Map Data", fileName = "MapData")]
    public sealed class MapData : ScriptableObject
    {
        public Vector3 playerSpawn;
        public List<Vector3> enemySpawns = new List<Vector3>();
        public List<Vector3> ammoSpawns = new List<Vector3>();
        public List<Vector3> healthSpawns = new List<Vector3>();
        public List<Vector3> grenadeSpawns = new List<Vector3>();
        [Tooltip("NavMesh horneado en el editor (agentes chicos para pasillos angostos).")]
        public NavMeshData navMesh;
    }
}
