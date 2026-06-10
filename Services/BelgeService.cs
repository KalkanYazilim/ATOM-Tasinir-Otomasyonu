using ClosedXML.Excel;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using W = DocumentFormat.OpenXml.Wordprocessing;

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
        var doc = QuestPDF.Fluent.Document.Create(container =>
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

    // ─── WORD (.docx) ─────────────────────────────────────────
    public byte[] WordTablo(string baslik, string altBilgi, IList<string> basliklar,
        IEnumerable<IList<string>> satirlar, string? mevzuat = null, string? dogrulamaKodu = null)
    {
        using var ms = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new W.Document();
            var body = mainPart.Document.AppendChild(new W.Body());

            body.AppendChild(OrtaParagraf("T.C. BAKANLIK – TAŞINIR YÖNETİM SİSTEMİ (ATOM)", 22, true));
            body.AppendChild(OrtaParagraf(baslik, 28, true, "1F3864"));
            if (!string.IsNullOrEmpty(altBilgi)) body.AppendChild(OrtaParagraf(altBilgi, 18, false, "808080"));

            var tablo = new W.Table();
            tablo.AppendChild(new W.TableProperties(
                new W.TableBorders(
                    new W.TopBorder { Val = W.BorderValues.Single, Size = 4 },
                    new W.BottomBorder { Val = W.BorderValues.Single, Size = 4 },
                    new W.LeftBorder { Val = W.BorderValues.Single, Size = 4 },
                    new W.RightBorder { Val = W.BorderValues.Single, Size = 4 },
                    new W.InsideHorizontalBorder { Val = W.BorderValues.Single, Size = 4 },
                    new W.InsideVerticalBorder { Val = W.BorderValues.Single, Size = 4 }),
                new W.TableWidth { Width = "5000", Type = W.TableWidthUnitValues.Pct }));

            var baslikSatir = new W.TableRow();
            foreach (var b in basliklar) baslikSatir.AppendChild(Hucre(b, true, "1F3864"));
            tablo.AppendChild(baslikSatir);

            foreach (var satir in satirlar)
            {
                var tr = new W.TableRow();
                foreach (var h in satir) tr.AppendChild(Hucre(h ?? "", false, null));
                tablo.AppendChild(tr);
            }
            body.AppendChild(tablo);

            body.AppendChild(new W.Paragraph());
            if (!string.IsNullOrEmpty(mevzuat)) body.AppendChild(KucukParagraf(mevzuat));
            if (!string.IsNullOrEmpty(dogrulamaKodu))
                body.AppendChild(KucukParagraf($"Belge Doğrulama Kodu: {dogrulamaKodu} — Bu belge elektronik onaylıdır (5070 sayılı Kanun)."));
            body.AppendChild(KucukParagraf($"Üretim Tarihi: {DateTime.Now:dd.MM.yyyy HH:mm}"));

            mainPart.Document.Save();
        }
        return ms.ToArray();
    }

    private static W.Paragraph OrtaParagraf(string metin, int boyut, bool kalin, string? renk = null)
    {
        var run = new W.Run();
        var props = new W.RunProperties(new W.FontSize { Val = boyut.ToString() });
        if (kalin) props.AppendChild(new W.Bold());
        if (renk != null) props.AppendChild(new W.Color { Val = renk });
        run.AppendChild(props);
        run.AppendChild(new W.Text(metin));
        var p = new W.Paragraph(run);
        p.ParagraphProperties = new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Center });
        return p;
    }

    private static W.Paragraph KucukParagraf(string metin)
    {
        var run = new W.Run(new W.RunProperties(new W.FontSize { Val = "16" }, new W.Italic()), new W.Text(metin));
        return new W.Paragraph(run);
    }

    // ─── RESMİ YAZI (.docx) — Resmî Yazışma Yönetmeliği formatı ─
    public byte[] WordResmiYazi(string kurum, string sayi, string konu, string ilgi,
        string muhatap, string govde, string imzaAd, string imzaUnvan, string? dogrulamaKodu = null)
    {
        using var ms = new MemoryStream();
        using (var wordDoc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var mainPart = wordDoc.AddMainDocumentPart();
            mainPart.Document = new W.Document();
            var body = mainPart.Document.AppendChild(new W.Body());

            body.AppendChild(OrtaParagraf("T.C.", 22, true));
            body.AppendChild(OrtaParagraf(kurum.ToUpper(), 22, true));
            body.AppendChild(new W.Paragraph());

            body.AppendChild(SatirParagraf($"Sayı : {sayi}", false));
            body.AppendChild(SatirParagraf($"Konu : {konu}", false));
            body.AppendChild(new W.Paragraph());
            body.AppendChild(new W.Paragraph());

            body.AppendChild(OrtaParagraf(muhatap.ToUpper(), 22, true));
            body.AppendChild(new W.Paragraph());

            if (!string.IsNullOrWhiteSpace(ilgi))
            {
                body.AppendChild(SatirParagraf($"İlgi : {ilgi}", false));
                body.AppendChild(new W.Paragraph());
            }

            foreach (var p in govde.Split('\n'))
                body.AppendChild(SatirParagraf("        " + p.Trim(), false));

            body.AppendChild(new W.Paragraph());
            body.AppendChild(new W.Paragraph());
            var imza = new W.Paragraph(new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Right }),
                new W.Run(new W.RunProperties(new W.FontSize { Val = "22" }, new W.Bold()), new W.Text(imzaAd)));
            body.AppendChild(imza);
            body.AppendChild(new W.Paragraph(new W.ParagraphProperties(new W.Justification { Val = W.JustificationValues.Right }),
                new W.Run(new W.RunProperties(new W.FontSize { Val = "20" }), new W.Text(imzaUnvan))));

            if (!string.IsNullOrEmpty(dogrulamaKodu))
            {
                body.AppendChild(new W.Paragraph());
                body.AppendChild(KucukParagraf($"Bu belge 5070 sayılı Kanun gereğince elektronik olarak imzalanmıştır. Doğrulama Kodu: {dogrulamaKodu}"));
            }
            mainPart.Document.Save();
        }
        return ms.ToArray();
    }

    private static W.Paragraph SatirParagraf(string metin)
        => SatirParagraf(metin, false);
    private static W.Paragraph SatirParagraf(string metin, bool kalin)
    {
        var props = new W.RunProperties(new W.FontSize { Val = "22" });
        if (kalin) props.AppendChild(new W.Bold());
        return new W.Paragraph(new W.Run(props, new W.Text(metin) { Space = SpaceProcessingModeValues.Preserve }));
    }

    private static W.TableCell Hucre(string metin, bool kalin, string? arkaplan)
    {
        var props = new W.RunProperties(new W.FontSize { Val = "18" });
        if (kalin) { props.AppendChild(new W.Bold()); props.AppendChild(new W.Color { Val = "FFFFFF" }); }
        var run = new W.Run(props, new W.Text(metin) { Space = SpaceProcessingModeValues.Preserve });
        var hucre = new W.TableCell(new W.Paragraph(run));
        if (arkaplan != null)
            hucre.TableCellProperties = new W.TableCellProperties(new W.Shading { Fill = arkaplan, Val = W.ShadingPatternValues.Clear });
        return hucre;
    }
}
