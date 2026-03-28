using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.OpenApi.Models;
using Razorpay.Api;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Wealthline.Functions.Functions.Data;
using Wealthline.Functions.Functions.Services;
using Wealthline.Functions.Models;

namespace Wealthline.Functions.Functions
{
    public class LuckyDrawFunction
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        private readonly string _razorKey;
        private readonly string _razorSecret;
        private readonly int _entryAmount;

        public LuckyDrawFunction(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;

            _razorKey = _config["RazorpayKey"];
            _razorSecret = _config["RazorpaySecret"];
            _entryAmount = Convert.ToInt32(_config["LuckyDrawEntryAmount"]);
        }

        // ✅ GET APPLY DATA (Agents + Config)
        [Function("GetApplyData")]

        [OpenApiOperation(operationId: "GetApplyData", tags: new[] { "LuckyDraw" },
          Summary = "Get apply page data",
          Description = "Returns entry amount, Razorpay key, and active agents")]

        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(object),
          Description = "Apply data response")]
        public async Task<HttpResponseData> GetApplyData(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            var agents = await _context.Agents
                .Where(a => a.IsActive)
                .Select(a => new
                {
                    a.Id,
                    a.AgentName,
                    a.AgentCode
                })
                .ToListAsync();

            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new
            {
                entryAmount = _entryAmount,
                razorKey = _razorKey,
                agents
            });

            return response;
        }

        // ✅ CREATE RAZORPAY ORDER
        [Function("CreateOrder")]

        [OpenApiOperation(operationId: "CreateOrder", tags: new[] { "LuckyDraw" },
          Summary = "Create Razorpay order",
          Description = "Creates Razorpay order for payment")]

        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(object),
          Description = "Order details")]
        public async Task<HttpResponseData> CreateOrder(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            RazorpayClient client = new RazorpayClient(_razorKey, _razorSecret);

            var options = new Dictionary<string, object>
            {
                { "amount", _entryAmount * 100 }, // in paise
                { "currency", "INR" },
                { "receipt", "LuckyDraw_" + DateTime.Now.Ticks }
            };

            Order order = client.Order.Create(options);

            var response = req.CreateResponse(HttpStatusCode.OK);

            await response.WriteAsJsonAsync(new
            {
                orderId = order["id"].ToString(),
                amount = _entryAmount
            });

            return response;
        }

        // ✅ VERIFY PAYMENT + SAVE ENTRY
        [Function("VerifyPayment")]

        [OpenApiOperation(operationId: "VerifyPayment", tags: new[] { "LuckyDraw" },
         Summary = "Verify payment",
         Description = "Verifies Razorpay payment and saves entry")]

        [OpenApiRequestBody("application/json", typeof(PaymentRequest), Required = true,
         Description = "Payment request data")]

        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(object),
         Description = "Payment verification result")]
        public async Task<HttpResponseData> VerifyPayment(
            [HttpTrigger(AuthorizationLevel.Anonymous, "post")] HttpRequestData req)
        {
            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var request = JsonSerializer.Deserialize<PaymentRequest>(body,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (request?.Entry == null)
                    return await Fail(req, "Invalid request");

                var entry = request.Entry;

                // 🔍 VALIDATIONS
                if (string.IsNullOrWhiteSpace(entry.FullName))
                    return await Fail(req, "Full Name is required");

                if (!Regex.IsMatch(entry.MobileNumber, "^[6-9][0-9]{9}$"))
                    return await Fail(req, "Invalid mobile number");

                if (!Regex.IsMatch(entry.AadhaarNumber, "^[0-9]{12}$"))
                    return await Fail(req, "Invalid Aadhaar number");

                // 🚫 Duplicate payment check
                bool paymentExists = await _context.LuckyDrawEntries
                    .AnyAsync(x => x.PaymentId == request.razorpay_payment_id);

                if (paymentExists)
                    return await Fail(req, "Payment already processed");

                // 🔐 VERIFY SIGNATURE
                string payload = request.razorpay_order_id + "|" + request.razorpay_payment_id;
                string generatedSignature = GenerateSignature(payload);

                if (generatedSignature != request.razorpay_signature)
                    return await Fail(req, "Payment verification failed");

                // ✅ SAVE ENTRY
                entry.PaymentId = request.razorpay_payment_id;
                entry.RazorpayOrderId = request.razorpay_order_id;
                entry.PaymentStatus = true;
                entry.EntryDate = DateTime.UtcNow;
                entry.EntryAmount = _entryAmount;
                entry.PrizeChoice = entry.PrizeChoice ?? "No Prize";
                entry.AgentId = entry.AgentId == Guid.Empty ? null : entry.AgentId;
                entry.CardNumber = GenerateCardNumber();

                _context.LuckyDrawEntries.Add(entry);
                await _context.SaveChangesAsync();

                var response = req.CreateResponse(HttpStatusCode.OK);

                await response.WriteAsJsonAsync(new
                {
                    success = true,
                    card = entry.CardNumber
                });

                return response;
            }
            catch (Exception ex)
            {
                return await Fail(req, "Unexpected error: " + ex.Message);
            }
        }

        // ✅ DOWNLOAD RECEIPT (PDF)
        [Function("DownloadReceipt")]

        [OpenApiOperation(operationId: "DownloadReceipt", tags: new[] { "LuckyDraw" },
        Summary = "Download receipt",
        Description = "Downloads PDF receipt using card number")]

        [OpenApiParameter(name: "card", In = ParameterLocation.Query, Required = true,
        Type = typeof(string), Description = "Card number")]

        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/pdf", typeof(byte[]),
        Description = "PDF receipt file")]
        public async Task<HttpResponseData> DownloadReceipt(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get")] HttpRequestData req)
        {
            try
            {
                var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string card = query["card"];

                if (string.IsNullOrWhiteSpace(card))
                {
                    return await Fail(req, "Card number is required", HttpStatusCode.BadRequest);
                }

                var entry = await _context.LuckyDrawEntries
                    .Include(x => x.Agent)
                    .FirstOrDefaultAsync(x => x.CardNumber == card);

                if (entry == null)
                {
                    return await Fail(req, "Receipt not found", HttpStatusCode.NotFound);
                }

                var pdf = PremiumReceiptService.Generate(entry);

                if (pdf == null || pdf.Length == 0)
                {
                    return await Fail(req, "Receipt generation failed", HttpStatusCode.InternalServerError);
                }

                var fileName = $"LuckyDraw_{entry.CardNumber}.pdf";
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "application/pdf");
                response.Headers.Add("Content-Disposition", $"attachment; filename=\"{fileName}\"");
                response.Headers.Add("Content-Length", pdf.Length.ToString());
                response.Headers.Add("Cache-Control", "no-store, no-cache, must-revalidate");
                response.Headers.Add("Pragma", "no-cache");
                response.Headers.Add("Access-Control-Expose-Headers", "Content-Disposition, Content-Length, Content-Type");

                await response.Body.WriteAsync(pdf, 0, pdf.Length);

                return response;
            }
            catch (Exception ex)
            {
                return await Fail(req, "Unexpected error while generating receipt: " + ex.Message,
                    HttpStatusCode.InternalServerError);
            }
        }

        // 🔧 HELPER: FAIL RESPONSE
        private async Task<HttpResponseData> Fail(HttpRequestData req, string message,
            HttpStatusCode statusCode = HttpStatusCode.BadRequest)
        {
            var res = req.CreateResponse(statusCode);
            await res.WriteAsJsonAsync(new
            {
                success = false,
                message
            });
            return res;
        }

        // 🔐 HELPER: SIGNATURE
        private string GenerateSignature(string payload)
        {
            byte[] keyBytes = Encoding.UTF8.GetBytes(_razorSecret);
            byte[] payloadBytes = Encoding.UTF8.GetBytes(payload);

            using var hmac = new HMACSHA256(keyBytes);

            return BitConverter.ToString(hmac.ComputeHash(payloadBytes))
                .Replace("-", "")
                .ToLower();
        }

        // 🔢 HELPER: CARD NUMBER
        private string GenerateCardNumber()
        {
            var last = _context.LuckyDrawEntries
                .OrderByDescending(x => x.Id)
                .FirstOrDefault();

            int next = last == null ? 1 : last.Id + 1;

            return $"WL-{DateTime.Now.Year}-{next:D5}";
        }
    }
}
