using Hl7.Fhir.Model;

namespace thunderpeak_receiver
{
    public sealed class MessageEcrPair
    {
        public Message Message { get; set; }
        public string Ecr { get; set; }
    }
}
