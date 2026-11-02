# Google-Play-Updateprüfung beim Start

Der vorhandene `StartupAppUpdateManager` startet automatisch nach dem Szenenladen und fragt Google Play nach einer neueren, für das Konto verfügbaren App-Version. Das Hauptmenü wartet auf den Abschluss dieser ersten Prüfung. Ein verfügbares Update öffnet den unmittelbaren Google-Play-Dialog; ein bereits laufendes Update wird beim Wiederöffnen fortgesetzt. Die Installation selbst führt Google Play nach Bestätigung des Spielers aus.

Der JNI-Aufruf verwendet jetzt den tatsächlichen booleschen Rückgabewert von `startUpdateFlowForResult(AppUpdateInfo, Activity, AppUpdateOptions, int)` und läuft auf dem Android-UI-Thread. Ein abgelehnter oder fehlgeschlagener Dialogstart wird nicht als gestartetes Update gemeldet. Der vorhandene manuelle Store-Fallback und die Mindestversionsprüfung des Konto-Backends bleiben erhalten. Eine fehlgeschlagene oder zeitlich abgelaufene Verfügbarkeitsprüfung lässt den anfänglichen Ladezustand wieder los; eine bekannte erforderliche Mindestversion bleibt gesperrt.

`StartupAppUpdateDependencies.xml` hält `com.google.android.play:app-update:2.1.0` auch bei erneutem Android Resolve im Projekt. Die bereits vorhandenen ProGuard-Regeln für `appupdate` und Google-Tasks bleiben bestehen. Kein zusätzliches vollständiges Play-Core- oder Unity-Plugin ist nötig.

Die Prüfung verteilt **veröffentlichte Google-Play-Versionen**, keine GitHub-Commits. Dafür sind höherer Android-Version-Code, dieselbe Paket-ID/Signierung und die Bereitstellung im für das Konto erreichbaren Play-Track nötig. Das Konto-Backend und Edgegap-Spielserver werden unabhängig davon bereitgestellt.

## Prüfung

`python Tools/Profile/StartupUpdateRegression/run.py --dotnet-root DOTNET_ROOT` kompiliert die echten Update-/Menüklassen für Android, Editor und Dedicated Server mit kontrollierten Java-/Unity-Grenzen. Geprüft werden unter anderem Start-Gate, keine verfügbaren Updates, unmittelbarer Dialog, JNI-Rückgabetyp/UI-Thread, Wiederaufnahme, Fehlstart, Store-Fallback, Timeout, Vordergrund-Abfrage und serverseitige Mindestversion. Die bestehende Google-Konto-Startup-Suite bleibt ebenfalls grün. Diese Tests installieren kein echtes Play-Update.

Abschließend zwei signierte Versionen im internen Play-Test nutzen: ältere Version über Play installieren, höhere Version veröffentlichen und nach Verfügbarkeit beim selben Testkonto den Appstart/Update-Dialog prüfen. Erfolg, Abbruch und Wiederaufnahme auf einem Android-Gerät kontrollieren.

Offizielle API: https://developer.android.com/reference/com/google/android/play/core/appupdate/AppUpdateManager
