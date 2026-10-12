"""Offline German/English rules for the clan's text chat.

Project-authored rules, not a downloaded word list. No network service, telemetry,
or additional package is used. This is bounded word/phrase matching, not semantic
moderation; extend the rules with reviewed examples as abuse patterns change.
"""
import re
import unicodedata

REJECTION_MESSAGE = ('Message blocked: insults, sexual content, hate speech and '
                     'threats are not allowed.')
HIDDEN_MESSAGE = '[Message hidden by chat filter]'
HIDDEN_NAME = 'Player'

# Deliberately omit ambiguous words such as "ass", "dick" (German adjective),
# "gay", "lesbian" and "trans". Identity words are not abuse by themselves.
# Inflections are explicit: substring matching would also censor ordinary words.
TERMS = {
    'insult': (
        'arschloch', 'arschlocher', 'arschloecher', 'arsch', 'hurensohn',
        'hurensohne', 'hurensoehne', 'hurentochter', 'hure', 'huren',
        'wichser', 'wixer', 'wixxer', 'schlampe', 'schlampen', 'fotze', 'fotzen',
        'drecksau', 'miststuck', 'miststueck', 'scheisse', 'scheiss',
        'scheis', 'kacke', 'fick', 'ficke', 'ficken', 'ficker', 'gefickt',
        'verfickt', 'fickt', 'fickst', 'idiot', 'idioten', 'idiotin', 'idiotinnen',
        'vollidiot', 'vollidioten', 'drecksidiot', 'drecksack', 'scheisskerl',
        'spast', 'spasti', 'spasten', 'missgeburt',
        'bastard', 'bastards', 'fuck', 'fucks', 'fucking', 'fucked', 'fucker',
        'fuckers', 'motherfucker', 'motherfuckers', 'shit', 'bullshit',
        'asshole', 'assholes', 'arsehole', 'bitch', 'bitches', 'dumbass',
        'dickhead', 'cunt', 'cunts', 'twat', 'whore', 'whores', 'slut', 'sluts',
        'idiots', 'stfu',
    ),
    'sexual': (
        'sex', 'sexting', 'cybersex', 'phone sex', 'porn', 'porno', 'pornos',
        'pornografie', 'pornographie', 'pornography', 'pornographic', 'pornhub',
        'hentai', 'blowjob', 'blowjobs', 'handjob', 'handjobs', 'cumshot',
        'cumshots', 'gangbang', 'deepthroat', 'anal sex', 'oral sex',
        'masturbate', 'masturbating', 'masturbation', 'masturbieren',
        'wichsen', 'wixen', 'orgasm', 'orgasmus', 'dildo', 'sex toy',
        'analverkehr', 'oralverkehr',
        'nudes', 'nude pics', 'nude photos', 'nude pictures',
        'naked pics', 'naked photos', 'naked pictures', 'nacktbilder', 'nacktfotos',
        'send nude', 'send naked', 'schick nackte', 'schick nackt',
        'suck my dick', 'suck my cock', 'lick my pussy', 'dick pic', 'dick pics',
        'schwanzbilder', 'schwanzfotos', 'zeig deine bruste',
        'zeig deine brueste', 'zeig deine titten', 'onlyfans',
    ),
    'hate': (
        'nigger', 'niggers', 'nigga', 'niggas', 'neger', 'faggot', 'faggots',
        'schwuchtel', 'schwuchteln', 'kanake', 'kanaken', 'heil hitler',
        'sieg heil', 'white power', 'kill all jews', 'gas the jews',
        'juden vergasen', 'auslander raus', 'auslaender raus',
    ),
    'threat': (
        'kill yourself', 'go kill yourself', 'kys', 'go die', 'hope you die',
        'you should die', 'bring dich um',
        'bring dich selber um', 'bring dich selbst um',
        'i will kill you', 'i ll kill you', 'im going to kill you',
        'i am going to kill you', 'ich bring dich um', 'ich bringe dich um',
        'ich tote dich', 'ich toete dich', 'ich werde dich umbringen',
        'ich werde dich toten', 'ich werde dich toeten',
        'i will rape you', 'i ll rape you', 'ich vergewaltige dich',
    ),
}

# Common Latin lookalikes from Cyrillic and Greek. No claim of full Unicode
# confusable coverage. NFKC also folds full-width/mathematical Latin characters.
LOOKALIKES = str.maketrans({
    'а': 'a', 'е': 'e', 'о': 'o', 'р': 'p', 'с': 'c', 'х': 'x', 'у': 'y',
    'і': 'i', 'ј': 'j', 'ѕ': 's', 'ӏ': 'l', 'к': 'k', 'м': 'm', 'т': 't',
    'α': 'a', 'ε': 'e', 'ο': 'o', 'ρ': 'p', 'τ': 't', 'υ': 'u', 'χ': 'x',
    'ι': 'i', 'κ': 'k',
})
LEET = str.maketrans({'0': 'o', '1': 'i', '3': 'e', '4': 'a', '5': 's',
                      '7': 't', '8': 'b', '@': 'a', '$': 's', '!': 'i'})
LEET_L = str.maketrans({'0': 'o', '1': 'l', '3': 'e', '4': 'a', '5': 's',
                        '7': 't', '8': 'b', '@': 'a', '$': 's', '!': 'i'})


def normalize(text):
    text = unicodedata.normalize('NFKC', text).casefold().translate(LOOKALIKES)
    text = unicodedata.normalize('NFKD', text)
    text = ''.join(c for c in text if unicodedata.category(c) not in ('Mn', 'Me', 'Cf'))
    # Bound regex work for stretched letters; preserve ordinary doubled letters.
    return re.sub(r'([^\W\d_])\1{2,}', r'\1\1', text)


def term_pattern(term):
    words = []
    for word in term.split():
        if len(word) >= 4 or word in ('sex', 'kys'):
            # Match separated/stretched letters; outer letters protect real words.
            words.append(r'[\W_]{0,3}'.join(re.escape(c) + '+' for c in word))
        else:
            # Short terms stay exact to avoid accidental joins of gameplay words.
            words.append(re.escape(word))
    # Digits and underscores may pad an insult, but enclosing letters must not
    # turn "analysis" or "assistant" into matches for shorter rule entries.
    return r'(?<![^\W\d_])' + r'[\W_]{1,12}'.join(words) + r'(?![^\W\d_])'


RULES = tuple((category, re.compile('|'.join(term_pattern(t) for t in terms)))
              for category, terms in TERMS.items())


def blocked_reason(text):
    """Return a rule category or None; the caller owns length/type validation."""
    normalized = normalize(text)
    variants = (normalized, normalize(normalized.translate(LEET)),
                normalize(normalized.translate(LEET_L)))
    for category, pattern in RULES:
        if any(pattern.search(value) for value in variants):
            return category
    return None


def require_allowed(text):
    if blocked_reason(text):
        # Do not echo the submitted abuse in HTTP errors or the game's status UI.
        raise ValueError(REJECTION_MESSAGE)


def visible_text(text):
    """Protect readers from retained messages submitted before this filter."""
    return HIDDEN_MESSAGE if blocked_reason(text) else text


def visible_name(name):
    return HIDDEN_NAME if blocked_reason(name) else name
