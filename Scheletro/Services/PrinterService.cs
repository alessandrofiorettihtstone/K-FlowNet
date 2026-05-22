using System.Net.Sockets;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Scheletro.Models;

namespace Scheletro.Services;

public class PrinterService
{
    private readonly IConfiguration _configuration;
    private readonly dbContext _db;

    public PrinterService(IConfiguration configuration, dbContext db)
    {
        _configuration = configuration;
        _db = db;
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
        var zpl = BuildCardZpl(card);

        return await SendZplAsync(printer.NetworkAddress, printer.Port, zpl, cancellationToken);
    }

    public async Task<(bool Success, string Message)> PrintSampleCardAsync(CancellationToken cancellationToken = default)
    {
        var ipAddress = _configuration["KFlow:Printer:NetworkAddress"] ?? "192.168.0.90";
        var port = _configuration.GetValue("KFlow:Printer:Port", 9100);

        var zpl = """
            ^XA
            ^PW840
            ^LL560
            ^LH0,0
            ^CF0,60
            ^FO30,30^FDK-FLOW^FS
            ^CF0,40
            ^FO30,120^FDCard: CTL00000^FS
            ^FO30,180^FDItem: 000000^FS
            ^FO30,240^FDCustomer: 000000^FS
            ^FO30,300^FDDescrizione:  ^FS
            ^FO600,30
            ^BQN,2,10
            ^FDQA,CTL00005^FS
            ^XZ
            """;

        return await SendZplAsync(ipAddress, port, zpl, cancellationToken);
    }

    public static string BuildCardZpl(CartellinoPrintPayload card)
    {
        var location = string.Join(" ", new[] { card.LocationCode, card.LocationName }
            .Where(x => !string.IsNullOrWhiteSpace(x)));

        return $"""
^XA
^CI28
^PW800
^LL400
^LH0,0

^FO10,10^GB780,380,2^FS

^FO22,22^GB760,48,48^FS
^FO38,32^A0N,30,30^FR^FDK-FLOW^FS
^FO620,34^A0N,22,22^FR^FDCARTELLINO^FS


^FO30,110^A0N,52,52^FD{ZplText(card.IDCard)}^FS

^FO610,52^BQN,2,7^FDQA,{ZplText(card.IDCard)}^FS

^FO30,175^GB505,1,1^FS

^FO30,187^A0N,24,24^FDCLIENTE^FS
^FO155,187^A0N,27,27^FD{ZplText(card.IDPartner)}^FS
^FO30,219^A0N,24,24^FB505,1,0,L,0^FD{ZplText(card.PartnerName, 50)}^FS

^FO30,253^GB505,1,1^FS

^FO30,265^A0N,24,24^FDARTICOLO^FS
^FO155,265^A0N,27,27^FD{ZplText(card.IDProduct)}^FS
^FO30,297^A0N,23,23^FB505,2,3,L,0^FD{ZplText(card.ProductName, 80)}^FS

^FO560,275^GB205,105,2^FS

^FO585,312^A0N,48,48^FD{card.Quantity}^FS
^FO710,324^A0N,30,30^FD{ZplText(card.UMBase)}^FS

^FO30,358^A0N,19,19^FDLOC:^FS
^FO85,358^A0N,19,19^FB280,1,0,L,0^FD{ZplText(location, 38)}^FS

^FO390,358^A0N,19,19^FDCROSS:^FS
^FO470,358^A0N,19,19^FB85,1,0,L,0^FD{ZplText(card.IDCross, 18)}^FS

^XZ
""";
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
    string? LocationCode,
    string? LocationName,
    string IDCross);
