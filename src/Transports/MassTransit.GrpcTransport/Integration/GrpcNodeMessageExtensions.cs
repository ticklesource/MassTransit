namespace MassTransit.GrpcTransport.Integration
{
    using System.Threading.Tasks;
    using Contracts;
    using Fabric;


    public static class GrpcNodeMessageExtensions
    {
        public static ValueTask SendWelcome(this IGrpcNode node, IGrpcNode hostNode)
        {
            return node.Writer.WriteAsync(new TransportMessage {Welcome = new Welcome {Node = new Node().Initialize(hostNode)}});
        }

        public static ValueTask DeliverMessage(this IGrpcNode node, GrpcTransportMessage message)
        {
            return node.Writer.WriteAsync(message.Message);
        }
    }
}
