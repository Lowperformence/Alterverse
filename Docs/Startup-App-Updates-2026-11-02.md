# Google-Play-Updateprüfung beim Start

Der vorhandene `StartupAppUpdateManager` startet automatisch nach dem Szenenladen und fragt Google Play nach einer neueren, für das Konto verfügbaren App-Version. Das Hauptmenü wartet auf den Abschluss dieser ersten Prüfung. Ein verfügbares Update öffnet den unmittelbaren Google-Play-Dialog; ein bereits laufendes Update wird beim Wiederöffnen fortgesetzt. Die Installation selbst führt Google Play nach Bestätigung des Spielers aus.

Der JNI-Aufruf verwendet jetzt den tatsächlichen booleschen Rückgabewert von `startUpdateFlowForResult(AppUpdateInfo, Activity, AppUpdateOptions, int)` und läuft auf dem Android-UI-Thread. Ein abgelehnter oder fehlgeschlagener Dialogstart wird nicht als gestartetes Update gemeldet. Der vorhandene manuelle Store-Fallback und die Mindestversionsprüfung des Konto-Backends bleiben erhalten. Eine fehlgeschlagene oder zeitlich abgelaufene Verfügbarkeitsprüfung lässt den anfänglichen Ladezustand wieder los; eine bekannte erforderliche Mindestversion bleibt gesperrt.

`StartupAppUpdateDependencies.xml` hält `com.google.android.play:app-update:2.1.0` auch bei erneutem Android Resolve im Projekt. Die bereits vorhandenen ProGuard-Regeln für `appupdate` und Google-Tasks bleiben bestehen. Kein zusätzliches vollständiges Play-Core- oder Unity-Plugin ist nötig.

Die Prüfung verteilt **veröffentlichte Google-Play-Versionen**, keine GitHub-Commits. Dafür sind höherer Android-Version-Code, dieselbe Paket-ID/Signierung und die Bereitstellung im für das Konto erreichbaren Play-Track nötig. Das Konto-Backend und Edgegap-Spielserver werden unabhängig davon bereitgestellt.

## Prüfung

`python Tools/Profile/StartupUpdateRegression/run.py --dotnet-root DOTNET_ROOT` kompiliert die echten Update-/Menüklassen für Android, Editor und Dedicated Server mit kontrollierten Java-/Unity-Grenzen. Geprüft werden unter anderem Start-Gate, keine verfügbaren Updates, unmittelbarer Dialog, JNI-Rückgabetyp/UI-Thread, Wiederaufnahme, Fehlstart, Store-Fallback, Timeout, Vordergrund-Abfrage und serverseitige Mindestversion. Die bestehende Google-Konto-Startup-Suite bleibt ebenfalls grün. Diese Tests installieren kein echtes Play-Update.

Abschließend zwei signierte Versionen im internen Play-Test nutzen: ältere Version über Play installieren, höhere Version veröffentlichen und nach Verfügbarkeit beim selben Testkonto den Appstart/Update-Dialog prüfen. Erfolg, Abbruch und Wiederaufnahme auf einem Android-Gerät kontrollieren.

Offizielle API: https://developer.android.com/reference/com/google/android/play/core/appupdate/AppUpdateManager

## Startkorrekturen vom 5. November 2026

Das Google-Kontofenster prüft jetzt `PaidShardCommerce.Synchronizing`, statt aus englischen Statustexten auf laufende Anfragen zu schließen. Ein bereits verifiziertes Google-Konto zeigt während des ersten Cloud-Ladens und während einer Wiederholung weiterhin den deaktivierten Wartezustand. Erst eine beendete fehlgeschlagene Synchronisierung bietet „TRY AGAIN“ an. Die Wallet-Abfrage setzt ihren Ladetext beim tatsächlichen Anfragebeginn.

Der Updater behält einen gestarteten nativen Ablauf als aktiv, prüft laufende Updates im Vordergrund im Zwei-Sekunden-Abstand und berücksichtigt sowohl Fokus- als auch Pause/Rückkehr-Ereignisse. Ein bereits heruntergeladenes Update wird im Vordergrund mit Play Cores `completeUpdate()` abgeschlossen; Google Play übernimmt Installation und Neustart der Hauptaktivität. Erfolgreich angeforderte Abschlüsse werden nicht mehrfach gesendet. Abgebrochene/fehlgeschlagene Abläufe geben die Wiederholungsmöglichkeit frei, ohne automatisch erneut den Zustimmungsdialog zu öffnen. Eine erfolgreiche Prüfung ohne weiteres verfügbares Update liest die installierte Buildnummer neu, entfernt die alte Update-Sperre und prüft die Mindestversion des Kontoservers erneut.

108 Prüfungen bestanden gegen kontrollierte SDK-/Java-/Unity-Grenzen: Google-Konto/Menü 54 Android + 3 Desktop, Updater/Menü 30 Android + 5 Editor + 1 Server, Account/Wallet 15. Gegenproben mit den ursprünglichen Produktionsdateien reproduzieren den voreiligen Retry-Button und den zu früh freigegebenen Update-Ablauf. Kein echter Play-Installationslauf und kein nativer Neustart wurden hier ausgeführt. Den vollständigen Ablauf mit zwei über Google Play installierten signierten Versionen auf dem betroffenen Android-Gerät prüfen. Nur der Android-Client muss neu gebaut werden; Konto- und Spielserver benötigen für diese Startkorrekturen kein Update.

Google beschreibt die automatische Hauptaktivitäts-Rückkehr nach `completeUpdate()` hier: https://developer.android.com/guide/playcore/in-app-updates/kotlin-java#flexible-restart

## Korrektur vom 6. November 2026

Bei einem bereits laufenden unmittelbaren Update (`DEVELOPER_TRIGGERED_UPDATE_IN_PROGRESS`) wird `completeUpdate()` nicht mehr aufgerufen: Es ist für heruntergeladene flexible Updates bestimmt. Stattdessen bleibt das unmittelbare Update in Googles Installations- und Neustartablauf. Ein unterbrochener Ablauf wird beim Wiedereintritt weiterhin fortgesetzt. Der Abschluss heruntergeladener flexibler Updates bleibt erhalten. Android/Editor/Server-Fixtures bestehen mit 32/5/1 Prüfungen, einschließlich beider Update-Arten und wiederholter Statusabfragen.

Die konkrete Ursache, warum ein bestimmtes Android-Gerät nach seinem Update nicht selbst öffnet, ist dadurch noch nicht nachgewiesen. Test mit zwei nacheinander veröffentlichten, signierten Play-Versionen und Geräte-Logcat bleibt erforderlich; bei einem Ausweichweg zur externen Store-Seite ist kein automatischer Neustart durch die App zugesichert. Google zum unmittelbaren Ablauf: https://developer.android.com/guide/playcore/in-app-updates
