#!/usr/bin/env python3
"""Compile production movement transport/history and scoreboard accessors with network fixtures.
No Unity player, native physics, device rendering, or live latency is simulated here.
"""
import argparse
import json
from pathlib import Path
import re
import subprocess
import tempfile


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--dotnet-root', type=Path, required=True)
    args = parser.parse_args()
    here = Path(__file__).resolve().parent
    source = here.parents[2] / 'Assets/Alterverse/Scripts'
    dotnet = args.dotnet_root.resolve()
    sdk = sorted((dotnet / 'sdk').iterdir())[-1]
    runtime = sorted((dotnet / 'shared/Microsoft.NETCore.App').iterdir())[-1]
    with tempfile.TemporaryDirectory(prefix='df-multiplayer-') as directory:
        directory = Path(directory)
        board = (source / 'Match/MatchScoreboard.cs').read_text()
        start = board.index('public void GetNetworkScore(')
        end = board.index('private static string CoopDisplayName', start)
        accessors = directory / 'ScoreboardAccessors.cs'
        accessors.write_text('using Alterverse.Combat;using Alterverse.Multiplayer;using UnityEngine;'
                             'namespace Alterverse.Match {public sealed partial class MatchScoreboard {' + board[start:end] + '}}')
        main_source = (source / 'Multiplayer/CoopHeroStateSync.cs').read_text()
        constants = re.findall(r'private const (?:string|float) (?:OwnerPoseMessage|HeroSnapshotMessage|OwnerSendInterval|MinimumMovementSlack)=[^;]+;', main_source)
        assert len(constants) == 4
        declarations = directory / 'TransportConstants.cs'
        declarations.write_text('namespace Alterverse.Multiplayer {public sealed partial class CoopHeroStateSync {' + ''.join(constants) + '}}')
        pickup = (source / 'Combat/CentralHealingPickup.cs').read_text()
        a = pickup.index('private void Update()')
        b = pickup.index('private void AnimateAvailable()', a)
        heal = directory / 'HealingGameplay.cs'
        heal.write_text('using UnityEngine;using Alterverse.Characters;namespace Alterverse.Combat {public sealed partial class CentralHealingPickup {' + pickup[a:b] + '}}')
        world = (source / 'Multiplayer/CoopWorldStateSync.cs').read_text()
        a = world.index('private void SendState(')
        b = world.index('private void OnState(', a)
        packets = directory / 'WorldPackets.cs'
        update = world[world.index('private void Update()'):world.index('private void RefreshEntities()')]
        interval = re.search(r'private const float SendInterval=[^;]+;', world)[0]
        packets.write_text('using System.Collections.Generic;using Alterverse.Combat;using UnityEngine;using Unity.Netcode;using Unity.Collections;namespace Alterverse.Multiplayer {public sealed partial class CoopWorldStateSync {' + interval + update + world[a:b] + '}}')
        locomotion = (source / 'Characters/HeroLocomotion.cs').read_text()
        a = locomotion.index('private uint movementRevision;')
        b = locomotion.index('public bool OwnsMovementFrame', a)
        epochs = directory / 'MovementEpochs.cs'
        epochs.write_text('namespace Alterverse.Characters {public sealed partial class HeroLocomotion {' + locomotion[a:b] + '}}')
        engine = directory / 'EngineStubs.cs'
        engine.write_text((here / 'EngineStubs.cs').read_text().replace('public sealed class HeroLocomotion:', 'public sealed partial class HeroLocomotion:'))
        output = directory / 'Regression.dll'
        response = directory / 'compile.rsp'
        files = [engine, epochs, here / 'Regression.cs', accessors, declarations, heal, packets, source / 'Combat/HeroCombatStatus.cs',
                 source / 'Multiplayer/CoopHeroStateSync.Transport.cs', source / 'Multiplayer/OwnerPoseHistory.cs']
        response.write_text('\n'.join(['-nologo', '-target:exe', '-langversion:9', '-nowarn:0649', '-out:' + str(output),
                                       *['-r:' + str(path) for path in runtime.glob('*.dll')], *map(str, files)]))
        subprocess.run([str(dotnet / 'dotnet'), str(sdk / 'Roslyn/bincore/csc.dll'), '@' + str(response)], check=True)
        output.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
            'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': runtime.name}}}))
        subprocess.run([str(dotnet / 'dotnet'), str(output)], check=True)


if __name__ == '__main__':
    main()
