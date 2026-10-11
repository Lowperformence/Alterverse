# Dimensions Front – Online-Ranked, Version 1

## Spielregeln

- Eigener **Solo-5v5-Modus** neben Casual und Offline. Zehn echte Spieler müssen verbunden sein und den Start bestätigen. Kein Bot-Fill beim Ranked-Start; bei fehlenden Spielern wird ohne Punkte abgebrochen.
- Spieler wählen ihren Helden vor der Suche. Gleiche Helden sind in dieser ersten Ranked-Version erlaubt; ein Draft-/Ban-System ist nicht enthalten. Der Server ersetzt im Ranked keinen gewählten Helden wegen einer Doppelwahl.
- Fünf Platzierungsspiele, Startwert 1000. Danach Bronze, Silber, Gold, Platin, Diamant, Meister und Dimensions-Legende; bis Meister jeweils Division 3 → 2 → 1. Backend/API verwenden englische Rangnamen.
- Eine einzige serverseitige Elo-Zahl dient als Matchmaking-Wert und sichtbare Rangpunkte. Team-Durchschnitt entscheidet die erwartete Siegchance; K=40 während Platzierung und K=24 danach. Bei ausgeglichenen Teams: zunächst ±20, später ±12. Gegen stärkere Gegner mehr Gewinn und weniger Verlust, gegen schwächere umgekehrt. Keine Punkte für Kills, Schaden oder lokale Profilstatistik.
- Ranggrenzen: Bronze <1000; Silber 1000; Gold 1200; Platin 1400; Diamant 1600; Meister 1800; Dimensions-Legende 2000. Punkte fallen nicht unter null.
- Top 50 je Saison, nach abgeschlossener Platzierung. Rangdaten liegen außerhalb der vom Client editierbaren Cloud-Profile. Anzeige enthält Namen und Rang, keine Account-IDs.
- Mindestens 60 Sekunden unverbundener Spielclient: zusätzliche 10 Punkte Abzug, mindestens 10 Punkte Verlust auch bei Teamsieg, keine Siegzählung für den Leaver und 15 Minuten Suchsperre. Ein Verbindungsabbruch wird damit ebenso behandelt wie absichtliches Verlassen. Das vorhandene Dedicated-Protokoll enthält noch keine Wiederbeitrittsfunktion nach Matchstart.
- Ranked startet keine automatische Wiederholungsrunde mit derselben Gruppe. Nach dem Ergebnis zurück ins Menü und erneut suchen.
- S1 wird dauerhaft gespeichert. Saisonwechsel erfolgt kontrolliert über `DF_RANKED_SEASON`; vor dem Wechsel Suche stoppen und laufende Matches abschließen. Neue Saison startet bei 1000, historische Daten bleiben gespeichert. Kein automatischer Kalenderwechsel, Rangverfall oder kosmetisches Saisonbelohnungssystem in dieser Version.

## Online-Ablauf und Vertrauensgrenzen

1. Unity-Login authentifiziert `/ranked/search` am bestehenden Account-Server. Der Server lädt die echte Elo-Zahl und erstellt eine Solo-Group im Profil `dimensions-front-ranked` mit `elo_rating` und `queue=ranked`. Der Client kann keine Rating-Zahl festlegen.
2. `/ranked/poll` und `/ranked/cancel` arbeiten ausschließlich mit der eigenen serverseitig gespeicherten Group-/Member-ID. Wiederholte Suche erzeugt keine weitere Group. Reservierungen laufen nach zehn Minuten ab, Matchmaker-Tickets nach fünf Minuten.
3. Account-Server signiert eine maximal zehn Minuten gültige Admission mit Account, Saison, Rating und genau der eigenen Ticket-ID. Kein Signierschlüssel befindet sich im Client.
4. Dedicated-Server verifiziert wie bisher das Ticket gegen die injizierten Edgegap-Tickets und Teams. Ranked-Deployment verlangt zusätzlich Admissions aller zehn Spieler. Nach den zehn Plan-ACKs registriert der Server das vollständige Roster am Account-Backend.
5. Registrierung prüft Signatur, Ablauf, Saison, zehn eindeutige Accounts/Tickets, 5v5 und noch aktive Ticketbindung. Admissions können nicht in zwei Matches verwendet werden. Ein abgebrochener Start wird mit einer Tombstone-Quittung ungültig gemacht, auch wenn eine Registrierung verspätet ankommt.
6. Nur das Dedicated-Backend kann `/ranked/server/register`, `/ranked/server/result` und `/ranked/server/void` aufrufen: separater Bearer-Serverschlüssel. Spieler-JWTs sind dafür ungültig. Der serverseitige Match-Ende-Event entscheidet den Sieger.
7. Rangpunkte aller zehn Accounts und die Match-Quittung werden in einer SQLite-/Firestore-Transaktion gespeichert. Wiederholung desselben Ergebnisses ist idempotent; widersprüchliches Ergebnis wird abgelehnt. Der Server wiederholt bei Ausfall dieselbe Quittung und wartet mit dem normalen Self-Stop bis zur Bestätigung. Ein externer harter Deployment-Stopp kann diese Wiederholung dennoch unterbrechen; solche Matches benötigen Betriebsprüfung. Es gibt noch keinen externen Ergebnis-Outbox-Worker.

## Bereitstellung – erforderlich, bevor es live spielbar ist

Der Quellcode alleine aktiviert keinen Cloud-Dienst. Unity-/Android- und Linux-Dedicated-Builds sowie Edgegap-/Account-Konfiguration sind notwendig. Dedicated-Hello/Plan verwendet jetzt Protokoll **3**; Casual und Ranked müssen mit aktualisiertem Client und Server laufen.

### Account-Server

Bestehendes `Server/commerce` neu deployen; vorhandene Unity-Authentifizierung, HTTPS und dauerhafte SQLite- bzw. Firestore-Speicherung bleiben Voraussetzung. Zusätzlich ausschließlich serverseitig setzen:

| Variable | Wert/Zweck |
|---|---|
| `DF_ENABLE_RANKED` | `1` |
| `DF_RANKED_MATCHMAKER_URL` | HTTPS-Basisadresse des verwendeten Matchmakers |
| `DF_RANKED_MATCHMAKER_TOKEN` | Group-Up-Matchmaker-Token, kein Edgegap-Account-API-Key |
| `DF_RANKED_ADMISSION_KEY` | Eigenes zufälliges Secret mit mindestens 32 Zeichen; persistent und separat vom Serverschlüssel |
| `DF_RANKED_SERVER_KEY` | Separates zufälliges Secret mit mindestens 32 Zeichen |
| `DF_RANKED_SEASON` | `S1` |

Secrets über den Deployment-Secret-Store bereitstellen; nicht im Unity Resources-Ordner, Git oder Client-Build. Alle Account-Worker müssen dieselben Schlüssel und Saison verwenden. Für Cloud Run wie bisher Firestore verwenden, keine ephemere SQLite-Datei.

### Edgegap und Dedicated-Server

- `Server/matchmaking/edgegap-matchmaker-v1.json` importieren/validieren. Das zusätzliche Profil `dimensions-front-ranked` ist unabhängig von Casual; unverändert 2 Teams × 5 Spieler in jeder Expansion. Rating-Differenz zunächst 150, nach 60 Sekunden 250, nach 120 Sekunden 400. Wartedauer höchstens fünf Minuten; keine Erweiterung auf kleinere Teams.
- Aktualisierten Linux-Dedicated-Build erstellen. Die zusätzliche App-Version `v0.1.1-ranked-server` anlegen, mit derselben Game-Port-Zuordnung wie die bestehende Casual-Version.
- Auf dieser Ranked-App-Version `DF_RANKED_SERVER=1`, `DF_RANKED_ACCOUNT_URL=<Account-HTTPS-Endpunkt>` und denselben `DF_RANKED_SERVER_KEY` wie am Account-Server injizieren. `ARBITRIUM_*` und `MM_*` werden weiterhin von Edgegap geliefert. Den Admission-Signierschlüssel braucht das Spielserver-Image nicht.
- Auf Casual `DF_RANKED_SERVER` nicht aktivieren. Auch die Casual-App-Version auf den neuen Dedicated-Protokollstand aktualisieren.
- `Assets/Alterverse/Resources/Commerce/AccountServerConfig.json` muss auf den aktualisierten Account-Server zeigen. Android-Client neu bauen; Ranked-Button im PLAY-Menü öffnet Rang, Top 50, Suche und Abbrechen.

### Abnahme

1. Zehn getrennte Accounts/Clients starten, Solo Ranked wählen und volle 5v5-Zuteilung prüfen. Mit neun Clients darf Ranked nicht mit Bots starten; Casual-Bot-Fill dagegen weiterhin prüfen.
2. Gewinner/Verlierer prüfen, Account-Neustart und Gerätewechsel: gleiche Punkte, gleiche Saison. Dasselbe Serverergebnis erneut senden: keine weitere Änderung.
3. Platzierung, Top 50, Abbruch vor Zuteilung und verspätete Antworten prüfen. Langer Disconnect: Leaver-Punkte/Sperre; kurze Unterbrechung darf keine Leaver-Strafe auslösen, Wiederbeitritt ist in V1 nicht implementiert.
4. Backend kurz unterbrechen, Ergebnis wiederholen lassen und anschließend die Punkte prüfen. Hard-Stop/Crash operativ prüfen; unbestätigte Ergebnisse werden vom Client nicht erfunden.
5. Android-Sichtprüfung: Ranked-Menü, Suche, Cancel und Ergebnistext auf kleinem Bildschirm.

## Automatische Validierung

`python -m unittest discover -s Server/commerce -q`: bestehende Account-/Commerce-/Social-Tests plus Ranked mit SQLite und strengem Firestore-Transaktionsfake. Kein Test greift auf ein Produktivkonto zu.

`python Tools/Ranked/Regression/run.py --dotnet-root <.NET-8-Installationsverzeichnis>`: kompiliert neun echte C#-Klassen, einschließlich Ranked-Service, Reporter, Dedicated-Handshake und Panel, gegen Engine-/Netzwerk-Substitute; prüft 29 Roster-/Modus-Invarianten. Dies ersetzt keinen Unity-Build, Matchmaker-Konfigurationsimport oder Test mit zehn echten Online-Clients.

Implementierungsprüfung: Backend-Tests bestanden; C#-Kompilation und 29 Invarianten bestanden; C#-Syntaxprüfung bestanden. Unity-/Android-Build, produktiver Matchmaker-Import und Live-5v5 sind in dieser Arbeitsumgebung nicht ausgeführt.
