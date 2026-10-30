# Google Play als einziges Android-Spielkonto

Stand: 30. Oktober 2026. Benutzerauftrag: Das alte automatisch generierte Konto nicht mehr vor dem Google-Play-Konto anmelden oder anzeigen.

## Verhalten

- Android startet ausschließlich über die native Google-Play-Autorisierung und die zugehörige Unity-Provider-Anmeldung. Ein gespeicherter Unity-Gast wird beim Start nicht automatisch wiederhergestellt oder mit Google verknüpft. Auch nach einer fehlgeschlagenen Google-Wiederherstellung wird kein Gastkonto reaktiviert.
- Die bestehende Schutzmarkierung für bereits verwendete Installationen bleibt erhalten: Diese dürfen ein vorhandenes Google-Konto wiederherstellen, aber bei einem Anmeldefehler kein Ersatzkonto anlegen. Die Erstregistrierung neuer Spieler erzeugt nur eine Google-gebundene Identität.
- Das Menü liest Heldenfreigaben, Arena, Audio und Sammlungen erst nach der verifizierten Kontoanbindung und dem Laden des passenden Cloud-Profils. Bis dahin zeigt es den Google-Anmeldestatus mit Wiederholungsmöglichkeit. Der aktuelle native Google-Anzeigename hat Vorrang vor einem veralteten gespeicherten Anzeigenamen.
- Co-op und Party-Initialisierung dürfen den Google-Start nicht durch eine eigene Wiederherstellung der gecachten Unity-Sitzung umgehen, auch nicht während einer laufenden Lobby-Initialisierung.
- Explizite Wechsel aus einem bereits aktiven Cloud-Konto behalten die vorhandenen Speicher-, Kauf- und Matchmaking-Prüfungen. Gespeicherte alte Profile werden weder gelöscht noch automatisch mit einem anderen Google-Konto zusammengeführt.
- Editor/Desktop behalten ihren bisherigen Entwicklungsablauf. Die bereits vorhandene Voraussetzung eines geladenen Cloud-Profils für Android-Spielstarts bleibt bestehen.

## Validierung

- Google-Start und Menü: 51 Android- und 3 Desktop-Prüfungen gegen kontrollierte SDK-/Engine-Grenzen. Enthält verzögerte native Anmeldung, altes bereits angemeldetes/generiertes Konto, verspätete Provider-Verifikation, verspätete Cloud-Daten, fehlerhafte Anmeldung, interaktive Wiederholung und fehlgeschlagene explizite Wiederherstellung mit erhaltener Profilsicherung.
- Gegenprobe: Der ursprüngliche AccountServerBootstrap kompiliert mit denselben neuen Fixtures und scheitert gezielt daran, dass er die gecachte Identität statt einer direkten Google-Provider-Anmeldung verwendet.
- Account/Wallet: 15 bestehende verzögerte Lade-, Kauf-, Speicher-, Reconnect- und Journal-Szenarien bestanden.
- Lobby/Ranked: Die vorhandenen Fixtures wurden um die Google-Anzeigenamen-API und die bereits zuvor eingeführten Entity-Registry-Versionen ergänzt. Die frühere Erwartung einer Weltsuche in jedem Frame wurde an den bereits bestehenden Registry-Cache angepasst; Produktionscode für Weltsynchronisierung wurde nicht geändert. Zusätzlich werden die drei Android-Lobby/Party-Ausweichwege und der Busy-Status ohne Google-Bootstrap geprüft.

Diese Prüfungen führen weder die native Google-Anmeldung noch einen Unity-/IL2CPP-/Android-Build aus. Ein sauberer Android-Neubau und ein Kaltstart auf dem betroffenen Handy bleiben erforderlich. In Unity: **DIMENSIONS FRONT > Release > Rebuild signed Android bundle (clean)**. Ein GitHub-Push aktualisiert keine installierte App. Diese Änderung benötigt keinen neuen Dedicated-Server-Build.

## Separater AdMob-Fehler: Internetrecherche

Die Meldung `Unable to obtain a JavascriptEngine` wurde am 30. Oktober 2026 in Primärquellen recherchiert. Die konkrete Geräteursache ist weiterhin nicht bestätigt; es wurde dafür kein spekulativer SDK-Wechsel oder Eingriff in die Belohnungslogik vorgenommen.

- [Google: Android Ad load errors](https://developers.google.com/admob/android/ad-load-errors) nennt die exakte Meldung und unter anderem den unzulässigen WebView-Einsatz in privilegierten Prozessen. Die endgültig generierte Manifestdatei und native Geräteprotokolle lagen für den betroffenen Build nicht vor.
- [Direkter Fehlerbericht mit bestätigter DNS-Ursache](https://github.com/googleads/googleads-mobile-flutter/issues/149#issuecomment-814525003): Der meldende Entwickler konnte die Anzeigenauslieferung durch Freigabe der AdMob-Adressen im verwendeten Pi-hole wiederherstellen. Das belegt eine mögliche Ursache, keine Diagnose für dieses Gerät. Ein geräteweiter DNS-/VPN-Filter kann bei WLAN und Mobilfunk aktiv bleiben.
- [Bericht über unterschiedliche nachgeladene Google-Werbemodule](https://github.com/googleads/googleads-mobile-flutter/issues/1422): Der Autor beobachtete den Fehler abhängig von der Dynamite-Modulversion und Region. Der Google-Mitarbeiter verwies an den SDK-Support; er bestätigte diese vermutete Ursache nicht. Daraus folgt keine belegte allgemeine Störung oder Empfehlung für einen SDK-Downgrade.
- [Antwort eines SDK-Mitarbeiters](https://github.com/googleads/googleads-mobile-flutter/issues/1356#issuecomment-3437491929): Native SDK-Protokolle enthalten zusätzliche Stacktraces zur eigentlichen Fehlerquelle. Für die weitere Eingrenzung sind die Android-Logcat-Meldungen um den Anzeigenfehler relevant, insbesondere Ads, Chromium/WebView und DynamiteModule. Die Spielklassen protokollieren den vollständigen Ladefehler bereits.

Weder ein defekter Google-Spielaccount noch ein erforderlicher Wechsel des Spielservers ist durch diese Werbemeldung belegt.
