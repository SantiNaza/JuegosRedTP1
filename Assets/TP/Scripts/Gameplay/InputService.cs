using UnityEngine;
using TP.Core;

namespace TP.Gameplay
{
    /// <summary>
    /// Lee el Input una sola vez por frame (orden -100, no pausable) y lo deja cacheado.
    /// El resto de los sistemas consulta IInputService en lugar de llamar a Input directamente.
    /// </summary>
    public sealed class InputService : IInputService, IUpdatable
    {
        public Vector2 Move { get; private set; }
        public Vector3 PointerScreenPosition { get; private set; }
        public bool FireHeld { get; private set; }
        public bool ReloadPressed { get; private set; }
        public bool MeleePressed { get; private set; }
        public bool GrenadePressed { get; private set; }
        public bool GrenadeReleased { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool PausePressed { get; private set; }

        public void Tick(float deltaTime)
        {
            Vector2 move = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            Move = move.sqrMagnitude > 1f ? move.normalized : move;

            PointerScreenPosition = Input.mousePosition;
            FireHeld = Input.GetMouseButton(0);
            MeleePressed = Input.GetMouseButtonDown(1);
            ReloadPressed = Input.GetKeyDown(KeyCode.R);
            GrenadePressed = Input.GetKeyDown(KeyCode.G);
            GrenadeReleased = Input.GetKeyUp(KeyCode.G);
            SprintHeld = Input.GetKey(KeyCode.LeftShift);
            PausePressed = Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.P);
        }
    }
}
