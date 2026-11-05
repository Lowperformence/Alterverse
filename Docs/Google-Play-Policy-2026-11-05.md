# Google-Play-Vorgaben: Umsetzung und offene Nachweise

Stand: 05.11.2026. Spielname: DIMENSIONS FRONT; GitHub-Projekt: Alterverse.
Dies ist ein Arbeitsstand, keine Storefreigabe. Die Play Console wurde hier nicht bearbeitet.

## Im Projekt umgesetzt

- Bereits vorhanden: Release-Minifizierung mit R8, optimierte Defaultregeln und JNI-Keep-Regeln. Target SDK 36, IL2CPP und ARM64 bleiben erhalten. Diese Richtlinienmeldung allein verlangt keinen Wechsel auf API 37.
- Neu: `AndroidPermissionPolicy` setzt in den generierten unityLibrary- und Launcher-Manifesten für 16 ungenutzte sensible Berechtigungen `tools:node="remove"`. Beide Deklarationsformen (`uses-permission` / `uses-permission-sdk-23`) werden berücksichtigt. Dadurch dürfen auch eingebundene Bibliotheken diese Zugriffe nicht unbemerkt ergänzen.
- Internet, Netzwerkstatus, optionale Mikrofonberechtigung, Billing und Werbe-SDK-Berechtigungen bleiben erhalten. Die Entfernung ersetzt keine Prüfung der SDK-Datenflüsse und keine Data-Safety-Erklärung.
- Aktuelle Netzwerkpfade verwenden Unity Relay bzw. einen zugewiesenen Dedicated Server. Einladungen verwenden Androids Teilen-Dialog. Es wurden keine Standort-, Telefonbuch-, SMS-, Anruflisten- oder Geräte-LAN-Suchfunktionen im Spielcode gefunden. Bei künftigem echtem LAN-Modus muss `ACCESS_LOCAL_NETWORK` samt Laufzeitabfrage und ggf. Console-Erklärung neu bewertet werden.
- Android-Schriftgrafiken werden bei `Application.lowMemory` und Fokusverlust aus dem begrenzten Cache entfernt und bei Bedarf neu erstellt. Das reduziert vermeidbare Cachebelegung; es belegt nicht das Einhalten der Play-Speichergrenzen.
- `Tools/AndroidPolicy/audit_bundle.py` prüft die finalen Manifestdateien **aller AAB-Module** mit Google bundletool, meldet sensible Berechtigungen und ermittelt die unkomprimierte DEX-Größe samt SHA-256 des untersuchten Builds.

## Android-Build und Nachweis

1. Vollständiges Projekt in Unity 6000.6.1f1 mit Android Build Support öffnen.
2. `DIMENSIONS FRONT > Release > Prepare Android upload` ausführen; vorhandene Release-Gates müssen bestehen.
3. Upload-Signatur lokal konfigurieren und einen freien Versionscode setzen. Keine Schlüssel/Passwörter ins Repository aufnehmen.
4. `DIMENSIONS FRONT > Release > Build signed Android bundle` ausführen.
5. Offizielles bundletool verwenden (https://github.com/google/bundletool/releases). Beispiel in PowerShell:

   ```powershell
   py Tools/AndroidPolicy/audit_bundle.py "Builds/Android/DIMENSIONS-FRONT-<versionCode>.aab" --bundletool "C:/Tools/bundletool-all.jar" --output "Builds/Android/policy-audit.json"
   ```

6. Bei Fehlern keine Storefreigabe behaupten. Das JSON enthält sensible Fehlberechtigungen, keine Spielerkonten/Kauftoken. Der Manifest-Callback arbeitet vor dem Android-Merge; erst die AAB-Prüfung belegt das tatsächlich ausgelieferte Ergebnis.
7. Signatur, native 16-KB-Kompatibilität, Google-Login, Billing, Anzeigen/Consent, Sprachchat, Teilen, Wechsel in den Hintergrund und erneutes Öffnen auf Geräten testen. Kein Unity-Editor und keine Android-Signatur stehen in dieser Arbeitsumgebung bereit; kein neues AAB wurde erzeugt.

## Konto und Fristen

| Punkt | Frist / Bedingung | Noch nötig |
|---|---|---|
| Händlerstatus | Bestehendes Konto: 01.02.2027; neue Konten sofort | Konto verwalten > Kontodetails > Über dich. Tatsächliche kommerzielle Tätigkeit erklären. Implementierte Shard-Käufe/Rewarded Ads sind Indizien, aber keine Bestätigung ihres produktiven Betriebs. Status nicht raten. |
| Händlerangaben je App | Bei Händlerstatus | App > Richtlinien und Programme > App-Inhalte; Selbstzertifizierung, Nutzungsbedingungen-Link, korrekte Kontaktangaben. Bei persönlichem Händlerkonto werden Telefon/Adresse öffentlich. |
| Japan-Autorisierung | 01.12.2026, falls der Zahlungsfall zutrifft | Tatsächliche Verkaufs-/Zahlungsregionen und Console-Aufgabe prüfen. Keine Region ungefragt deaktivieren. |
| Arbeitsspeicher | Ab Februar 2027 | Android Vitals > Memory; P90 Anon RSS + Swap nach Gerätespeicher und App-Zustand, zusätzlich Bitmap-Speicher prüfen. Messungen über 28 Tage; Unity-Profiler allein ist kein Nachweis. |
| DEX-Optimierung | Ab Februar 2027, Spiele mit >50 MB DEX | App Bundle Explorer: jeweils mindestens 25 % Obfuscation, Optimization, Shrinking. AAB-Gesamtgröße ist ungeeignet; lokaler DEX-Wert ist eine Schätzung, Consolewert maßgeblich. |
| Standort/Kontakte | 27.01.2027, abhängig von Nutzung/Target SDK | Der aktuelle Build soll diese ungenutzten Rechte nicht erhalten. Bei späterer Nutzung Picker-/Erklärungspflicht prüfen. |
| Lokales Netzwerk | 25.08.2027, API 37+ und Berechtigungsnutzung | Aktuell entfernt. Kein Formular nur aufgrund von Internet-Multiplayer einreichen. |
| Manage Contacts | 25.08.2027, genaue Android-/Berechtigungsbedingungen beachten | Aktuell entfernt. |
| Zero-Tap-Anmeldung | Spiele derzeit ausgenommen | Kein verpflichtender Umbau aufgrund dieser Meldung. |
| AppFunctions, rabattierte Abos, Krypto, Auto/XR | Nur bei entsprechenden Funktionen | Kein Bedarf im geprüften Spielcode erkannt. Keine pauschale Compliance-Aussage über ungesichtete SDKs. |

## Rechtstexte und Data Safety

Der öffentlich versionierte Text in `Lowperformence/dimensions-front-privacy/index.md` (Stand September 2026) enthält `[DEINE E-MAIL-ADRESSE]`. Er beschreibt heutige Konten, Online-Multiplayer, Werbung/Consent, Clan-Chat und Sprachchat nicht ausreichend konkret.

Ein nicht veröffentlichter Entwurf liegt in `Docs/Legal-Review-Draft-2026-11-05.md`. Vor Veröffentlichung fehlen:

- Vollständiger rechtlicher Anbietername, ladungsfähige Anschrift, Supportkontakt und ggf. weitere Pflichtangaben; Trockenbau-Daten nicht ungeprüft als Spielanbieter übernehmen.
- Welche Kauf-/Werbe-/Chatfunktionen tatsächlich live sind und welche Zielgruppe/Länder bedient werden.
- Tatsächliche Hostinganbieter, Speicherorte, Lösch-/Aufbewahrungsfristen, Backupfristen und funktionierender Kontolöschweg.
- SDK-Daten-/Consent-Konfiguration mit finalem Netzwerkverhalten abgleichen; kein „keine Datenerhebung“ erklären.
- Nutzungsbedingungen und Datenschutzerklärung auf der bestehenden öffentlichen Website veröffentlichen, URLs prüfen, dann im Spiel und in Play Console eintragen. Entwürfe mit Platzhaltern nicht verlinken.

## Quellen

- Ankündigung: https://support.google.com/googleplay/android-developer/answer/18100766
- Fristen: https://support.google.com/googleplay/android-developer/table/12921780
- Händlerstatus: https://support.google.com/googleplay/android-developer/answer/17594752
- Technik/Speicher/DEX: https://support.google.com/googleplay/android-developer/answer/17492799
- Berechtigungen: https://support.google.com/googleplay/android-developer/answer/16909972
- Manifest-Merge: https://developer.android.com/build/manage-manifests
- Unity lowMemory: https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Application-lowMemory.html

## Durchgeführte Prüfungen

- 110 Prüfungen der echten C#-Release-/R8-/Berechtigungshelfer mit Unity-Editor-Ersatztypen bestanden. Einschließlich beider Manifestmodule, beider Berechtigungsformen, Erhalt von Internet/Mikrofon, Wiederholbarkeit und Fehlerfällen.
- 10 Prüfungen des tatsächlichen Android-Schriftcache-Codes mit Unity-Ersatztypen bestanden: Wiederverwendung, Fokusverlust, Speicherwarnung, Wiederaufbau, wiederholte Initialisierung.
- 5 Python-Tests des finalen Bundle-Prüfers bestanden, einschließlich Berechtigungen in zusätzlichen Modulen und ungültigen Eingaben. Bundletool-Aufrufe sind in diesen Tests simuliert; kein echtes AAB lag vor.
- Zusätzlich 1.196 vorhandene Sprach-/Menü-/Banner-Prüfungen über Android Release, Development, Editor und Server bestanden; die Test-Ersatztypen wurden um die zwei verwendeten Unity-Ereignisse ergänzt.
- GitHub Actions: Android-R8-/Policy-Workflow einschließlich echter R8-Prüfung bestanden.
- `git diff --check` bestanden. Dies ersetzt keine Unity-Kompilierung, Gradle-Manifest-Zusammenführung, Geräteprüfung oder Android-Vitals-Messung.
