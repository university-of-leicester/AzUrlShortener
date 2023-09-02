/*
```c#
Input:
    {
         // [Required]
        "PartitionKey": "d",

         // [Required]
        "RowKey": "doc",

        // [Optional] all other properties
    }

Output:
    {
        "ShortUrl": "http://c5m.ca/azFunc",
        "LongUrl": "https://docs.microsoft.com/en-ca/azure/azure-functions/functions-create-your-first-function-visual-studio"
    }
*/

using Cloud5mins.ShortenerTools.Core.Domain;
using Cloud5mins.ShortenerTools.Core.Messages;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using System;
using System.IO;
using System.Net;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Cloud5mins.ShortenerTools.Functions
{

    public class UrlDelete
    {
        private readonly ILogger _logger;
        private readonly ShortenerSettings _settings;

        public UrlDelete(ILoggerFactory loggerFactory, ShortenerSettings settings)
        {
            _logger = loggerFactory.CreateLogger<UrlList>();
            _settings = settings;
        }

        [Function("UrlDelete")]
        public async Task<HttpResponseData> Run(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post", Route = "api/UrlDelete")] HttpRequestData req,
            ExecutionContext context
        )
        {
            _logger.LogInformation($"__trace deleting shortURL: {req}");
            string userId = string.Empty;
            ShortUrlEntity input;
            ShortUrlEntity result;
            try
            {
                // Validation of the inputs
                if (req == null)
                {
                    return req.CreateResponse(HttpStatusCode.NotFound);
                }

                using (var reader = new StreamReader(req.Body))
                {
                    var body = await reader.ReadToEndAsync();
                    input = JsonSerializer.Deserialize<ShortUrlEntity>(body, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (input == null)
                    {
                        return req.CreateResponse(HttpStatusCode.NotFound);
                    }
                }

                StorageTableHelper stgHelper = new StorageTableHelper(_settings.DataStorage);

                ShortUrlEntity eShortUrl = await stgHelper.GetShortUrlEntity(input);

                if ( ! (eShortUrl?.IsArchived??false))
                {
                    var notArchivedResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await notArchivedResponse.WriteAsJsonAsync(new { message = "Not found or not archived", requested=input, found=eShortUrl });
                    return notArchivedResponse;
                }

                result = await stgHelper.DeleteShortUrlEntity(eShortUrl);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An unexpected error was encountered.");
                var badRequest = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequest.WriteAsJsonAsync(new { ex.Message });
                return badRequest;
            }

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(result);
            return response;
        }
    }
}
