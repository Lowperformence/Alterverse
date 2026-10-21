"""Compile the actual catalog, crystal artwork and shop method with a drawing fixture.

Optional PNGs approximate the Unity font/panels; this is not a device screenshot.
Requires Pillow only when --output is supplied.
"""
import argparse
import json
from pathlib import Path
import subprocess
import tempfile

p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--dotnet-root', type=Path, required=True)
p.add_argument('--output', type=Path)
a = p.parse_args()
root = Path(__file__).resolve().parents[3]
here = Path(__file__).resolve().parent
src = root / 'Assets/Alterverse/Scripts'
menu = (src / 'UI/MainMenuController.cs').read_text()
method = menu[menu.index('        private void DrawShardShop()'):menu.index('        private void DrawSkinShop()')]
dotnet = a.dotnet_root.resolve()
csc = sorted((dotnet / 'sdk').glob('*/Roslyn/bincore/csc.dll'))[-1]
refs = sorted((dotnet / 'packs/Microsoft.NETCore.App.Ref').glob('*/ref/net8.0'))[-1]
version = sorted((dotnet / 'shared/Microsoft.NETCore.App').iterdir())[-1].name
with tempfile.TemporaryDirectory(prefix='df-shop-visual-') as temp:
    temp = Path(temp)
    harness = temp / 'Harness.cs'
    harness.write_text('''using System;using System.IO;using System.Text.Json;
using Alterverse.Shop;using Alterverse.Profile;using UnityEngine;
namespace Alterverse.UI {class MainMenuController {
float Left=>20;float ContentWidth;float Right=>Left+ContentWidth;FrontEndTheme theme=new();
''' + method + '''
public static void Main(string[] args){Resources.Catalog=args[0];
var ui=new MainMenuController{ContentWidth=float.Parse(args[1])};ui.DrawShardShop();
int count=GUI.Commands.Count;Event.current.type=EventType.Layout;
ShardShopVisualOverlay.DrawPackCard(new Rect(0,0,200,192),ShardPacks.All[0],0);
if(GUI.Commands.Count!=count)throw new Exception("Artwork must only run on Repaint");
File.WriteAllText(args[2],JsonSerializer.Serialize(new {commands=GUI.Commands,textures=GUI.Textures},new JsonSerializerOptions{IncludeFields=true}));
}}}''')
    dll = temp / 'Visual.dll'
    rsp = temp / 'compile.rsp'
    rsp.write_text('\n'.join(['-nologo', '-langversion:9', '-nowarn:0649', '-out:' + str(dll),
        *['-r:' + str(f) for f in refs.glob('*.dll')], str(here / 'EngineStubs.cs'), str(harness),
        str(src / 'Shop/ShardPacks.cs'), str(src / 'UI/ShardShopVisualOverlay.cs')]))
    subprocess.run([str(dotnet / 'dotnet'), str(csc), '@' + str(rsp)], check=True)
    dll.with_suffix('.runtimeconfig.json').write_text(json.dumps({'runtimeOptions': {
        'tfm': 'net8.0', 'framework': {'name': 'Microsoft.NETCore.App', 'version': version}}}))
    for width in (672, 992, 1360):
        capture = temp / (str(width) + '.json')
        subprocess.run([str(dotnet / 'dotnet'), str(dll),
            str(root / 'Assets/Alterverse/Resources/Commerce/ShardCatalog.json'), str(width), str(capture)], check=True)
        scene = json.loads(capture.read_text())
        commands = scene['commands']
        buttons = [c for c in commands if c['kind'] == 'button']
        assert len(buttons) == 7, 'Subscription, manage, four packs and reconnect must remain accessible'
        for i, command in enumerate(buttons):
            r = command['rect']
            assert r['x'] >= 20 and r['x'] + r['width'] <= width + 20.1 and r['y'] + r['height'] <= 700
            for other in buttons[i + 1:]:
                s = other['rect']
                assert not (r['x'] < s['x'] + s['width'] and s['x'] < r['x'] + r['width'] and
                            r['y'] < s['y'] + s['height'] and s['y'] < r['y'] + r['height']), 'Overlapping purchase controls'
        assert len(scene['textures']) == 3, 'Only cached crystal, glow and white textures are used'
        if a.output:
            from PIL import Image, ImageDraw, ImageFont
            a.output.mkdir(parents=True, exist_ok=True)
            image = Image.new('RGBA', (width + 40, 458), '#080e1e')
            textures = {}
            for name, texture in scene['textures'].items():
                bitmap = Image.new('RGBA', (texture['width'], texture['height']))
                bitmap.putdata([tuple(pixel[k] for k in ('r','g','b','a')) for pixel in texture['pixels']])
                textures[name] = bitmap.transpose(Image.Transpose.FLIP_TOP_BOTTOM)
            draw = ImageDraw.Draw(image)
            for c in commands:
                r = c['rect']; x, y, w, h = r['x'], r['y'] - 250, r['width'], r['height']
                if c['kind'] == 'texture':
                    bitmap = textures[c['texture']]
                    if c['fit']:
                        scale = min(w / bitmap.width, h / bitmap.height)
                        tw, th = round(bitmap.width * scale), round(bitmap.height * scale)
                        x += (w - tw) / 2; y += (h - th) / 2
                    else: tw, th = round(w), round(h)
                    bitmap = bitmap.resize((max(1,tw), max(1,th)), Image.Resampling.LANCZOS)
                    tint = c['tint']
                    channels = bitmap.split()
                    bitmap = Image.merge('RGBA', tuple(ch.point(lambda v, f=tint[k]: round(v*f)) for ch,k in zip(channels, ('r','g','b','a'))))
                    image.alpha_composite(bitmap, (round(x),round(y)))
                elif c['kind'] in ('panel', 'button'):
                    primary = c.get('primary', False)
                    draw.rounded_rectangle((x,y,x+w,y+h), radius=9, fill='#352b1d' if primary else '#0c1629',
                                           outline='#d7b87e' if primary else '#354355', width=1)
                if 'text' in c:
                    size = c['size']
                    font_path = '/usr/share/fonts/truetype/dejavu/DejaVuSans' + ('-Bold' if size >= 19 else '') + '.ttf'
                    while True:
                        font = ImageFont.truetype(font_path, size)
                        lines = ['']
                        for word in c['text'].split():
                            candidate = (lines[-1] + ' ' + word).strip()
                            if font.getlength(candidate) > w - 8 and lines[-1]: lines.append(word)
                            else: lines[-1] = candidate
                        if len(lines)*(size+2) <= h or size <= 12: break
                        size -= 1
                    for n, line in enumerate(lines):
                        tx = x + (w - font.getlength(line))/2 if c.get('center') or c['kind']=='button' else x
                        draw.text((tx,y+(h-len(lines)*(size+2))/2+n*(size+2)), line, font=font,
                                  fill='#9eb3cc' if c.get('muted') else '#f3faff')
            image.convert('RGB').save(a.output / ('shop-' + str(width) + '.png'))
print('PASS actual shop method/catalog/artwork compilation, Repaint-only drawing and non-overlapping controls at 672/992/1360 widths; fonts and panels are fixtures.')
