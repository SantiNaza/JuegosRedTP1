using UnityEngine;
using Photon.Pun;
using ExitGames.Client.Photon; // Librería obligatoria para el PhotonPeer

// 1. Definimos el Struct que pidió la cátedra
public struct PlayerStats
{
    public float vidaActual;
    public int cargador;
    public int nivelMejoras;
}

public class PlayerNetworkSync : MonoBehaviourPun, IPunObservable
{
    private HealthSystem healthSystem;
    private TopDownWeaponController weaponController;

    void Awake()
    {
        healthSystem = GetComponent<HealthSystem>();
        weaponController = GetComponent<TopDownWeaponController>();

        // 2. REGISTRO EN PHOTON: Le enseñamos a la red qué es un "PlayerStats"
        // Le pasamos el tipo, una letra única para identificarlo (ej: 'S'), y los métodos de traducción.
        PhotonPeer.RegisterType(typeof(PlayerStats), (byte)'S', SerializePlayerStats, DeserializePlayerStats);
    }

    // ==========================================
    // TRADUCTORES DEL STRUCT (Requisito de RegisterType)
    // ==========================================

    public static byte[] SerializePlayerStats(object customObject)
    {
        PlayerStats stats = (PlayerStats)customObject;

        // float (4 bytes) + int (4 bytes) + int (4 bytes) = 12 bytes en total
        byte[] bytes = new byte[12];

        System.BitConverter.GetBytes(stats.vidaActual).CopyTo(bytes, 0);
        System.BitConverter.GetBytes(stats.cargador).CopyTo(bytes, 4);
        System.BitConverter.GetBytes(stats.nivelMejoras).CopyTo(bytes, 8);

        return bytes;
    }

    public static object DeserializePlayerStats(byte[] data)
    {
        PlayerStats stats = new PlayerStats();

        stats.vidaActual = System.BitConverter.ToSingle(data, 0);
        stats.cargador = System.BitConverter.ToInt32(data, 4);
        stats.nivelMejoras = System.BitConverter.ToInt32(data, 8);

        return stats;
    }

    // ==========================================
    // ENVÍO Y RECEPCIÓN (La tubería de datos)
    // ==========================================

    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Creamos nuestro paquete local
            PlayerStats misStats = new PlayerStats();
            misStats.vidaActual = healthSystem.currentHealth;

            if (weaponController != null)
            {
                misStats.cargador = weaponController.cargadoresActuales;
            }

            // Acá podrías conectar tu sistema de guardado local XOR para sumar el nivel total de las armas
            misStats.nivelMejoras = 1;

            // Photon recibe el struct. 
            // Gracias al "Unreliable On Change" de tu PhotonView, ESTO ES SOLO DELTA.
            stream.SendNext(misStats);
        }
        else
        {
            // Las compus de tus compañeros reciben y aplican el struct
            PlayerStats statsRecibidos = (PlayerStats)stream.ReceiveNext();

            healthSystem.currentHealth = statsRecibidos.vidaActual;

            if (weaponController != null)
            {
                weaponController.cargadoresActuales = statsRecibidos.cargador;
            }
        }
    }
}