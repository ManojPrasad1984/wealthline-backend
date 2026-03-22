namespace Wealthline.Functions.Models
{
    public class PaymentRequest
    {
        public LuckyDrawEntry Entry { get; set; }

        public string razorpay_payment_id { get; set; }

        public string razorpay_order_id { get; set; }

        public string razorpay_signature { get; set; }
    }
}
