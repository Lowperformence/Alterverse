"""Behavioral corpus: abuse, evasions and legitimate German/English game chat."""
import unittest

from chat_filter import (HIDDEN_MESSAGE, HIDDEN_NAME, REJECTION_MESSAGE,
                         blocked_reason, require_allowed, visible_name, visible_text)


class ChatFilterTests(unittest.TestCase):
    def test_german_and_english_abuse(self):
        for text in ('Du bist ein Idiot!', 'Ihr seid Arschlöcher.', 'Hurensohn',
                     'fuck off', 'you are a stupid bitch', 'Vollidioten'):
            with self.subTest(text=text):
                self.assertEqual(blocked_reason(text), 'insult')

    def test_sexual_content_and_solicitations(self):
        for text in ('Send nudes', 'schick mir Nacktbilder', 'watch porn',
                     'want a blowjob?', 'suck my dick', 'send naked photos',
                     'https://pornhub.com/watch', 'cybersex', 'sex',
                     'send me naked pics', 'zeig deine Brüste', 'Dildo'):
            with self.subTest(text=text):
                self.assertEqual(blocked_reason(text), 'sexual')

    def test_hate_and_direct_threats(self):
        for text in ('heil hitler', 'white power', 'Schwuchtel', 'Ausländer raus'):
            with self.subTest(text=text):
                self.assertEqual(blocked_reason(text), 'hate')
        for text in ('kill yourself', 'kys', "I'll kill you", 'I will kill you',
                     'ich bring dich um', 'bring dich selbst um',
                     'ich werde dich töten', 'ich töte dich',
                     'ich werde dich umbringen', 'I will rape you'):
            with self.subTest(text=text):
                self.assertEqual(blocked_reason(text), 'threat')

    def test_common_obfuscation_is_blocked(self):
        for text in ('1d10t', 's1ut', 'f.u.c.k', 'f u c k', 'f_u_c_k',
                     'FFFFUUUCCCKKK', 'fück', 'ｆｕｃｋ', '𝐟𝐮𝐜𝐤',
                     'f\u200buck', 'fіck', 'fυck', 'sch3i$$3',
                     's 3 x', 'k.y.s', 'send N.U.D.E.S', 'arsch-l0ch',
                     'du_idiot', 'idiot123', '123fuck', 'f-u-c-k123'):
            with self.subTest(text=text):
                self.assertIsNotNone(blocked_reason(text))

    def test_legitimate_chat_and_identity_words_are_preserved(self):
        for text in ('GG, well played!', 'Welcome to the clan!', 'Hi leader',
                     'Kill the boss first', 'Get 5 kills with Nerok',
                     'Use your ultimate on their tank', 'Sivra needs healing',
                     'Dicke Rüstung für Brak', 'Die Kiste ist links',
                     'Klasse Spiel!', 'Bitte den Punkt verteidigen',
                     'Wir müssen uns verbessern', 'Wir treffen uns um 19:30',
                     'Scunthorpe', 'assistant', 'assessment', 'Sussex',
                     'analysis', 'cockpit', 'Dick ist unser Spielername',
                     'Ich bin gay', 'lesbian', 'trans', 'heterosexual',
                     '中文聊天', 'こんにちは', '🙂 gg', 'https://example.org/game'):
            with self.subTest(text=text):
                self.assertIsNone(blocked_reason(text))
                self.assertEqual(visible_text(text), text)

    def test_error_and_history_do_not_echo_abuse(self):
        with self.assertRaisesRegex(ValueError, '^Message blocked:') as error:
            require_allowed('hurensohn')
        self.assertEqual(str(error.exception), REJECTION_MESSAGE)
        self.assertNotIn('hurensohn', str(error.exception))
        self.assertEqual(visible_text('send nudes'), HIDDEN_MESSAGE)
        self.assertEqual(visible_name('hurensohn'), HIDDEN_NAME)
        self.assertEqual(visible_name('Traveler'), 'Traveler')

    def test_bounded_stretched_and_repeated_inputs(self):
        self.assertEqual(blocked_reason('f' * 60 + 'u' * 60 + 'c' * 60 + 'k' * 60), 'insult')
        for text in ('a' * 240, 'i_' * 120, '1' * 240, 's.' * 120):
            with self.subTest(text=text[:10]):
                self.assertIsNone(blocked_reason(text))
