using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    public Image fillImage;
    public Transform target;
    public Vector3 offset = new Vector3(0, 1.2f, 0);

    void Update()
    {
        if (target != null)
        {
            transform.position = target.position + offset;
        }

        transform.forward = Camera.main.transform.forward;
    }

    public void SetHealth(float current, float max)
    {
        fillImage.fillAmount = current / max;
    }
}