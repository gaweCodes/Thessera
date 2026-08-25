using System.Collections.Concurrent;

namespace GaWeCodes.Thessera.Core.Dispatching;

internal sealed class PipelineBehaviorRegistry
{
    private readonly ConcurrentDictionary<Type, int> _orders = new();

    public void Register(Type openGenericBehavior, int order) => _orders[openGenericBehavior] = order;

    public bool TryGetOrder(Type behaviorType, out int order) =>
        _orders.TryGetValue(Definition(behaviorType), out order);

    public int GetOrder(Type closedBehaviorType) =>
        TryGetOrder(closedBehaviorType, out var order)
            ? order
            : throw new InvalidOperationException(
                $"The pipeline behavior '{closedBehaviorType}' has no registered order. Register it with " +
                "options.AddPipelineBehavior(typeof(MyBehavior<,>), order) instead of adding it to the service " +
                "collection directly.");

    private static Type Definition(Type behaviorType) =>
        behaviorType.IsGenericType ? behaviorType.GetGenericTypeDefinition() : behaviorType;
}
