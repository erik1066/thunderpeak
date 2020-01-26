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
using Hl7.Fhir.Serialization;
using System.Collections.Generic;
using System.Linq;
using JUST;

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

        /// <summary>
        /// Transforms a message into a FHIR eCR resource
        /// </summary>
        /// <param name="context">DurableActivityContext</param>
        /// <param name="binder">Binder for Blob storage where the transformed eCR will reside</param>
        /// <param name="log">Logger</param>
        /// <returns>eCR FHIR resource</returns>
        [FunctionName(nameof(A_FormatMessage))]
        public static async Task<string> A_FormatMessage([ActivityTrigger] IDurableActivityContext context,
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
            outputBlob.Properties.ContentType = "application/json";

            Bundle ecr = new Bundle();

            FhirJsonSerializer fhirJsonSerializer = new FhirJsonSerializer();
            fhirJsonSerializer.Settings.Pretty = true;

            if (message.ContentFormat == ContentFormat.Hl7v251)
            {
                // if in HL7v2 format, convert to FHIR format using our special v2-to-FHIR format converter
                Cdc.Surveillance.Converters.EcrFhirConverter converter = new Cdc.Surveillance.Converters.EcrFhirConverter();
                ecr = converter.Convert(content, message.Id.ToString());
            }
            else if (message.ContentFormat == ContentFormat.Json)
            {
                // if it's already in FHIR, we may not need to do anything, but let's try and do a basic validation on it by parsing it... TODO see if we really need to do anything here at all, can we just drop it into storage?
                FhirJsonParser parser = new FhirJsonParser();
                parser.Settings.PermissiveParsing = true;
                ecr = parser.Parse<Bundle>(content);
            }

            string fhirContent = fhirJsonSerializer.SerializeToString(ecr);
            outputBlob.Metadata.Add("length", fhirContent.Length.ToString());

            using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(fhirContent)))
            {
                await outputBlob.UploadFromStreamAsync(stream);
            }

            log.LogInformation($"Formatted message with ID = {message.Id.ToString()}");

            return fhirContent;
        }

        /// <summary>
        /// Transforms a FHIR eCR resource into a flattened Json format
        /// </summary>
        /// <param name="context">DurableActivityContext</param>
        /// <param name="binder">Binder for Blob storage where the transformed Json will reside</param>
        /// <param name="log">Logger</param>
        /// <returns>Flattened Json</returns>
        [FunctionName(nameof(A_FlattenMessage))]
        public static async Task<string> A_FlattenMessage([ActivityTrigger] IDurableActivityContext context,
            IBinder binder,
            ILogger log)
        {
            var messageEcrPair = context.GetInput<MessageEcrPair>();

            string ecr = messageEcrPair.Ecr;
            Message message = messageEcrPair.Message;

            var outputBlob = await binder.BindAsync<CloudBlockBlob>(
                new BlobAttribute($"messages-flattened/{message.Id.ToString()}.json")
                {
                    Connection = "AzureWebJobsStorage"
                });

            SetBlobMetadata(outputBlob, message);
            outputBlob.Properties.ContentType = "application/json";

            var transformedJson = TransformJson(ecr);

            using (Stream stream = new MemoryStream(Encoding.UTF8.GetBytes(transformedJson)))
            {
                await outputBlob.UploadFromStreamAsync(stream);
            }

            log.LogInformation($"Flattened Json for message with ID = {message.Id.ToString()}");

            return transformedJson;
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

        private static string TransformJson(string json)
        {

            string transformDocument = @"
{
    ""profile"": ""#valueof($.id)"",
    ""patient.identifier"": ""#valueof($.entry[?(@.resource.resourceType=='Patient')].resource.identifier[0].value)"",
    ""patient.gender"": ""#valueof($.entry[?(@.resource.resourceType=='Patient')].resource.gender)"",
    ""patient.birthDate"": ""#valueof($.entry[?(@.resource.resourceType=='Patient')].resource.birthDate)"",
    ""patient.deceasedDateTime"": ""#valueof($.entry[?(@.resource.resourceType=='Patient')].resource.deceasedDateTime)"",
    
    ""patient.address.city"": ""#ifcondition(#exists( $.entry[?(@.resource.resourceType=='Patient')].resource.address[0] ),true,#valueof($.entry[?(@.resource.resourceType=='Patient')].resource.address[0].city))"",
    ""patient.address.county"": ""#ifcondition(#exists( $.entry[?(@.resource.resourceType=='Patient')].resource.address[0] ),true,#valueof($.entry[?(@.resource.resourceType=='Patient')].resource.address[0].district))"",
    ""patient.address.state"": ""#ifcondition(#exists( $.entry[?(@.resource.resourceType=='Patient')].resource.address[0] ),true,#valueof($.entry[?(@.resource.resourceType=='Patient')].resource.address[0].state))"",
    ""patient.address.postalCode"": ""#ifcondition(#exists( $.entry[?(@.resource.resourceType=='Patient')].resource.address[0] ),true,#valueof($.entry[?(@.resource.resourceType=='Patient')].resource.address[0].postalCode))"",
    ""patient.address.country"": ""#ifcondition(#exists( $.entry[?(@.resource.resourceType=='Patient')].resource.address[0] ),true,#valueof($.entry[?(@.resource.resourceType=='Patient')].resource.address[0].country))"",

    ""patient.ethnicity"": ""#ifcondition(#exists($.entry[?(@.resource.resourceType=='Patient')].resource.extension[?(@.url=='http://hl7.org/fhir/us/core/StructureDefinition/us-core-ethnicity')]),true, #valueof($.entry[?(@.resource.resourceType=='Patient')].resource.extension[?(@.url=='http://hl7.org/fhir/us/core/StructureDefinition/us-core-ethnicity')].extension[0].valueCoding.display) )"",
    ""patient.ethnicityCode"": ""#ifcondition(#exists($.entry[?(@.resource.resourceType=='Patient')].resource.extension[?(@.url=='http://hl7.org/fhir/us/core/StructureDefinition/us-core-ethnicity')]),true, #valueof($.entry[?(@.resource.resourceType=='Patient')].resource.extension[?(@.url=='http://hl7.org/fhir/us/core/StructureDefinition/us-core-ethnicity')].extension[0].valueCoding.code) )"",

    ""patient.race"": ""#ifcondition(#exists($.entry[?(@.resource.resourceType=='Patient')].resource.extension[?(@.url=='http://hl7.org/fhir/us/core/StructureDefinition/us-core-race')]),true, #valueof($.entry[?(@.resource.resourceType=='Patient')].resource.extension[?(@.url=='http://hl7.org/fhir/us/core/StructureDefinition/us-core-race')].extension[0].valueCoding.display) )"",
    ""patient.raceCode"": ""#ifcondition(#exists($.entry[?(@.resource.resourceType=='Patient')].resource.extension[?(@.url=='http://hl7.org/fhir/us/core/StructureDefinition/us-core-race')]),true, #valueof($.entry[?(@.resource.resourceType=='Patient')].resource.extension[?(@.url=='http://hl7.org/fhir/us/core/StructureDefinition/us-core-race')].extension[0].valueCoding.code) )"",
}";

            //            string transformDocument = @"
            //{
            //    ""profile"": ""#valueof($.identifier[?(@.use=='official')].value)"",
            //    ""legacyCaseId"": ""#valueof($.identifier[?(@.use=='old')].value)"",
            //    ""patient.gender"": ""#valueof($..patient.gender)"",
            //    ""patient.birthDate"": ""#valueof($.patient.birthDate)"",
            //    ""patient.deceasedDateTime"": ""#valueof($.patient.deceasedDateTime)"",
            //    ""patient.address.city"": ""#ifcondition(#exists($.patient.address[0]),true,#valueof($.patient.address[0].city))"",
            //    ""patient.address.county"": ""#ifcondition(#exists($.patient.address[0]),true,#valueof($.patient.address[0].district))"",
            //    ""patient.address.state"": ""#ifcondition(#exists($.patient.address[0]),true,#valueof($.patient.address[0].state))"",
            //    ""patient.address.zip"": ""#ifcondition(#exists($.patient.address[0]),true,#valueof($.patient.address[0].postalCode))"",
            //    ""patient.address.country"": ""#ifcondition(#exists($.patient.address[0]),true,#valueof($.patient.address[0].country))"",

            //    ""exposureAddress.city"": ""#ifcondition(#exists($.exposureAddress[0]),true,#valueof($.exposureAddress[0].city))"",
            //    ""exposureAddress.county"": ""#ifcondition(#exists($.exposureAddress[0]),true,#valueof($.exposureAddress[0].district))"",
            //    ""exposureAddress.state"": ""#ifcondition(#exists($.exposureAddress[0]),true,#valueof($.exposureAddress[0].state))"",
            //    ""exposureAddress.zip"": ""#ifcondition(#exists($.exposureAddress[0]),true,#valueof($.exposureAddress[0].postalCode))"",
            //    ""exposureAddress.country"": ""#ifcondition(#exists($.exposureAddress[0]),true,#valueof($.exposureAddress[0].country))"",

            //    ""importedAddress.city"": ""#ifcondition(#exists($.importedAddress[0]),true,#valueof($.importedAddress[0].city))"",
            //    ""importedAddress.county"": ""#ifcondition(#exists($.importedAddress[0]),true,#valueof($.importedAddress[0].district))"",
            //    ""importedAddress.state"": ""#ifcondition(#exists($.importedAddress[0]),true,#valueof($.importedAddress[0].state))"",
            //    ""importedAddress.zip"": ""#ifcondition(#exists($.importedAddress[0]),true,#valueof($.importedAddress[0].postalCode))"",
            //    ""importedAddress.country"": ""#ifcondition(#exists($.importedAddress[0]),true,#valueof($.importedAddress[0].country))"",

            //    ""transmissionMode"": ""#ifcondition(#exists($.transmissionMode),true,#valueof($.transmissionMode.text))"",
            //    ""outbreak"": ""#ifcondition(#exists($.outbreak),true,#valueof($.outbreak))"",
            //    ""resultStatus"": ""#ifcondition(#exists($.resultStatus),true,#valueof($.resultStatus))"",
            //    ""importedIndicator"": ""#ifcondition(#exists($.importedIndicator),true,#valueof($.importedIndicator.text))"",
            //    ""multinationalReportingCriteria"": ""#ifcondition(#exists($.multinationalReportingCriteria),true,#valueof($.multinationalReportingCriteria.text))"",
            //}";

            string transformedString = JsonTransformer.Transform(transformDocument, json);

            return transformedString;
        }

        public static Dictionary<string, JsonElement> FlattenJson(string json)
        {
            IEnumerable<(string Path, JsonProperty P)> GetLeaves(string path, JsonProperty p)
                => p.Value.ValueKind != JsonValueKind.Object
                    ? new[] { (Path: path == null ? p.Name : path + "." + p.Name, p) }
                    : p.Value.EnumerateObject().SelectMany(child => GetLeaves(path == null ? p.Name : path + "." + p.Name, child));

            using (JsonDocument document = JsonDocument.Parse(json)) // Optional JsonDocumentOptions options
                return document.RootElement.EnumerateObject()
                    .SelectMany(p => GetLeaves(null, p))
                    .ToDictionary(k => k.Path, v => v.P.Value.Clone()); //Clone so that we can use the values outside of using
        }
    }
}
