# Pacchetti offline

Collocare i pacchetti indipendenti in `Patches/Disc1/VERSIONE/` o `Patches/Disc2/VERSIONE/`, accanto agli eseguibili. Ogni pacchetto contiene `manifest.json` e il file `translation.shid` indicato dal manifest.

I manifest identificano esattamente ISO sorgente, patch e risultato tramite dimensioni e SHA-256. Per aggiornare la traduzione aggiungere un pacchetto con versione numerica successiva. Usare soltanto fonti attendibili: gli hash non autenticano l'autore.

I pacchetti locali, inclusi manifest e report, sono esclusi da Git. La build di distribuzione li include soltanto con l'opzione esplicita per il collaudo privato. Nessuna ISO, BIOS o risorsa completa del gioco deve essere pubblicata nel repository.
