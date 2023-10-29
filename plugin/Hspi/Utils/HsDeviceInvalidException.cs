using System;
using System.Runtime.Serialization;

namespace Hspi.Utils
{
    [Serializable]
    public sealed class HsDeviceInvalidException : ApplicationException
    {
        public HsDeviceInvalidException()
        {
        }

        public HsDeviceInvalidException(string message) : base(message)
        {
        }

        public HsDeviceInvalidException(string message, Exception innerException) : base(message, innerException)
        {
        }

        private HsDeviceInvalidException(SerializationInfo serializationInfo, StreamingContext streamingContext) :
            base(serializationInfo, streamingContext)
        {
        }
    }
}