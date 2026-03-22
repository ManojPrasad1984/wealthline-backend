using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;
using Wealthline.Functions.Functions.Data;
using Wealthline.Functions.Functions.Services;

namespace Wealthline.Functions.Functions
{
    public class AgentFunction
    {
       
        private readonly ApplicationDbContext _context;
        private readonly AgentService _agentService;

        public AgentFunction(ApplicationDbContext context, AgentService agentService)
        {
            _context = context;
            _agentService = agentService;
        }
        [Function("CreateAgent")]

        [OpenApiOperation(operationId: "CreateAgent", tags: new[] { "Agent" })]
        [OpenApiRequestBody("application/json", typeof(Agent), Required = true)]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(object))]
        // ✅ CREATE AGENT (POST)
        
        public async Task<HttpResponseData> CreateAgent(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();

                var model = JsonSerializer.Deserialize<Agent>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (model == null)
                    return await Fail(req, "Invalid data");

                // 🔹 Generate Agent Code (same logic)
                model.AgentCode = GenerateAgentCode();

                model.CreatedAt = DateTime.UtcNow;
                model.Id = Guid.NewGuid();

                _context.Agents.Add(model);
                await _context.SaveChangesAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);

                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    message = "Agent created successfully",
                    data = model
                });

                return response;
            }
            catch (Exception ex)
            {
                return await Fail(req, "Error: " + ex.Message);
            }
        }

        // ✅ GET ALL AGENTS (Optional but useful)
        [Function("GetAgents")]
        [OpenApiOperation(operationId: "GetAgents", tags: new[] { "Agent" },
        Summary = "Get all agents",
        Description = "Returns list of all agents")]

        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(List<Agent>),
        Description = "List of agents")]
        public async Task<HttpResponseData> GetAgents(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var agents = await _context.Agents
                .OrderByDescending(a => a.CreatedAt)
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(agents);

            return response;
        }

        // ✅ GENERATE AGENT CODE (same as MVC logic)
        private string GenerateAgentCode()
        {
            var lastAgent = _context.Agents
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefault();

            int nextNumber = 1;

            if (lastAgent != null && !string.IsNullOrEmpty(lastAgent.AgentCode))
            {
                var numberPart = lastAgent.AgentCode.Replace("AGT", "");
                int.TryParse(numberPart, out nextNumber);
                nextNumber++;
            }

            return "AGT" + nextNumber.ToString("D3");
        }

        // 🔧 HELPER: FAIL RESPONSE
        private async Task<HttpResponseData> Fail(HttpRequestData req, string message)
        {
            var res = req.CreateResponse(HttpStatusCode.BadRequest);

            await res.WriteAsJsonAsync(new
            {
                success = false,
                message
            });

            return res;
        }
    }
}