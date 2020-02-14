using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Hl7.Fhir.Model;

namespace thunderpeak_receiver
{
    public static class ProcessMessageStartup
    {
        [FunctionName("ProcessMessageStartup")]
        public static async Task<IActionResult> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest request,
            [DurableClient] IDurableOrchestrationClient starter,
            ILogger log)
        {
            log.LogInformation("Http POST received");

            // Extract the HTTP payload, convert to base 64, and create a 'Message' DTO for transporting the data across the Azure workflow
            string requestBody = await new StreamReader(request.Body).ReadToEndAsync();
            byte[] requestBodyBytes = System.Text.Encoding.UTF8.GetBytes(requestBody);
            string base64content = System.Convert.ToBase64String(requestBodyBytes);
            Message message = new Message()
            {
                Content = base64content,
            };

            log.LogInformation("Message object created");

            // Start the Azure workflow
            string instanceId = await starter.StartNewAsync<Message>("Orchestrator", message);

            log.LogInformation($"Starting orchestration with ID = '{instanceId}'...");

            return starter.CreateCheckStatusResponse(request, instanceId);
        }

        [FunctionName("Orchestrator")]
        public static async Task<object> RunOrchestrator(
            [OrchestrationTrigger] IDurableOrchestrationContext context,
            ILogger log)
        {
            Message message = context.GetInput<Message>();

            if (!context.IsReplaying)
            {
                log.LogInformation("Calling activity functions...");
            }

            try
            {
                Message updatedMessage = await context.CallActivityAsync<Message>(nameof(ProcessMessageActivities.A_StoreRawMessage), message);
                string ecr = await context.CallActivityAsync<string>(nameof(ProcessMessageActivities.A_FormatMessage), updatedMessage);

                MessageEcrPair messageEcrPair = new MessageEcrPair()
                {
                    Message = updatedMessage,
                    Ecr = ecr
                };

                string transform = await context.CallActivityAsync<string>(nameof(ProcessMessageActivities.A_FlattenMessage), messageEcrPair);

                return new
                {
                    InstanceId = context.InstanceId,
                };

            }
            catch (Exception ex)
            {
                if (!context.IsReplaying)
                {
                    log.LogInformation($"Caught an exception: {ex.Message}");
                }

                await context.CallActivityAsync(nameof(ProcessMessageActivities.A_Cleanup), message.Id.ToString());

                return new
                {
                    Error = "Failed to process message",
                    Message = ex.Message,
                    InstanceId = context.InstanceId,
                };
            }
        }
    }
}
