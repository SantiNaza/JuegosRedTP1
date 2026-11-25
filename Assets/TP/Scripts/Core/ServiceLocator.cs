using System;
using System.Collections.Generic;

namespace TP.Core
{
    /// <summary>
    /// Service Locator simple. NO es estático ni singleton: lo crea el Bootstrap (composition root)
    /// y se lo pasa a quien lo necesite. Los servicios se registran por interfaz.
    /// </summary>
    public sealed class ServiceLocator
    {
        private readonly Dictionary<Type, object> services = new Dictionary<Type, object>(32);

        public void Register<TService>(TService implementation) where TService : class
        {
            if (implementation == null) throw new ArgumentNullException(nameof(implementation));
            services[typeof(TService)] = implementation;
        }

        public TService Get<TService>() where TService : class
        {
            if (services.TryGetValue(typeof(TService), out object service)) return (TService)service;
            throw new InvalidOperationException($"[ServiceLocator] Servicio no registrado: {typeof(TService).Name}");
        }

        public bool TryGet<TService>(out TService service) where TService : class
        {
            if (services.TryGetValue(typeof(TService), out object found))
            {
                service = (TService)found;
                return true;
            }
            service = null;
            return false;
        }

        /// <summary>Libera todos los servicios IDisposable (al salir de la escena) y vacía el registro.</summary>
        public void DisposeAll()
        {
            var disposed = new HashSet<object>();
            foreach (object service in services.Values)
            {
                if (service is IDisposable disposable && disposed.Add(service)) disposable.Dispose();
            }
            services.Clear();
        }
    }
}
