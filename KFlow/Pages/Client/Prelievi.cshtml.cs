using System.Globalization;
using System.IO.Compression;
using System.Security;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Scheletro.Models;

namespace Scheletro.Pages.Client;

[Authorize]
public class PrelieviModel : PageModel
{
    private readonly dbContext _db;

    public PrelieviModel(dbContext db)
    {
        _db = db;
    }

    [BindProperty(SupportsGet = true)]
    public PrelieviFilters Filters { get; set; } = new();

    public IReadOnlyList<PrelieviRowViewModel> Rows { get; private set; } = Array.Empty<PrelieviRowViewModel>();

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync(string? sortColumn)
    {
        Filters.ToggleSort(sortColumn);
        await LoadRowsAsync();
    }

    public string GetSortIcon(string column) => Filters.GetSortIcon(column);

    public async Task<FileResult> OnGetExportAsync()
    {
        await LoadRowsAsync();

        var fileName = $"Prelievi_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";
        return File(
            PrelieviExcelExport.Create(Rows),
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }

    public async Task<IActionResult> OnPostUpdateMovementAsync(int id, double? quantity, string? returnUrl)
    {
        if (quantity is null || quantity <= 0)
        {
            ErrorMessage = "Inserisci una quantità maggiore di zero.";
            return RedirectAfterPost(returnUrl);
        }

        var transaction = await _db.PickingTransactions
            .SingleOrDefaultAsync(x => x.ID == id && x.IsEnable);

        if (transaction is null)
        {
            ErrorMessage = "Movimento di prelievo non trovato o non più disponibile.";
            return RedirectAfterPost(returnUrl);
        }

        transaction.Qty = quantity.Value;
        transaction.LastUpdate = DateTime.Now;
        transaction.LastUpdatedBy = GetOperatorDisplayName();
        await _db.SaveChangesAsync();

        SuccessMessage = "Quantità del prelievo aggiornata correttamente.";
        return RedirectAfterPost(returnUrl);
    }

    public async Task<IActionResult> OnPostDeleteMovementAsync(int id, string? returnUrl)
    {
        var transaction = await _db.PickingTransactions
            .SingleOrDefaultAsync(x => x.ID == id && x.IsEnable);

        if (transaction is null)
        {
            ErrorMessage = "Movimento di prelievo non trovato o già eliminato.";
            return RedirectAfterPost(returnUrl);
        }

        transaction.IsEnable = false;
        transaction.LastUpdate = DateTime.Now;
        transaction.LastUpdatedBy = GetOperatorDisplayName();
        await _db.SaveChangesAsync();

        SuccessMessage = "Movimento di prelievo eliminato correttamente.";
        return RedirectAfterPost(returnUrl);
    }

    public async Task<JsonResult> OnGetSuggerimentiRicercaAsync(string prefixText, string tipoRicerca)
    {
        if (string.IsNullOrWhiteSpace(prefixText) || prefixText.Trim().Length < 2)
        {
            return new JsonResult(Array.Empty<string>());
        }

        var searchText = prefixText.Trim();
        var searchType = tipoRicerca?.Trim().ToUpperInvariant();
        var activeTransactions = _db.PickingTransactions
            .AsNoTracking()
            .Where(x => x.IsEnable);

        if (searchType == "P")
        {
            var productions = await activeTransactions
                .Where(x => x.Picking.Contains(searchText))
                .Select(x => x.Picking)
                .Distinct()
                .OrderBy(x => x)
                .Take(20)
                .ToListAsync();

            return new JsonResult(productions);
        }

        if (searchType == "A")
        {
            var articles = await (
                from transaction in activeTransactions
                join product in _db.Products.AsNoTracking() on transaction.ItemCode equals product.ID into products
                from product in products.DefaultIfEmpty()
                where transaction.ItemCode.Contains(searchText)
                    || (product != null && product.Name.Contains(searchText))
                orderby transaction.ItemCode
                select new { transaction.ItemCode, Description = product == null ? string.Empty : product.Name }
            )
                .Distinct()
                .Take(20)
                .ToListAsync();

            return new JsonResult(articles.Select(x => string.IsNullOrWhiteSpace(x.Description)
                ? x.ItemCode
                : $"{x.ItemCode} - {x.Description}"));
        }

        return new JsonResult(Array.Empty<string>());
    }

    private async Task LoadRowsAsync()
    {
        var production = Filters.Production?.Trim() ?? string.Empty;
        var article = Filters.Article?.Trim() ?? string.Empty;

        var query =
            from transaction in _db.PickingTransactions.AsNoTracking()
            join product in _db.Products.AsNoTracking() on transaction.ItemCode equals product.ID into products
            from product in products.DefaultIfEmpty()
            where transaction.IsEnable
                && (string.IsNullOrWhiteSpace(production) || transaction.Picking.Contains(production))
                && (string.IsNullOrWhiteSpace(article)
                    || transaction.ItemCode.Contains(article)
                    || (product != null && product.Name.Contains(article)))
            select new
            {
                Production = transaction.Picking,
                transaction.ItemCode,
                DescriptionCode = product == null ? string.Empty : product.Name,
                transaction.ItemPF,
                transaction.DescriptionPF,
                transaction.QtyPF,
                transaction.IDCard,
                transaction.Operator,
                transaction.Qty,
                transaction.CreateDate,
                transaction.LastUpdate,
                transaction.LastUpdatedBy,
                transaction.ID
            };

        var movements = await query
            .OrderByDescending(x => x.LastUpdate ?? x.CreateDate)
            .ToListAsync();

        var rows = movements
            .GroupBy(x => new { x.Production, x.ItemCode, x.DescriptionCode })
            .Select(group => new PrelieviRowViewModel
            {
                Production = group.Key.Production,
                ArticleCode = group.Key.ItemCode,
                DescriptionCode = group.Key.DescriptionCode,
                ItemPF = group.Select(x => x.ItemPF).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                DescriptionPF = group.Select(x => x.DescriptionPF).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)),
                QtyPF = group.Select(x => x.QtyPF).FirstOrDefault(value => value.HasValue),
                TransactionCount = group.Count(),
                TotalQuantity = group.Sum(x => x.Qty),
                LastPickingDate = group.Max(x => x.LastUpdate ?? x.CreateDate),
                Movements = group
                    .OrderByDescending(x => x.LastUpdate ?? x.CreateDate)
                    .Select(x => new PickingMovementRowViewModel
                    {
                        CardId = x.IDCard,
                        Operator = x.Operator,
                        ItemPF = x.ItemPF,
                        DescriptionPF = x.DescriptionPF,
                        QtyPF = x.QtyPF,
                        Quantity = x.Qty,
                        CreateDate = x.CreateDate,
                        LastUpdate = x.LastUpdate,
                        LastUpdatedBy = x.LastUpdatedBy,
                        Id = x.ID
                    })
                    .ToList()
            });

        Rows = (Filters.SortBy switch
        {
            "Production" when Filters.SortDirection == "asc" => rows.OrderBy(x => x.Production),
            "Production" => rows.OrderByDescending(x => x.Production),
            "Article" when Filters.SortDirection == "asc" => rows.OrderBy(x => x.ArticleCode),
            "Article" => rows.OrderByDescending(x => x.ArticleCode),
            "Movements" when Filters.SortDirection == "asc" => rows.OrderBy(x => x.TransactionCount),
            "Movements" => rows.OrderByDescending(x => x.TransactionCount),
            "TotalQuantity" when Filters.SortDirection == "asc" => rows.OrderBy(x => x.TotalQuantity),
            "TotalQuantity" => rows.OrderByDescending(x => x.TotalQuantity),
            "LastPicking" when Filters.SortDirection == "asc" => rows.OrderBy(x => x.LastPickingDate),
            _ => rows.OrderByDescending(x => x.LastPickingDate)
        })
            .ThenBy(x => x.Production)
            .ThenBy(x => x.ArticleCode)
            .ToList();
    }

    private string GetOperatorDisplayName() => string.Join(" ", new[]
    {
        User.FindFirstValue(ClaimTypes.GivenName),
        User.FindFirstValue(ClaimTypes.Surname)
    }.Where(value => !string.IsNullOrWhiteSpace(value))).Trim();

    private IActionResult RedirectAfterPost(string? returnUrl) =>
        !string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl)
            ? LocalRedirect(returnUrl)
            : RedirectToPage();
}

public class PrelieviFilters
{
    public string? Production { get; set; }

    public string? Article { get; set; }

    public string SortBy { get; set; } = string.Empty;

    public string SortDirection { get; set; } = "desc";

    public void ToggleSort(string? sortColumn)
    {
        if (sortColumn is not ("Production" or "Article" or "Movements" or "TotalQuantity" or "LastPicking"))
        {
            return;
        }

        if (SortBy == sortColumn)
        {
            SortDirection = SortDirection == "asc" ? "desc" : "asc";
            return;
        }

        SortBy = sortColumn;
        SortDirection = sortColumn is "Production" or "Article" ? "asc" : "desc";
    }

    public string GetSortIcon(string column)
    {
        if (SortBy != column)
        {
            return "bi-arrow-down-up text-muted";
        }

        return SortDirection == "asc" ? "bi-sort-up" : "bi-sort-down";
    }
}

public class PrelieviRowViewModel
{
    public string Production { get; set; } = string.Empty;

    public string ArticleCode { get; set; } = string.Empty;

    public string DescriptionCode { get; set; } = string.Empty;

    public string? ItemPF { get; set; }

    public string? DescriptionPF { get; set; }

    public double? QtyPF { get; set; }

    public int TransactionCount { get; set; }

    public double TotalQuantity { get; set; }

    public DateTime LastPickingDate { get; set; }

    public IReadOnlyList<PickingMovementRowViewModel> Movements { get; set; } = Array.Empty<PickingMovementRowViewModel>();
}

public class PickingMovementRowViewModel
{
    public int Id { get; set; }

    public string CardId { get; set; } = string.Empty;

    public string Operator { get; set; } = string.Empty;

    public string? ItemPF { get; set; }

    public string? DescriptionPF { get; set; }

    public double? QtyPF { get; set; }

    public double Quantity { get; set; }

    public DateTime CreateDate { get; set; }

    public DateTime? LastUpdate { get; set; }

    public string LastUpdatedBy { get; set; } = string.Empty;
}

internal static class PrelieviExcelExport
{
    public static byte[] Create(IReadOnlyList<PrelieviRowViewModel> rows)
    {
        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Default Extension="rels" ContentType="application/vnd.openxmlformats-package.relationships+xml"/>
                  <Default Extension="xml" ContentType="application/xml"/>
                  <Override PartName="/xl/workbook.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml"/>
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/worksheets/sheet2.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml"/>
                  <Override PartName="/xl/styles.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml"/>
                </Types>
                """);
            WriteEntry(archive, "_rels/.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument" Target="xl/workbook.xml"/>
                </Relationships>
                """);
            WriteEntry(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets>
                    <sheet name="Riepilogo" sheetId="1" r:id="rId1"/>
                    <sheet name="Movimenti" sheetId="2" r:id="rId2"/>
                  </sheets>
                </workbook>
                """);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml"/>
                  <Relationship Id="rId2" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet2.xml"/>
                  <Relationship Id="rId3" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles" Target="styles.xml"/>
                </Relationships>
                """);
            WriteEntry(archive, "xl/styles.xml", """
                <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
                <styleSheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">
                  <numFmts count="1"><numFmt numFmtId="164" formatCode="dd/mm/yyyy hh:mm"/></numFmts>
                  <fonts count="2"><font><sz val="11"/><name val="Calibri"/></font><font><b/><color rgb="FFFFFFFF"/><sz val="11"/><name val="Calibri"/></font></fonts>
                  <fills count="3"><fill><patternFill patternType="none"/></fill><fill><patternFill patternType="gray125"/></fill><fill><patternFill patternType="solid"><fgColor rgb="FF1F5F7D"/><bgColor indexed="64"/></patternFill></fill></fills>
                  <borders count="1"><border><left/><right/><top/><bottom/><diagonal/></border></borders>
                  <cellStyleXfs count="1"><xf numFmtId="0" fontId="0" fillId="0" borderId="0"/></cellStyleXfs>
                  <cellXfs count="4"><xf numFmtId="0" fontId="0" fillId="0" borderId="0" xfId="0"/><xf numFmtId="0" fontId="1" fillId="2" borderId="0" xfId="0" applyFont="1" applyFill="1"/><xf numFmtId="4" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/><xf numFmtId="164" fontId="0" fillId="0" borderId="0" xfId="0" applyNumberFormat="1"/></cellXfs>
                </styleSheet>
                """);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", BuildSummarySheet(rows));
            WriteEntry(archive, "xl/worksheets/sheet2.xml", BuildMovementsSheet(rows));
        }

        return stream.ToArray();
    }

    private static string BuildSummarySheet(IReadOnlyList<PrelieviRowViewModel> rows)
    {
        var sheet = new StringBuilder();
        sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sheet.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews><cols>");
        sheet.Append("<col min=\"1\" max=\"1\" width=\"20\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"18\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"38\" customWidth=\"1\"/><col min=\"4\" max=\"5\" width=\"16\" customWidth=\"1\"/><col min=\"6\" max=\"6\" width=\"18\" customWidth=\"1\"/><col min=\"7\" max=\"7\" width=\"38\" customWidth=\"1\"/><col min=\"8\" max=\"8\" width=\"14\" customWidth=\"1\"/><col min=\"9\" max=\"9\" width=\"21\" customWidth=\"1\"/>");
        sheet.Append("</cols><sheetData>");
        sheet.Append(Row(1,
            TextCell("A", 1, "Produzione", 1),
            TextCell("B", 1, "Articolo", 1),
            TextCell("C", 1, "Descrizione", 1),
            TextCell("D", 1, "Item PF", 1),
            TextCell("E", 1, "Descrizione PF", 1),
            TextCell("F", 1, "Qty PF", 1),
            TextCell("G", 1, "Movimenti", 1),
            TextCell("H", 1, "Quantità totale", 1),
            TextCell("I", 1, "Ultimo prelievo", 1)));

        for (var index = 0; index < rows.Count; index++)
        {
            var rowNumber = index + 2;
            var row = rows[index];
            sheet.Append(Row(rowNumber,
                TextCell("A", rowNumber, row.Production),
                TextCell("B", rowNumber, row.ArticleCode),
                TextCell("C", rowNumber, row.DescriptionCode),
                TextCell("D", rowNumber, row.ItemPF),
                TextCell("E", rowNumber, row.DescriptionPF),
                row.QtyPF is null ? TextCell("F", rowNumber, string.Empty) : NumberCell("F", rowNumber, row.QtyPF.Value, 2),
                NumberCell("G", rowNumber, row.TransactionCount),
                NumberCell("H", rowNumber, row.TotalQuantity, 2),
                NumberCell("I", rowNumber, row.LastPickingDate.ToOADate(), 3)));
        }

        sheet.Append("</sheetData><autoFilter ref=\"A1:I").Append(rows.Count + 1).Append("\"/></worksheet>");
        return sheet.ToString();
    }

    private static string BuildMovementsSheet(IReadOnlyList<PrelieviRowViewModel> rows)
    {
        var sheet = new StringBuilder();
        var movementCount = rows.Sum(row => row.Movements.Count);

        sheet.Append("<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>");
        sheet.Append("<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\"><sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"1\" topLeftCell=\"A2\" activePane=\"bottomLeft\" state=\"frozen\"/></sheetView></sheetViews><cols>");
        sheet.Append("<col min=\"1\" max=\"1\" width=\"20\" customWidth=\"1\"/><col min=\"2\" max=\"2\" width=\"18\" customWidth=\"1\"/><col min=\"3\" max=\"3\" width=\"38\" customWidth=\"1\"/><col min=\"4\" max=\"4\" width=\"18\" customWidth=\"1\"/><col min=\"5\" max=\"5\" width=\"38\" customWidth=\"1\"/><col min=\"6\" max=\"6\" width=\"14\" customWidth=\"1\"/><col min=\"7\" max=\"7\" width=\"20\" customWidth=\"1\"/><col min=\"8\" max=\"8\" width=\"14\" customWidth=\"1\"/><col min=\"9\" max=\"9\" width=\"24\" customWidth=\"1\"/><col min=\"10\" max=\"10\" width=\"21\" customWidth=\"1\"/>");
        sheet.Append("</cols><sheetData>");
        sheet.Append(Row(1,
            TextCell("A", 1, "Produzione", 1),
            TextCell("B", 1, "Articolo", 1),
            TextCell("C", 1, "Descrizione", 1),
            TextCell("D", 1, "Item PF", 1),
            TextCell("E", 1, "Descrizione PF", 1),
            TextCell("F", 1, "Qty PF", 1),
            TextCell("G", 1, "Cartellino", 1),
            TextCell("H", 1, "Quantità", 1),
            TextCell("I", 1, "Operatore", 1),
            TextCell("J", 1, "Data riferimento", 1)));

        var rowNumber = 2;
        foreach (var summaryRow in rows)
        {
            foreach (var movement in summaryRow.Movements)
            {
                sheet.Append(Row(rowNumber,
                    TextCell("A", rowNumber, summaryRow.Production),
                    TextCell("B", rowNumber, summaryRow.ArticleCode),
                    TextCell("C", rowNumber, summaryRow.DescriptionCode),
                    TextCell("D", rowNumber, movement.ItemPF),
                    TextCell("E", rowNumber, movement.DescriptionPF),
                    movement.QtyPF is null ? TextCell("F", rowNumber, string.Empty) : NumberCell("F", rowNumber, movement.QtyPF.Value, 2),
                    TextCell("G", rowNumber, movement.CardId),
                    NumberCell("H", rowNumber, movement.Quantity, 2),
                    TextCell("I", rowNumber, string.IsNullOrWhiteSpace(movement.LastUpdatedBy) ? movement.Operator : movement.LastUpdatedBy),
                    NumberCell("J", rowNumber, (movement.LastUpdate ?? movement.CreateDate).ToOADate(), 3)));
                rowNumber++;
            }
        }

        sheet.Append("</sheetData><autoFilter ref=\"A1:J").Append(movementCount + 1).Append("\"/></worksheet>");
        return sheet.ToString();
    }

    private static string Row(int rowNumber, params string[] cells) => $"<row r=\"{rowNumber}\">{string.Concat(cells)}</row>";

    private static string TextCell(string column, int row, string? value, int style = 0) =>
        $"<c r=\"{column}{row}\" s=\"{style}\" t=\"inlineStr\"><is><t>{SecurityElement.Escape(value ?? string.Empty)}</t></is></c>";

    private static string NumberCell(string column, int row, double value, int style = 0) =>
        $"<c r=\"{column}{row}\" s=\"{style}\"><v>{value.ToString(CultureInfo.InvariantCulture)}</v></c>";

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }
}
