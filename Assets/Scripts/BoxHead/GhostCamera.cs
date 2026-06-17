using UnityEngine;

public class GhostCamera : MonoBehaviour
{
    [Header("Velocidad de Espectador")]
    public float speed = 15f;

    void Update()
    {
        // Leemos el input tradicional de WASD
        float h = Input.GetAxisRaw("Horizontal");
        float v = Input.GetAxisRaw("Vertical");

        // Creamos el vector de movimiento (X y Z, ignorando la altura Y)
        Vector3 move = new Vector3(h, 0f, v).normalized;

        // Movemos la cámara en el espacio del mundo para no arruinar el ángulo Top-Down
        transform.Translate(move * speed * Time.deltaTime, Space.World);
    }
}