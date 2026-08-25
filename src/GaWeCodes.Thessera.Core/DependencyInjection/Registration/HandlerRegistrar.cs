using System.Reflection;
using GaWeCodes.Thessera.Application.Cqrs;
using GaWeCodes.Thessera.Application.DomainEvents;
using GaWeCodes.Thessera.Application.IntegrationEvents;
using GaWeCodes.Thessera.Application.ReadModels;
using GaWeCodes.Thessera.Core.Dispatching;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GaWeCodes.Thessera.Core.DependencyInjection.Registration;

internal sealed class HandlerRegistrar(IServiceCollection services, PipelineBehaviorRegistry behaviorRegistry)
{
    private static readonly Type[] SingleHandlerInterfaceDefinitions =
    [
        typeof(ICommandHandler<>),
        typeof(ICommandHandler<,>),
        typeof(IQueryHandler<,>),
    ];

    private static readonly Type[] MultiHandlerInterfaceDefinitions =
    [
        typeof(IProjectionHandler<>),
        typeof(IIntegrationEventMapper<>),
        typeof(IReadModelRebuilder<,>),
    ];

    private readonly Dictionary<Type, Type> _singleHandlers = [];
    private readonly HashSet<Assembly> _scannedAssemblies = [];

    public IReadOnlyCollection<Assembly> ScannedAssemblies => _scannedAssemblies;

    public void AddFrom(Assembly assembly)
    {
        Type[] types;
        try
        {
            types = assembly.GetTypes();
        }
        catch (ReflectionTypeLoadException exception)
        {
            throw new InvalidOperationException(
                $"The types of assembly '{assembly.FullName}' could not be loaded. " +
                "The most common cause is a missing package reference.",
                exception);
        }

        _scannedAssemblies.Add(assembly);

        foreach (var type in types)
        {
            if (type is not { IsClass: true, IsAbstract: false } || type.IsGenericTypeDefinition)
            {
                continue;
            }

            RegisterContractsOf(type);
        }
    }

    public void AddPipelineBehavior(Type openGenericBehavior, int order)
    {
        if (!openGenericBehavior.IsGenericTypeDefinition || openGenericBehavior.GetGenericArguments().Length != 2)
        {
            throw new ArgumentException(
                "A pipeline behavior must be an open-generic type definition with two type parameters " +
                "(TRequest, TResponse), for example typeof(MyBehavior<,>).",
                nameof(openGenericBehavior));
        }

        var implementsBehavior = Array.Exists(
            openGenericBehavior.GetInterfaces(),
            static @interface => @interface.IsGenericType
                && @interface.GetGenericTypeDefinition() == typeof(IPipelineBehavior<,>));

        if (!implementsBehavior)
        {
            throw new ArgumentException(
                $"Type '{openGenericBehavior}' does not implement {typeof(IPipelineBehavior<,>)}.",
                nameof(openGenericBehavior));
        }

        behaviorRegistry.Register(openGenericBehavior, order);
        services.TryAddEnumerable(ServiceDescriptor.Transient(typeof(IPipelineBehavior<,>), openGenericBehavior));
    }

    private void RegisterContractsOf(Type type)
    {
        foreach (var contract in type.GetInterfaces())
        {
            if (contract.IsGenericType
                && Array.IndexOf(MultiHandlerInterfaceDefinitions, contract.GetGenericTypeDefinition()) >= 0)
            {
                services.TryAddEnumerable(ServiceDescriptor.Scoped(contract, type));
            }
            else if (contract.IsGenericType
                && Array.IndexOf(SingleHandlerInterfaceDefinitions, contract.GetGenericTypeDefinition()) >= 0)
            {
                RegisterSingleHandler(contract, type);
            }
        }
    }

    private void RegisterSingleHandler(Type contract, Type implementation)
    {
        if (_singleHandlers.TryGetValue(contract, out var existing))
        {
            if (existing == implementation)
            {
                return;
            }

            throw new InvalidOperationException(
                $"Two handlers were found for '{contract}': '{existing}' and '{implementation}'. " +
                "A command or query must have exactly one handler.");
        }

        _singleHandlers.Add(contract, implementation);
        services.AddScoped(contract, implementation);
    }
}
