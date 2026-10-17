# Offline/Online-Abgleich und offene Lastfreigabe — 17.10.2026

Ausgangspunkt: dev/vertical-slice a7cba6aa43674421289dd32f154882f580c4b3eb. Der laufende v0.1.6-Container enthält diese neuen Reparaturen noch nicht. Die neue Android-App wurde bisher nicht als auf dem Testhandy installiert bestätigt.

## Gefundene und reparierte Unterschiede

| Ablauf | Fehler im Online-Weg | Korrektur |
|---|---|---|
| Eigener Dash | Snapshot meldete erzwungene Bewegung, aber keinen Dash; der Besitzer übernahm den Serverweg nicht. | Dash und erzwungene Bewegung werden an den Besitzer übertragen. Eine Bewegungsrevision markiert Anfang und Ende sowie den Respawn-Endpunkt. |
| Ende von Dash/Haken | Ein bereits unterwegs befindlicher alter Besitzer-Pose konnte nach dem Ende die Serverposition zurückziehen. Das Endpaket konnte auch verloren gehen. | Server akzeptiert nur Besitzer-Posen mit der aktuellen Bewegungsrevision. Spätere normale Snapshots wiederholen die Endrevision und stellen den Endpunkt wieder her. |
| Angriff halten / Fähigkeit erneut drücken | Ein vor dem lokalen Angriff erstellter Snapshot mit Cooldown 0 konnte die laufende lokale Vorhersage löschen. | Fortlaufende Befehlsnummern und Serverbestätigung. Ein Cooldown wird erst übernommen, wenn der Snapshot den betreffenden Befehl bestätigt. Unabhängige Aktionen warten nicht unnötig aufeinander. |
| Abgelehnter Angriff | Lokale Vorhersage braucht auch bei einer erlaubten, aber wegen Cooldown/Spielregeln abgelehnten Anfrage eine Antwort. | Wohlgeformte autorisierte Befehle werden vor der Spielprüfung bestätigt; Duplikate werden nicht noch einmal ausgeführt. |
| Fähigkeit anderer Spieler | Semantischer Cast erzeugte VFX, aber rief die Körperanimation des entfernten Helden nicht auf. | Cast überträgt die tatsächliche Vorbereitungszeit; Empfänger startet die entfernte Animation und berücksichtigt Paketalter. Die eigene vorhergesagte Animation wird nicht erneut gestartet. |
| Alter Android-Client | Ältere Apps konnten denselben Protokollstand wie der neue Server melden, obwohl der allgemeine VFX-Empfänger fehlte. | Dedicated-Protokoll 6 / Party-Protokoll 4 und neue Bewegungs-/Befehlsnachrichten. Server und Android müssen gemeinsam aus demselben Stand gebaut werden. |
| Diagnose kurzer Kampf-Aussetzer | Durchschnittliche CPU-Auslastung zeigt keine einzelnen langsamen Frames. | Kampfbericht enthält serverFps, frameP95Ms und frameMaxMs in begrenzten 10-Sekunden-Intervallen. P95 wird auf ganze Millisekunden aufgerundet, Werte ab 200 ms werden als >=200 ausgewiesen. |

Treffer, Schaden, Heilung, Schilde und Projektilkollision bleiben durch dieselben Gameplay-Klassen auf dem Server autoritativ. Client-VFX führen keinen zusätzlichen Schaden aus.

## Automatisierter Abgleich aller 50 Aktionen

Je Held wurden normaler Angriff und alle vier Fähigkeiten über den Offline-Einstieg und über echte serialisierte Client-Befehle mit dem Server-Empfänger verglichen, jeweils bei 30/60/120 FPS. Szenarien enthalten verwundete Verbündete und einen verwundeten Spieler, damit Heil-/Schildfähigkeiten gültig geprüft werden; die anfängliche Ultimatesperre wird regulär abgewartet.

| Held | Normaler Angriff | Q | W | E | Ultimate |
|---|---|---|---|---|---|
| Zhaev Null | bestanden | bestanden | bestanden | bestanden | bestanden |
| Auren Kest | bestanden | bestanden | bestanden | bestanden | bestanden |
| Sivra Quell | bestanden | bestanden | bestanden | bestanden | bestanden |
| Ione Vaska | bestanden | bestanden | bestanden | bestanden | bestanden |
| Tahla Venn | bestanden | bestanden | bestanden | bestanden | bestanden |
| Brakk Orun | bestanden | bestanden | bestanden | bestanden | bestanden |
| THE TRAVELER | bestanden | bestanden | bestanden | bestanden | bestanden |
| Nerok Abyss | bestanden | bestanden | bestanden | bestanden | bestanden |
| Andromeda Timmy | bestanden | bestanden | bestanden | bestanden | bestanden |
| Void Knight | bestanden | bestanden | bestanden | bestanden | bestanden |

Verglichen werden eigene HP/Schilde, gegnerische HP/Schilde, verbündete HP/Schilde, Dash-Endpunkt, Restcooldown und Reihenfolge semantischer VFX-Ereignisse. Die echte HeroLocomotion wird kompiliert; Kollision, Zeit, Netzwerk und Grafik werden kontrolliert ersetzt. Das ist kein Nachweis nativer Physik, Renderqualität oder gleich schneller Reaktion unter echter Netzwerklatenz.

Zusätzlich erzeugt die echte VFX-Fassade zehn gleichzeitige Flächenwarnungen; jeder Kampfakteur behält eine sichtbare Warnung, und die Warnungen laufen aus. Dieser Grafik-Ersatztest belegt keine GPU-Frametime.

Weitere Prüfungen: normaler/erzwungener Dash, Wandkontakt, Unterbrechung, verlorene Start- und Endsnapshots, verspätete Besitzer-Pakete, neue Runde, doppelte Befehle, unabhängige Befehlsbestätigungen, abgelehnte Befehle, entfernte Körperanimation und keine Wiederholung der eigenen Animation. Vorherige VFX-Prüfungen umfassen die 15 allgemeinen Ereignisse, echte Fernkampf-/Nahkampfbefehle und einen Server ohne unterstützten Grafiktreiber.

## 4v4 mit Leistungsreserve für zehn

**Vom Nutzer bestätigt: 4v4 bleibt bestehen; zehn sind eine Leistungsreserve, kein gewünschter Wechsel auf 5v5. Produktiv ist weiterhin 4v4 konfiguriert: MatchSession.TeamSize=4, MaxPlayers=8; auch das Matchmaking ist 4v4. Zehn Spieler werden damit nicht als gültige Runde aufgenommen. Das Spielformat, Roster, Matchmaking und die Zulassung bleiben unverändert.**

Der reale Snapshot-Schreiber erzeugt 912 Anwendungsbytes für acht Helden. Derselbe Datensatz für zehn Helden wäre 1.138 Bytes; Transport-/Nachrichtenheader kommen hinzu. Bei derzeit 12 Snapshots/s entspräche das für zehn Empfänger ungefähr 136.560 Bytes/s (1,09 Mbit/s) allein für Heldensnapshots. Weltzustand, Effekte, ACKs und Transport sind zusätzlich. Diese Rechnung prüft ein Paketbudget, keine tatsächliche Serverleistung.

Es gab bisher nur den Live-Test mit einem Menschen und Bot-Füllung. Die Beobachtung einer kurzen CPU-Phase mit Reserven beweist keine Stabilität mit acht oder zehn gleichzeitig angreifenden Menschen.

## Noch offene Freigabe — echte Geräte und Last

- [ ] Linux-Server und Android aus exakt demselben neuen Commit bauen; Android auf dem Testhandy tatsächlich installieren und beide Stände protokollieren.
- [ ] Linux-Container lokal starten: Unity/UDP-Listener und korrekter EntryPoint. Die bekannten Serialisierungswarnungen müssen anhand des nativen Buildlogs geprüft werden.
- [ ] In einer Runde Dash während Bewegung/Angriff testen: sichtbare Bewegung, korrekter Endpunkt, kein Rücksprung nach Ende, Haken/Unterbrechung/Wandkontakt.
- [ ] Alle 50 Aktionen auf dem Handy: Auslösen, Vorbereitung, Projektil/Flug, Kontakt, HP/Schild/KDA und Abbruch. Unterschiedliche Grafikstufen prüfen.
- [ ] Vollständige aktuelle 4v4-Runde mit acht unabhängig verbundenen Clients: gleichzeitige Bewegung, gehaltene Basics, Flächenfähigkeiten, Fernkampf, Heilung, Schilde und Respawns.
- [ ] Je Lastlauf 15 Minuten warm, dann 30 Minuten Dauerlast. Server-Frameverteilung, maximale Aussetzer, Client-Frametimes, RTT/Jitter/Paketverlust, Bandbreite und Eingabe-bis-Rückmeldung gemeinsam erfassen. Neues serverFps/P95/Max-Protokoll hilft beim Zuordnen zu den Kampfereignissen.
- [ ] Kontrollierte Netzwerkprofile: ca. 30/80/150 ms RTT, zusätzlicher Jitter und 1–3 % Verlust; wiederholte Suche/Abbruch/Wiedereintritt separat testen. Das sind vorgeschlagene Testprofile, keine gemessenen Live-Werte.
- [ ] Leistungsreserve für zehn über einen separaten nativen Lastlauf mit acht echten Clients und zwei zusätzlichen synthetischen Kampfakteuren bzw. entsprechend zusätzlicher Gameplay-/Netzwerklast messen. Diese Testakteure dürfen keine produktiven Spielerplätze oder Ergebnisse belegen. Das produktive Spielformat bleibt 4v4.
- [ ] Erst anhand der Messwerte CPU-Ressourcen/Snapshotfrequenz oder Vorhersage/Interpolation abstimmen. Keine Zusage „lagfrei“ aus Ersatzengine-Tests ableiten.

Diese Umgebung besitzt weder Unity/Android-Player noch zehn laufende Spielclients. Deshalb ist die native Mehrspieler-Lastfreigabe offen; die Serverversion im Dashboard wird vor dieser Prüfung nicht auf einen noch nicht gebauten Container umgestellt.

## Lokale Prüfergebnisse vor dem Sichern

- 2.701 Combat-/VFX-Prüfungen einschließlich 150 Offline/Server-Vergleichsszenarien, echter HeroLocomotion, zehn gleichzeitiger Warnungen und Befehls-/Cooldown-Bestätigung.
- 196 Transport-/Bewegungs-/HP-/KDA-/Heilungsprüfungen einschließlich verlorener Dash-Start-/Endmeldungen und Paketbudget.
- 1.245 bestehende Skill-Einstiegsprüfungen.
- 1.933 Lobby-/Identitäts-/Ranglisten-/Handshake-Prüfungen einschließlich Ablehnung alter Protokoll-5-Apps.
- Gegenprobe: Der vorherige Basic-Cooldown-Methodenkörper, nur mit an neue Aufrufer angepasster Signatur, scheitert am neuen Test für einen überholten Null-Cooldown. Anschließend wurde die reparierte Quelldatei wiederhergestellt.

Ausgeführt mit .NET-8-C#-Compiler und kontrollierten Unity/Netcode/Grafik-Ersatztypen. Keine native Freigabe für Android, Linux-Physik oder die Leistungsreserve. Die bestehenden Hand-Animations- und Reichweitenänderungen aus anderen Commits bleiben erhalten.
