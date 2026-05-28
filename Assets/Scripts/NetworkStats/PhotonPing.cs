using System.Collections;
using System.Collections.Generic;
using Photon.Pun;
using UnityEngine;

public class PhotonPing : MonoBehaviour
{
    
    void Start()
    {
        //PhotonManager.Instance.OnRoom += PingPhoton;

    }

    [ContextMenu("PingPhoton")]
    private void PingPhoton()
    {
        int currentPing = PhotonNetwork.GetPing();
        Debug.Log("Ping Photon:" + currentPing);
    }

}
