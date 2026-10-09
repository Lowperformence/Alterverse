# Google-Konto, Kaufwiederherstellung, Neptune und Nerok

Stand: 09.10.2026, `dev/vertical-slice`. Änderungen im Quellcode; keine Unity-/Android-Laufzeitabnahme und kein Backend-Deployment.

## Konten und Echtgeldkäufe

Der bisherige ACCOUNT-Dialog war eine separate Username-/Password-Anmeldung. Die Play-Games-Ranglistenanmeldung verknüpfte das Unity-Konto nicht automatisch. Der Dialog bietet jetzt GOOGLE ACCOUNT mit Kontoverknüpfung bzw. Wiederherstellung an. Der Profil-Anzeigename bleibt davon unabhängig.

Eine neue Android-Installation versucht zuerst die Anmeldung mit Google Play Games. Bestehende Installationen verknüpfen ihre bisherige Unity-ID, damit das Wallet seinen Besitzer behält. Eine bereits anderweitig vergebene Google-Verknüpfung wird nicht erzwungen. Die Wiederherstellung eines bestehenden Google-Spielstands erfolgt ausdrücklich und ohne Zusammenführen von Konten. Ein unverbundenes Gastkonto mit Kaufhistorie wird dabei geschützt, auch wenn sein Guthaben bereits ausgegeben wurde.

Der Shop verlangt eine bestätigte Google-/Unity-Verknüpfung, geladenes Cloud-Wallet und aktiviertes Server-Billing. Neue Käufe sind während einer Kontoverknüpfung oder Wiederherstellung blockiert. Ranglistenwerte werden erst aus dem passenden geladenen Cloud-Profil übertragen. Multiplayer und Konto verwenden denselben Anmeldevorgang.

Ein konkreter Firestore-Fehler ist korrigiert: Ein bereits gutgeschriebener Kauf konnte bei erneuter Abfrage abgelehnt werden, sobald Google ihn als konsumiert meldete. Bereits gespeicherte Belege werden jetzt zuerst anhand Account, Produkt und Menge geprüft und liefern das aktuelle Wallet zurück. Ein unbekannter konsumierter Beleg bleibt abgelehnt; es gibt keine zweite Gutschrift.

**Konfiguration noch erforderlich:** Im eingecheckten `Assets/GooglePlayGames/Resources/PlayGamesSettings.asset` fehlen App-ID und Web-Client-ID. Der Google-Play-Games-Provider muss mit dem passenden OAuth-Client im Unity Dashboard konfiguriert sein. Dashboard-/Play-Console-Einstellungen und der Stand des laufenden Cloud-Run-Servers wurden hier nicht verifiziert. Einrichtung und Android-Abnahme stehen in [Account-Server-Setup.md](Account-Server-Setup.md).

## Neptune

Der gemeldete Rücksprung ins Menü wurde im Quellcode untersucht, aber ohne Unity-Laufzeitprotokoll nicht reproduziert. Fehler im optionalen Taucher-Wächtermodell verwenden jetzt den vorhandenen Standard-Wächter, statt den gesamten Match-Aufbau abzubrechen. Optionale Planetendekoration wird bei Fehlern für diesen Matchversuch deaktiviert und nicht jeden Frame erneut aufgebaut.

Ein verbleibender Match-Aufbaufehler protokolliert Karte, Ladephase und Exception. Nach dem Abbruch wird die Kameramaske wiederhergestellt und im Play-Menü eine Ladefehlermeldung angezeigt. Der bestehende Netzwerk-/Session-Abbau wird weiter verwendet.

Zur Abnahme Neptune offline starten, Match verlassen und erneut starten; anschließend online prüfen. Bei einem weiteren Rücksprung ist der erste Console-Fehler einschließlich Stacktrace und der Meldung `startup failed during ...` erforderlich. Vorhandene Unity-Menüchecks: `DIMENSIONS FRONT/Checks/All maps - shaders and assets` und `Active map - renderers (Play Mode)`.

## Nerok

Die Laufzeitgeometrie verwendet einen flacheren Kopf (Höhe ×0,74, Tiefe ×0,84) und schlankere Ober-/Unterarme (Dicke ×0,74). Die schwarzen Unterarmplatten samt Zubehör werden aus dem Körpermesh entfernt. Braune, geschlossene Lederbänder umschließen beide Unterarme und folgen den Unterarmknochen. Die Änderung wirkt im Menü und im Match. Die Blender-Quelldatei wurde nicht neu exportiert.

Die Darstellung muss im Unity-Menü sowie bei Bewegung, Angriff und Fähigkeiten auf Clipping und Passform geprüft werden; hier war keine Unity-Bildausgabe verfügbar.

## Ausgeführte Prüfungen

- `python3 -m unittest discover -s Server/commerce -q`: **104 Tests bestanden**, einschließlich Wiederherstellung nach Kauf/Spending, konsumiertem Beleg, Kontotrennung und Kaufhistorie bei Guthaben null. SQLite ist echt temporär; Firestore und Google-Verifikation sind Test-Fixtures.
- **41 lokale C#-Dateien** ohne Syntaxfehler geparst. Dies ist keine C#-Typprüfung oder Unity-Kompilierung.
- Änderungen mit dem ursprünglichen Branchinhalt verglichen; Unity-Assets, bestehende Transaktionsjournale und Identitäten werden nicht migriert.

Vor echten Zahlungen: Google-Konfiguration vervollständigen, Backendänderung deployen und einen signierten Play-Testbuild mit Login, Kauf, App-Neustart und Wiederherstellung auf einem zweiten Gerät prüfen. Refund-/Chargeback-Reconciliation bleibt die bereits dokumentierte separate Betriebsaufgabe.
