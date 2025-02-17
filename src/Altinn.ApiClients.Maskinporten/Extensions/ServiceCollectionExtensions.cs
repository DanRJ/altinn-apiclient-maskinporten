﻿using System;
using System.Linq;
using Altinn.ApiClients.Maskinporten.Factories;
using Altinn.ApiClients.Maskinporten.Helpers;
using Altinn.ApiClients.Maskinporten.Interfaces;
using Altinn.ApiClients.Maskinporten.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Altinn.ApiClients.Maskinporten.Extensions
{
    /// <summary>
    /// We add all IClientDefinition implementation to the DI container and immediately add a reference to the corresponding
    /// httpClient and configuration to a static list via the helpers in MaskinportenClientDefinitionHelper.
    /// The MaskinportenHttpMessageHandlerFactory relies on the index of a given httpClient refence/configuration
    /// matching the index of the IClientDefinition service when injected as a IEnumerable&lt;IClientDefinition&gt;
    /// This way we can support having multiple named/typed HTTP clients using the same IClientDefinition implementation,
    /// (but different singleton instances), whilst the IClientDefinition can use normal DI in its constructors
    /// </summary>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Registers a Maskinporten client definition instance with a unique key. This allows for several instances using the
        /// same client definition but varying configurations. Use IHttpClientBuilder.AddMaskinportenHttpMessageHandler
        /// to attach it to a HttpClient, using the same key.
        /// </summary>
        /// <typeparam name="TClientDefinition">The client definition</typeparam>
        /// <param name="services">Service collection</param>
        /// <param name="clientDefinitionKey">Key to uniquely identify this client definition instance</param>
        /// <param name="config">Configuration to use</param>
        public static void RegisterMaskinportenClientDefinition<TClientDefinition>(this IServiceCollection services,
            string clientDefinitionKey, IConfiguration config)
            where TClientDefinition : class, IClientDefinition
        {
            AddMaskinportenClientCommon(services);
            services.AddSingleton<IClientDefinition, TClientDefinition>();
            MaskinportenClientDefinitionHelper.AddClientDefinitionInstance(clientDefinitionKey, config);
        }

        /// <summary>
        /// Adds a named Maskinporten-enabled HTTP client with the given configuration which can be created with HttpClientFactory
        /// </summary>
        /// <typeparam name="TClientDefinition">The client definition</typeparam>
        /// <param name="services">Service collection</param>
        /// <param name="httpClientName">Name of HTTP client.</param>
        /// <param name="config">Configuration to use</param>
        /// <param name="configureClientDefinition">Delegate for configuring the client definition</param>
        public static IHttpClientBuilder AddMaskinportenHttpClient<TClientDefinition>(this IServiceCollection services,
            string httpClientName, IConfiguration config, Action<TClientDefinition> configureClientDefinition = null)
            where TClientDefinition : class, IClientDefinition
        {
            services.RegisterMaskinportenClientDefinition<TClientDefinition>(httpClientName, config);
            return services.AddHttpClient(httpClientName)
                .AddMaskinportenHttpMessageHandler(httpClientName, configureClientDefinition);
        }

        /// <summary>
        /// Adds a typed Maskinporten-enabled HTTP client with the given configuration to the dependency injection container
        /// </summary>
        /// <typeparam name="TClientDefinition">The client definition</typeparam>
        /// <typeparam name="THttpClient">The HTTP client type</typeparam>
        /// <param name="services">Service collection</param>
        /// <param name="config">Configuration to use</param>
        /// <param name="configureClientDefinition">Delegate for configuring the client definition</param>
        public static IHttpClientBuilder AddMaskinportenHttpClient<TClientDefinition, THttpClient>(this IServiceCollection services,
            IConfiguration config, Action<TClientDefinition> configureClientDefinition = null)
            where TClientDefinition : class, IClientDefinition
            where THttpClient : class
        {
            services.RegisterMaskinportenClientDefinition<TClientDefinition>(typeof(THttpClient).FullName, config);

            return services.AddHttpClient<THttpClient>()
                .AddMaskinportenHttpMessageHandler<TClientDefinition, THttpClient>(configureClientDefinition);
        }

        private static void AddMaskinportenClientCommon(IServiceCollection services)
        {
            // We need a provider to cache tokens. If one is not already provided by the user, use MemoryTokenCacheProvider
            if (services.All(x => x.ServiceType != typeof(ITokenCacheProvider)))
            {
                services.AddMemoryCache();
                services.TryAddSingleton<ITokenCacheProvider, MemoryTokenCacheProvider>();
            }

            // We only need a single Maskinporten-service for all clients. This can be used directly if low level access is required.
            services.TryAddSingleton<IMaskinportenService, MaskinportenService>();
            services.TryAddSingleton<MaskinportenHttpMessageHandlerFactory>();
        }
    }
}
