# Clan-Chatfilter

Stand: 12. Oktober 2026. Der Inhaltsfilter läuft im Account-Server für den vorhandenen Clan-Textchat.

## Verhalten

- Neue Nachrichten werden vor dem Speichern auf deutsche und englische Beleidigungen, sexuelle Ausdrücke und Aufforderungen, Hassausdrücke, direkte Drohungen und Aufforderungen zur Selbsttötung geprüft.
- Treffer werden vollständig abgewiesen. HTTP 400 enthält eine feste Meldung ohne Wiederholung des beanstandeten Textes. Der vorhandene Unity-Client zeigt diese Servermeldung an und behält den Entwurf zur Korrektur; erst erfolgreich gesendete Nachrichten werden aus dem Eingabefeld entfernt.
- Häufige Umgehungen werden berücksichtigt: Groß-/Kleinschreibung, Umlaute/Akzente, Unicode-Varianten, einige kyrillische/griechische Buchstaben, Zahlenersatz, Trennzeichen und wiederholte Buchstaben.
- Wortgrenzen schützen normale Wörter wie `assistant`, `Sussex`, `analysis` und `cockpit`. Identitätsbegriffe wie `gay`, `lesbian` und `trans` allein werden nicht blockiert. Normale Spielaussagen wie `Kill the boss first` bleiben erlaubt.
- Bereits gespeicherte problematische Nachrichten erscheinen als `[Message hidden by chat filter]`; problematische Absendernamen im Chat als `Player`. Die gespeicherten Originale und Nachrichten-IDs bleiben erhalten, sodass Autor oder Clanleiter die Nachricht weiterhin löschen können.
- Länge, Mitgliedschaft, Löschrechte, Reihenfolge und Wiederholungsbehandlung des vorhandenen Chats gelten weiterhin. Ein vom Client übermitteltes Abschalten des Filters wird ignoriert.

## Aufbau und Herkunft

`Server/commerce/chat_filter.py` enthält die für dieses Projekt verfassten Regeln und die Normalisierung. Es wurden keine fremden Wortlisten oder Bibliotheksquellen übernommen. Der Filter benötigt keine zusätzlichen Python-Pakete, keinen API-Schlüssel, keine externen Anfragen und keine laufenden Gebühren.

SQLite und Firestore verwenden dieselben Prüfungen in `social.py`; der Firestore-Adapter übernimmt sie automatisch. Die Regeln gelten für den Clan-Textchat. Sprachchat und Bilder werden damit nicht moderiert. Clan-/Profilnamen außerhalb der Chatansicht werden durch diese Änderung nicht moderiert.

Die Erkennung ist regelbasiert: Sie versteht weder jeden Kontext noch alle Sprachen oder neuen Umschreibungen. Die Einträge in `TERMS` können mit konkreten Missbrauchsbeispielen erweitert werden. Ergänzungen müssen jeweils auch gegen normale Spielnachrichten geprüft werden.

## Aktivierung und Prüfung

Die Änderung wird nach dem Neubauen und Ausrollen des bestehenden Account-Servers wirksam; ein GitHub-Push allein aktualisiert einen laufenden Server nicht. Dafür gelten die bestehenden Anweisungen in [Account-Server-Setup.md](Account-Server-Setup.md). Ein Unity-/Android-Neubau ist für diese Serverprüfung nicht erforderlich. Der Client muss den vorhandenen Clan-Chatserver erreichen.

```bash
python -m unittest discover -s Server/commerce -q
```

`test_chat_filter.py` prüft die Kategorien, Umgehungen, unauffällige Nachrichten und Ausblendung. Die erweiterten Szenarien in `test_social.py` prüfen tatsächliche HTTP-Antworten, verworfene Nachrichten ohne Speicherung, korrigierte Wiederholungen und erhaltene Löschrechte. Dieselben Social-Szenarien laufen für SQLite und einen strengen Firestore-Testadapter. Dieser Adapter ersetzt keinen Test am echten Firestore-Dienst.
