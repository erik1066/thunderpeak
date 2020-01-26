using System;
using System.Collections.Generic;
using System.Text;

namespace thunderpeak_receiver
{
    public sealed class Message
    {
        private string _content = string.Empty;

        public Guid Id { get; set; }
        public string Content
        {
            get
            {
                return _content;
            }
            set
            {
                _content = value;
                Hash = Common.ComputeSha256Hash(Content);
            }
        }
        public DateTimeOffset DateReceived { get; set; }
        public string Hash { get; private set; }
        public string Sender { get; set; } = "Unknown";
        public ContentFormat ContentFormat { get; set; } = ContentFormat.Unknown;

        public string GetPreferredFileExtension()
        {
            return ContentFormat switch
            {
                ContentFormat.Json => ".json",
                ContentFormat.Xml => ".xml",
                ContentFormat.Hl7v251 => ".hl7",
                _ => ".txt"
            };
        }
    }

    public enum ContentFormat
    {
        Json,
        Xml,
        Netss,
        Hl7v251,
        Other,
        Unknown
    }
}
