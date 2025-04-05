using System.Diagnostics;
using MiduX.Core.Interfaces;
using MiduX.Localization;
using Microsoft.Extensions.Logging;

namespace MiduX.Pipeline
{
    public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger) : IPipelineBehavior<TRequest, TResponse>
         where TRequest : IRequest<TResponse>
    {
        private readonly ILogger<LoggingBehavior<TRequest, TResponse>> _logger = logger;

        public async Task<TResponse> Handle(TRequest request, Func<Task<TResponse>> next, CancellationToken cancellationToken)
        {
            var requestName = typeof(TRequest).Name;
            _logger.LogInformation(Messages.Get("Log_ProcessingStarted", requestName));
            var stopwatch = Stopwatch.StartNew();

            try
            {
                var response = await next();
                stopwatch.Stop();
                _logger.LogInformation(Messages.Get("Log_ProcessingCompleted", requestName, stopwatch.ElapsedMilliseconds));
                return response;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                _logger.LogError(ex, Messages.Get("Log_ProcessingError", requestName, stopwatch.ElapsedMilliseconds));
                throw;
            }
        }
    }
}