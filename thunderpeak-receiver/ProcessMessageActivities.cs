using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;
using System.Text;
using Hl7.Fhir.Model;

namespace thunderpeak_receiver
{
    public static class ProcessMessageActivities
    {
        /// <summary>
        /// Updates the Message DTO with an identifier and a date received timestamp and then stores it
        /// </summary>
        /// <param name="context">DurableActivityContext</param>
        /// <param name="log">Logger</param>
        /// <returns>Updated Message DTO</returns>
        [FunctionName(nameof(A_StoreRawMessage))]
        public static async Task<Message> A_StoreRawMessage([ActivityTrigger] IDurableActivityContext context,
            IBinder binder,
            ILogger log)
        {
            var originalMessage = context.GetInput<Message>();
            Message message = new Message()
            {
                Content = originalMessage.Content,
                Id = Guid.NewGuid(),
                DateReceived = DateTimeOffset.Now,
            };

            var base64EncodedContent = System.Convert.FromBase64String(message.Content);
            var content = System.Text.Encoding.UTF8.GetString(base64EncodedContent);
            var trimmedContent = content.TrimStart();

            if (trimmedContent.StartsWith("MSH") || trimmedContent.StartsWith("BHS"))
            {
                message.ContentFormat = ContentFormat.Hl7v251;
            }
            else if (trimmedContent.StartsWith("{"))
            {
                message.ContentFormat = ContentFormat.Json;
            }

            log.LogInformation($"Message DTO updated successfully. ID = {message.Id.ToString()}");

            var outputBlob = await binder.BindAsync<CloudBlockBlob>(
                new BlobAttribute($"messages-raw/{message.Id.ToString()}.{message.GetPreferredFileExtension()}")
                {
                    Connection = "AzureWebJobsStorage"
                });

            SetBlobMetadata(outputBlob, message);
            outputBlob.Metadata.Add("length", content.Length.ToString());

            //string outputJson = JsonSerializer.Serialize(message, typeof(Message), new JsonSerializerOptions() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

            using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(content)))
            {
                await outputBlob.UploadFromStreamAsync(stream);
            }

            log.LogInformation($"Message DTO stored. ID = {message.Id.ToString()}");
            return message;
        }

        [FunctionName(nameof(A_FormatMessage))]
        public static async Task<Case> A_FormatMessage([ActivityTrigger] IDurableActivityContext context,
            IBinder binder,
            ILogger log)
        {
            var message = context.GetInput<Message>();

            var base64EncodedContent = System.Convert.FromBase64String(message.Content);
            var content = System.Text.Encoding.UTF8.GetString(base64EncodedContent).Trim();

            var outputBlob = await binder.BindAsync<CloudBlockBlob>(
                new BlobAttribute($"messages-fhir/{message.Id.ToString()}.json")
                {
                    Connection = "AzureWebJobsStorage"
                });

            SetBlobMetadata(outputBlob, message);

            string fhirContent = content;
            Case ecr = new Case();

            if (message.ContentFormat == ContentFormat.Hl7v251)
            {
                Cdc.Surveillance.Converters.FhirConverter converter = new Cdc.Surveillance.Converters.FhirConverter();
                ecr = converter.Convert(content);
                fhirContent = JsonSerializer.Serialize(ecr);
            }

            outputBlob.Metadata.Add("length", fhirContent.Length.ToString());

            using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(fhirContent)))
            {
                await outputBlob.UploadFromStreamAsync(stream);
            }

            log.LogInformation($"Formatted message with ID = {message.Id.ToString()}");

            return ecr;
        }

        private static void SetBlobMetadata(CloudBlockBlob blob, Message message)
        {
            var base64EncodedContent = System.Convert.FromBase64String(message.Content);
            var content = System.Text.Encoding.UTF8.GetString(base64EncodedContent);

            var (format, contentType) = Common.DetermineContentType(content);
            blob.Properties.ContentType = contentType;

            blob.Metadata.Add("format", format);
            blob.Metadata.Add("sha256", message.Hash);
            blob.Metadata.Add("sender", message.Sender);
            blob.Metadata.Add("id", message.Id.ToString());
            blob.Metadata.Add("datereceived", message.DateReceived.ToString("u"));
        }
    }
}
