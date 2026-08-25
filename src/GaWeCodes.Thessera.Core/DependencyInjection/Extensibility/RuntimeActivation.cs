namespace GaWeCodes.Thessera.Core.DependencyInjection.Extensibility;

public sealed class RuntimeActivation
{
    public IRuntimeActivator? Activator { get; private set; }

    public TActivator GetOrAdd<TActivator>(Func<TActivator> create)
        where TActivator : class, IRuntimeActivator
    {
        ArgumentNullException.ThrowIfNull(create);

        if (Activator is null)
        {
            var activator = create();
            Activator = activator;
            return activator;
        }

        return Activator as TActivator ?? throw new InvalidOperationException(
            $"Two different runtimes were selected for the same host ({Activator.GetType().Name} and " +
            $"{typeof(TActivator).Name}). A host runs exactly one messaging runtime, because that runtime owns the " +
            "outbox, the inbox and the local queues every domain event travels through. Two of them would each hold " +
            "half of the delivery guarantees. Choose one runtime for the whole host.");
    }
}
