"""Embed the existing protocol data and SQL schema for the single Node file."""
import ast
import json
from pathlib import Path
import sys

ROOT=Path(__file__).resolve().parents[2]

def generated():
    sys.path.insert(0,str(ROOT/'Server/commerce'))
    import clan_chat
    import firestore_social
    sql=[]
    for name in ('ledger.py','social.py','clan_chat.py'):
        tree=ast.parse((ROOT/'Server/commerce'/name).read_text())
        for node in ast.walk(tree):
            if (isinstance(node,ast.Call) and isinstance(node.func,ast.Attribute)
                and node.func.attr=='executescript' and node.args and isinstance(node.args[0],ast.Constant)):
                sql.append(node.args[0].value)
    sql.append('ALTER TABLE social_clan ADD COLUMN avatar INTEGER NOT NULL DEFAULT -1;')
    values={'DF_SOCIAL_SQL':'\n'.join(sql),
            'DF_IDENTITY':json.loads((ROOT/'Assets/Alterverse/Resources/Social/SafeIdentityCatalog.json').read_text()),
            'DF_SOCIAL_TABLES':firestore_social.TABLES,'DF_SOCIAL_KEYS':firestore_social.KEYS,
            'DF_CHAT_RULES':clan_chat.CHAT_RULES,'DF_LEADER_RULES':clan_chat.LEADER_RULES,
            'DF_REPORT_REASONS':clan_chat.REASONS}
    text='// Embedded from the existing schema, identity catalog and clan-chat rules.\n'
    text+=''.join('const '+key+' = '+json.dumps(value,ensure_ascii=False,separators=(',',':'))+';\n' for key,value in values.items())
    folded={chr(i):chr(i).casefold() for i in range(0x110000) if chr(i).casefold()!=chr(i).lower()}
    return text+'const DF_CASE_FOLD = '+json.dumps(folded,ensure_ascii=True,separators=(',',':'))+';\n'

if __name__=='__main__':
    target=ROOT/'Server/commerce/node/standalone-data.inc.js'
    if '--check' in sys.argv:
        if target.read_text()!=generated():raise SystemExit('Standalone protocol data differs from the existing rules; regenerate it.')
        print('Embedded clan schemas, catalog, moderation terms and Unicode rules match the existing Python source.')
    else:
        target.write_text(generated(),encoding='utf-8',newline='\n')
        print(target)
