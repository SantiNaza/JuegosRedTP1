using System;
using System.Collections.Generic;

namespace TP.Core
{
    /// <summary>
    /// Bus de eventos tipado para comunicar sistemas sin referencias directas
    /// (ej: EnemySystem publica EnemyKilledEvent y la UI / WaveSystem / Progresión lo escuchan).
    /// Los eventos son structs: publicar no genera basura (GC).
    /// </summary>
    public sealed class EventBus
    {
        private readonly Dictionary<Type, Delegate> handlers = new Dictionary<Type, Delegate>(32);

        public void Subscribe<T>(Action<T> handler) where T : struct
        {
            Type key = typeof(T);
            handlers.TryGetValue(key, out Delegate existing);
            handlers[key] = Delegate.Combine(existing, handler);
        }

        public void Unsubscribe<T>(Action<T> handler) where T : struct
        {
            Type key = typeof(T);
            if (!handlers.TryGetValue(key, out Delegate existing)) return;

            Delegate remaining = Delegate.Remove(existing, handler);
            if (remaining == null) handlers.Remove(key);
            else handlers[key] = remaining;
        }

        public void Publish<T>(T evt) where T : struct
        {
            if (handlers.TryGetValue(typeof(T), out Delegate d))
            {
                ((Action<T>)d).Invoke(evt);
            }
        }

        /// <summary>Se llama al destruir el Bootstrap: evita que queden suscripciones vivas (memory leaks).</summary>
        public void Clear() => handlers.Clear();
    }
}
