namespace MassTransit.Transports
{
    using System;


    public class FatalException : Exception
    {
        const string DefaultMessage = "Throw this exception to exit MassTransit pipeline and skip all the following built-in error handling";

        public FatalException()
            : base(DefaultMessage, null)
        {
        }

        public FatalException(Exception innerException)
            : this(DefaultMessage,
                innerException)
        {
        }

        public FatalException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
