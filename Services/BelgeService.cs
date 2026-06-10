using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace ATOM.Services;

/// <summary>Excel (.xlsx) ve PDF belge üretimi.</summary>
public class BelgeService
{
    // ─── EXCEL ────────────────────────────────────────────────
    public byte[] ExcelTablo(string sayfaAdi, IList<string> basliklar, IEnumerable<IList<object?>> satirlar)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(KisaltSayfaAdi(sayfaAdi));

        for (int c = 0; c < basliklar.Count; c++)
        {
            var hucre = ws.Cell(1, c + 1);
            hucre.Value = basliklar[c];
            hucre.Style.Font.Bold = true;
            hucre.Style.Fill.BackgroundColor = XLColor.FromHtml("#1a3a6b");
            hucre.Style.Font.FontColor = XLColor.White;
        }

        int r = 2;
        foreach (var satir in satirlar)
        {
            for (int c = 0; c < satir.Count; c++)
            {
                var v = satir[c];
                var hucre = ws.Cell(r, c + 1);
                switch (v)
                {
                    case null: hucre.Value = ""; break;
                    case int i: hucre.Value = i; break;
                    case long l: hucre.Value = l; break;
                    case decimal d: hucre.Value = d; break;
                    case double db: hucre.Value = db; break;
                    case DateTime dt: hucre.Value = dt; break;
                    default: hucre.Value = v.ToString(); break;
                }
            }
            r++;
        }

        ws.Columns().AdjustToContents();
        ws.SheetView.FreezeRows(1);
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static string KisaltSayfaAdi(string s)
    {
        s = new string(s.Where(c => !"[]*?/\\:".Contains(c)).ToArray());
        return s.Length > 31 ? s.Substring(0, 31) : (string.IsNullOrEmpty(s) ? "Sayfa1" : s);
    }

    // ─── PDF (tablo raporu) ───────────────────────────────────
    public byte[] PdfTablo(string baslik, string altBilgi, IList<string> basliklar,
        IEnumerable<IList<string>> satirlar, string? mevzuat = null, bool yatay = false)
    {
        var doc = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(yatay ? PageSizes.A4.Landscape() : PageSizes.A4);
                page.Margin(1.2f, Unit.Centimetre);
                page.DefaultTextStyle(t => t.FontSize(9).FontFamily("Arial"));

                page.Header().Column(col =>
                {
                    col.Item().Text("T.C. BAKANLIK – TAŞINIR YÖNETİM SİSTEMİ (ATOM)")
                        .FontSize(11).Bold().AlignCenter();
                    col.Item().Text(baslik).FontSize(13).Bold().AlignCenter().FontColor(Colors.Blue.Darken3);
                    if (!string.IsNullOrEmpty(altBilgi))
                        col.Item().Text(altBilgi).FontSize(9).AlignCenter().FontColor(Colors.Grey.Darken1);
                    col.Item().PaddingTop(4).LineHorizontal(1).LineColor(Colors.Grey.Medium);
                });

                page.Content().PaddingVertical(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        for (int i = 0; i < basliklar.Count; i++) cols.RelativeColumn();
                    });

                    table.Header(header =>
                    {
                        foreach (var b in basliklar)
                            header.Cell().Background(Colors.Blue.Darken3).Padding(4)
                                .Text(b).FontColor(Colors.White).Bold().FontSize(8);
                    });

                    bool cift = false;
                    foreach (var satir in satirlar)
                    {
                        cift = !cift;
                        foreach (var hucre in satir)
                            table.Cell().Background(cift ? Colors.Grey.Lighten4 : Colors.White)
                                .Padding(3).Text(hucre ?? "").FontSize(8);
                    }
                });

                page.Footer().Column(col =>
                {
                    if (!string.IsNullOrEmpty(mevzuat))
                        col.Item().Text(mevzuat).FontSize(7).Italic().FontColor(Colors.Grey.Darken1);
                    col.Item().Row(row =>
                    {
                        row.RelativeItem().Text($"Üretim: {DateTime.Now:dd.MM.yyyy HH:mm}").FontSize(7).FontColor(Colors.Grey.Medium);
                        row.RelativeItem().AlignRight().Text(t => { t.Span("Sayfa "); t.CurrentPageNumber(); t.Span(" / "); t.TotalPages(); });
                    });
                });
            });
        });

        return doc.GeneratePdf();
    }
}
