# K-Flow Migration Notes

Data analisi: 2026-05-19.

## Fonti analizzate

- `C:\ASW\repo\K-Flow.zip`: archivio legacy ASP.NET Web Forms. La cartella `C:\ASW\repo\K-Flow` era gia estratta ed e stata usata solo in lettura.
- `C:\ASW\repo\K-Flow\KFlow.bak`: backup SQL Server. Il `RESTORE FILELISTONLY` non e riuscito con l'utenza disponibile per mancanza di permesso `CREATE DATABASE`; per lo schema e stata usata in sola lettura l'istanza locale `.\SQLEXPRESS`, database `K-Flow`, gia presente.
- `C:\ASW\repo\Scheletro.zip`: scheletro ASP.NET Core Razor Pages, gia estratto in `C:\ASW\repo\Scheletro`.

## Struttura legacy rilevante

- Progetto Web Forms .NET Framework 4.8: `K-Flow.csproj`.
- UI Web Forms:
  - `Client/Login.aspx`
  - `Client/KanbanBoard.aspx`
  - `Mobile/Default.aspx`
  - `Mobile/LeggiCartellinoMain.aspx`
  - `BarcodeScanner.aspx`
  - `StampaCartellino.aspx`
  - `WebForm1.aspx`
- Master page:
  - `Client/MasterPages/Default.Master`
  - `Mobile/MasterPages/Default.Master`
- Componenti e helper:
  - `wwwroot/Scripts/ErrorModal.ascx`
  - `wwwroot/Scripts/html5-qrcode.min.js`
  - `Classi/Logger.cs`
  - `Classi/ZplPrinter.cs`
- Accesso dati legacy:
  - Entity Framework 6 database-first via `EF/Model1.edmx`
  - `EZ_Entities` con `DbSet` per `Users`, `Cards`, `Status`, `Partners`, `Products`, `tblLog`, `vwKanbanBoard`
  - Nessuna stored procedure applicativa rilevante; nel DB sono presenti solo procedure standard di database diagrams.

## Logiche principali individuate

- Login desktop:
  - Accesso tramite `Username` + `Password` oppure tramite `CID` GUID.
  - Utente disabilitato bloccato tramite `Users.Enabled`.
  - Cookie legacy `login` con valore `guid`.
  - Redirect legacy a `/Client/KanbanBoard.aspx`; nella migrazione la route pubblica e `/Client/KanbanBoard.cshtml`.
- Master page desktop:
  - Recupera utente da sessione o cookie `login`.
  - Mostra nome/cognome e logout.
- Kanban board:
  - Carica stati abilitati da `Status`.
  - Legge righe dalla vista `vwKanbanBoard`.
  - Filtra per cartellino, cliente, articolo e stati.
  - Raggruppa per stato, combinazione, partner e prodotto.
  - Permette cambio stato su singolo `Cards.IDCard` o su tutti i cartellini con lo stesso `Cards.IDCombination`.
- Mobile:
  - `Mobile/Default.aspx` mostra login CID in modale.
  - `Mobile/LeggiCartellinoMain.aspx` legge barcode/QRCode, carica dettaglio cartellino da `vwKanbanBoard`, consente cambio stato con regole:
    - stati tra 301 e 699 non autorizzati;
    - da stato 300 non si puo tornare sotto 300;
    - salvo stati 100 e 700, il salto non puo superare 100.
- Scanner standalone:
  - Usa `html5-qrcode.min.js`, beep e modale fotocamera.
- Stampa:
  - Invia ZPL a stampante di rete su porta 9100.

## Schema database rilevante

Tabelle applicative rilevate:

- `Users`: `Guid`, `IDUser`, `Username`, `Password`, `Name`, `Surname`, `Role`, `Department`, `Enabled`, `CreateDate`, `LastModify`, `Sector`.
- `Cards`: `Guid`, `IDCard`, `IDCombination`, `Quantity`, `Status`.
- `Combination`: `ID`, `Guid`, `PartnerType`, `IDPartner`, `LocationID`, `IDProduct`, `IDCross`, `PDR`, `MOQ`.
- `Partners`: `Guid`, `PartnerType`, `ID`, `Name`, `Sector`.
- `Products`: `Guid`, `ID`, `Name`, `UMBase`.
- `Status`: `ID`, `Guid`, `Description`, `Color`, `Enabled`.
- `Locations`: `LocationID`, `LocationGuid`, `PartnerType`, `PartnerID`, `LocationCode`, `LocationName`.
- `Sectors`: `SectorID`, `SectorGuid`, `SectorName`.
- `tblLog`: `id`, `ODP`, `IDMix`, `IDRows`, `IDUser`, `Text`, `PostingDate`.

Vista applicativa:

- `vwKanbanBoard`: join tra `Cards`, `Combination`, `Partners`, `Products`, `Status`, `Locations`.

Script di riferimento creato:

- `C:\ASW\repo\Scheletro\Database\KFlow.schema.sql`

## Mapping pagine

| Legacy Web Forms | ASP.NET Core Razor Pages |
| --- | --- |
| `/Client/Login.aspx` | `Pages/Client/Login.cshtml` con route `/Client/Login.cshtml` |
| `/Client/KanbanBoard.aspx` | `Pages/Client/KanbanBoard.cshtml` con route `/Client/KanbanBoard.cshtml` |
| Menu anagrafiche legacy | `Pages/Client/Clienti.cshtml` con route `/Client/Clienti.cshtml` |
| Menu anagrafiche legacy | `Pages/Client/Articoli.cshtml` con route `/Client/Articoli.cshtml` |
| `/Mobile/Default.aspx` | `Pages/Mobile/Default.cshtml` con route `/Mobile/Default.cshtml` |
| `/Mobile/LeggiCartellinoMain.aspx` | `Pages/Mobile/LeggiCartellinoMain.cshtml` con route `/Mobile/LeggiCartellinoMain.cshtml` |
| `/BarcodeScanner.aspx` | `Pages/BarcodeScanner.cshtml` con route `/BarcodeScanner.cshtml` |
| `/StampaCartellino.aspx` | `Pages/StampaCartellino.cshtml` con route `/StampaCartellino.cshtml` |
| `/WebForm1.aspx` | `Pages/WebForm1.cshtml`, redirect a Kanban `.cshtml` |
| `Client/MasterPages/Default.Master` | `Pages/Shared/_KFlowLayout.cshtml` |
| `Mobile/MasterPages/Default.Master` | `Pages/Shared/_MobileLayout.cshtml` |

## Decisioni tecniche

- Mantenuto Razor Pages, coerente con lo scheletro esistente.
- Aggiornato EF Core manualmente sullo schema legacy, evitando scaffolding automatico dal `.bak` perche il ripristino temporaneo non era autorizzato.
- Mantenuti nomi tabella/colonna legacy nel modello EF Core.
- Mantenuto accesso diretto a `vwKanbanBoard` e aggiornamento diretto di `Cards`, come nel legacy.
- Configurata `Cards` come tabella con trigger in EF Core per evitare l'uso della clausola SQL Server `OUTPUT` durante `SaveChanges`.
- Aggiunto drag and drop dei cartellini tra colonne Kanban come alternativa alla freccia di cambio stato.
- Aggiunte pagine anagrafiche per clienti e articoli con chiavi legacy non modificabili in edit, per non rompere riferimenti gia usati da `Combination`.
- Sostituito ViewState/PostBack con model binding, handler Razor Pages, `TempData` e form GET/POST.
- Cookie auth ASP.NET Core impostato su 7 giorni e cookie legacy `login` preservato per auto-login CID.
- Connection string resa modificabile in `appsettings.json` senza password reale hardcoded.
- Layout modernizzato con Bootstrap locale e CSS dedicato `wwwroot/css/kflow.css`.
- Copiati nel nuovo progetto solo gli asset necessari: loghi K-Flow/EZ e `html5-qrcode.min.js`.

## Parti incomplete o da verificare manualmente

- Ripristino `.bak`: da eseguire con utente SQL dotato di permesso `CREATE DATABASE` o da SQL Server Management Studio.
- Password legacy: il DB usa password in chiaro come nel sistema originario; non e stato introdotto hashing per non alterare il comportamento.
- Pulsanti impostazioni/elimina sui cartellini erano presenti senza logica server nel legacy; sono visibili come azioni disabilitate.
- Stampa ZPL: migrata come invio rete configurabile, ma richiede verifica con stampante reale.
- Scanner fotocamera: richiede browser con permessi camera e contesto HTTPS/localhost.
- Validazione end-to-end con dati reali richiede configurare la connection string con credenziali locali.
