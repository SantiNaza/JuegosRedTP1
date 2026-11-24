using System.Collections.Generic;

namespace TP.Core
{
    /// <summary>Lógica que se ejecuta una vez por frame (equivalente a Update).</summary>
    public interface IUpdatable { void Tick(float deltaTime); }

    /// <summary>Lógica de física con paso fijo (equivalente a FixedUpdate).</summary>
    public interface IFixedUpdatable { void FixedTick(float fixedDeltaTime); }

    /// <summary>Lógica que corre después de Update (equivalente a LateUpdate: cámara, seguimiento).</summary>
    public interface ILateUpdatable { void LateTick(float deltaTime); }

    /// <summary>
    /// Custom Update Manager. Es el único punto del proyecto que gestiona el Game Loop:
    /// - registro / baja de sistemas (seguro durante la iteración, con colas pendientes),
    /// - orden de ejecución (order: menor primero),
    /// - control de frecuencia (interval: 0 = cada frame, > 0 = cada N segundos, para sistemas no frecuentes),
    /// - pausa (los sistemas "pausables" dejan de recibir ticks; la UI y el input siguen).
    /// No usa Update() de MonoBehaviour: lo invoca el PlayerLoopInjector desde el PlayerLoop de Unity.
    /// </summary>
    public sealed class UpdateManager
    {
        private readonly TickGroup<IUpdatable> updates = new TickGroup<IUpdatable>();
        private readonly TickGroup<IFixedUpdatable> fixedUpdates = new TickGroup<IFixedUpdatable>();
        private readonly TickGroup<ILateUpdatable> lateUpdates = new TickGroup<ILateUpdatable>();

        public bool IsPaused { get; private set; }

        /// <summary>Cantidad total de sistemas registrados (para el HUD de debug / profiling).</summary>
        public int RegisteredCount => updates.Count + fixedUpdates.Count + lateUpdates.Count;

        public void SetPaused(bool paused) => IsPaused = paused;

        public void Register(IUpdatable target, int order = 0, float interval = 0f, bool pausable = true)
            => updates.Add(target, order, interval, pausable);

        public void Register(IFixedUpdatable target, int order = 0, bool pausable = true)
            => fixedUpdates.Add(target, order, 0f, pausable);

        public void Register(ILateUpdatable target, int order = 0, bool pausable = true)
            => lateUpdates.Add(target, order, 0f, pausable);

        public void Unregister(IUpdatable target) => updates.Remove(target);
        public void Unregister(IFixedUpdatable target) => fixedUpdates.Remove(target);
        public void Unregister(ILateUpdatable target) => lateUpdates.Remove(target);

        public void Clear()
        {
            updates.Clear();
            fixedUpdates.Clear();
            lateUpdates.Clear();
        }

        // ---------------- Llamados desde el PlayerLoop ----------------

        public void RunUpdate(float scaledDelta, float unscaledDelta)
        {
            updates.BeginIteration();
            List<TickEntry<IUpdatable>> list = updates.Entries;
            for (int i = 0; i < list.Count; i++)
            {
                TickEntry<IUpdatable> e = list[i];
                if (e.Removed) continue;
                if (e.Pausable && IsPaused) continue;

                float dt = e.Pausable ? scaledDelta : unscaledDelta;
                if (e.Interval <= 0f)
                {
                    e.Target.Tick(dt);
                    continue;
                }

                // Sistemas de baja frecuencia: acumulan tiempo y se ejecutan cada "Interval" segundos.
                e.Accumulator += dt;
                if (e.Accumulator >= e.Interval)
                {
                    float elapsed = e.Accumulator;
                    e.Accumulator = 0f;
                    e.Target.Tick(elapsed);
                }
            }
            updates.EndIteration();
        }

        public void RunFixedUpdate(float fixedDelta)
        {
            fixedUpdates.BeginIteration();
            List<TickEntry<IFixedUpdatable>> list = fixedUpdates.Entries;
            for (int i = 0; i < list.Count; i++)
            {
                TickEntry<IFixedUpdatable> e = list[i];
                if (e.Removed || (e.Pausable && IsPaused)) continue;
                e.Target.FixedTick(fixedDelta);
            }
            fixedUpdates.EndIteration();
        }

        public void RunLateUpdate(float scaledDelta, float unscaledDelta)
        {
            lateUpdates.BeginIteration();
            List<TickEntry<ILateUpdatable>> list = lateUpdates.Entries;
            for (int i = 0; i < list.Count; i++)
            {
                TickEntry<ILateUpdatable> e = list[i];
                if (e.Removed || (e.Pausable && IsPaused)) continue;
                e.Target.LateTick(e.Pausable ? scaledDelta : unscaledDelta);
            }
            lateUpdates.EndIteration();
        }

        // ---------------- Estructuras internas ----------------

        private sealed class TickEntry<T> where T : class
        {
            public T Target;
            public int Order;
            public float Interval;
            public float Accumulator;
            public bool Pausable;
            public bool Removed;
        }

        private sealed class TickGroup<T> where T : class
        {
            public readonly List<TickEntry<T>> Entries = new List<TickEntry<T>>(32);
            private readonly List<TickEntry<T>> pendingAdd = new List<TickEntry<T>>(8);
            private bool iterating;
            private bool hasRemovals;

            public int Count => Entries.Count + pendingAdd.Count;

            public void Add(T target, int order, float interval, bool pausable)
            {
                if (target == null || Contains(target)) return;

                var entry = new TickEntry<T>
                {
                    Target = target,
                    Order = order,
                    Interval = interval,
                    // Desfase inicial para que los sistemas con intervalo no caigan todos en el mismo frame.
                    Accumulator = interval > 0f ? interval * ((Entries.Count % 4) * 0.25f) : 0f,
                    Pausable = pausable
                };

                if (iterating) pendingAdd.Add(entry);
                else Insert(entry);
            }

            public void Remove(T target)
            {
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (!ReferenceEquals(Entries[i].Target, target)) continue;
                    if (iterating)
                    {
                        Entries[i].Removed = true;
                        hasRemovals = true;
                    }
                    else
                    {
                        Entries.RemoveAt(i);
                    }
                    return;
                }

                for (int i = 0; i < pendingAdd.Count; i++)
                {
                    if (ReferenceEquals(pendingAdd[i].Target, target))
                    {
                        pendingAdd.RemoveAt(i);
                        return;
                    }
                }
            }

            public void Clear()
            {
                Entries.Clear();
                pendingAdd.Clear();
                hasRemovals = false;
            }

            public void BeginIteration() => iterating = true;

            public void EndIteration()
            {
                iterating = false;

                if (hasRemovals)
                {
                    for (int i = Entries.Count - 1; i >= 0; i--)
                    {
                        if (Entries[i].Removed) Entries.RemoveAt(i);
                    }
                    hasRemovals = false;
                }

                if (pendingAdd.Count > 0)
                {
                    for (int i = 0; i < pendingAdd.Count; i++) Insert(pendingAdd[i]);
                    pendingAdd.Clear();
                }
            }

            private bool Contains(T target)
            {
                for (int i = 0; i < Entries.Count; i++)
                    if (!Entries[i].Removed && ReferenceEquals(Entries[i].Target, target)) return true;
                for (int i = 0; i < pendingAdd.Count; i++)
                    if (ReferenceEquals(pendingAdd[i].Target, target)) return true;
                return false;
            }

            // Inserción ordenada (estable): menor "Order" se ejecuta primero.
            private void Insert(TickEntry<T> entry)
            {
                int index = Entries.Count;
                for (int i = 0; i < Entries.Count; i++)
                {
                    if (Entries[i].Order > entry.Order)
                    {
                        index = i;
                        break;
                    }
                }
                Entries.Insert(index, entry);
            }
        }
    }
}
