using UnityEngine;
using UnityEngine.EventSystems;

public class SingleEventSystem : MonoBehaviour
{
    private void Awake()
    {
        if (FindObjectsOfType<EventSystem>().Length > 1)
            Destroy(gameObject);
    }
}