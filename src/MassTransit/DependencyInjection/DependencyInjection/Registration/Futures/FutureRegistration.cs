namespace MassTransit.DependencyInjection.Registration
{
    using System;
    using Configuration;
    using Internals;
    using Microsoft.Extensions.DependencyInjection;
    using Transports;


    public class FutureRegistration<TFuture> :
        IFutureRegistration
        where TFuture : class, SagaStateMachine<FutureState>
    {
        IFutureDefinition<TFuture> _definition;

        public FutureRegistration()
        {
            IncludeInConfigureEndpoints = !Type.HasAttribute<ExcludeFromConfigureEndpointsAttribute>();
        }

        public Type Type => typeof(TFuture);

        public bool IncludeInConfigureEndpoints { get; set; }

        public void Configure(IReceiveEndpointConfigurator configurator, IRegistrationContext context)
        {
            var stateMachine = context.GetRequiredService<TFuture>();
            ISagaRepository<FutureState> repository = new DependencyInjectionSagaRepository<FutureState>(context);

            var decoratorRegistration = context.GetService<ISagaRepositoryDecoratorRegistration<FutureState>>();
            if (decoratorRegistration != null)
                repository = decoratorRegistration.DecorateSagaRepository(repository);

            var sagaConfigurator = new StateMachineSagaConfigurator<FutureState>(stateMachine, repository, configurator);

            GetFutureDefinition(context)
                .Configure(configurator, sagaConfigurator, context);

            LogContext.Info?.Log("Configured endpoint {Endpoint}, Future: {FutureType}",
                configurator.InputAddress.GetEndpointName(), TypeCache<TFuture>.ShortName);

            configurator.AddEndpointSpecification(sagaConfigurator);

            IncludeInConfigureEndpoints = false;
        }

        public IFutureDefinition GetDefinition(IRegistrationContext context)
        {
            return GetFutureDefinition(context);
        }

        IFutureDefinition<TFuture> GetFutureDefinition(IServiceProvider provider)
        {
            if (_definition != null)
                return _definition;

            _definition = provider.GetService<IFutureDefinition<TFuture>>() ?? new DefaultFutureDefinition<TFuture>();

            var endpointDefinition = provider.GetService<IEndpointDefinition<TFuture>>();
            if (endpointDefinition != null)
                _definition.EndpointDefinition = endpointDefinition;

            return _definition;
        }
    }
}
