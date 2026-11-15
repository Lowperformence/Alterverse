"""Parse modified C# files; this is not a Unity compile."""
from pathlib import Path
from tree_sitter import Language, Parser
import tree_sitter_c_sharp

root=Path(__file__).resolve().parents[3]
parser=Parser(Language(tree_sitter_c_sharp.language()))
for path in (
    'Assets/Alterverse/Scripts/Combat/KillstreakRewardState.cs',
    'Assets/Alterverse/Scripts/UI/CombatFeedbackHUD.cs',
    'Assets/Alterverse/Scripts/Profile/GameLanguage.cs',
    'Assets/Alterverse/Scripts/Multiplayer/CoopGameplayCommands.cs',
    'Assets/Alterverse/Scripts/Multiplayer/KillstreakNetworkSync.cs',
    'Assets/Alterverse/Scripts/Multiplayer/CoopWorldStateSync.cs',
    'Assets/Alterverse/Scripts/Multiplayer/CoopWorldEventSync.cs',
    'Assets/Alterverse/Scripts/Multiplayer/CoopPlanHandshake.cs',
    'Assets/Alterverse/Scripts/Multiplayer/OnlinePlanHandshake.cs',
    'Assets/Alterverse/Scripts/Multiplayer/DedicatedOnlineMatchHandshake.cs',
):
    tree=parser.parse((root/path).read_bytes())
    if tree.root_node.has_error:raise SystemExit('C# syntax error: '+path+'\n'+str(tree.root_node))
print('PASS changed C# syntax (not a native Unity build)')
