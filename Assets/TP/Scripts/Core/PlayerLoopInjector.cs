using System;
using UnityEngine;
using UnityEngine.LowLevel;

namespace TP.Core
{
    /// <summary>
    /// Inyecta el UpdateManager directamente en el PlayerLoop de Unity.
    /// Así el proyecto no necesita ningún Update()/FixedUpdate()/LateUpdate() en MonoBehaviours:
    /// Unity llama a estos tres sistemas propios una vez por frame (o por paso de física).
    /// </summary>
    public sealed class PlayerLoopInjector : IDisposable
    {
        // Tipos "marcador" para identificar nuestros sistemas dentro del PlayerLoop.
        private struct TPUpdate { }
        private struct TPFixedUpdate { }
        private struct TPLateUpdate { }

        private readonly UpdateManager manager;
        private bool injected;

        public PlayerLoopInjector(UpdateManager manager)
        {
            this.manager = manager;
        }

        public void Inject()
        {
            if (injected) return;

            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            // Por si quedó algo de una ejecución anterior (recarga de escena), primero limpiamos.
            RemoveOurSystems(ref root, null);

            AppendTo(ref root, typeof(UnityEngine.PlayerLoop.Update), new PlayerLoopSystem
            {
                type = typeof(TPUpdate),
                updateDelegate = OnUpdate
            });
            AppendTo(ref root, typeof(UnityEngine.PlayerLoop.FixedUpdate), new PlayerLoopSystem
            {
                type = typeof(TPFixedUpdate),
                updateDelegate = OnFixedUpdate
            });
            AppendTo(ref root, typeof(UnityEngine.PlayerLoop.PreLateUpdate), new PlayerLoopSystem
            {
                type = typeof(TPLateUpdate),
                updateDelegate = OnLateUpdate
            });

            PlayerLoop.SetPlayerLoop(root);
            injected = true;
        }

        public void Dispose()
        {
            if (!injected) return;
            PlayerLoopSystem root = PlayerLoop.GetCurrentPlayerLoop();
            // Solo quitamos los sistemas de ESTE injector (si ya hay otro Bootstrap activo, no lo tocamos).
            RemoveOurSystems(ref root, this);
            PlayerLoop.SetPlayerLoop(root);
            injected = false;
        }

        private void OnUpdate() => manager.RunUpdate(Time.deltaTime, Time.unscaledDeltaTime);
        private void OnFixedUpdate() => manager.RunFixedUpdate(Time.fixedDeltaTime);
        private void OnLateUpdate() => manager.RunLateUpdate(Time.deltaTime, Time.unscaledDeltaTime);

        private static void AppendTo(ref PlayerLoopSystem root, Type phase, PlayerLoopSystem system)
        {
            PlayerLoopSystem[] phases = root.subSystemList;
            for (int i = 0; i < phases.Length; i++)
            {
                if (phases[i].type != phase) continue;

                PlayerLoopSystem[] current = phases[i].subSystemList ?? Array.Empty<PlayerLoopSystem>();
                var extended = new PlayerLoopSystem[current.Length + 1];
                Array.Copy(current, extended, current.Length);
                extended[current.Length] = system;
                phases[i].subSystemList = extended;
                return;
            }
        }

        /// <param name="owner">null = quitar todos los sistemas TP; si no, solo los creados por ese injector.</param>
        private static void RemoveOurSystems(ref PlayerLoopSystem root, PlayerLoopInjector owner)
        {
            PlayerLoopSystem[] phases = root.subSystemList;
            if (phases == null) return;

            for (int i = 0; i < phases.Length; i++)
            {
                PlayerLoopSystem[] subs = phases[i].subSystemList;
                if (subs == null) continue;

                int keep = 0;
                for (int j = 0; j < subs.Length; j++)
                    if (!ShouldRemove(subs[j], owner)) keep++;
                if (keep == subs.Length) continue;

                var filtered = new PlayerLoopSystem[keep];
                int k = 0;
                for (int j = 0; j < subs.Length; j++)
                    if (!ShouldRemove(subs[j], owner)) filtered[k++] = subs[j];
                phases[i].subSystemList = filtered;
            }
        }

        private static bool ShouldRemove(PlayerLoopSystem system, PlayerLoopInjector owner)
        {
            if (!IsOurs(system.type)) return false;
            if (owner == null) return true;
            return system.updateDelegate != null && ReferenceEquals(system.updateDelegate.Target, owner);
        }

        private static bool IsOurs(Type t)
            => t == typeof(TPUpdate) || t == typeof(TPFixedUpdate) || t == typeof(TPLateUpdate);
    }
}
