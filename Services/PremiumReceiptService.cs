using QRCoder;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SkiaSharp;
using Wealthline.Functions.Models;

namespace Wealthline.Functions.Functions.Services
{
    public class PremiumReceiptService
    {
        public static byte[] Generate(LuckyDrawEntry entry)
        {
            try
            {
                var receiptId = $"WRR-{DateTime.Now:yyyyMMdd}-{entry.Id}";

                var qrBytes = GenerateQr($"https://wealthline.com/verify/{entry.CardNumber}");
                var barcodeBytes = GenerateBarcode(entry.CardNumber ?? "NA");

                // ✅ Signature Safe Load
                byte[] signatureBytes = null;
                try
                {
                    var path = Path.Combine(Directory.GetCurrentDirectory(), "signature", "signature.png");
                    if (File.Exists(path))
                        signatureBytes = File.ReadAllBytes(path);
                }
                catch { }

                var document = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Size(PageSizes.A4);
                        page.Margin(20);

                        page.Content().Column(col =>
                        {
                            col.Spacing(10);

                            // =========================
                            // TOP COUPON HEADER
                            // =========================
                            col.Item().Height(120).Row(row =>
                            {
                                // LEFT RED STRIP
                                row.ConstantItem(70)
                                    .Background("#C62828")
                                    .AlignCenter()
                                    .AlignMiddle()
                                    .RotateLeft()
                                    .Text($"No {entry.CardNumber ?? "NA"}")
                                    .FontColor(Colors.White)
                                    .FontSize(14)
                                    .Bold();

                                // RIGHT YELLOW PANEL
                                row.RelativeItem()
                                    .Background("#FFC107")
                                    .Padding(12)
                                    .Column(c =>
                                    {
                                        c.Item().AlignCenter()
                                            .Text("WEALTHLINE ROYAL RESIDENCES")
                                            .FontSize(14)
                                            .Bold();

                                        c.Item().AlignCenter()
                                            .Text("LUCKY DRAW COUPON")
                                            .FontSize(16)
                                            .Bold()
                                            .FontColor("#C62828");

                                        c.Item().Text($"Receipt No : {receiptId}").FontSize(10);
                                        c.Item().Text($"Participant : {entry.FullName ?? "-"}").FontSize(10);
                                        c.Item().Text($"Mobile : {entry.MobileNumber ?? "-"}").FontSize(10);
                                    });
                            });

                            // =========================
                            // TITLE
                            // =========================
                            col.Item().AlignCenter()
                                .Text("Lucky Draw Registration Receipt")
                                .FontSize(16)
                                .Bold();

                            col.Item().LineHorizontal(1);

                            // =========================
                            // DETAILS TABLE
                            // =========================
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(c =>
                                {
                                    c.ConstantColumn(160);
                                    c.RelativeColumn();
                                });

                                AddRow(table, "Address", entry.Address);
                                AddRow(table, "Aadhaar", entry.AadhaarNumber);
                                AddRow(table, "Selected Prize", entry.PrizeChoice);
                                AddRow(table, "Payment ID", entry.PaymentId);

                                AddRow(table, "Referred By",
                                    entry.Agent != null
                                        ? $"{entry.Agent.AgentName} ({entry.Agent.AgentCode})"
                                        : "N/A");

                                AddRow(table, "Payment Date",
                                    entry.EntryDate?.ToString("dd MMM yyyy hh:mm tt") ?? "-");
                            });

                            // =========================
                            // AMOUNT HIGHLIGHT
                            // =========================
                            col.Item().AlignCenter()
                                .Background("#F57C00")
                                .Padding(8)
                                .Text($"Registration Amount: ₹{entry.EntryAmount}")
                                .FontSize(14)
                                .Bold()
                                .FontColor(Colors.White);

                            // =========================
                            // QR + BARCODE
                            // =========================
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(c =>
                                {
                                    c.Item().Text("QR Verification").FontSize(10).Bold();
                                    if (qrBytes != null)
                                        c.Item().Width(90).Image(qrBytes);
                                });

                                row.RelativeItem().AlignRight().Column(c =>
                                {
                                    c.Item().Text("Card Barcode").FontSize(10).Bold();
                                    if (barcodeBytes != null)
                                        c.Item().Width(160).Image(barcodeBytes);
                                });
                            });

                            // =========================
                            // TERMS (FULL ORIGINAL)
                            // =========================
                            col.Item().Column(c =>
                            {
                                c.Item().Text("नियम एवं शर्तें (Terms & Conditions)")
                                    .Bold()
                                    .FontSize(10);

                                c.Item().Text("1. यह लकी ड्रॉ योजना सीमित समय के लिए है.").FontSize(8);
                                c.Item().Text("2. एक व्यक्ति केवल एक ही इनाम जीत सकता है.").FontSize(8);
                                c.Item().Text("3. विजेताओं का चयन कंप्यूटर / कूपन द्वारा रैंडम ड्रॉ से किया जाएगा.").FontSize(8);
                                c.Item().Text("4. 1000 Sq.ft प्लॉट चयनित लोकेशन में ही मान्य होगा. अन्य शुल्क विजेता द्वारा वहन किए जाएंगे.").FontSize(8);
                                c.Item().Text("5. LCD TV और सिल्वर कॉइन की ब्रांड उपलब्धता अनुसार दी जाएगी.").FontSize(8);
                                c.Item().Text("6. इनाम नकद में नहीं बदले जाएंगे.").FontSize(8);
                                c.Item().Text("7. गलत जानकारी देने पर एंट्री रद्द मानी जाएगी.").FontSize(8);
                                c.Item().Text("8. विजेताओं की घोषणा फोन / WhatsApp / ऑफिस नोटिस से की जाएगी.").FontSize(8);
                                c.Item().Text("9. विजेता को पहचान प्रमाण दिखाना अनिवार्य होगा.").FontSize(8);
                                c.Item().Text("10. आयोजक को योजना में बदलाव या ड्रॉ रद्द करने का अधिकार है.").FontSize(8);
                                c.Item().Text("11. आयोजक का निर्णय अंतिम होगा.").FontSize(8);
                                c.Item().Text("12. कूपन सुरक्षित रखना प्रतिभागी की जिम्मेदारी होगी.").FontSize(8);
                            });

                            // =========================
                            // SIGNATURE
                            // =========================
                            col.Item().PaddingTop(10).Row(row =>
                            {
                                row.RelativeItem();

                                row.ConstantItem(200).AlignCenter().Column(c =>
                                {
                                    if (signatureBytes != null)
                                        c.Item().Height(40).Image(signatureBytes);

                                    c.Item().Width(140).LineHorizontal(1);
                                    c.Item().Text("Authorized Signature").FontSize(8);
                                });
                            });

                            // =========================
                            // FOOTER
                            // =========================
                            col.Item().AlignCenter()
                                .Text("Thank you for participating in Wealthline Royal Residences")
                                .FontSize(8);
                        });
                    });
                });

                return document.GeneratePdf();
            }
            catch (Exception ex)
            {
                return Document.Create(c =>
                {
                    c.Page(p => p.Content().Text("Error generating receipt: " + ex.Message));
                }).GeneratePdf();
            }
        }

        // =========================
        // HELPERS
        // =========================

        static void AddRow(TableDescriptor table, string label, string value)
        {
            table.Cell().Padding(3).Text(label).Bold().FontSize(9);
            table.Cell().Padding(3).Text(value ?? "-").FontSize(9);
        }

        static byte[] GenerateQr(string text)
        {
            try
            {
                using var generator = new QRCodeGenerator();
                var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
                var qr = new PngByteQRCode(data);
                return qr.GetGraphic(20);
            }
            catch { return null; }
        }

        static byte[] GenerateBarcode(string text)
        {
            try
            {
                var barcode = new BarcodeStandard.Barcode();

                var skImage = barcode.Encode(
                    BarcodeStandard.Type.Code128,
                    text,
                    SKColors.Black,
                    SKColors.White,
                    260,
                    70);

                using var data = skImage.Encode(SKEncodedImageFormat.Png, 100);
                return data.ToArray();
            }
            catch { return null; }
        }
    }
}