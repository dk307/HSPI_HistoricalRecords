using System;
using System.Runtime.Serialization;

namespace Hspi.Database
{
    [Serializable]
    public class SqliteInvalidException : Exception
    {
        public SqliteInvalidException()
        {
        }

        public SqliteInvalidException(string message) : base(message)
        {
        }

        public SqliteInvalidException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected SqliteInvalidException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}