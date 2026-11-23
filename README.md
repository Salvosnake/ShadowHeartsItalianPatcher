# Shadow Hearts: Covenant Italian Patcher

**La traduzione italiana di Shadow Hearts: Covenant USA, applicata alla tua ISO originale.**

Patcher desktop multipiattaforma scritto in **C# e .NET 10**, con interfaccia **Avalonia UI** in italiano. Riconosce automaticamente il Disco 1 o il Disco 2, verifica la compatibilità dell'immagine e genera una nuova ISO italiana senza modificare l'originale.

Il programma funziona **completamente offline**: applica traduzioni già preparate attraverso pacchetti differenziali aggiornabili separatamente dall'applicazione.

> **Stato: beta.** Traduzione italiana disponibile per i testi censiti di entrambi i dischi USA. Il patcher verifica che la ISO generata corrisponda esattamente alla versione italiana prevista dal pacchetto. La versione beta è aperta alle segnalazioni della comunità.

## Indice

- [Funzionalità](#funzionalità)
- [Compatibilità](#compatibilità)
- [Requisiti](#requisiti)
- [Guida all'utilizzo](#guida-allutilizzo)
- [Pacchetti e aggiornamenti](#pacchetti-e-aggiornamenti)
- [Come funziona](#come-funziona)
- [Compilazione dai sorgenti](#compilazione-dai-sorgenti)
- [Utilizzo da terminale](#utilizzo-da-terminale)
- [Generazione di una nuova patch](#generazione-di-una-nuova-patch)
- [Test e validazione](#test-e-validazione)
- [Build autonome](#build-autonome)
- [Struttura del progetto](#struttura-del-progetto)
- [Problemi comuni](#problemi-comuni)
- [Segnalazioni](#segnalazioni)
- [Licenze e contenuti](#licenze-e-contenuti)

## Funzionalità

- Riconoscimento automatico del disco attraverso struttura ISO, `SYSTEM.CNF` e identificativo dell'eseguibile.
- Verifica SHA-256 dell'intera ISO prima dell'applicazione.
- Interfaccia italiana con selettori di file e cartelle, tema chiaro e scuro.
- Avanzamento, stato delle operazioni e log visualizzabili nella finestra.
- Annullamento delle operazioni e gestione dei temporanei prodotti.
- Creazione di una nuova ISO con verifica finale di dimensione, hash e identificativo del disco.
- Conservazione della sorgente e rifiuto delle sovrascritture di risultati esistenti.
- Elaborazione a blocchi con offset a 64 bit, senza caricare l'intera ISO in RAM.
- GUI e CLI basate sullo stesso motore.
- Pacchetti di traduzione indipendenti e versionati, aggiornabili senza ricompilare la GUI.

## Compatibilità

### Edizioni del gioco

| Edizione | Identificativo | Supporto |
|---|---|---|
| USA — Disco 1 | `SLUS_210.41` | Con pacchetto compatibile con l'esatto hash sorgente |
| USA — Disco 2 | `SLUS_210.44` | Con pacchetto compatibile con l'esatto hash sorgente |
| PAL / Giappone / altre revisioni | Altri identificativi o hash | Non supportate dai pacchetti previsti |

**Il nome del file e il codice SLUS da soli non sono sufficienti.** Ogni pacchetto specifica dimensione e SHA-256 della ISO originale accettata. Un'immagine diversa, già modificata o con un altro layout viene rifiutata anche se appartiene alla stessa edizione.

Sono gestite ISO a settori da **2048 byte** con filesystem ISO9660, comprese immagini ibride ISO9660/UDF attraverso la loro struttura ISO9660. Non sono implementati un lettore UDF generico o il supporto diretto a RAW2352, CSO e CHD.

### Sistemi operativi

| Sistema | Target di pubblicazione | Stato del patcher |
|---|---|---|
| Linux x64 | `linux-x64` | Compilazione ed esecuzione provate su Ubuntu |
| Windows x64 | `win-x64` | Build disponibile; esecuzione sulla piattaforma non verificata |
| macOS Apple Silicon | `osx-arm64` | Build disponibile; esecuzione non verificata, firma non configurata |
| macOS Intel | `osx-x64` | Build disponibile; esecuzione non verificata, firma non configurata |

La disponibilità degli script di build non implica che siano già disponibili release pubbliche scaricabili. Le build macOS non includono una procedura di firma o notarizzazione.

## Requisiti

Per applicare una patch servono:

1. La propria ISO originale USA, corrispondente alla versione supportata dal pacchetto.
2. L'applicazione compilata per il sistema operativo utilizzato.
3. Il pacchetto di traduzione compatibile, ottenuto separatamente da una fonte attendibile.
4. Spazio libero per **una nuova ISO completa**, più il margine richiesto dal programma.
5. Una destinazione che consenta file delle dimensioni previste: FAT32 non può contenere file da 4 GiB o superiori.

Le build **self-contained** non richiedono l'installazione di .NET, Python o strumenti di traduzione. Restano necessarie le librerie di sistema usate dall'interfaccia, ad esempio quelle grafiche e dei font su Linux.

Questo repository non fornisce ISO originali, ISO italiane complete, BIOS PS2 o archivi proprietari del gioco.

## Guida all'utilizzo

### 1. Predisponi i pacchetti

Estrai l'applicazione e conserva i pacchetti nella cartella `Patches` accanto agli eseguibili:

```text
ShadowHeartsItalianPatcher.Desktop[.exe]
ShadowHeartsItalianPatcher.Cli[.exe]
Patches/
├── Disc1/
│   └── 1.1.0/
│       ├── manifest.json
│       └── translation.shid
└── Disc2/
    └── 1.1.0/
        ├── manifest.json
        └── translation.shid
```

`1.1.0` è una versione di esempio. Usa i file e le versioni effettivamente forniti con il pacchetto, senza modificarne il manifest.

### 2. Seleziona la ISO originale

Avvia `ShadowHeartsItalianPatcher.Desktop` e premi **Sfoglia…**. Il programma legge la ISO, riconosce il disco e calcola SHA-256. Per immagini di diversi gigabyte la verifica può richiedere tempo.

La finestra mostra disco, regione, esito della verifica e versione della traduzione disponibile. La generazione è abilitata soltanto quando viene trovato un pacchetto compatibile e viene scelta una cartella valida.

### 3. Genera la ISO italiana

Scegli la directory di destinazione e premi **Genera ISO italiana**. La ISO sorgente rimane aperta in sola lettura. Il risultato finale si chiama:

```text
ShadowHeartsCovenant_USA_Disc1_Italian.iso
ShadowHeartsCovenant_USA_Disc2_Italian.iso
```

Viene generato soltanto il disco selezionato. Per tradurre anche l'altro, ripeti la procedura sulla rispettiva ISO originale.

### 4. Attendi la verifica finale

Il completamento viene dichiarato dopo la verifica della nuova ISO e un ulteriore controllo dell'originale. Puoi aprire la destinazione con **Apri cartella**.

Se esiste già un file con il nome finale previsto, scegli un'altra directory: il patcher non lo sovrascrive. Il pulsante **Annulla** richiede l'interruzione dell'operazione e la rimozione del temporaneo di quella stessa operazione.

## Pacchetti e aggiornamenti

Applicazione e traduzione hanno versioni indipendenti. Ogni pacchetto contiene:

- Il file differenziale `translation.shid`.
- Un `manifest.json` con versione della traduzione e identificativo del disco.
- Hash SHA-256 e dimensioni di sorgente, patch e risultato atteso.
- Versione minima del patcher e informazioni di compatibilità.

Per aggiornare la traduzione, aggiungi il nuovo pacchetto nella directory del disco corrispondente. Per un determinato hash sorgente il programma sceglie la **versione numerica compatibile più recente**. Non occorre ricompilare l'applicazione.

Le patch sono pensate per partire dalla **ISO originale USA**, anche negli aggiornamenti: non applicare un nuovo pacchetto a una ISO italiana precedente.

Gli hash verificano integrità e compatibilità, ma **non autenticano l'autore**. I manifest non hanno firma digitale: installa soltanto pacchetti attendibili. I pacchetti reali sono esclusi dal controllo versione di questo repository.

## Come funziona

Il formato **SHID** descrive come ricostruire la ISO italiana usando quella originale:

| Istruzione | Operazione |
|---|---|
| `COPY` | Copia una sequenza di byte dalla ISO originale |
| `LITERAL` | Inserisce nuovi byte contenuti nel pacchetto |
| `ZERO` | Scrive una sequenza di zeri |
| `END` | Termina la ricostruzione |

Il flusso di istruzioni è compresso con Brotli. SHID è il formato implementato da questo progetto: non è una patch xdelta/VCDIFF.

L'applicazione non esegue i repacker PS2 durante il patching. Ricostruisce esattamente la ISO italiana di riferimento, conservandone il layout e le correzioni tecniche. Il file viene scritto inizialmente come `.ship-<identificatore>.partial` e rinominato soltanto dopo i controlli.

Dettagli del formato, del generatore e delle verifiche: [PATCHING_ARCHITECTURE.md](PATCHING_ARCHITECTURE.md).

## Compilazione dai sorgenti

Serve l'**SDK .NET 10** compatibile con [global.json](global.json). Il ripristino delle dipendenze richiede accesso a NuGet; l'applicazione compilata funziona offline.

Dalla radice del repository:

```sh
dotnet --info
dotnet restore ShadowHeartsItalianPatcher.sln
dotnet build ShadowHeartsItalianPatcher.sln -c Release
dotnet test ShadowHeartsItalianPatcher.sln -c Release
dotnet run -c Release --project src/ShadowHeartsItalianPatcher.Desktop
```

Il codice segue le regole di [.editorconfig](.editorconfig). Per verificare la formattazione C#:

```sh
dotnet format whitespace ShadowHeartsItalianPatcher.sln --no-restore --verify-no-changes
```

Durante lo sviluppo la variabile `SHIP_PATCHES` permette alla GUI di usare una directory di pacchetti diversa da quella accanto all'eseguibile. Esempio Bash:

```sh
SHIP_PATCHES="$PWD/Patches" \
  dotnet run -c Release --project src/ShadowHeartsItalianPatcher.Desktop
```

## Utilizzo da terminale

La CLI riutilizza lo stesso servizio della GUI. Gli esempi seguenti partono dalla radice del repository; sostituisci i nomi delle immagini con i tuoi file.

### Riconoscere una ISO

```sh
dotnet run -c Release --project src/ShadowHeartsItalianPatcher.Cli -- \
  inspect "original.iso" "Patches"
```

### Applicare la patch

La cartella di destinazione deve esistere e non deve già contenere il risultato con lo stesso nome:

```sh
mkdir -p output

dotnet run -c Release --project src/ShadowHeartsItalianPatcher.Cli -- \
  apply "original.iso" "output" "Patches"
```

### Controllare un pacchetto

```sh
dotnet run -c Release --project src/ShadowHeartsItalianPatcher.Cli -- \
  audit "Patches/Disc1/1.1.0"
```

`audit` verifica hash, struttura e istruzioni della patch e riepiloga i byte copiati, letterali e zero. Non sostituisce una prova di applicazione né una revisione dei diritti di distribuzione.

Con le build autonome puoi sostituire il prefisso `dotnet run ... --` con `./ShadowHeartsItalianPatcher.Cli` su Linux o `ShadowHeartsItalianPatcher.Cli.exe` su Windows.

I risultati sono scritti in JSON su standard output; avanzamento ed errori su standard error. `Ctrl+C` richiede l'annullamento.

| Codice di uscita | Significato |
|---:|---|
| `0` | Operazione riuscita |
| `1` | Errore gestito |
| `2` | Sintassi del comando non valida |
| `3` | Con `inspect`, ISO riconosciuta ma pacchetto compatibile assente |
| `130` | Operazione annullata |

## Generazione di una nuova patch

Questa procedura è destinata a chi mantiene la traduzione. Richiede la ISO originale supportata e la nuova ISO italiana di riferimento, già ricostruita e verificata con gli strumenti della traduzione.

```sh
dotnet run -c Release --project src/ShadowHeartsItalianPatcher.Cli -- \
  create \
  "original.iso" \
  "italian-reference.iso" \
  "Patches/Disc1/1.1.0" \
  "1.1.0" \
  "Traduzione italiana beta aggiornata"
```

La directory del nuovo pacchetto **non deve esistere**. La versione deve essere numerica, ad esempio `1.1.0`. Il comando produce patch, manifest e `generation-report.json` tramite una directory di staging temporanea.

Per il Disco 2 usa le relative immagini e una directory `Patches/Disc2/...`. Non usare come sorgente una ISO ricostruita da file estratti presumendo che coincida con l'immagine originale dell'utente.

Dopo la generazione:

1. Applica il pacchetto alla ISO originale in una directory di prova separata.
2. Confronta **tutti i byte** del risultato con la ISO italiana di riferimento.
3. Controlla gli hash e verifica che la sorgente sia rimasta invariata.
4. Esegui il collaudo in gioco appropriato alle modifiche.
5. Revisiona contenuti, attribuzioni e licenze prima di predisporre una distribuzione.

Il generatore indicizza blocchi della sorgente e usa più memoria del decoder. Il limite attuale dell'indice è di quattro milioni di blocchi da 2048 byte, circa 8 GB di sorgente; questo limite non appartiene al decoder.

## Test e validazione

I test xUnit impiegano fixture sintetiche, senza includere immagini del gioco. La suite copre:

- Riconoscimento di entrambi i dischi e rifiuto di strutture corrotte o non supportate.
- Hash incompatibili, patch corrotte e versioni non supportate.
- Ricostruzione esatta e conservazione dell'originale.
- Destinazioni preesistenti e spazio insufficiente.
- Annullamento, errori durante la scrittura e pulizia dei temporanei dell'operazione.
- Offset oltre 4 GiB, limiti delle istruzioni e casi di overflow.
- Gestione dello staging e conteggio dei byte nell'audit.

Per una prova locale con immagini reali, su Bash:

```sh
SOURCE_ISO="original.iso" \
REFERENCE_ISO="italian-reference.iso" \
PACKAGE_DIR="Patches" \
OUTPUT_DIR="output-integration" \
  ./scripts/integration.sh
```

Usa una directory di destinazione nuova o vuota. Lo script applica la patch, confronta il risultato con `cmp` e calcola gli hash con `sha256sum`.

**Tre verifiche diverse:** i test automatici controllano il motore; il confronto binario controlla la ricostruzione della ISO di riferimento; le prove in gioco verificano il comportamento della traduzione. Nessuna di queste prove, da sola, dimostra il completamento delle altre.

## Build autonome

Gli script creano applicazione desktop e CLI self-contained, con pubblicazione single-file e librerie native estratte dal bundle quando necessario.

Da Bash:

```sh
./scripts/publish.sh linux-x64
./scripts/publish.sh win-x64
./scripts/publish.sh osx-arm64
./scripts/publish.sh osx-x64

# Tutti i target:
./scripts/publish.sh all
```

Da PowerShell:

```powershell
./scripts/publish.ps1 -Rid win-x64
# Oppure tutti i target:
./scripts/publish.ps1 -Rid all
```

Gli output sono in `artifacts/<target>/`. Gli script generano file locali e non pubblicano release online. Non includono normalmente i pacchetti di traduzione. Per includerli nel collaudo privato:

```sh
./scripts/publish.sh linux-x64 --with-local-patches
```

```powershell
./scripts/publish.ps1 -Rid win-x64 -WithLocalPatches
```

La cross-compilazione non sostituisce l'esecuzione sulla piattaforma finale. La directory interna `private/` non viene copiata dagli script. Prima di distribuire una build controlla sempre i contenuti della directory di output, soprattutto se già usata per collaudi privati.

## Struttura del progetto

```text
ShadowHeartsItalianPatcher.sln
src/
├── ShadowHeartsItalianPatcher.Core/
├── ShadowHeartsItalianPatcher.Infrastructure/
├── ShadowHeartsItalianPatcher.Desktop/
└── ShadowHeartsItalianPatcher.Cli/
tests/
└── ShadowHeartsItalianPatcher.Tests/
scripts/
Patches/
docs/
licenses/
```

| Progetto | Responsabilità |
|---|---|
| **Core** | Modelli, manifest, interfacce e regole indipendenti dalla GUI |
| **Infrastructure** | ISO9660, SHA-256, catalogo, codec SHID, filesystem e generatore dei pacchetti |
| **Desktop** | Viste Avalonia, MVVM, comandi asincroni e Dependency Injection |
| **Cli** | Comandi `inspect`, `apply`, `create` e `audit` |
| **Tests** | Test unitari e di integrazione sintetica con xUnit |

Directory di build, output locali, pacchetti reali e documentazione interna sono escluse tramite [.gitignore](.gitignore). Le dipendenze e le relative licenze sono documentate in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md), [docs/dependency-licenses.json](docs/dependency-licenses.json) e `licenses/dependencies/`.

## Problemi comuni

| Messaggio o situazione | Cosa controllare |
|---|---|
| Nessun pacchetto compatibile | Presenza del manifest nella directory corretta e corrispondenza di hash, dimensione e disco |
| SHA-256 della ISO non supportato | La ISO deve essere quella originale esatta prevista dal pacchetto; il solo nome o seriale non basta |
| Patch corrotta | Ripristinare il pacchetto da una fonte attendibile, senza modificare il manifest |
| ISO finale già presente | Scegliere una directory diversa o spostare consapevolmente il risultato precedente |
| Spazio insufficiente | Liberare spazio sul volume di destinazione e verificare i limiti del filesystem |
| Verifica iniziale lunga | Il programma sta leggendo diversi gigabyte per calcolare l'hash; la velocità dipende dal dispositivo |
| File `.partial` dopo arresto forzato | Non è un risultato verificato; a patcher fermo può essere rimosso dopo averne controllato origine e percorso |
| Gioco bloccato dopo patch riuscita | Conservare versione del pacchetto, punto di riproduzione e log: indicare anche configurazione dell’emulatore e log; la sola riuscita della patch non identifica la causa del blocco |

Un'interruzione di corrente o la terminazione forzata possono impedire la pulizia dei temporanei. Il programma non cancella automaticamente file residui al successivo avvio.

## Segnalazioni

Per segnalare un problema usa le [Issues del progetto](https://github.com/Salvosnake/ShadowHeartsItalianPatcher/issues), includendo:

- Sistema operativo e versione del patcher.
- Disco e versione del pacchetto utilizzato.
- Operazione eseguita e messaggio di errore.
- Passaggi necessari per riprodurre il problema.
- Per problemi in gioco: scena o menu interessato, versione dell'emulatore e log pertinente.

Prima di allegare log o screenshot, rimuovi percorsi personali, credenziali e altri dati privati. **Non allegare ISO, BIOS o archivi completi del gioco.**

## Licenze e contenuti

Il codice originale del patcher è distribuito sotto [licenza MIT](LICENSE). Gli avvisi delle dipendenze sono in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) e nella directory `licenses/`.

La licenza del patcher non concede diritti su Shadow Hearts: Covenant, sui suoi asset o sui contenuti delle patch derivate. Nomi e marchi appartengono ai rispettivi titolari. Il progetto è indipendente e non è affiliato ai titolari del gioco.

Una patch differenziale può contenere frammenti di dati proprietari: dimensioni ridotte e hash corretti non costituiscono autorizzazione alla sua distribuzione. Pacchetti di traduzione, attribuzioni e contenuti richiedono una revisione separata prima della pubblicazione.
