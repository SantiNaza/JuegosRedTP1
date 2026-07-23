using UnityEngine;
using Photon.Pun;

// NOTA: el color ya NO se aplica al modelo del personaje.
// Los personajes son unicos, asi que no hace falta teñirlos.
// El color se usa SOLO para los nombres (HUDManager y MatchManager lo leen
// directo de la CustomProperty "color"). Este componente quedo inerte a
// proposito para no romper la referencia del prefab; podes quitarlo si querés.
[RequireComponent(typeof(PhotonView))]
public class PlayerColor : MonoBehaviourPun
{
    // Sin logica: intencionalmente NO tiñe el modelo.
}
