namespace MassTransit.AmazonSqsTransport.Middleware
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Threading;
    using System.Threading.Tasks;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Internals;
    using MassTransit.Middleware;
    using Microsoft.Extensions.Configuration;
    using Microsoft.Extensions.Logging;
    using Transports;
    using Util;


    /// <summary>
    /// Receives messages from AmazonSQS, pushing them to the InboundPipe of the service endpoint.
    /// </summary>
    public sealed class AmazonSqsMessageReceiver :
        Agent,
        DeliveryMetrics
    {
        readonly ClientContext _client;
        readonly SqsReceiveEndpointContext _context;
        readonly TaskCompletionSource<bool> _deliveryComplete;
        readonly IReceivePipeDispatcher _dispatcher;
        readonly ReceiveSettings _receiveSettings;

        /// <summary>
        /// The basic consumer receives messages pushed from the broker.
        /// </summary>
        /// <param name="client">The model context for the consumer</param>
        /// <param name="context">The topology</param>
        public AmazonSqsMessageReceiver(ClientContext client, SqsReceiveEndpointContext context)
        {
            _client = client;
            _context = context;

            _receiveSettings = client.GetPayload<ReceiveSettings>();

            _deliveryComplete = TaskUtil.GetTask<bool>();

            _dispatcher = context.CreateReceivePipeDispatcher();
            _dispatcher.ZeroActivity += HandleDeliveryComplete;

            var consumeTask = Task.Run(() => Consume());
            consumeTask.ContinueWith(async _ =>
            {
                try
                {
                    if (!IsStopping)
                        await this.Stop("Consume Loop Exited").ConfigureAwait(false);
                }
                catch (Exception exception)
                {
                    LogContext.Warning?.Log(exception, "Stop Faulted");
                }
            });
        }

        long DeliveryMetrics.DeliveryCount => _dispatcher.DispatchCount;
        int DeliveryMetrics.ConcurrentDeliveryCount => _dispatcher.MaxConcurrentDispatchCount;

        async Task Consume()
        {
            await GetQueueAttributes().ConfigureAwait(false);

            using var algorithm = new RequestRateAlgorithm(new RequestRateAlgorithmOptions
            {
                PrefetchCount = _receiveSettings.PrefetchCount,
                ConcurrentResultLimit = _receiveSettings.ConcurrentMessageLimit,
                RequestResultLimit = 10
            });

            SetReady();

            try
            {
                while (!IsStopping)
                {
                    if (Environment.GetEnvironmentVariable("MassTransit__LogPolling") == "true")
                    {
                        LogContext.Info?.Log("Polling SQS for messages: {QueueUrl}", _receiveSettings.QueueUrl);
                    }
                    if (_receiveSettings.IsOrdered)
                    {
                        await algorithm.Run(ReceiveMessages, (m, _) => HandleMessage(m), GroupMessages, OrderMessages, Stopping)
                            .ConfigureAwait(false);
                    }
                    else
                        await algorithm.Run(ReceiveMessages, (m, _) => HandleMessage(m), Stopping).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException exception) when (exception.CancellationToken == Stopping)
            {
            }
            catch (Exception exception)
            {
                LogContext.Warning?.Log(exception, "Consume Loop faulted");
            }
        }

        protected override Task StopAgent(StopContext context)
        {
            LogContext.Debug?.Log("Stopping consumer: {InputAddress}", _context.InputAddress);

            SetCompleted(ActiveAndActualAgentsCompleted(context));

            return Completed;
        }

        async Task GetQueueAttributes()
        {
            var queueInfo = await _client.GetQueueInfo(_receiveSettings.EntityName).ConfigureAwait(false);

            _receiveSettings.QueueUrl = queueInfo.Url;

            if (queueInfo.Attributes.TryGetValue(QueueAttributeName.VisibilityTimeout, out var value)
                && int.TryParse(value, out var visibilityTimeout)
                && visibilityTimeout != _receiveSettings.VisibilityTimeout)
            {
                LogContext.Debug?.Log("Using queue visibility timeout of {VisibilityTimeout}", TimeSpan.FromSeconds(visibilityTimeout).ToFriendlyString());

                _receiveSettings.VisibilityTimeout = visibilityTimeout;
            }
        }

        async Task HandleMessage(Message message)
        {
            if (IsStopping)
                return;

            var redelivered = message.Attributes.TryGetInt("ApproximateReceiveCount", out var receiveCount) && receiveCount > 1;

            var context = new AmazonSqsReceiveContext(message, redelivered, _context, _client, _receiveSettings, _client.ConnectionContext);
            try
            {
                await _dispatcher.Dispatch(context, context).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                context.LogTransportFaulted(exception);
            }
            finally
            {
                context.Dispose();
            }
        }

        static IEnumerable<IGrouping<string, Message>> GroupMessages(IEnumerable<Message> messages)
        {
            return messages.GroupBy(x => x.Attributes.TryGetValue(MessageSystemAttributeName.MessageGroupId, out var groupId) ? groupId : "");
        }

        static IEnumerable<Message> OrderMessages(IEnumerable<Message> messages)
        {
            return messages.OrderBy(x => x.Attributes.TryGetValue("SequenceNumber", out var sequenceNumber) ? sequenceNumber : "",
                SequenceNumberComparer.Instance);
        }

        async Task<IEnumerable<Message>> ReceiveMessages(int messageLimit, CancellationToken cancellationToken)
        {
            try
            {
                IList<Message> messages = await _client
                    .ReceiveMessages(_receiveSettings.EntityName, messageLimit, _receiveSettings.WaitTimeSeconds, cancellationToken)
                    .ConfigureAwait(false);

                if (Environment.GetEnvironmentVariable("MassTransit__LogPolling") == "true")
                {
                    LogContext.Info?.Log("SQS messages received: {MessageCount}",
                        messages.Count);
                }

                return messages;
            }
            catch (OperationCanceledException)
            {
                return Array.Empty<Message>();
            }
        }

        Task HandleDeliveryComplete()
        {
            if (IsStopping)
                _deliveryComplete.TrySetResult(true);

            return Task.CompletedTask;
        }

        async Task ActiveAndActualAgentsCompleted(StopContext context)
        {
            if (_dispatcher.ActiveDispatchCount > 0)
            {
                try
                {
                    await _deliveryComplete.Task.OrCanceled(context.CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    LogContext.Warning?.Log("Stop canceled waiting for message consumers to complete: {InputAddress}", _context.InputAddress);
                }
            }
        }


        class SequenceNumberComparer :
            IComparer<string>
        {
            public static readonly SequenceNumberComparer Instance = new SequenceNumberComparer();

            public int Compare(string x, string y)
            {
                if (string.IsNullOrWhiteSpace(x))
                    throw new ArgumentNullException(nameof(x));

                if (string.IsNullOrWhiteSpace(y))
                    throw new ArgumentNullException(nameof(y));

                if (x.Length != y.Length)
                    return x.Length > y.Length ? 1 : -1;

                return string.Compare(x, y, StringComparison.Ordinal);
            }
        }
    }
}
