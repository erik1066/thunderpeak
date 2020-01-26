using System.Security.Cryptography;
using System.Text;

namespace thunderpeak_receiver
{
    public static class Common
    {
        public const string FORMAT_TYPE_FHIR = "fhir";
        public const string FORMAT_TYPE_HL7V2 = "hl7v2";

        public static string ComputeSha256Hash(string rawData)
        {
            // Create a SHA256   
            using (SHA256 sha256Hash = SHA256.Create())
            {
                // ComputeHash - returns byte array  
                byte[] bytes = sha256Hash.ComputeHash(Encoding.UTF8.GetBytes(rawData));

                // Convert byte array to a string   
                StringBuilder builder = new StringBuilder();
                for (int i = 0; i < bytes.Length; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }
                return builder.ToString();
            }
        }

        public static (string Format, string ContentType) DetermineContentType(string message)
        {
            string trimmedMessage = message.TrimStart();
            string format = FORMAT_TYPE_HL7V2;
            string contentType = "text/plain";

            if (trimmedMessage.StartsWith("{"))
            {
                // FHIR
                contentType = "application/json";
                format = FORMAT_TYPE_FHIR;
            }
            else if (trimmedMessage.StartsWith("MSH"))
            {
                // single HL7 message - not batched
                contentType = "text/plain";
            }
            else if (trimmedMessage.StartsWith("BHS"))
            {
                // HL7 batch (TODO: Should we bother with this??)
                contentType = "text/plain";
            }

            return (format, contentType);
        }
    }
}
