using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using KFlow.Class;
using KFlow.Models;
using PrinterEntity = KFlow.Models.Printer;

namespace KFlow.Pages.Client;

[Authorize]
public class PrinterModel : PageModel
{
    private readonly dbContext _db;
    private readonly LoggerDTO _logger;

    public PrinterModel(dbContext db, LoggerDTO logger)
    {
        _db = db;
        _logger = logger;
    }

    [BindProperty]
    public CreatePrinterInput Input { get; set; } = new();

    [BindProperty]
    public EditPrinterInput EditInput { get; set; } = new();

    public IReadOnlyList<PrinterRowViewModel> Rows { get; private set; } = Array.Empty<PrinterRowViewModel>();

    public bool ShowCreateModal { get; private set; }

    public bool ShowEditModal { get; private set; }

    [TempData]
    public string? SuccessMessage { get; set; }

    [TempData]
    public string? ErrorMessage { get; set; }

    public async Task OnGetAsync()
    {
        await LoadPageDataAsync();
    }

    public async Task<IActionResult> OnPostCreateAsync(string? returnUrl)
    {
        ModelState.Clear();
        NormalizeCreateInput();
        await ValidateCreateInputAsync();

        if (!ModelState.IsValid)
        {
            ShowCreateModal = true;
            ErrorMessage = "Controlla i dati della stampante.";
            await LoadPageDataAsync();
            return Page();
        }

        try
        {
            var printer = new PrinterEntity
            {
                Ip = Input.Ip,
                Port = Input.Port
            };

            _db.Printers.Add(printer);
            await _db.SaveChangesAsync();

            SuccessMessage = $"Stampante {printer.IdPrinter} creata correttamente.";
            Log("CreatePrinter", SuccessMessage);
            return RedirectAfterPost(returnUrl);
        }
        catch (Exception ex)
        {
            ShowCreateModal = true;
            ErrorMessage = $"Errore durante la creazione della stampante: {ex.Message}";
            Log("CreatePrinterError", ex.Message);
            await LoadPageDataAsync();
            return Page();
        }
    }

    public async Task<IActionResult> OnPostUpdateAsync(string? returnUrl)
    {
        ModelState.Clear();
        NormalizeEditInput();
        await ValidateEditInputAsync();

        if (!ModelState.IsValid)
        {
            ShowEditModal = true;
            ErrorMessage = "Controlla i dati della stampante.";
            await LoadPageDataAsync(preserveEditInput: true);
            return Page();
        }

        try
        {
            var printer = await _db.Printers.FirstOrDefaultAsync(x => x.IdPrinter == EditInput.IdPrinter);
            if (printer is null)
            {
                ShowEditModal = true;
                ErrorMessage = "Stampante non trovata.";
                ModelState.AddModelError("EditInput.IdPrinter", "Stampante non trovata.");
                await LoadPageDataAsync(preserveEditInput: true);
                return Page();
            }

            printer.Ip = EditInput.Ip;
            printer.Port = EditInput.Port;
            await _db.SaveChangesAsync();

            SuccessMessage = $"Stampante {printer.IdPrinter} aggiornata correttamente.";
            Log("UpdatePrinter", SuccessMessage);
            return RedirectAfterPost(returnUrl);
        }
        catch (Exception ex)
        {
            ShowEditModal = true;
            ErrorMessage = $"Errore durante la modifica della stampante: {ex.Message}";
            Log("UpdatePrinterError", ex.Message);
            await LoadPageDataAsync(preserveEditInput: true);
            return Page();
        }
    }

    public async Task<IActionResult> OnPostDeleteAsync(int idPrinter, string? returnUrl)
    {
        try
        {
            var printer = await _db.Printers.FirstOrDefaultAsync(x => x.IdPrinter == idPrinter);
            if (printer is null)
            {
                ErrorMessage = "Stampante non trovata.";
                return RedirectAfterPost(returnUrl);
            }

            _db.Printers.Remove(printer);
            await _db.SaveChangesAsync();

            SuccessMessage = $"Stampante {idPrinter} eliminata correttamente.";
            Log("DeletePrinter", SuccessMessage);
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Errore durante l'eliminazione della stampante: {ex.Message}";
            Log("DeletePrinterError", ex.Message);
        }

        return RedirectAfterPost(returnUrl);
    }

    private async Task ValidateCreateInputAsync()
    {
        ValidatePrinterFields("Input", Input.Ip, Input.Port);

        if (ModelState.IsValid)
        {
            var duplicateExists = await _db.Printers
                .AsNoTracking()
                .AnyAsync(x => x.Ip == Input.Ip && x.Port == Input.Port);

            if (duplicateExists)
            {
                ModelState.AddModelError("Input.Ip", "Esiste gia una stampante con lo stesso IP e porta.");
            }
        }
    }

    private async Task ValidateEditInputAsync()
    {
        if (EditInput.IdPrinter <= 0)
        {
            ModelState.AddModelError("EditInput.IdPrinter", "Stampante non valida.");
        }
        else
        {
            var exists = await _db.Printers
                .AsNoTracking()
                .AnyAsync(x => x.IdPrinter == EditInput.IdPrinter);

            if (!exists)
            {
                ModelState.AddModelError("EditInput.IdPrinter", "Stampante non trovata.");
            }
        }

        ValidatePrinterFields("EditInput", EditInput.Ip, EditInput.Port);

        if (ModelState.IsValid)
        {
            var duplicateExists = await _db.Printers
                .AsNoTracking()
                .AnyAsync(x =>
                    x.IdPrinter != EditInput.IdPrinter &&
                    x.Ip == EditInput.Ip &&
                    x.Port == EditInput.Port);

            if (duplicateExists)
            {
                ModelState.AddModelError("EditInput.Ip", "Esiste gia una stampante con lo stesso IP e porta.");
            }
        }
    }

    private void ValidatePrinterFields(string prefix, string ip, int port)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            ModelState.AddModelError($"{prefix}.Ip", "IP obbligatorio.");
        }
        else if (ip.Length > 50)
        {
            ModelState.AddModelError($"{prefix}.Ip", "IP non puo superare 50 caratteri.");
        }

        if (port is < 1 or > 65535)
        {
            ModelState.AddModelError($"{prefix}.Port", "Porta non valida.");
        }
    }

    private async Task LoadPageDataAsync(bool preserveEditInput = false)
    {
        Rows = await _db.Printers
            .AsNoTracking()
            .OrderBy(x => x.IdPrinter)
            .Select(x => new PrinterRowViewModel
            {
                IdPrinter = x.IdPrinter,
                Ip = x.Ip,
                Port = x.Port
            })
            .ToListAsync();

        if (preserveEditInput && EditInput.IdPrinter > 0)
        {
            var printer = await _db.Printers
                .AsNoTracking()
                .Where(x => x.IdPrinter == EditInput.IdPrinter)
                .Select(x => new { x.Ip, x.Port })
                .FirstOrDefaultAsync();

            if (printer is not null)
            {
                EditInput.Ip = printer.Ip;
                EditInput.Port = printer.Port;
            }
        }
    }

    private void NormalizeCreateInput()
    {
        Input.Ip = Input.Ip?.Trim() ?? string.Empty;
    }

    private void NormalizeEditInput()
    {
        EditInput.Ip = EditInput.Ip?.Trim() ?? string.Empty;
    }

    private IActionResult RedirectAfterPost(string? returnUrl)
    {
        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return LocalRedirect(returnUrl);
        }

        return LocalRedirect("/Client/Printer.cshtml");
    }

    private void Log(string functionName, string? text)
    {
        _logger.Log(new LoggerDTO.logDTO
        {
            typeMessage = "Printer",
            TypeFunction = functionName,
            TypeText = text ?? string.Empty,
            IDUser = User.Claims.FirstOrDefault(x => x.Type == "IDUser")?.Value ?? string.Empty
        });
    }
}

public class CreatePrinterInput
{
    [Required(ErrorMessage = "IP obbligatorio.")]
    [StringLength(50, ErrorMessage = "IP non puo superare 50 caratteri.")]
    public string Ip { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "Porta non valida.")]
    public int Port { get; set; } = 9100;
}

public class EditPrinterInput
{
    public int IdPrinter { get; set; }

    [Required(ErrorMessage = "IP obbligatorio.")]
    [StringLength(50, ErrorMessage = "IP non puo superare 50 caratteri.")]
    public string Ip { get; set; } = string.Empty;

    [Range(1, 65535, ErrorMessage = "Porta non valida.")]
    public int Port { get; set; } = 9100;
}

public class PrinterRowViewModel
{
    public int IdPrinter { get; set; }

    public string Ip { get; set; } = string.Empty;

    public int Port { get; set; }
}
