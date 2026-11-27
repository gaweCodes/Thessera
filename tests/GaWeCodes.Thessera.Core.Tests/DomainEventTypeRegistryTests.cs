using System.Reflection;
using System.Reflection.Emit;
using GaWeCodes.Thessera.Core.Messaging.DomainEvents;
using GaWeCodes.Thessera.Domain.Events;
using GaWeCodes.Thessera.Domain.Naming;

namespace GaWeCodes.Thessera.Tests;

public sealed class DomainEventTypeRegistryTests
{
    [Fact]
    public void ARegisteredEvent_ResolvesInBothDirections()
    {
        var registry = new DomainEventTypeRegistry([typeof(FlushProbeStarted).Assembly]);

        Assert.Equal("flush-probe-started-v1", registry.NameOf(typeof(FlushProbeStarted)));
        Assert.Equal(typeof(FlushProbeStarted), registry.Resolve("flush-probe-started-v1"));
    }

    [Fact]
    public void ADomainEventWithoutAnEventName_FailsAtRegistrationNotAtTheFirstCommit()
    {
        var assembly = AssemblyWithDomainEvents(("UnnamedEvent", null));

        var exception = Assert.Throws<InvalidOperationException>(() => new DomainEventTypeRegistry([assembly]));

        Assert.Contains("EventName", exception.Message, StringComparison.Ordinal);
        Assert.Contains("UnnamedEvent", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TwoEventsSharingOneName_FailAtRegistration()
    {
        var assembly = AssemblyWithDomainEvents(("First", "shared-v1"), ("Second", "shared-v1"));

        var exception = Assert.Throws<InvalidOperationException>(() => new DomainEventTypeRegistry([assembly]));

        Assert.Contains("shared-v1", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ScanningTheSameAssemblyTwice_IsIdempotent()
    {
        var assembly = typeof(FlushProbeStarted).Assembly;

        var registry = new DomainEventTypeRegistry([assembly, assembly]);

        Assert.Equal(typeof(FlushProbeStarted), registry.Resolve("flush-probe-started-v1"));
    }

    [Fact]
    public void NamesByType_CannotBeCastBackAndMutated()
    {
        var registry = new DomainEventTypeRegistry([typeof(FlushProbeStarted).Assembly]);

        var asDictionary = Assert.IsAssignableFrom<IDictionary<Type, string>>(registry.NamesByType);

        Assert.True(asDictionary.IsReadOnly);
        Assert.Throws<NotSupportedException>(() => asDictionary.Remove(typeof(FlushProbeStarted)));
        Assert.Equal("flush-probe-started-v1", registry.NameOf(typeof(FlushProbeStarted)));
    }

    [Fact]
    public void AnUnregisteredType_NamesTheRegistrationCallInsteadOfFailingObscurely()
    {
        var registry = new DomainEventTypeRegistry([]);

        var exception = Assert.Throws<InvalidOperationException>(() => registry.NameOf(typeof(FlushProbeStarted)));

        Assert.Contains("AddDomainEventsFrom", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARetiredEvent_IsUpcastAlongTheWholeChainToTheCurrentVersion()
    {
        var registry = new DomainEventTypeRegistry([typeof(UpcastProbeTitled).Assembly]);

        var upcast = registry.Upcast(new UpcastProbeTitled("Pasta"));

        Assert.Equal(new UpcastProbeRenamed("Pasta", Language: null), upcast);
    }

    [Fact]
    public void ACurrentEvent_IsReturnedAsItIs()
    {
        var registry = new DomainEventTypeRegistry([typeof(UpcastProbeTitled).Assembly]);
        var current = new UpcastProbeRenamed("Pasta", "it");

        Assert.Same(current, registry.Upcast(current));
    }

    [Fact]
    public void ARetiredEventFromAnUnregisteredAssembly_IsNotUpcastSilently()
    {
        var registry = new DomainEventTypeRegistry([]);

        var exception = Assert.Throws<InvalidOperationException>(() => registry.Upcast(new UpcastProbeTitled("Pasta")));

        Assert.Contains("AddDomainEventsFrom", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARetiredEventNamingTwoSuccessors_FailsAtRegistration()
    {
        var module = DynamicModule();
        var first = EmitEvent(module, "FirstSuccessor", "first-successor-v1");
        var second = EmitEvent(module, "SecondSuccessor", "second-successor-v1");
        EmitEvent(module, "TwoSuccessors", "two-successors-v1", first, second);

        var exception = Assert.Throws<InvalidOperationException>(() => new DomainEventTypeRegistry([module.Assembly]));

        Assert.Contains("more than one successor", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARetiredEventWhoseSuccessorIsNotRegistered_FailsAtRegistration()
    {
        var unscanned = EmitEvent(DynamicModule(), "UnscannedSuccessor", "unscanned-successor-v1");
        var module = DynamicModule();
        EmitEvent(module, "OrphanedRetired", "orphaned-retired-v1", unscanned);

        var exception = Assert.Throws<InvalidOperationException>(() => new DomainEventTypeRegistry([module.Assembly]));

        Assert.Contains("UnscannedSuccessor", exception.Message, StringComparison.Ordinal);
        Assert.Contains("AddDomainEventsFrom", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ARetiredEventThatIsItsOwnSuccessor_FailsAtRegistration()
    {
        var module = DynamicModule();
        EmitEvent(module, "SelfSucceeding", "self-succeeding-v1", successorIsItself: true);

        var exception = Assert.Throws<InvalidOperationException>(() => new DomainEventTypeRegistry([module.Assembly]));

        Assert.Contains("lead back", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUpcastReturningNull_FailsWhenReadInsteadOfLosingTheEvent()
    {
        var module = DynamicModule();
        var successor = EmitEvent(module, "Successor", "successor-v1");
        var retired = EmitEvent(module, "ReturnsNull", "returns-null-v1", successor);
        var registry = new DomainEventTypeRegistry([module.Assembly]);

        var exception = Assert.Throws<InvalidOperationException>(
            () => registry.Upcast((IDomainEvent)Activator.CreateInstance(retired)!));

        Assert.Contains("ReturnsNull.Upcast", exception.Message, StringComparison.Ordinal);
    }

    private static ModuleBuilder DynamicModule()
    {
        var assemblyName = new AssemblyName("GeneratedRetiredEvents" + Guid.NewGuid().ToString("N")[..8]);
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        return assembly.DefineDynamicModule(assemblyName.Name!);
    }

    private static Type EmitEvent(
        ModuleBuilder module,
        string typeName,
        string eventName,
        params Type[] successors) =>
        EmitEvent(module, typeName, eventName, successorIsItself: false, successors);

    private static Type EmitEvent(
        ModuleBuilder module,
        string typeName,
        string eventName,
        bool successorIsItself,
        params Type[] successors)
    {
        var type = module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class | TypeAttributes.Sealed);
        type.AddInterfaceImplementation(typeof(IDomainEvent));
        type.DefineDefaultConstructor(MethodAttributes.Public);
        type.SetCustomAttribute(new CustomAttributeBuilder(
            typeof(EventNameAttribute).GetConstructor([typeof(string)])!,
            [eventName]));

        var openUpcast = typeof(IRetiredDomainEvent<>).GetMethod(nameof(IRetiredDomainEvent<IDomainEvent>.Upcast))!;
        var allSuccessors = successorIsItself ? new Type[] { type } : successors;

        for (var index = 0; index < allSuccessors.Length; index++)
        {
            var successor = allSuccessors[index];
            var contract = typeof(IRetiredDomainEvent<>).MakeGenericType(successor);
            type.AddInterfaceImplementation(contract);

            var upcast = type.DefineMethod(
                $"Upcast{index}",
                MethodAttributes.Private | MethodAttributes.Virtual | MethodAttributes.Final
                    | MethodAttributes.HideBySig | MethodAttributes.NewSlot,
                successor,
                Type.EmptyTypes);
            var il = upcast.GetILGenerator();
            il.Emit(OpCodes.Ldnull);
            il.Emit(OpCodes.Ret);

            var contractUpcast = successorIsItself
                ? TypeBuilder.GetMethod(contract, openUpcast)
                : contract.GetMethod(openUpcast.Name)!;

            type.DefineMethodOverride(upcast, contractUpcast);
        }

        return type.CreateType();
    }

    private static Assembly AssemblyWithDomainEvents(params (string TypeName, string? EventName)[] events)
    {
        var assemblyName = new AssemblyName("GeneratedDomainEvents" + Guid.NewGuid().ToString("N")[..8]);
        var assembly = AssemblyBuilder.DefineDynamicAssembly(assemblyName, AssemblyBuilderAccess.Run);
        var module = assembly.DefineDynamicModule(assemblyName.Name!);
        var attributeConstructor = typeof(EventNameAttribute).GetConstructor([typeof(string)])!;

        foreach (var (typeName, eventName) in events)
        {
            var type = module.DefineType(typeName, TypeAttributes.Public | TypeAttributes.Class);
            type.AddInterfaceImplementation(typeof(IDomainEvent));

            if (eventName is not null)
            {
                type.SetCustomAttribute(new CustomAttributeBuilder(attributeConstructor, [eventName]));
            }

            type.CreateType();
        }

        return assembly;
    }
}
