﻿namespace MassTransit.AmazonSqsTransport
{
    using System;
    using System.Collections.Generic;
    using Amazon.SQS;
    using Amazon.SQS.Model;
    using Context;
    using Transports;


    public sealed class AmazonSqsReceiveContext :
        BaseReceiveContext,
        AmazonSqsMessageContext,
        TransportReceiveContext
    {

        private StringMessageBody _body;

        public AmazonSqsReceiveContext(Message message, bool redelivered, SqsReceiveEndpointContext context, ClientContext clientContext,
            ReceiveSettings settings, ConnectionContext connectionContext)
            : base(redelivered, context, settings, clientContext, connectionContext)
        {
            TransportMessage = message;

            _body = new StringMessageBody(message?.Body);
        }

        protected override IHeaderProvider HeaderProvider => new AmazonSqsHeaderProvider(TransportMessage);

        public override MessageBody Body => _body;

        public void OverwriteMessageBody(string message) => _body = new StringMessageBody(message);

        public Message TransportMessage { get; }

        public Dictionary<string, MessageAttributeValue> Attributes => TransportMessage.MessageAttributes;

        public IDictionary<string, object> GetTransportProperties()
        {
            var properties = new Lazy<Dictionary<string, object>>(() => new Dictionary<string, object>());

            if (TransportMessage.Attributes.TryGetValue(MessageSystemAttributeName.MessageGroupId, out var messageGroupId)
                && !string.IsNullOrWhiteSpace(messageGroupId))
                properties.Value[AmazonSqsTransportPropertyNames.GroupId] = messageGroupId;

            if (TransportMessage.Attributes.TryGetValue(MessageSystemAttributeName.MessageDeduplicationId, out var messageDeduplicationId)
                && !string.IsNullOrWhiteSpace(messageDeduplicationId))
                properties.Value[AmazonSqsTransportPropertyNames.DeduplicationId] = messageDeduplicationId;

            return properties.IsValueCreated ? properties.Value : null;
        }
    }
}
