# K-Flow ASP.NET Core

Migrazione incrementale del legacy ASP.NET Web Forms K-Flow verso ASP.NET Core Razor Pages.

## Prerequisiti

- .NET SDK 9.x.
- SQL Server Express o SQL Server locale.
- Permessi SQL per ripristinare `KFlow.bak` oppure accesso a un database `K-Flow` gia ripristinato.
- Browser moderno. Per scanner fotocamera usare `localhost` o HTTPS.

## Ripristino database dal `.bak`

Backup sorgente:

```text
C:\ASW\repo\K-Flow\KFlow.bak
```

Esempio SQL Server Management Studio:

1. Aprire SSMS con un utente autorizzato.
2. Eseguire Restore Database.
3. Selezionare `Device` e scegliere `KFlow.bak`.
4. Ripristinare in un database locale, ad esempio `K-Flow`.
5. Verificare che siano presenti le tabelle `Users`, `Cards`, `Status`, `Partners`, `Products`, `Combination`, `Locations`, `tblLog` e la vista `vwKanbanBoard`.

Script di schema di riferimento:

```text
C:\ASW\repo\Scheletro\Database\KFlow.schema.sql
```

## Connection string

Configurare `C:\ASW\repo\Scheletro\Scheletro\appsettings.json`:

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.\\SQLEXPRESS;Database=K-Flow;User ID=YOUR_USER;Password=YOUR_PASSWORD;MultipleActiveResultSets=True;Encrypt=False;TrustServerCertificate=True"
  }
}
```

Non inserire password reali nel repository. In sviluppo si puo usare anche:

```powershell
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=.\SQLEXPRESS;Database=K-Flow;User ID=...;Password=...;MultipleActiveResultSets=True;Encrypt=False;TrustServerCertificate=True"
```

## Avvio

```powershell
cd C:\ASW\repo\Scheletro
dotnet restore
dotnet build
dotnet run --project .\Scheletro\Scheletro.csproj
```

Rotte principali:

- `/Client/Login.cshtml`
- `/Client/KanbanBoard.cshtml`
- `/Client/Clienti.cshtml`
- `/Client/Articoli.cshtml`
- `/Client/Cartellini.cshtml`
- `/Mobile/Default.cshtml`
- `/Mobile/LeggiCartellinoMain.cshtml`
- `/BarcodeScanner.cshtml`
- `/StampaCartellino.cshtml`

## Funzionalita migrate

- Login desktop con username/password o CID.
- Auto-login tramite cookie legacy `login`.
- Logout.
- Kanban board con filtri per cartellino, cliente, articolo e stati.
- Clienti con ricerca, creazione e modifica.
- Articoli con ricerca, creazione e modifica.
- Gestione cartellini con creazione, modifica e creazione combination.
- Cambio stato su singolo cartellino.
- Cambio stato dei cartellini tramite drag and drop tra colonne Kanban.
- Cambio stato su tutti i cartellini di una combinazione.
- Login mobile tramite CID.
- Lettura cartellino mobile da input o fotocamera.
- Regole mobile di cambio stato ereditate dal legacy.
- Scanner barcode standalone.
- Invio ZPL a stampante di rete configurabile.

Nota EF Core: la tabella `Cards` e configurata come tabella con trigger, cosi SQL Server non usa la clausola `OUTPUT` incompatibile con i trigger durante il salvataggio dei cambi stato.

## Da verificare

- Ripristino effettivo del `.bak` con un utente SQL autorizzato.
- Login con utenti reali del database migrato.
- Scanner su dispositivo fisico con permessi camera.
- Stampa su stampante di produzione.
