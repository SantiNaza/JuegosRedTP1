using System;
using System.Collections.Generic;

namespace TP.Core
{
    /// <summary>
    /// Object Pool genérico. Se usa para enemigos, proyectiles, granadas y efectos visuales.
    ///
    /// Tiene un modo "sin pooling" (poolingEnabled = false) que crea y destruye en cada uso,
    /// igual que un Instantiate()/Destroy() constante. Sirve para generar la evidencia de profiling
    /// comparando ambas versiones (PerformanceConfig → usePooling).
    /// </summary>
    public sealed class Pool<T> where T : class
    {
        private readonly Stack<T> available;
        private readonly Func<T> create;
        private readonly Action<T> onGet;
        private readonly Action<T> onRelease;
        private readonly Action<T> onDestroy;
        private readonly bool poolingEnabled;

        public int ActiveCount { get; private set; }
        public int AvailableCount => available.Count;
        /// <summary>Total de instancias creadas desde el inicio (con pooling debería estabilizarse).</summary>
        public int TotalCreated { get; private set; }

        public Pool(Func<T> create, Action<T> onGet, Action<T> onRelease, Action<T> onDestroy,
                    int prewarm, bool poolingEnabled)
        {
            this.create = create ?? throw new ArgumentNullException(nameof(create));
            this.onGet = onGet;
            this.onRelease = onRelease;
            this.onDestroy = onDestroy;
            this.poolingEnabled = poolingEnabled;
            available = new Stack<T>(Math.Max(prewarm, 4));

            if (!poolingEnabled) return;
            for (int i = 0; i < prewarm; i++)
            {
                T item = CreateItem();
                onRelease?.Invoke(item);
                available.Push(item);
            }
        }

        public T Get()
        {
            T item = poolingEnabled && available.Count > 0 ? available.Pop() : CreateItem();
            onGet?.Invoke(item);
            ActiveCount++;
            return item;
        }

        public void Release(T item)
        {
            if (item == null) return;
            onRelease?.Invoke(item);
            ActiveCount--;

            if (poolingEnabled) available.Push(item);
            else onDestroy?.Invoke(item);
        }

        /// <summary>Destruye los elementos guardados (los activos los libera su sistema dueño).</summary>
        public void Clear()
        {
            while (available.Count > 0) onDestroy?.Invoke(available.Pop());
        }

        private T CreateItem()
        {
            TotalCreated++;
            return create();
        }
    }
}
