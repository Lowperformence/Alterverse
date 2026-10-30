# Dimensions Front: Performance und Werbeladen

Stand: 30. Oktober 2026. Ausgangspunkt: `2ce6ea303439d594012139b487edbc1b40e68717`, Branch `codex/epic-toon-replacement-20261024`. Die in diesem Ausgangspunkt bereits enthaltenen Werbekorrekturen bleiben vollständig erhalten und werden mitgeprüft.

„Nicht optimiert“ beschreibt ein beobachtetes Problem, aber noch keine Diagnose. Niedrige Geräte-FPS, einzelne lange Frames, Server-Überlastung, Netzwerklatenz und Paketjitter benötigen unterschiedliche Messungen. Die folgenden Änderungen behandeln konkrete wiederholte Arbeit und einen belegten Fehler im Werbe-Ladeablauf. Ein allgemeiner FPS- oder Ping-Gewinn wurde nicht auf einem Android-Gerät gemessen.

## Änderungen

- **Online-Weltsuche:** Die Objektzuordnung bleibt zwischen unveränderten Snapshots erhalten. Änderungen am Health-Register, explizite neue Pool-Kennungen und ein anderer Matchplan lösen sofort eine neue Suche aus. Ein zusätzlicher Audit nach einer Sekunde erfasst nachträglich hinzugefügte Komponenten. Ein Entfernen und Hinzufügen mit gleichbleibender Objektanzahl im selben Frame wird jetzt ebenfalls erkannt. HP, Schilde, Positionen und zuverlässige Löschmeldungen werden weiterhin durch die vorhandenen Versandpfade ermittelt.
- **Server-Präsentation:** Die Weltobjekte bauen auf dem autoritativen Server keine Renderer-/Collider-Listen für die reine Clientdarstellung auf. Der Materialschutz wird bei Dedicated-Server-Anforderung oder fehlendem Grafikgerät nicht installiert.
- **Materialprüfung:** Nach der unveränderten ersten Prüfung werden spätere Reparaturen auf höchstens 48 Renderer pro Frame verteilt. Die regelmäßige Szenensuche bleibt erhalten. Wiederverwendete Materiallisten ersetzen die pro Renderer erzeugten `sharedMaterials`-Arrays; der Kantenschutz verwendet ebenfalls eine wiederverwendete Liste. Unterstützte Materialien und die vorhandene Wiederherstellung ungültiger Normalmaps bleiben erhalten.
- **Effekt-/Profileinstellungen:** Mobile Qualität wird je Frame und Konto einmal aus dem Profilspeicher gelesen. Ein Kontowechsel oder eine Einstellung über den Setter invalidiert sofort; weitere Änderungen werden im nächsten Frame sichtbar. Das vermeidet insbesondere die wiederholte Anlage von Profileinträgen bei vielen Effekten im Gastmodus.
- **Heldenvorschau:** Android Battery verwendet 768 × 864 ohne MSAA, Balanced 1024 × 1152 mit zweifachem MSAA. Desktop und Performance behalten 1536 × 1728 mit vierfachem MSAA. Balanced berechnet damit nominell 44,4 Prozent der bisherigen Zielpixel; dies ist keine gemessene FPS-Steigerung. Bildseitenverhältnis, vergrößerte Karte, Heldenmaßstab, Podest und Kamerarahmung bleiben erhalten. Qualitätswechsel geben das alte Ziel frei; eine fehlgeschlagene Neuanlage behält das vorherige Bild.
- **Werbetafel, vorhandene Korrektur mitgeprüft:** Ein fehlgeschlagener erster Banner-Load wird nach 30/60/120 Sekunden erneut versucht, sobald die Platzierung sichtbar und die Einwilligung bereit ist. Versteckte/pausierte Platzierungen starten keinen Wiederholungsversuch. Nach einem erfolgreichen Load bleibt die automatische Aktualisierung beim SDK; ein fehlgeschlagener SDK-Refresh verwirft eine bereits geladene Anzeige nicht. Ergebnisse bereits zerstörter Banner werden ignoriert.
- **Werbevideos, vorhandene Korrektur mitgeprüft:** Die Statusmeldung zeigt den tatsächlichen SDK-Fehlercode und Fehlertext; der vollständige Providerfehler bleibt im Gerätelog. Eine ausstehende Videobelohnung blockiert weitere Videos, aber nicht mehr die gemeinsame SDK-Initialisierung für die Werbetafel. Die bestehenden Belohnungs-, Konto- und Consent-Regeln bleiben bestehen.

Netzwerk-Nachrichtenformate, 20-Hz-Bewegungsversand, Trefferberechnung und Spielbalance wurden durch diese Änderungen nicht geändert. Werbe-IDs und SDK-Versionen wurden nicht geändert.

## Ausgeführte Prüfungen

Alle C#-Läufe verwenden .NET 8 und kontrollierte Unity-/SDK-/Netzwerk-Fixtures. Sie ersetzen keinen Unity-Import, keinen Shader-/IL2CPP-/Gradle-Build und keinen echten Geräte- oder Serverlasttest.

| Prüfung | Ergebnis |
| --- | --- |
| `Tools/Multiplayer/EntityCacheRegression/run.py` | 12 Prüfungen bestanden; 600 vollständige Komponentensuchen bei 12.000 Aufrufen über zehn simulierte Minuten nach dem ersten Aufbau. Sofortige Invalidierung bei gleicher Anzahl/neuem Objekt, Pool-Rekey, Planwechsel und Registerreset; Clientdarstellung und nachträgliche Komponenten geprüft. |
| Gegenprobe mit vorherigem Quellstand | Kompiliert; scheitert erwartungsgemäß an der Prüfung gegen wiederholte stabile Weltsuchen. |
| `Tools/Multiplayer/Regression/run.py` | 296 bestehende Bewegungs-, Transport-, Status-, Heilungs- und Scoreboard-Prüfungen bestanden. |
| `Tools/Multiplayer/WorldMotionRegression/run.py` | 2.960 Prüfungen zur gepufferten Weltdarstellung, Samplingzeiten und Paketjitter bestanden. |
| `Tools/Materials/Regression/run.py` | 229 Material-, Normalmap-, Import- und Reparaturbudget-Prüfungen bestanden. |
| `Tools/UI/MenuPedestalRegression/run.py` | 3.780 Geometrie-, Boden-, Cache-, Kamera-, Lebensdauer- und Qualitätsprüfungen bestanden; vollständige Showcase- und MobilePerformanceProfile-Quellen gegen Engine-Fixtures kompiliert. |
| `Tools/Shop/RewardedAdsRegression/run.py` | Release-Android, Development-Android, Editor und Server bestanden; tatsächliche Fehleranzeige und Initialisierung bei ausstehenden Belohnungen sowie bestehende Belohnungs-/Kontowechsel-/Consent-Prüfungen bestanden. |
| `Tools/UI/LanguageBannerRegression/run.py` | Banner-Wiederherstellung, Backoff, Sichtbarkeit, veraltete Callbacks, bestehende Anzeigen bei Refresh-Fehlern, Layout und Sprachauswahl im aktuellen Branch geprüft. |
| C#-Syntax | Geänderte C#-Quellen ohne Parserfehler. |

Ausführen mit `python3 <genannter Pfad> --dotnet-root <.NET-8-SDK>`. Die Gegenprobe verwendet zusätzlich `--source-root <vorheriger Checkout>`. Die Material- und Menü-Fixtures wurden an die verwendeten dokumentierten Listen- bzw. RenderTexture-APIs angepasst.

## Noch nativ zu verifizieren

1. In Unity 6000.6.1f1 Android-Client und Dedicated Server aus diesem gemeinsamen Stand bauen. Ein GitHub-Push aktualisiert weder installierte Apps noch laufende Server.
2. Auf einem betroffenen Handy Menü, Qualitätswechsel, Laden, eine Offline-Runde und ein vollständiges Online-4v4 testen. Helden, Minion-Spawns, Pool-Wiederverwendung, Respawns, Hot Zone, HP/Schilde und Rundenende vergleichen. Geräte-Frametimes, GC und Temperatur sowie Server-FPS/p95/max, CPU und Speicher unter gleicher Last erfassen; RTT/Paketjitter getrennt bewerten.
3. Im Android-Development-Build Googles bestehende Testanzeigen verwenden. Danach eine Release-Version mit den vorhandenen produktiven Anzeigenblöcken prüfen. Bei weiterem „Unavailable“ den `[DF Ads/Banner]`- oder `[DF Ads/Rewarded]`-Fehler aus Logcat auswerten und im AdMob-Konto Auslieferungs-/Anzeigenblockstatus prüfen. Fehlendes Anzeigenangebot oder eine kontoseitige Sperre kann eine Codeänderung nicht beheben. Eine tatsächlich ausgelieferte Anzeige wurde hier nicht beobachtet.

Primärdokumentation für die verwendeten APIs und das SDK-Verhalten: [Unity Renderer.GetSharedMaterials](https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Renderer.GetSharedMaterials.html), [AdMob Banner](https://developers.google.com/admob/unity/banner), [AdMob Ladefehler](https://developers.google.com/admob/unity/ad-load-errors). Die Seiten wurden bei dieser Prüfung abgerufen. Das öffentliche `app-ads.txt` ließ sich aus dieser Umgebung nicht verifizieren (HTTP 403); daraus wurde kein Fehler an der Datei abgeleitet.
