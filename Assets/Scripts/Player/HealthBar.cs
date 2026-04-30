using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public Image fillImage;
    public Transform target;
    public Vector3 offset = new Vector3(0, 2f, 0);

    private Transform cam;

    private void Start()
    {
        cam = Camera.main.transform;
    }

    void LateUpdate()
    {
        if (target != null)
        {
            transform.position = target.position + offset;
        }

        if (cam == null) return;

        // Hace que mire EXACTAMENTE como la cámara (sin rotaciones raras)
        transform.rotation = cam.rotation;
    }

    public void SetHealth(float current, float max)
    {
        fillImage.fillAmount = current / max;
    }
}