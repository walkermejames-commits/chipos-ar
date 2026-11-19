using System;
using System.Collections.Generic;
using UnityEngine;

namespace ChipOSMarkII
{
    /// <summary>
    /// Registers runtime services by concrete type and exposes safe lookup helpers.
    /// </summary>
    public sealed class ServiceRegistry
    {
        private readonly Dictionary<Type, IChipOSService> servicesByType = new Dictionary<Type, IChipOSService>();
        private readonly List<IChipOSService> registeredServices = new List<IChipOSService>();

        public int Count => registeredServices.Count;
        public IReadOnlyList<IChipOSService> RegisteredServices => registeredServices;

        public bool Register<TService>(TService service) where TService : class, IChipOSService
        {
            return Register(typeof(TService), service);
        }

        public bool Register(Type serviceType, IChipOSService service)
        {
            if (serviceType == null || service == null)
            {
                Debug.LogWarning($"[ServiceRegistry] Cannot register: serviceType={serviceType?.Name ?? "null"}, service={(service != null ? service.ServiceName : "null")}.");
                return false;
            }

            if (!serviceType.IsInstanceOfType(service))
            {
                Debug.LogWarning($"[ServiceRegistry] Service '{service.ServiceName}' is not assignable to type '{serviceType.Name}'.");
                return false;
            }

            if (servicesByType.TryGetValue(serviceType, out var existing))
            {
                return ReferenceEquals(existing, service);
            }

            servicesByType.Add(serviceType, service);
            if (!registeredServices.Contains(service))
            {
                registeredServices.Add(service);
            }

            return true;
        }

        public bool Unregister<TService>() where TService : class, IChipOSService
        {
            return Unregister(typeof(TService));
        }

        public bool Unregister(Type serviceType)
        {
            if (serviceType == null || !servicesByType.TryGetValue(serviceType, out var service))
            {
                return false;
            }

            servicesByType.Remove(serviceType);

            var stillReferenced = false;
            foreach (var entry in servicesByType)
            {
                if (ReferenceEquals(entry.Value, service))
                {
                    stillReferenced = true;
                    break;
                }
            }

            if (!stillReferenced)
            {
                registeredServices.Remove(service);
            }

            return true;
        }

        public bool Contains<TService>() where TService : class, IChipOSService
        {
            return servicesByType.ContainsKey(typeof(TService));
        }

        public bool TryGet<TService>(out TService service) where TService : class, IChipOSService
        {
            if (servicesByType.TryGetValue(typeof(TService), out var registered))
            {
                service = registered as TService;
                return service != null;
            }

            service = null;
            return false;
        }

        public TService Get<TService>() where TService : class, IChipOSService
        {
            return TryGet<TService>(out var service) ? service : null;
        }
    }
}
