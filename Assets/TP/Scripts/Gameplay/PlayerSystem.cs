using System.Collections.Generic;
using UnityEngine;
using TP.Config;
using TP.Core;

namespace TP.Gameplay
{
    /// <summary>
    /// Jugador: movimiento con sprint/stamina, apuntado con el mouse, vida, knockback y animación.
    /// El GameObject del jugador es solo la vista (Rigidbody + modelo); toda la lógica vive acá.
    /// </summary>
    public sealed class PlayerSystem : IPlayerService, IUpdatable, IFixedUpdatable
    {
        private static readonly int IsWalkingHash = Animator.StringToHash("IsWalking");
        private static readonly int IsRunningHash = Animator.StringToHash("IsRunning");
        /// <summary>Duración del giro del golpe (constante: PlayerConfig ya tiene 10 campos).</summary>
        private const float MeleeSpinDuration = 0.3f;

        private readonly PlayerConfig config;
        private readonly IInputService input;
        private readonly IProgressionService progression;
        private readonly EventBus events;
        private readonly Camera camera;

        private GameObject go;
        private Transform tr;
        private Rigidbody rb;
        private Animator animator;
        private bool hasWalkingParam;
        private bool hasRunningParam;

        private float moveSpeed;
        private float maxStamina;
        private float stamina;
        private bool exhausted;
        private Vector3 moveDirection;
        private bool sprinting;
        private Quaternion desiredRotation = Quaternion.identity;
        private float knockbackTimer;
        private bool lastWalking;
        private bool lastRunning;

        // Giro del golpe: se rota el hijo del modelo (no la raíz, que sigue al mouse).
        private Transform spinTarget;
        private Quaternion spinBaseRotation;
        private float spinTimer;

        public bool IsSpawned => go != null;
        public bool IsAlive { get; private set; }
        public Vector3 Position => tr != null ? tr.position : Vector3.zero;
        public Transform Transform => tr;
        public Transform FirePoint { get; private set; }
        public Vector3 AimPoint { get; private set; }
        public float Health { get; private set; }
        public float MaxHealth { get; private set; }
        public float Stamina01 => maxStamina > 0f ? stamina / maxStamina : 0f;
        public float MoveSpeedMultiplier { private get; set; } = 1f;

        public PlayerSystem(PlayerConfig config, IInputService input, IProgressionService progression,
                            EventBus events, Camera camera)
        {
            this.config = config;
            this.input = input;
            this.progression = progression;
            this.events = events;
            this.camera = camera;
        }

        /// <summary>Nombres de los modelos elegibles dentro del prefab (para el selector del menú).</summary>
        public static List<string> GetModelNames(GameObject prefab)
        {
            var names = new List<string>();
            if (prefab == null) return names;
            foreach (Animator a in FindModels(prefab.transform)) names.Add(a.gameObject.name);
            return names;
        }

        public void Spawn(Vector3 position)
        {
            if (go == null)
            {
                go = Object.Instantiate(config.prefab, position, Quaternion.identity);
                go.name = "Player";
                tr = go.transform;
                SetLayerRecursively(go, Layers.Player);

                rb = go.GetComponent<Rigidbody>();
                if (rb == null) rb = go.AddComponent<Rigidbody>();
                rb.constraints = RigidbodyConstraints.FreezeRotation;
                rb.interpolation = RigidbodyInterpolation.Interpolate;
                rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                SelectModel(progression.SelectedModel);
                FirePoint = FindOrCreateFirePoint(tr);
            }

            rb.isKinematic = false;
            tr.SetPositionAndRotation(position, Quaternion.identity);
            desiredRotation = Quaternion.identity;

            StopMeleeSpin();
            moveSpeed = config.moveSpeed + progression.GetBonus(StatType.MoveSpeed);
            maxStamina = config.maxStamina + progression.GetBonus(StatType.Stamina);
            MaxHealth = config.maxHealth + progression.GetBonus(StatType.MaxHealth);
            Health = MaxHealth;
            stamina = maxStamina;
            exhausted = false;
            IsAlive = true;
            MoveSpeedMultiplier = 1f;

            events.Publish(new PlayerHealthChangedEvent { Current = Health, Max = MaxHealth });
        }

        // ------------------------------ Ticks ------------------------------

        public void Tick(float deltaTime)
        {
            if (!IsAlive || go == null) return;

            Vector2 move = input.Move;
            moveDirection = new Vector3(move.x, 0f, move.y);
            bool moving = moveDirection.sqrMagnitude > 0.0001f;

            UpdateStamina(deltaTime, moving);
            UpdateAim();
            UpdateAnimation(moving);
            UpdateMeleeSpin(deltaTime);
        }

        public void FixedTick(float fixedDeltaTime)
        {
            if (!IsAlive || rb == null) return;

            rb.MoveRotation(desiredRotation);

            if (knockbackTimer > 0f)
            {
                knockbackTimer -= fixedDeltaTime;
                return;
            }

            float speed = moveSpeed * MoveSpeedMultiplier * (sprinting ? config.sprintMultiplier : 1f);
            Vector3 velocity = moveDirection * speed;
            velocity.y = rb.velocity.y;
            rb.velocity = velocity;
        }

        // ------------------------------ Vida ------------------------------

        public void TakeDamage(float amount)
        {
            if (!IsAlive || amount <= 0f) return;

            Health = Mathf.Max(0f, Health - amount);
            events.Publish(new PlayerHealthChangedEvent { Current = Health, Max = MaxHealth });

            if (Health <= 0f) Die();
        }

        public void Heal(float amount)
        {
            if (!IsAlive || amount <= 0f) return;
            Health = Mathf.Min(MaxHealth, Health + amount);
            events.Publish(new PlayerHealthChangedEvent { Current = Health, Max = MaxHealth });
        }

        public void ApplyKnockback(Vector3 impulse)
        {
            if (!IsAlive || rb == null) return;
            knockbackTimer = 0.2f;
            rb.velocity = Vector3.zero;
            rb.AddForce(impulse, ForceMode.Impulse);
        }

        // ------------------------------ Golpe ------------------------------

        public void PlayMeleeSpin()
        {
            if (!IsAlive || spinTarget == null) return;
            // Si ya estaba girando, se reinicia desde la pose original.
            spinTarget.localRotation = spinBaseRotation;
            spinTimer = MeleeSpinDuration;
        }

        private void UpdateMeleeSpin(float deltaTime)
        {
            if (spinTimer <= 0f) return;

            spinTimer -= deltaTime;
            if (spinTimer <= 0f)
            {
                StopMeleeSpin();
                return;
            }

            float t = 1f - spinTimer / MeleeSpinDuration;
            float inv = 1f - t;
            float eased = 1f - inv * inv * inv; // ease-out cúbico: arranca rápido y frena al final
            spinTarget.localRotation = spinBaseRotation * Quaternion.Euler(0f, 360f * eased, 0f);
        }

        private void StopMeleeSpin()
        {
            spinTimer = 0f;
            if (spinTarget != null) spinTarget.localRotation = spinBaseRotation;
        }

        private void Die()
        {
            StopMeleeSpin();
            IsAlive = false;
            rb.velocity = Vector3.zero;
            rb.isKinematic = true;
            // Igual que en el juego original: el personaje queda "acostado".
            tr.rotation = Quaternion.Euler(90f, tr.eulerAngles.y, 0f);
            SetAnimation(false, false);
            events.Publish(new PlayerDiedEvent { Position = tr.position });
        }

        // ------------------------------ Internos ------------------------------

        private void UpdateStamina(float deltaTime, bool moving)
        {
            bool wantsSprint = input.SprintHeld && moving && MoveSpeedMultiplier >= 1f;
            sprinting = wantsSprint && !exhausted;

            if (sprinting)
            {
                stamina -= deltaTime;
                if (stamina <= 0f)
                {
                    stamina = 0f;
                    exhausted = true;
                    sprinting = false;
                    events.Publish(new HudMessageEvent { Text = "¡SIN AIRE!", Color = Color.cyan, Duration = 1f });
                }
            }
            else if (stamina < maxStamina)
            {
                stamina = Mathf.Min(maxStamina, stamina + deltaTime);
                // Igual que el original: si te agotás, tenés que recuperar todo el aire para volver a correr.
                if (exhausted && stamina >= maxStamina) exhausted = false;
            }
        }

        private void UpdateAim()
        {
            if (camera == null) return;

            float aimHeight = FirePoint != null ? FirePoint.position.y : tr.position.y + 1f;
            var plane = new Plane(Vector3.up, new Vector3(0f, aimHeight, 0f));
            Ray ray = camera.ScreenPointToRay(input.PointerScreenPosition);
            if (!plane.Raycast(ray, out float distance)) return;

            Vector3 point = ray.GetPoint(distance);
            AimPoint = point;

            Vector3 flat = point - tr.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f) desiredRotation = Quaternion.LookRotation(flat);
        }

        private void UpdateAnimation(bool moving)
        {
            // Solo tocamos el Animator cuando el estado cambia (no cada frame).
            if (moving == lastWalking && sprinting == lastRunning) return;
            SetAnimation(moving, sprinting);
        }

        private void SetAnimation(bool walking, bool running)
        {
            lastWalking = walking;
            lastRunning = running;
            if (animator == null) return;
            if (hasWalkingParam) animator.SetBool(IsWalkingHash, walking);
            if (hasRunningParam) animator.SetBool(IsRunningHash, running);
        }

        private void SelectModel(int index)
        {
            List<Animator> models = FindModels(tr);
            if (models.Count == 0)
            {
                animator = go.GetComponentInChildren<Animator>();
            }
            else
            {
                index = Mathf.Clamp(index, 0, models.Count - 1);
                for (int i = 0; i < models.Count; i++) models[i].gameObject.SetActive(i == index);
                animator = models[index];
            }

            spinTarget = animator != null && animator.transform != tr ? animator.transform : null;
            if (spinTarget != null) spinBaseRotation = spinTarget.localRotation;

            if (animator == null) return;
            animator.applyRootMotion = false; // el movimiento lo hace el Rigidbody, no la animación
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            foreach (AnimatorControllerParameter p in animator.parameters)
            {
                if (p.nameHash == IsWalkingHash) hasWalkingParam = true;
                if (p.nameHash == IsRunningHash) hasRunningParam = true;
            }
        }

        /// <summary>
        /// Cada hijo con Animator (que no sea la raíz) y con alguna malla debajo se considera un modelo elegible.
        /// Los "Missing Prefab" del prefab original tienen Animator pero no malla, así que se ignoran.
        /// </summary>
        private static List<Animator> FindModels(Transform root)
        {
            var result = new List<Animator>();
            foreach (Animator a in root.GetComponentsInChildren<Animator>(true))
            {
                if (a.transform != root && a.GetComponentInChildren<Renderer>(true) != null) result.Add(a);
            }
            return result;
        }

        private static Transform FindOrCreateFirePoint(Transform root)
        {
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name.ToLowerInvariant().Contains("firepoint")) return t;
            }

            var firePoint = new GameObject("FirePoint").transform;
            firePoint.SetParent(root, false);
            firePoint.localPosition = new Vector3(0f, 1.2f, 0.7f);
            return firePoint;
        }

        private static void SetLayerRecursively(GameObject obj, int layer)
        {
            obj.layer = layer;
            foreach (Transform child in obj.transform) SetLayerRecursively(child.gameObject, layer);
        }
    }
}
