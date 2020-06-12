using System;
using Google.Protobuf;

namespace Exchange.Core.Extensions
{
    public static class ByteStringGuidExtensions
    {
        public static ByteString ToByteString(this Guid guid) => ByteString.CopyFrom(guid.ToByteArray());
        public static Guid ToGuid(this ByteString byteString) => new Guid(byteString.Span);
    }
}