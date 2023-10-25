﻿using System;
using System.Runtime.Serialization;

namespace Hspi.Utils
{
    [Serializable]
    public sealed class HsDeviceInvalidException : Exception
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

        protected HsDeviceInvalidException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}