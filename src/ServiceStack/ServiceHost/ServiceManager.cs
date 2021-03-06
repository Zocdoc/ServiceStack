﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Autofac;
using ServiceStack.DependencyInjection;
using ServiceStack.Logging;
using ServiceStack.Text;

namespace ServiceStack.ServiceHost
{
	public class ServiceManager
		: IDisposable
	{
		private static readonly ILog Log = LogManager.GetLogger(typeof(ServiceManager));

		public DependencyService DependencyService { get; private set; }
		public ServiceController ServiceController { get; private set; }
        public ServiceMetadata Metadata { get; internal set; }

        //public ServiceOperations ServiceOperations { get; set; }
        //public ServiceOperations AllServiceOperations { get; set; }

        public ServiceManager(IContainer dependencyContainer, params Assembly[] assembliesWithServices)
            : this(new DependencyService(dependencyContainer), assembliesWithServices)
		{
		}

        public ServiceManager(DependencyService dependencyService, params Assembly[] assembliesWithServices)
        {
            if (dependencyService == null)
            {
                throw new ArgumentNullException("dependencyService");
            }
            this.DependencyService = dependencyService;
            this.Metadata = new ServiceMetadata();
            this.ServiceController = new ServiceController(() => GetAssemblyTypes(assembliesWithServices), this.Metadata);
        }

        /// <summary>
        /// Inject alternative dependencyService and strategy for resolving Service Types
        /// </summary>
        public ServiceManager(DependencyService dependencyService, ServiceController serviceController)
        {
            if (serviceController == null)
            {
                throw new ArgumentNullException("serviceController");
            }
            if (dependencyService == null)
            {
                throw new ArgumentNullException("dependencyService");
            }

            this.DependencyService = dependencyService;
            this.Metadata = serviceController.Metadata; //always share the same metadata
            this.ServiceController = serviceController;
        }

		private List<Type> GetAssemblyTypes(Assembly[] assembliesWithServices)
		{
			var results = new List<Type>();
			string assemblyName = null;
			string typeName = null;

			try
			{
				foreach (var assembly in assembliesWithServices)
				{
					assemblyName = assembly.FullName;
					foreach (var type in assembly.GetTypes())
					{
						typeName = type.Name;
						results.Add(type);
					}
				}
				return results;
			}
			catch (Exception ex)
			{
				var msg = string.Format("Failed loading types, last assembly '{0}', type: '{1}'", assemblyName, typeName);
				Log.Error(msg, ex);
				throw new Exception(msg, ex);
			}
		}

        public ServiceManager Init()
		{
            this.ServiceController.Register(DependencyService);

            foreach (var type in this.Metadata.ServiceTypes)
            {
                this.DependencyService.Register(type, DependencyService.Sharing.None, registerAsImplementedInterfaces: false, includeNonPublicConstructors: false);
            }

            return this;
        }

		public void RegisterService<T>()
		{
			if (!typeof(T).IsGenericType
				|| typeof(T).GetGenericTypeDefinition() != typeof(IService<>))
				throw new ArgumentException("Type {0} is not a Web Service that inherits IService<>".Fmt(typeof(T).FullName));

			this.ServiceController.RegisterGService(typeof(T), this.DependencyService);
			this.DependencyService.RegisterAutoWired<T>();
		}

		public Type RegisterService(Type serviceType)
		{
            var genericServiceType = serviceType.GetTypeWithGenericTypeDefinitionOf(typeof(IService<>));
            try
			{
                if (genericServiceType != null)
                {
                    this.ServiceController.RegisterGService(serviceType, DependencyService);
                    this.DependencyService.RegisterAutoWiredType(serviceType);
                    return genericServiceType;
                }

                var isNService = typeof(IService).IsAssignableFrom(serviceType);
                if (isNService)
                {
                    this.ServiceController.RegisterNService(serviceType, DependencyService);
                    this.DependencyService.RegisterAutoWiredType(serviceType);
                    return null;
                }

                throw new ArgumentException("Type {0} is not a Web Service that inherits IService<> or IService".Fmt(serviceType.FullName));
            }
			catch (Exception ex)
			{
				Log.Error(ex);
			    return genericServiceType;
			}
		}

        /// <summary>
        /// Registers the given service instance to be reused for each request that would route to 
        /// its service type.
        /// </summary>
        /// <remarks>
        /// This method should not be used in production.  Because it reuses a single instance for 
        /// each request, it can't really do dependency injection safely.  This method is only 
        /// useful by the functional testing harness, which self-hosts a ServiceStack with mock 
        /// implementations.
        /// </remarks>
        /// <param name="serviceImplementation">The singleton instance of a service to use.</param>
	    public void RegisterService(IService serviceImplementation)
        {
            var serviceType = serviceImplementation.GetType();

            DependencyService.RegisterSingletonInstance(serviceImplementation, registerAsImplementedInterfaces: false);

            this.ServiceController.RegisterNService(serviceType, DependencyService);

            DependencyService.UpdateRegistrations();
        }

		public object Execute(object dto)
		{
			return this.ServiceController.Execute(dto, null);
		}

		public void Dispose()
		{
		}

		public void AfterInit()
		{
			this.ServiceController.AfterInit();
		}
	}

}
