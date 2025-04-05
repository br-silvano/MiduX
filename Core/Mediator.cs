using System.Collections.Concurrent;
using MiduX.Core.Interfaces;
using MiduX.Exceptions;
using MiduX.Localization;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace MiduX.Core
{
    public class Mediator(IServiceProvider serviceProvider) : IMediator
    {
        private static readonly ConcurrentDictionary<(Type Request, Type Response), Func<IServiceProvider, object>> _handlerFactories =
            new();

        private readonly IServiceProvider _serviceProvider = serviceProvider;

        public async Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var handlers = scope.ServiceProvider.GetServices<INotificationHandler<TNotification>>();
                var tasks = handlers.Select(handler => handler.Handle(notification, cancellationToken));
                await Task.WhenAll(tasks);
            }
            catch (Exception ex)
            {
                throw new MediatorException(Messages.Get("Error_PublishNotification", typeof(TNotification).Name, ex.Message), ex);
            }
        }

        public async Task<TResponse> Send<TRequest, TResponse>(TRequest request, CancellationToken cancellationToken = default)
                    where TRequest : IRequest<TResponse>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var key = (typeof(TRequest), typeof(TResponse));
                var factory = _handlerFactories.GetOrAdd(key, _ => sp => sp.GetRequiredService(typeof(IRequestHandler<TRequest, TResponse>)));
                var handler = (IRequestHandler<TRequest, TResponse>)factory(scope.ServiceProvider);

                var behaviors = scope.ServiceProvider.GetServices<IPipelineBehavior<TRequest, TResponse>>()
                                    .Reverse().ToList();
                Func<Task<TResponse>> next = () => handler.Handle(request, cancellationToken);
                foreach (var behavior in behaviors)
                {
                    var currentNext = next;
                    next = () => behavior.Handle(request, currentNext, cancellationToken);
                }

                return await next();
            }
            catch (ValidationException vex)
            {
                throw new MediatorException(Messages.Get("Error_Validation", typeof(TRequest).Name, vex.Message), vex);
            }
            catch (Exception ex)
            {
                throw new MediatorException(Messages.Get("Error_ProcessingRequest", typeof(TRequest).Name, ex.Message), ex);
            }
        }
    }
}