using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

public class LevelGenerator : MonoBehaviour
{
    [Header("Mapa")]
    public Texture2D mapTexture;
    public float pixelOffset = 1f;   // Tamaño de cada píxel en el mundo
    public float yOffset = 0.5f;     // Levanta todo el mapa en Y para que no quede enterrado

    [Header("Prefabs de terreno")]
    public GameObject wallPrefab;    // Negro
    public GameObject floorPrefab;   // Blanco / debajo de tiles caminables

    [Header("Prefabs de elementos (opcionales, pueden ir null)")]
    public GameObject doorPrefab;          // Verde
    public GameObject extractionPrefab;    // Amarillo
    public GameObject reviveZonePrefab;    // Gris (100,100,100)

    [Header("Marcadores de spawn (opcionales)")]
    public GameObject playerSpawnMarker;   // Azul
    public GameObject enemySpawnMarker;    // Rojo
    public GameObject ammoSpawnMarker;     // Cyan
    public GameObject healSpawnMarker;     // Magenta

    [Header("Posiciones registradas (read-only en runtime)")]
    public List<Vector3> playerSpawns = new List<Vector3>();
    public List<Vector3> enemySpawns = new List<Vector3>();
    public List<Vector3> ammoSpawns = new List<Vector3>();
    public List<Vector3> healSpawns = new List<Vector3>();
    public List<Vector3> doorPositions = new List<Vector3>();
    public List<Vector3> extractionPositions = new List<Vector3>();

    [Header("NavMesh (bake por API)")]
    public int agentTypeID = 0; // 0 = Humanoid (el agente por defecto)
    public float boundsPadding = 4f;

    private NavMeshData navMeshData;
    private readonly List<NavMeshBuildSource> navSources = new List<NavMeshBuildSource>();

    // Gris RGB(100,100,100) en espacio 0..1
    private static readonly Color Gray = new Color(100f / 255f, 100f / 255f, 100f / 255f);

    private void Start()
    {
        GenerateLevel();
    }

    public void GenerateLevel()
    {
        if (mapTexture == null) return;

        for (int x = 0; x < mapTexture.width; x++)
        {
            for (int y = 0; y < mapTexture.height; y++)
            {
                Color pixelColor = mapTexture.GetPixel(x, y);
                if (pixelColor.a < 0.1f) continue;

                Vector3 position = new Vector3(x * pixelOffset, yOffset, y * pixelOffset);
                ProcessPixel(pixelColor, position);
            }
        }

        // Horneamos el NavMesh con el mapa ya construido
        BakeNavMesh();
    }

    private void BakeNavMesh()
    {
        // 1. Área que abarca todo el mapa
        Bounds bounds = new Bounds(transform.position, Vector3.zero);
        Renderer[] renderers = GetComponentsInChildren<Renderer>();

        if (renderers.Length == 0)
        {
            Debug.LogWarning("No hay tiles para hornear el NavMesh.");
            return;
        }

        foreach (Renderer r in renderers)
            bounds.Encapsulate(r.bounds);

        bounds.Expand(boundsPadding);

        // 2. Recolectamos la geometría de los tiles
        navSources.Clear();
        List<NavMeshBuildMarkup> markups = new List<NavMeshBuildMarkup>();

        NavMeshBuilder.CollectSources(
            transform,
            ~0,
            NavMeshCollectGeometry.RenderMeshes,
            0,
            markups,
            navSources
        );

        // 3. Configuración del agente  <<<<<< ACÁ van los ajustes del voxel/radio
        NavMeshBuildSettings settings = NavMesh.GetSettingsByID(agentTypeID);

        settings.agentRadius = 0.15f;   // agente chico para pasajes angostos
        settings.agentHeight = 1.8f;
        settings.agentClimb = 0.6f;     // mayor que el escalón del yOffset (0.5)
        settings.agentSlope = 45f;
        settings.voxelSize = 0.05f;     // <<<<<< ESTE es el voxel size
        settings.overrideVoxelSize = true;

        // 4. Construcción
        navMeshData = NavMeshBuilder.BuildNavMeshData(
            settings,
            navSources,
            bounds,
            Vector3.zero,
            Quaternion.identity
        );

        // 5. Limpiamos lo anterior y aplicamos
        NavMesh.RemoveAllNavMeshData();
        NavMesh.AddNavMeshData(navMeshData);

        Debug.Log("NavMesh horneado por API en runtime. Fuentes: " + navSources.Count);
    }

    void ProcessPixel(Color color, Vector3 pos)
    {
        // --- NEGRO: pared ---
        if (ColorMatch(color, Color.black))
        {
            SpawnTile(wallPrefab, pos, false);
            return;
        }

        // --- BLANCO: espacio libre (solo piso) ---
        if (ColorMatch(color, Color.white))
        {
            SpawnTile(floorPrefab, pos, true);
            return;
        }

        // El resto son tiles caminables: primero piso debajo...
        SpawnTile(floorPrefab, pos, true);

        // ...y luego el elemento/marcador encima.
        if (ColorMatch(color, Color.blue))            // Azul: spawn jugadores
        {
            playerSpawns.Add(pos);
            SpawnTile(playerSpawnMarker, pos, false);
        }
        else if (ColorMatch(color, Color.red))        // Rojo: spawn enemigos
        {
            enemySpawns.Add(pos);
            SpawnTile(enemySpawnMarker, pos, false);
        }
        else if (ColorMatch(color, Color.green))      // Verde: puerta
        {
            doorPositions.Add(pos);
            SpawnTile(doorPrefab, pos, false);
        }
        else if (ColorMatch(color, Color.yellow))     // Amarillo: extracción
        {
            extractionPositions.Add(pos);
            SpawnTile(extractionPrefab, pos, false);
        }
        else if (ColorMatch(color, Color.cyan))       // Cyan: spawn munición
        {
            ammoSpawns.Add(pos);
            SpawnTile(ammoSpawnMarker, pos, false);
        }
        else if (ColorMatch(color, Color.magenta))    // Magenta: spawn curación
        {
            healSpawns.Add(pos);
            SpawnTile(healSpawnMarker, pos, false);
        }
        else if (ColorMatch(color, Gray))             // Gris: zona de revivir
        {
            SpawnTile(reviveZonePrefab, pos, false);
        }
    }

    // Instancia un prefab y lo escala al tamaño del pixel.
    void SpawnTile(GameObject prefab, Vector3 pos, bool isFloor)
    {
        if (prefab == null) return;

        // El piso IGNORA el yOffset y queda a nivel del suelo (apenas debajo de 0
        // para evitar z-fighting). Todo lo demás conserva el yOffset que trae pos.
        Vector3 finalPos = isFloor ? new Vector3(pos.x, -0.01f, pos.z) : pos;

        GameObject instance = Instantiate(prefab, finalPos, Quaternion.identity, transform);
        AdjustScale(instance, pixelOffset);
    }

    void AdjustScale(GameObject obj, float targetSize)
    {
        Renderer renderer = obj.GetComponentInChildren<Renderer>();
        if (renderer != null)
        {
            Vector3 currentSize = renderer.bounds.size;

            // Evitar división por cero si el bounds es plano en algún eje
            float scaleX = currentSize.x > 0.0001f
                ? (targetSize / currentSize.x) * obj.transform.localScale.x
                : obj.transform.localScale.x;
            float scaleZ = currentSize.z > 0.0001f
                ? (targetSize / currentSize.z) * obj.transform.localScale.z
                : obj.transform.localScale.z;

            obj.transform.localScale = new Vector3(scaleX, obj.transform.localScale.y, scaleZ);
        }
    }

    bool ColorMatch(Color a, Color b)
    {
        // Comparamos solo RGB (ignoramos alpha)
        return Mathf.Abs(a.r - b.r) < 0.1f
            && Mathf.Abs(a.g - b.g) < 0.1f
            && Mathf.Abs(a.b - b.b) < 0.1f;
    }

#if UNITY_EDITOR
    [ContextMenu("Generar Mapa (Editor)")]
    public void GenerateInEditor()
    {
        // Limpiar lo anterior
        for (int i = transform.childCount - 1; i >= 0; i--)
            DestroyImmediate(transform.GetChild(i).gameObject);

        playerSpawns.Clear();
        enemySpawns.Clear();
        ammoSpawns.Clear();
        healSpawns.Clear();
        doorPositions.Clear();
        extractionPositions.Clear();

        GenerateLevel();
    }
#endif
}