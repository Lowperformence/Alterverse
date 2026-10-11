> Ergänzung nach dem Release-Pass: [AI-Difficulty.md](AI-Difficulty.md) dokumentiert die ausdrücklich nachbeauftragte, gespeicherte Bot-Schwierigkeit. Aktuell 210 lokale C#-Dateien syntaktisch geprüft; Unity-/Gerätefreigaben bleiben offen.

# DIMENSIONS FRONT — Google Play Release Checklist

Stand: 2026-09-19. Branch: `dev/vertical-slice`. **CODE READY zur Unity-Validierung**, nicht zur Veröffentlichung freigegeben. Feature Freeze nach Charakter-/Menü-Pass; keine weiteren freiwilligen Erweiterungen.

## DONE

- Alle sechs Hero-Darstellungen überarbeitet: Silhouetten, Kopf-/Schulterformen, Waffen, Stoff-/Metalltrennung und an Bewegungspivots gebundene Rüstung. Standard und VOIDFALL nutzen denselben Code im Match und echten 3D-Menü. Details: [Character-Final-Pass.md](Character-Final-Pass.md).
- Dunkler kosmischer Menü-Hintergrund, individuelle Hero-Akzente, integrierte Portraits; bestehende Navigation, Shopzustände und Safe-Area-Layout erhalten. [Main-Menu-Final-Pass.md](Main-Menu-Final-Pass.md).
- Keine Änderungen an Hero-Balance, Bewegungsgeschwindigkeiten, Schaden, Fähigkeitenwerten, Front-Regeln oder Belohnungstabellen. Keine käuflichen Kampfwerte.
- Alle 84 OGG-Dateien mit FFmpeg ohne Decodierfehler geprüft; kein Hörtest.
- Lizenznachweise für 84 OGG-Dateien (2 Musik, 29 allgemeine SFX, 53 Hero-SFX) erhalten. [ThirdPartyAudioLicenses.md](ThirdPartyAudioLicenses.md), [HeroAudioAssets.json](HeroAudioAssets.json).
- Source-Audit von Start/Menü/Match-Aufbau, Eingabe-/Todes-/Pause-Gates, verzögerten Fähigkeiten, Matchende/Belohnungsjournal/Heimkehr, lokalen Profilen und Kosmetikkäufen. Das ist keine Ausführung dieser Abläufe.
- 208 lokale C#-Dateien syntaktisch geprüft: keine Parserfehler. Keine C#-/Unity-Kompilierung. `git diff --check` fehlerfrei.
- `python -m unittest discover -s Server/commerce -v`: **11 Tests bestanden**. Abgedeckt: parallele doppelte Token, Persistenz/Neustart, Pending/Cancelled, falscher Account, unbekannt konsumierter Token, wiederholter Kosmetikkauf, Authentifizierung, manipulierte HTTP-Payloads und Google-Ausfall. Google-Zugriff in diesen Tests simuliert; kein echter Store-Test.
- Kontowechsel während asynchronem Store-Verbindungsaufbau wird vor Produktabruf erneut geprüft; zerstörte Clientobjekte werden bei asynchroner Fehlerbehandlung berücksichtigt. Fehlgeschlagener externer Token-Getter propagiert nicht mehr aus Shop-Gettern; 401/403 sperrt das bezahlte Wallet bis zur erneuten Synchronisation.
- Veraltete lokale Founder-/Recall-Entwicklungs-Gutschriftmethoden sind nur noch im Editor enthalten. Vorhandene lokale Entitlements bleiben kompatibel; sie sind kein sicherer Echtgeldnachweis. Founder-Echtgeldkauf bleibt nicht verfügbar. Shard-Sandbox ist ausschließlich Editor und isoliert vom echten Profil.
- Release-Build-Einstieg erzwingt `BuildOptions.None` und deaktiviert Development, Script Debugging, Profiler-Verbindung und Deep Profiling. Preprocessor weist widersprüchliche Release-Optionen, falsches aktives Ziel, geänderte Package-ID, fehlende Szenen, ARM64/IL2CPP-/SDK-/Signing-Probleme zurück. Keystore muss existieren und außerhalb des Projektordners liegen. Private Schlüssel-/Umgebungsdateien werden zusätzlich ignoriert.
- Android-App-ID-Korrektur (11.10.2026): `Prepare Android upload` und alle drei Bundle-Build-Einstiege stellen vor der Prüfung die bestehende ID `com.lowperformence.dimensionsfront` wieder her und speichern sie. Die reine Validierung und der Preprocessor ändern nichts und melden bei einer Abweichung den tatsächlichen Wert samt Reparatur-Menü. Signierung, Version und andere Plattform-IDs bleiben erhalten.
- Player-facing Branding im geprüften Spielcode: DIMENSIONS FRONT; technische Namespaces/Ordner bleiben unverändert.

### Tatsächliche Android-Konfiguration

| Bereich | Repositorybefund / Grenze |
|---|---|
| Unity | `6000.6.1f1` laut ProjectVersion; hier nicht installiert |
| Anwendung | `com.lowperformence.dimensionsfront` — unverändert; Product Name `DIMENSIONS FRONT` |
| Version | `0.1.0`, Version Code `1`; vor Upload in Console auf freie Nummer prüfen |
| SDK | Minimum 26, Target 36; installierter SDK/NDK-Stand hier nicht verifiziert |
| Backend / ABI | IL2CPP, ARM64 (`AndroidTargetArchitectures: 2`) |
| Orientierung | Landscape Left; beide Landscape-Autorotationen erlaubt, Portrait verboten; tatsächliches Android-16-Großbildschirmverhalten testen |
| Auflösung / UI | Keine neue erzwungene Renderauflösung; vorhandene Safe-Area-Transformation; reale Touchgröße offen |
| Grafik-APIs | Explizit Vulkan und OpenGLES3, Automatik aus; beide auf Geräten prüfen |
| URP | Android Quality 0 verweist auf Mobile_RPAsset: Render Scale 0.8, MSAA 1, 1024er Hauptlicht-Schatten, zusätzliche Schatten aus, SRP Batcher an; Qualitätsskript kann Laufzeitwerte anpassen |
| Stripping | Engine-Stripping an; Managed-Stripping-/IL2CPP-Compiler-Konfiguration ohne expliziten Android-Override. Effektiven Editorwert und tatsächlichen IL2CPP-Build prüfen, nicht geraten |
| Texturen | Vorhandene Android-Kompressionskonfiguration erhalten; CosmicAtlas 1024, nicht lesbar, keine Mipmaps; Menütextur einmalig erzeugt und CPU-Kopie freigegeben |
| Audio | Musik Streaming, SFX Mono-Vorbis; AudioManager 32 echte Stimmen, eigener Cue-Pool begrenzt; Android-Wiedergabe/Hörtest offen |
| Berechtigungen | Keine projektspezifischen Android-Manifeste gefunden; ForceInternet/ForceSDCard aus. IAP/UnityWebRequest können Manifest-Berechtigungen ergänzen. Finale Berechtigungen nur am gebauten Bundle bestätigen |
| Signing | Custom Keystore nicht eingerichtet; keine Signatur erzeugt |
| Dependencies | Purchasing 5.4.3 im Manifest; im vorhandenen Lockfile noch nicht aufgelöst. Unity muss Auflösung und native Android-Abhängigkeiten prüfen |
| Startszene | `Assets/Scenes/SampleScene.unity` ist auf GitHub vorhanden und in Build Settings aktiviert. Im bisherigen lokalen Teil-Checkout nicht enthalten; kein Remote-Dateiverlust |

Target 36 entspricht der aktuell veröffentlichten Vorgabe für neue mobile Apps/Updates ab 31.08.2026. [Google Play Target API](https://support.google.com/googleplay/android-developer/answer/11926878?hl=en). Native IL2CPP-/SDK-Bibliotheken und erzeugte APKs aus dem AAB auf 16-KB-Kompatibilität prüfen; Projekteinstellungen allein beweisen sie nicht. [Android-Prüfanleitung](https://developer.android.com/guide/practices/page-sizes).

### Data Safety: konkrete Befunde

| Daten / Codepfad | Verarbeitung / Grenze |
|---|---|
| Anzeigename, Avatar, XP/Level, Siege/Matches/Kills/Assists/Objectives, verdiente Shards, lokale Kosmetik, Lautstärke, Onboarding, Qualitätswahl | PlayerPrefs auf dem Gerät. Eigener Spielcode hat dafür keinen Uploadpfad. Android-Backup-Verhalten muss im endgültigen Manifest geprüft werden |
| MatchTelemetry | Laufzeitstatistik und Debug-Log mit Hero-/Teamwerten; kein eigener Analytics-Upload. Kein Beweis gegen SDK-Telemetrie |
| Bezahltes Wallet | HTTPS-POST an extern einzurichtenden Dienst: Bearer-Token, Produkt-ID, Google-Kauftoken bzw. Kosmetik-ID; Server liefert Revision, Guthaben und Entitlements |
| Accountbindung | Vom echten Login zu liefernde obfuskierte Account-ID an Google Play; Backend authentifiziert diese Zuordnung. Obfuskierung ist keine Anonymisierung |
| Commerce-Server | SQLite speichert Account-ID, rohen Kauftoken, Produkt/Menge/Gutschrift, Wallet und Kosmetikbesitz. Google-API-Abfrage über Produkt/Kauftoken; keine Kartennummern im eigenen Code. Aufbewahrung, Löschung, Zugriff, Backups und Betriebslogs sind noch nicht festgelegt |
| Unity IAP 5.4.3 | Gelesene SDK-Quellen enthalten Kaufereignis-Versand, Geräte-/OS-/Sprachdaten und optionale Installations-, Spieler-, Analytics-/Werbe-Identifier. Tatsächlich gesendete Felder hängen von Plattform, Unity-Konfiguration, initialisierten Diensten und Consent ab; kein Netzwerk-Mitschnitt durchgeführt |
| Unity-Dienste | UnityConnect/Analytics/Ads/Cloud-Crashreporting in den alten Projekteinstellungen deaktiviert. IAP-Paket hat eigene Data-/Insights-Pfade und Services-Core-Abhängigkeit; diese Flags belegen keine vollständige Datensperre |

SDK-Evidenz: Purchasing-5.4.3-`package.json`, `Runtime/Stores/Data/Insights/PurchaseEventEmitter.cs`, `Models/DeviceInfo.cs`, `Models/PlayerIdentity.cs`, `Models/OrderData.cs`, `Runtime/Utilities/UnityUtil.cs`; Quellen zur API-/Datenprüfung separat gelesen, noch nicht von Unity aufgelöst. Eigene Gegenstücke: `PaidShardCommerce.cs`, `GooglePlayShardStore.cs`, `Server/commerce/application.py`, `ledger.py`.

**Keine „keine Datenerhebung“-Erklärung abgeben.** Finale SDKs, Unity Developer Data-/Consent-Konfiguration, Login und Hosting prüfen; anschließend Zwecke, Erhebung/Weitergabe, erforderliche/optionale Verarbeitung, Verschlüsselung und Löschmöglichkeiten korrekt deklarieren. [Google Play Data Safety](https://support.google.com/googleplay/android-developer/answer/10787469?hl=en).

### Third-Party-/Asset-Audit

- Musik und Kenney-SFX: CC0, Quellen/Original-Lizenz/Dateimanifest dokumentiert; keine Assets mit nur privater/nichtkommerzieller Lizenz neu hinzugefügt.
- Hero-, Waffen-, Flügel-, Gelände-, Menü- und UI-Geometrie dieses Passes: Projektcode, keine neuen Fremdmodelle oder Fremdtexturen.
- `CosmicAtlas.jpg`: laut bestehender `Completion-Pass.md` mit Imagegen erzeugt; Herkunftshinweis erhalten, keine nachträglich erfundene Stock-Lizenz. Finale Rechte-/Markenprüfung und Storefreigabe durch Herausgeber offen.
- Keine externen `.ttf`, `.otf`, `.fbx` oder `.obj` in der geprüften GitHub-Dateiliste. UI nutzt Unity-Schrift-/GUI-Ressourcen. Unity-URP-Tutorialdateien einschließlich Editor-Icon sind weiterhin im Baseline-Projekt.
- Unity-Pakete behalten ihre eigenen Lizenz-/Third-Party-Notices; für IAP wurden `LICENSE.md` und `Third Party Notices.md` im SDK gefunden. Finale aufgelöste SDK-Liste und erforderliche Hinweise im Releasepaket prüfen.

## USER ACTION REQUIRED

1. Vollständigen Branch in **Unity 6000.6.1f1 mit Android Build Support, SDK/NDK/OpenJDK** öffnen; Android Build Profile aktivieren. Purchasing auflösen lassen, Consolefehler beheben und erzeugtes Lockfile prüfen/committen. Der Microsoft-GDK-Dialog betrifft Windows/Xbox: für diesen Android-Auftrag **Don't install**; keinen FakeStore als echten Einkauf testen.
2. Die folgende Abnahmematrix ausführen und Logs/Ergebnisse festhalten. Bestehende Editorchecks unter `DIMENSIONS FRONT/Checks` nutzen. Keine Freigabe anhand der Parserprüfung.
3. Upload-Keystore/Backup/Alias außerhalb des Repositories bereitstellen; Passwörter nur lokal oder als CI-Secrets. Play App Signing einrichten, Package-ID beibehalten, freie Versionsnummer/-code wählen. Keine Schlüssel oder Passwörter committen.
4. `DIMENSIONS FRONT > Release > Build signed Android bundle` ausführen. Erwarteter Ausgabepfad: `Builds/Android/DIMENSIONS-FRONT-<versionCode>.aab`. Buildbericht, Signatur, Paket-ID, Target/Min SDK, ABI, Berechtigungen und native 16-KB-Kompatibilität kontrollieren; daraus installierte APKs testen.
5. Für **aktive Echtgeldkäufe** echten Login inklusive sauberem Logout/Accountwechsel, authentifiziertes HTTPS-Backend, dauerhafte Datenbank/Backups, serverseitige Google-Berechtigung, Refund-/Chargeback-/Entitlement-Reconciliation und Supportprozess bereitstellen. Keine Google-Servercredentials in den Client. Siehe [GooglePlay-Shards-Setup.md](GooglePlay-Shards-Setup.md).
6. Play Console: Developer-/Identitätsprüfung, Kontakt-/Händlerdaten; Produkte `df_shards_small` (500), `df_shards_medium` (1200), `df_shards_large` (2600), `df_shards_premium` (5500), reale Preise/Regionen und License Tester einrichten. Echten signierten internen Testbuild verwenden.
7. Store Listing fertigstellen: DIMENSIONS FRONT, wahrheitsgemäß **lokales AI-5v5** beschreiben; finale App-Icons einschließlich Android-Launcher-Icon, Feature Graphic und Screenshots aus dem laufenden Spiel. Keine Konzeptgrafik als Gameplay-Screenshot ausgeben.
8. Öffentliche Privacy Policy mit tatsächlichem Anbieter/Kontakt, Datenflüssen, Aufbewahrung und Löschweg veröffentlichen; In-App-Zugang bereitstellen. Data Safety, Zielgruppe, Content Rating, Werbungserklärung und App Access entsprechend dem finalen Build ausfüllen. Ein Privacy-Policy-Link ist im Spiel noch nicht eingerichtet.
9. Interne/geschlossene Tests und Produktionszugang entsprechend dem tatsächlichen Konto erledigen. Für betroffene neuere persönliche Konten nennt Google mindestens 12 Tester über 14 zusammenhängende Tage; Consolevorgaben prüfen. [Offizielle Testanforderungen](https://support.google.com/googleplay/android-developer/answer/14151465?hl=en). Erst danach geprüftes AAB hochladen; Upload wurde hier nicht durchgeführt.

### Abnahmematrix — noch AUSZUFÜHREN

| Bereich | Konkreter Nachweis |
|---|---|
| Import/Build | Package-Auflösung, Unity-C#-Kompilierung, Android IL2CPP/ARM64 ohne Fehler; Shader/Emission/Normalmaps auch im gestrippten Player vorhanden |
| Gesamtablauf | Kaltstart → Menü → jeden Hero auswählen → Match → Bewegung/Angriff/Q/W/E/R → Bots/Minions/Türme/Planeten/Front/Core → Sieg und Niederlage → Belohnung → Menü → zweites Match |
| Regeln / Steuerung | Alle sechs Heroes, analog/diagonal, Dash/Wände/CC/Tod/Respawn; Core erst bei eigener Kontrolle <20% verwundbar; 100%-Sieg, Timer, Overtime/Sudden Death; keine Balanceänderung zur Testvereinfachung |
| Persistenz | XP, Level, Stats, verdiente Shards, Kosmetikbesitz/-ausrüstung, Avatar/Name/Audio: ändern → App beenden → neu starten. Journal-Unterbrechung vor/nach Save auf Wegwerfprofil testen; keine doppelte Belohnung/Abbuchen |
| Lebenszyklus | Background/Resume während Start-Countdown, Angriff/Dash, Resultat, Namens-Tastatur und Kauf; kein freigegebener Match-Pausegrund durch anderes System |
| Billing | Erfolg, Cancel, Ablehnung, Zahlungs-Pending, mindestens zwei offene Orders, doppelte Callbacks, Backendausfall, App-Kill nach Servergutschrift vor Storebestätigung, Neustart-Recovery, Logout/Accountwechsel während Connect/Fulfill/BuyCosmetic, Tokenablauf/401, zerstörte Clientobjekte, Refund/Revoke |
| Grafik/UI | Standard+VOIDFALL für jeden Hero; Gameplay-Distanz, Animation/Clipping, Menü-Transparenz; 16:9/20:9/Tablet/Cutout, lange Profilnamen und Storepreise, echte Daumentouchflächen |
| Audio/Leistung | Alle 84 Clips im Spiel hören; volle Kämpfe mit zehn Heroes, Minions, Front und VFX; Audioüberlagerung, FPS/GC/Draw Calls/GPU-Speicher/Thermik und Vulkan/GLES3 auf repräsentativen Geräten messen |
| Privacy/Store | Finale Netzwerkverbindungen/SDKs/Manifest gegen Privacy Policy und Data Safety prüfen; Storetests mit Testkonten, Pre-launch Report und Consolewarnungen auflösen |

## BLOCKERS

1. **Unity/Android-Ausführung fehlt:** keine Unity-Kompilierung, kein Play-Mode-Test, kein Android-Build, kein Geräte-/Hörtest. Daher keine belegte visuelle Abnahme, Crashfreiheit oder Mobile-Performance. Purchasing-Lockfile/native Auflösung offen.
2. **Kein signiertes AAB:** Keystore/Signing und Buildumgebung fehlen; Storeassets/Launcher-Icon und Consolefreigaben nicht abgeschlossen. AAB: **Not Generated**.
3. **Echtgeldbetrieb gesperrt:** Produktionslogin, Backendbetrieb und Google-Konfiguration fehlen; Refund-/Chargeback-Reconciliation ist nicht implementiert. Vor Freischaltung zwingend lösen. Ohne diese Integration bleibt der Echtgeldshop unavailable; lokale AI-Matches und verdiente Kosmetik sind davon getrennt.
4. **Datenschutzfreigabe fehlt:** veröffentlichte Privacy Policy/In-App-Link, endgültige SDK-Daten-/Consent-Konfiguration und auf dieser Basis ausgefüllte Data Safety fehlen. Kein behaupteter „No data collected“-Status.

### Statusgrenze

Nur **CODE READY zur weiteren Validierung** ist erreicht. **UNITY COMPILED**, **PLAY MODE TESTED**, **ANDROID BUILD TESTED**, **AAB GENERATED**, **DEVICE TESTED** und **GOOGLE PLAY UPLOADED** sind **nicht erreicht**.

Bewusst verschoben: neue Features, zusätzliche Dekoration, weitere Skins/Heroes, neue Animationspipeline und freiwillige Detail-/Optimierungsrunden. Nach Sichern/Push endet dieser Pass.

