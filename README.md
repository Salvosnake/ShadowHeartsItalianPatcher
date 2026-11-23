# Shadow Hearts: Covenant Italian Patcher

Applicazione desktop C# e Avalonia per generare una nuova ISO italiana di **Shadow Hearts: Covenant USA** dalla propria ISO originale supportata. Funziona offline e conserva il file originale.

## Stato

Il patcher supporta i pacchetti per Disco 1 (`SLUS_210.41`) e Disco 2 (`SLUS_210.44`). Ogni pacchetto accetta soltanto la precisa immagine sorgente identificata nel relativo manifest tramite dimensione e SHA-256.

Le traduzioni sono candidate al beta testing. La verifica del patcher certifica l'identità del risultato rispetto all'immagine di riferimento del pacchetto; non certifica una partita completa o la compatibilità con ogni configurazione di emulazione.

Questo repository contiene il codice dell'applicazione, fixture sintetiche e licenze. Non include ISO, BIOS, risorse originali del gioco o pacchetti di traduzione. I pacchetti devono essere ottenuti separatamente da una fonte attendibile.

## Utilizzo

1. Collocare i pacchetti compatibili nella directory `Patches`, accanto all'eseguibile.
2. Avviare l'applicazione e selezionare la propria ISO originale USA.
3. Attendere il riconoscimento automatico del disco e la verifica SHA-256.
4. Selezionare una cartella di destinazione con spazio sufficiente.
5. Premere **Genera ISO italiana**.

Il risultato viene scritto in un temporaneo e rinominato soltanto dopo i controlli. Una ISO finale preesistente non viene sovrascritta. È possibile annullare l'operazione. Le build self-contained non richiedono l'installazione di .NET.

Sono supportate ISO a settori da 2048 byte con filesystem ISO9660, anche in immagini ibride ISO9660/UDF. PAL, giapponese, immagini modificate, RAW2352, CSO e CHD non sono supportati da questi pacchetti.

## Sviluppo

Prerequisito: SDK .NET 10 compatibile con `global.json`. Il ripristino iniziale richiede accesso a NuGet; l'applicazione non usa servizi online durante il patching.

```sh
dotnet restore ShadowHeartsItalianPatcher.sln
dotnet build ShadowHeartsItalianPatcher.sln -c Release
dotnet test ShadowHeartsItalianPatcher.sln -c Release
dotnet run --project src/ShadowHeartsItalianPatcher.Desktop
```

La variabile facoltativa `SHIP_PATCHES` permette di indicare la directory dei pacchetti alla GUI durante lo sviluppo.

## Architettura

| Progetto | Responsabilità |
|---|---|
| Core | Modelli, interfacce e regole dei manifest |
| Infrastructure | Lettura ISO, SHA-256, delta binario e operazioni sui file |
| Desktop | Avalonia, MVVM e Dependency Injection |
| Cli | Ispezione, applicazione, generazione e audit dei pacchetti |
| Tests | Test xUnit con fixture sintetiche e stream di grandi dimensioni |

Il formato SHID usa istruzioni COPY/LITERAL/ZERO compresse con Brotli. Il risultato deve coincidere con l'hash della ISO italiana indicato dal manifest. Vedere [PATCHING_ARCHITECTURE.md](PATCHING_ARCHITECTURE.md).

## CLI e aggiornamenti delle patch

```sh
dotnet run -c Release --project src/ShadowHeartsItalianPatcher.Cli -- inspect "original.iso" "Patches"
dotnet run -c Release --project src/ShadowHeartsItalianPatcher.Cli -- apply "original.iso" "output" "Patches"
dotnet run -c Release --project src/ShadowHeartsItalianPatcher.Cli -- create "original.iso" "italian-reference.iso" "Patches/Disc1/1.1.0" "1.1.0" "Candidata beta"
dotnet run -c Release --project src/ShadowHeartsItalianPatcher.Cli -- audit "Patches/Disc1/1.1.0"
```

La cartella di output deve esistere. La directory di un nuovo pacchetto deve essere inesistente. Per il Disco 2 usare le rispettive immagini e `Patches/Disc2/...`.

Ogni aggiornamento della traduzione genera un nuovo pacchetto dalla ISO originale alla nuova candidata italiana. Applicarlo e confrontare integralmente l'output con il riferimento prima di distribuirlo. Il patcher seleziona la versione numerica compatibile più recente: non serve ricompilare la GUI.

Per l'integrazione locale, `scripts/integration.sh` usa le variabili `SOURCE_ISO`, `REFERENCE_ISO`, `PACKAGE_DIR` e `OUTPUT_DIR`. Le immagini reali non devono essere aggiunte ai test o al repository.

## Build autonome

```sh
./scripts/publish.sh linux-x64
./scripts/publish.sh win-x64
./scripts/publish.sh osx-arm64
./scripts/publish.sh osx-x64
```

Su Windows: `./scripts/publish.ps1 -Rid win-x64`. I risultati sono in `artifacts/`, esclusa da Git. La cross-compilazione non sostituisce i test sul sistema di destinazione. Firma e notarizzazione non sono configurate.

Per collaudo privato si possono includere pacchetti locali con `--with-local-patches` o `-WithLocalPatches`. Queste opzioni non costituiscono una pubblicazione online. La directory interna `private/` è esclusa da Git e non viene copiata dagli script di pubblicazione.

## Limiti e sicurezza

I pacchetti non hanno firma digitale: SHA-256 verifica integrità e compatibilità, non l'identità dell'autore. Usare soltanto pacchetti attendibili. Dopo una chiusura forzata o una mancanza di corrente può rimanere un file `.partial`, mai dichiarato ISO finale valida.

## Licenze

Il codice originale del patcher è distribuito sotto licenza MIT. Le licenze delle dipendenze sono in [THIRD_PARTY_NOTICES.md](THIRD_PARTY_NOTICES.md) e `licenses/`. La licenza del codice non concede diritti sui contenuti del gioco o sulle patch derivate. La distribuzione dei pacchetti richiede una revisione separata dei contenuti e dei diritti applicabili.
