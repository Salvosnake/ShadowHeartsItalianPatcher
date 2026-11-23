# Architettura del patching

## Metodo

L'applicazione usa un delta binario tra una ISO originale esatta e una ISO italiana di riferimento. Questo evita di duplicare nel patcher i parser, i repacker e gli strumenti di masterizzazione della traduzione. Il risultato conserva esattamente i byte, gli offset e il layout della candidata di riferimento.

Il formato gestito SHID usa Brotli del runtime .NET e non richiede eseguibili esterni. Non è compatibile con xdelta/VCDIFF. Una ricostruzione strutturata richiederebbe mantenere una seconda implementazione dei formati PS2; una libreria delta alternativa può essere aggiunta tramite `IDeltaCodec` con una nuova versione di algoritmo.

## Formato SHID0001

L'header contiene 8 byte ASCII `SHID0001`, seguiti dalla dimensione finale come Int64 little-endian. Il corpo è un flusso Brotli di istruzioni:

| Opcode | Dati | Effetto |
|---|---|---|
| 0 | Nessuno | Fine, ammessa solo alla dimensione attesa |
| 1 | Offset e lunghezza Int64 | Copia dalla sorgente |
| 2 | Lunghezza Int64 e byte | Inserisce un letterale, massimo 4 MiB |
| 3 | Lunghezza Int64 | Scrive zeri |

Il generatore indicizza blocchi da 2048 byte tramite una chiave di 128 bit derivata da SHA-256 e confronta byte per byte ogni candidato COPY. Accorpa copie contigue e sequenze di zeri. L'indice è limitato a quattro milioni di blocchi; il decoder usa offset a 64 bit e un buffer da 1 MiB senza costruire tale indice.

## Verifiche e transazione

Il manifest specifica algoritmo, versione, disco, dimensioni e SHA-256 di sorgente, patch e destinazione. Il servizio controlla spazio disponibile, pacchetto, identità ISO9660, SYSTEM.CNF, firma ELF e hash sorgente prima di applicare il delta.

Il risultato viene scritto in un temporaneo nella destinazione. Dopo flush, controllo dimensione, hash e identità del risultato, viene riaperta e ricontrollata la sorgente. Solo allora il temporaneo viene rinominato, senza sovrascritture. Errori e annullamento eliminano soltanto il temporaneo dell'operazione corrente.

Il generatore usa una directory di staging casuale, verifica nuovamente le immagini di riferimento e rende disponibile il pacchetto con un rename finale. La validazione reale di un pacchetto richiede una successiva applicazione e un confronto integrale con la ISO di riferimento.

## Confini della verifica

L'ispettore legge ISO9660 e i file di avvio nella radice; non è un estrattore UDF generale né un validatore di tutte le risorse PS2. L'hash completo vincola il pacchetto a un layout sorgente esatto. L'identità dei byte con il riferimento non sostituisce il collaudo del gioco.

I test unitari impiegano fixture sintetiche. Immagini, report di integrazione con percorsi locali, checkpoint e pacchetti reali restano fuori dal repository pubblico. Gli hash non autenticano il produttore del pacchetto e non costituiscono autorizzazione alla distribuzione dei suoi contenuti.
