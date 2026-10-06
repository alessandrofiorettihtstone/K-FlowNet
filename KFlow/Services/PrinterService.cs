using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Scheletro.Models;

namespace Scheletro.Services;

public class PrinterService
{
    private readonly IConfiguration _configuration;
    private readonly dbContext _db;
    private readonly IHttpClientFactory _httpClientFactory;

    public PrinterService(
        IConfiguration configuration,
        dbContext db,
        IHttpClientFactory httpClientFactory)
    {
        _configuration = configuration;
        _db = db;
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IReadOnlyList<PrinterDestination>> GetConfiguredPrintersAsync(
        CancellationToken cancellationToken = default)
    {
        var printers = await _db.Printers
            .AsNoTracking()
            .Where(x => x.Ip != string.Empty && x.Port > 0)
            .OrderBy(x => x.IdPrinter)
            .Select(x => new PrinterDestination(
                x.IdPrinter.ToString(),
                $"Stampante {x.IdPrinter}",
                x.Ip,
                x.Port))
            .ToListAsync(cancellationToken);

        if (printers.Count > 0)
        {
            return printers;
        }

        var defaultAddress = _configuration["KFlow:Printer:NetworkAddress"]?.Trim() ?? "192.168.0.90";
        var defaultPort = _configuration.GetValue("KFlow:Printer:Port", 9100);
        return new[]
        {
            new PrinterDestination(
                Value: string.Empty,
                Name: "Stampante predefinita",
                NetworkAddress: defaultAddress,
                Port: defaultPort)
        };
    }

    public async Task<(bool Success, string Message)> PrintCardAsync(
        CartellinoPrintPayload card,
        string? printerValue,
        CancellationToken cancellationToken = default)
    {
        var printer = await ResolvePrinterAsync(printerValue, cancellationToken);
        var zpl = await BuildCardZplAsync(card, cancellationToken);

        return await SendZplAsync(printer.NetworkAddress, printer.Port, zpl, cancellationToken);
    }

    public async Task<(bool Success, byte[]? Content, string Message)> RenderCardPreviewAsync(
        CartellinoPrintPayload card,
        CancellationToken cancellationToken = default)
    {
        var host = _configuration["KFlow:Printer:Preview:ApiUrl"]?.Trim()
            ?? "https://api.labelary.com";
        var dpmm = _configuration["KFlow:Printer:Preview:Dpmm"]?.Trim() ?? "8dpmm";
        var width = _configuration["KFlow:Printer:Preview:Width"]?.Trim() ?? "4";
        var height = _configuration["KFlow:Printer:Preview:Height"]?.Trim() ?? "2";
        var endpoint = $"{host.TrimEnd('/')}/v1/printers/{dpmm}/labels/{width}x{height}/0/";

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(
                    await BuildCardZplAsync(card, cancellationToken),
                    Encoding.UTF8,
                    "application/x-www-form-urlencoded")
            };
            request.Headers.Accept.ParseAdd("image/png");

            var client = _httpClientFactory.CreateClient();
            using var response = await client.SendAsync(request, cancellationToken);
            var content = await response.Content.ReadAsByteArrayAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                var error = Encoding.UTF8.GetString(content);
                return (false, null, $"Errore rendering ZPL ({(int)response.StatusCode}): {error}");
            }

            return (true, content, "Anteprima generata.");
        }
        catch (Exception ex)
        {
            return (false, null, $"Errore rendering ZPL: {ex.Message}");
        }
    }

    public async Task<(bool Success, string Message)> PrintSampleCardAsync(CancellationToken cancellationToken = default)
    {
        var ipAddress = _configuration["KFlow:Printer:NetworkAddress"] ?? "192.168.0.90";
        var port = _configuration.GetValue("KFlow:Printer:Port", 9100);

        var zpl = await BuildCardZplAsync(
            new CartellinoPrintPayload(
                "CTL00000",
                "000000",
                "Cliente di prova",
                "000000",
                "Descrizione di prova",
                1,
                "PZ",
                null,
                null,
                null,
                ""),
            cancellationToken);

        return await SendZplAsync(ipAddress, port, zpl, cancellationToken);
    }

    private async Task<string> BuildCardZplAsync(
        CartellinoPrintPayload card,
        CancellationToken cancellationToken)
    {
        var location = string.Join(" ", new[] { card.LocationCode, card.LocationName }
            .Where(x => !string.IsNullOrWhiteSpace(x)));
        var storageLocation = string.IsNullOrWhiteSpace(card.StorageLocation)
            ? location
            : card.StorageLocation;

        var template = await _db.Zpls
            .AsNoTracking()
            .OrderBy(x => x.Id)
            .Select(x => x.ZPL)
            .FirstOrDefaultAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(template))
        {
            throw new InvalidOperationException("Nessun template ZPL configurato nella tabella ZPL.");
        }

        var values = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["#IDCard#"] = ZplText(card.IDCard),
            ["#2#"] = ZplText(card.IDPartner),
            ["#3#"] = ZplText(card.PartnerName, 50),
            ["#IDProduct#"] = ZplText(card.IDProduct),
            ["#5#"] = ZplText(card.ProductName, 80),
            ["#6#"] = card.Quantity.ToString(),
            ["#7#"] = ZplText(card.UMBase),
            ["#8#"] = ZplText(storageLocation, 38),
            ["#UBICAZIONE#"] = ZplText(storageLocation, 38),
            ["#9#"] = ZplText(card.IDCross, 18)
        };

        foreach (var value in values)
        {
            template = template.Replace(value.Key, value.Value, StringComparison.Ordinal);
        }

        return template;
    }

    private async Task<PrinterDestination> ResolvePrinterAsync(
        string? printerValue,
        CancellationToken cancellationToken)
    {
        var printers = await GetConfiguredPrintersAsync(cancellationToken);
        var printer = printers.FirstOrDefault(x => x.Value == printerValue);
        return printer ?? printers[0];
    }

    private static async Task<(bool Success, string Message)> SendZplAsync(
        string ipAddress,
        int port,
        string zpl,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(ipAddress, port, cancellationToken);
            await using var stream = client.GetStream();
            var data = Encoding.UTF8.GetBytes(zpl);
            await stream.WriteAsync(data, cancellationToken);
            return (true, $"ZPL inviato a {ipAddress}:{port}.");
        }
        catch (Exception ex)
        {
            return (false, $"Errore rete: {ex.Message}");
        }
    }

    private static string ZplText(string? value, int maxLength = 50)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "-";
        }

        var text = value
            .Replace("^", string.Empty, StringComparison.Ordinal)
            .Replace("~", string.Empty, StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Trim();

        return text.Length <= maxLength ? text : text[..maxLength];
    }
}

public record PrinterDestination(
    string Value,
    string Name,
    string NetworkAddress,
    int Port);

public record CartellinoPrintPayload(
    string IDCard,
    string IDPartner,
    string PartnerName,
    string IDProduct,
    string ProductName,
    int Quantity,
    string UMBase,
    string? StorageLocation,
    string? LocationCode,
    string? LocationName,
    string IDCross);
