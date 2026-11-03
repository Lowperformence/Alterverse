#!/usr/bin/env python3
"""Read-only, deterministic 1-D engagement baseline. Not a Unity combat emulator."""
from __future__ import annotations
import argparse, ast, gzip, hashlib, json, math, re
from dataclasses import dataclass, field
from pathlib import Path
from itertools import combinations
ROOT=Path(__file__).resolve().parents[2]
SCRIPTS=ROOT/'Assets/Alterverse/Scripts'
DEFAULT_DT=.00625
def read(name):
    matches=list(SCRIPTS.rglob(name)); assert len(matches)==1, name
    return matches[0].read_text()
def number(expr,constants):
    expr=re.sub(r'(?<=\d)f\b','',expr.strip())
    for k,v in sorted(constants.items(),key=lambda x:-len(x[0])): expr=expr.replace(k,str(v))
    node=ast.parse(expr,mode='eval')
    assert all(isinstance(n,(ast.Expression,ast.Constant,ast.BinOp,ast.Mult,ast.Div,ast.Add,ast.Sub,ast.UnaryOp,ast.USub,ast.Load)) for n in ast.walk(node)),expr
    return float(eval(compile(node,'<source-number>','eval'),{'__builtins__':{}},{}))
def load_catalog():
    constants={}
    for cls in ['TimmyBalance','VoidKnightBalance','HeroAbilityKit','HeroIdentity']:
        src=read(cls+'.cs')
        for block in re.findall(r'public const (?:float|int) ([^;]+);',src):
            for pair in block.split(','):
                if '=' not in pair: continue
                k,v=pair.strip().split('=',1); k=k.strip()
                try: constants[cls+'.'+k]=number(v,{**constants,**{a.split('.')[-1]:b for a,b in constants.items()}})
                except (AssertionError,SyntaxError,NameError): pass
    identity=read('HeroIdentity.cs'); profiles=read('HeroCombatProfile.cs'); kit=read('HeroAbilityKit.cs')
    names=dict(re.findall(r'HeroId\.(\w+)=>"([^"]+)"',identity))
    health_switch=identity.split('(id switch{')[1].split('})*')[0]
    health_expr=dict(re.findall(r'HeroId\.(\w+)=>([^,}]+)',health_switch))
    attack_expr=dict(re.findall(r'HeroId\.(\w+)=>([^,}]+)',identity.split('AttackDamage(HeroId id)')[1]))
    shields=dict(re.findall(r'HeroId\.(\w+) => ([^,\n]+)',read('Health.cs').split('private const')[0]))
    winds=dict(re.findall(r'HeroId\.(\w+)=>([^,\n]+)',read('HeroAttackDelivery.cs').split('return hero switch')[1].split('};')[0]))
    out={}
    for hero,args in re.findall(r'HeroId\.(\w+)=>new HeroCombatStats\(([^)]+)\)',profiles):
        vals=[number(x,constants) for x in args.split(',')]
        fixed_hp=dict(re.findall(r'id==HeroId\.(\w+)\?([^:?]+)',identity.split('BaseHealth(HeroId id)=>')[1].split('(id switch')[0]))
        hp=number(fixed_hp[hero],constants) if hero in fixed_hp else number(health_expr[hero],constants)*constants['HeroIdentity.GlobalHealthMultiplier']
        shield=number(shields.get(hero,'50f').replace('Alterverse.Characters.',''),constants)
        block=kit.split('HeroId.'+hero+'=>new HeroAbilityLoadout(')[1].split('HeroId.')[0].split('_=>new HeroAbilityLoadout')[0]
        specs=[]
        for match in re.finditer(r'new\("([^"]+)",([^)]*)\)',block):
            values=[number(x,constants) for x in match[2].split(',')]; values += [0]*(5-len(values))
            specs.append(dict(zip(['name','cooldown','power','range','radius','windup'],[match[1]]+values)))
        assert len(specs)==5,(hero,specs)
        out[hero]=dict(id=hero,name=names[hero],hp=hp,shield=shield,damage=number(attack_expr[hero],constants),interval=vals[1],range=vals[0],speed=vals[2]*number(re.search(r'GlobalMoveSpeedMultiplier = ([^;]+)',profiles)[1],constants),windup=number(winds.get(hero,'.16f'),constants),specs=specs)
    assert len(out)==10
    return out
@dataclass
class Unit:
    p:dict; team:int; x:float; index:int=0
    hp:float=field(init=False); base:float=field(init=False)
    temp:float=0; expiry:float=0; recharge:float=math.inf; ready:float=0; attack_until:float=0
    qready:float=0; wready:float=0; dashready:float=0; first:float|None=None; prehit:float=0; prehit_total:float=0
    damage:float=0; healing:float=0; support:float=0; absorbed:float=0; attacks:int=0; contact:float=0; regen:float=0
    def __post_init__(self): self.hp=self.p['hp']; self.base=self.p['shield']
    def heal(self,amount):
        applied=min(amount,max(0,self.p['hp']-self.hp)); self.hp+=applied; self.healing+=applied; return applied
    def shield(self,amount,duration,t):
        self.temp=max(self.temp if t<self.expiry else 0,min(amount,self.p['hp']*.4)); self.expiry=max(self.expiry,t+duration)
    def hit(self,amount,t):
        if t>=self.expiry:self.temp=0
        if t>=self.recharge:self.base=self.p['shield'];self.recharge=math.inf
        before=self.hp+self.base+self.temp
        used=min(self.temp,amount);self.temp-=used;amount-=used
        usedbase=min(self.base,amount);self.base-=usedbase;amount-=usedbase
        self.absorbed+=used+usedbase
        if self.base<self.p['shield']:self.recharge=t+15
        hpdamage=min(self.hp,amount);self.hp-=hpdamage
        if self.first is None:self.prehit+=hpdamage;self.prehit_total+=used+usedbase+hpdamage
        return before-(self.hp+self.base+self.temp),hpdamage

def simulate(catalog,left,right,distance=8,duration=30,dt=DEFAULT_DT,kiting=True,skills=True,passives=True):
    if not math.isfinite(dt) or dt<=0 or not math.isfinite(duration) or duration<=0:
        raise ValueError('Positive finite timestep and duration required')
    if not left or not right or len(set(left))!=len(left) or len(set(right))!=len(right):
        raise ValueError('Two nonempty teams without within-team duplicates required')
    attack_slow=float(re.search(r'attackMovementMultiplier = ([0-9.]+)f',read('HeroMotor.cs'))[1])
    units=[Unit(catalog[h],team,(-distance/2 if team==0 else distance/2),i) for team,roster in enumerate([left,right]) for i,h in enumerate(roster)]
    events=[]; sequence=0; t=0
    while t<duration:
        living=[u for u in units if u.hp>0]
        if len({u.team for u in living})<2:break
        for u in living:
            if t>=u.expiry:u.temp=0
            if t>=u.recharge:u.base=u.p['shield'];u.recharge=math.inf
            if passives and u.p['id']=='Kaelon' and u.hp<u.p['hp']:
                u.regen+=dt
                if u.regen>=1:u.heal(2);u.regen-=1
            else:u.regen=0
        # Simultaneous movement computed from the same starting positions.
        moves=[]
        for u in living:
            enemies=[v for v in living if v.team!=u.team]
            target=min(enemies,key=lambda v:(v.hp,abs(v.x-u.x),v.index))
            d=abs(target.x-u.x);sign=1 if target.x>u.x else -1
            if d<=u.p['range']:u.contact+=dt
            speed=u.p['speed']*(attack_slow if t<u.attack_until and u.p['id']!='Timmy' else 1)
            direction=sign if d>u.p['range'] else -sign if kiting and u.p['range']>4 and d<=u.p['range'] else 0
            delta=direction*speed*dt
            dashslots={'Veyr':3,'Nyra':2,'Elyra':3,'Traveler':2,'Timmy':3}
            if skills and u.p['id'] in dashslots and t>=u.dashready:
                spec=u.p['specs'][dashslots[u.p['id']]]
                if (u.p['range']<4 and d>u.p['range'] and d<spec['range']+u.p['range']) or (u.p['range']>4 and d<3):
                    delta=sign*min(spec['range'],max(0,d-1.5)) if u.p['range']<4 else -sign*spec['range']
                    u.dashready=t+spec['cooldown']
                    if u.p['id']=='Veyr':u.shield(30,2.25,t)
                    if u.p['id']=='Elyra':u.shield(50,2.25,t)
            moves.append((u,delta))
        for u,delta in moves:u.x+=delta
        for u in living:
            enemies=[v for v in living if v.team!=u.team]
            target=min(enemies,key=lambda v:(v.hp,abs(v.x-u.x),v.index)); d=abs(target.x-u.x)
            if skills and t>=u.wready:
                w=u.p['specs'][2]
                if u.p['id']=='Mira':
                    allies=[v for v in living if v.team==u.team and abs(v.x-u.x)<=w['range'] and v.hp<v.p['hp']*.95]
                    if allies:u_target=min(allies,key=lambda v:v.hp/v.p['hp']);applied=u_target.heal(w['power']);u.support+=applied if u_target is not u else 0;u.wready=t+w['cooldown']
                elif u.p['id']=='Kaelon' and u.temp<=5:u.shield(w['power'],4,t);u.wready=t+w['cooldown']
                elif u.p['id']=='Rook' and u.hp<u.p['hp']*.95:u.heal(w['power']);u.shield(30,3,t);u.wready=t+w['cooldown']
            if t>=u.ready and d<=u.p['range']:
                events.append((t+u.p['windup'],sequence,u,target,u.p['damage'],True));sequence+=1
                u.ready=t+u.p['interval'];u.attack_until=t+max(.18,u.p['windup']+.18)
                u.attacks+=1
            # Q baseline for uncomplicated single-target Q only. Other kits explicitly omitted.
            if skills and u.p['id'] in {'Veyr','Nyra','Mira','Traveler'} and t>=u.qready:
                q=u.p['specs'][1]
                if d<=q['range']:
                    wind={'Veyr':.13,'Nyra':.12,'Mira':.16,'Traveler':.14}[u.p['id']]
                    events.append((t+wind,sequence,u,target,q['power'],False));sequence+=1;u.qready=t+q['cooldown']
                    if u.p['id']=='Veyr':u.shield(20,2,t)
        due=[e for e in events if e[0]<=t+dt/2];events=[e for e in events if e[0]>t+dt/2]
        # Snapshot alive sources permits trades within a discrete step, independent of unit order.
        alive_ids={id(u) for u in living}
        for _,_,u,v,amount,basic in sorted(due,key=lambda e:(e[0],e[1])):
            if id(u) not in alive_ids or v.hp<=0:continue
            # Ranged cone release requires target still within source range; facing is idealized.
            release_range=u.p['range'] if basic else u.p['specs'][1]['range']
            if release_range>4 and abs(u.x-v.x)>release_range:continue
            dealt,hpdamage=v.hit(amount*(1.05 if u.p['id']=='Rook' else 1),t);u.damage+=dealt
            if dealt>0 and u.first is None:u.first=t
            if passives and basic and u.p['id']=='Rook' and 0<u.hp/u.p['hp']<.6:u.heal(hpdamage*.12)
        t=round(t+dt,8)
    team_hp=[sum(max(0,u.hp)+u.base+(u.temp if t<u.expiry else 0) for u in units if u.team==team and u.hp>0) for team in (0,1)]
    maxima=[sum(u.p['hp']+u.p['shield'] for u in units if u.team==team) for team in (0,1)]
    # Win=1; timeout: residual effective-health share. No hidden designer stat weights.
    value=1 if team_hp[1]==0 and team_hp[0]>0 else 0 if team_hp[0]==0 and team_hp[1]>0 else .5+(team_hp[0]/maxima[0]-team_hp[1]/maxima[1])*.5
    return dict(value=value,seconds=t,units=[dict(hero=u.p['id'],team=u.team,hp_remaining=max(0,u.hp),damage=u.damage,effective_healing_received=u.healing,ally_healing_generated=u.support,shield_absorbed=u.absorbed,hp_lost_before_first_hit=u.prehit,hp_and_shield_lost_before_first_hit=u.prehit_total,hp_fraction_lost_before_first_hit=u.prehit/u.p['hp'],first_hit=u.first,range_uptime=u.contact/max(t,dt)) for u in units])

def run(catalog,all_teams=False):
    ids=list(catalog); results=[]; values={h:[] for h in ids}
    for a,b in combinations(ids,2):
        for distance in (2,8):
            for kite in (False,True):
                for mode,skills,passives in [('basic_engagement',False,False),('supported_passives',False,True),('limited_kit',True,True)]:
                    r=simulate(catalog,[a],[b],distance=distance,kiting=kite,skills=skills,passives=passives)
                    row=dict(a=a,b=b,distance=distance,kiting_policy='retreat_inside_range' if kite else 'hold_inside_range',mode=mode,**r)
                    results.append(row)
                    if mode=='basic_engagement':values[a].append(r['value']);values[b].append(1-r['value'])
    scores={h:200*sum(v)/len(v) for h,v in values.items()}
    fine_values={h:[] for h in ids}; flips=[]; max_delta=0
    for row in results:
        if row['mode']!='basic_engagement':continue
        fine=simulate(catalog,[row['a']],[row['b']],distance=row['distance'],kiting=row['kiting_policy']=='retreat_inside_range',skills=False,passives=False,dt=DEFAULT_DT/2)
        fine_values[row['a']].append(fine['value']);fine_values[row['b']].append(1-fine['value'])
        max_delta=max(max_delta,abs(fine['value']-row['value']))
        classify=lambda v:1 if v>.5 else -1 if v<.5 else 0
        if classify(fine['value'])!=classify(row['value']):flips.append(dict(a=row['a'],b=row['b'],distance=row['distance'],policy=row['kiting_policy'],default_value=row['value'],fine_value=fine['value']))
    fine_scores={h:200*sum(v)/len(v) for h,v in fine_values.items()}
    numerical_audit=dict(default_dt=DEFAULT_DT,fine_dt=DEFAULT_DT/2,basic_scenarios=180,outcome_direction_flips=flips,max_scenario_value_delta=max_delta,max_index_delta=max(abs(scores[h]-fine_scores[h]) for h in ids),fine_indices=fine_scores)

    # Matched swap across identical allied core and opposing composition; distinct heroes per team.
    anchors=[['Kaelon','Mira','Nyra'],['Rook','Elyra','Traveler'],['Nerok','Timmy','Veyr']]
    teams=[]
    for core in anchors:
        enemy=next(c for c in anchors if c!=core)+['VoidKnight']
        for h in ids:
            if h in core:continue
            r=simulate(catalog,core+[h],enemy,duration=45)
            teams.append(dict(core=core,added=h,enemy=enemy,**r))
    sensitivity={}
    for h in ids:
        modes={}
        for mode in ('basic_engagement','supported_passives','limited_kit'):
            v=[r['value'] if r['a']==h else 1-r['value'] for r in results if h in (r['a'],r['b']) and r['mode']==mode]
            modes[mode]=200*sum(v)/len(v)
        sensitivity[h]=modes
    matchup={h:{other:200*sum(r['value'] if r['a']==h else 1-r['value'] for r in results if {r['a'],r['b']}=={h,other} and r['mode']=='basic_engagement')/4 for other in ids if other!=h} for h in ids}
    for core in anchors:
        subset=[r for r in teams if r['core']==core]
        mean=sum(r['value'] for r in subset)/len(subset)
        for r in subset:r['utility_above_core_candidate_mean']=r['value']-mean
    composition_baseline=[]
    if all_teams:
        references=[['Kaelon','Mira','Nyra','VoidKnight'],['Rook','Elyra','Traveler','Timmy'],['Nerok','Veyr','Nyra','Mira']]
        for composition in combinations(ids,4):
            matches=[dict(enemy=enemy,**simulate(catalog,list(composition),enemy,duration=30,skills=False,passives=False)) for enemy in references]
            composition_baseline.append(dict(team=list(composition),mean_value=sum(m['value'] for m in matches)/len(matches),matches=matches))
    return dict(numerical_audit=numerical_audit,composition_baseline=composition_baseline,catalog=catalog,basic_engagement_index_reference_mean_100=scores,mode_sensitivity=sensitivity,matchup_indices=matchup,duels=results,team_swaps=teams)

def main():
    parser=argparse.ArgumentParser();parser.add_argument('--all-teams',action='store_true',help='Evaluate all 210 unique teams against three fixed reference teams, basics only');parser.add_argument('--output',type=Path,default=Path(__file__).parent/'output');args=parser.parse_args()
    catalog=load_catalog();result=run(catalog,args.all_teams)
    result['source_sha256']={str(p.relative_to(ROOT)):hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(SCRIPTS.rglob('*.cs'))}
    args.output.mkdir(parents=True,exist_ok=True)
    (args.output/'results.json').write_text(json.dumps(result,indent=2)+'\n')
    (args.output/'results.json.gz').write_bytes(gzip.compress(json.dumps(result,separators=(',',':')).encode(),mtime=0))
    lines=['# Offline engagement baseline','', '**Model scores, not live win rates or a balance verdict.**', '',f"{len(result['duels'])} duel scenarios and {len(result['team_swaps'])} controlled team substitutions; {len(result['composition_baseline'])} complete team compositions against three reference opponents.", '', '| Hero | Basic engagement index (mean 100) | HP | Base shield | Basic DPS |','|---|---:|---:|---:|---:|']
    for h,p in catalog.items():lines.append(f"| {p['name']} | {result['basic_engagement_index_reference_mean_100'][h]:.1f} | {p['hp']:.1f} | {p['shield']:.0f} | {p['damage']/p['interval']:.2f} |")
    if result['composition_baseline']:
        ordered=sorted(result['composition_baseline'],key=lambda r:r['mean_value'],reverse=True)
        lines += ['', '## Composition baseline sample', '', 'Basic attacks only, against the same three reference teams. These extremes measure basic-attack composition strength in this lane model; support/AOE kits are excluded.', '', '| Composition | Mean scenario value |', '|---|---:|']
        for row in ordered[:3]+ordered[-3:]:lines.append('| '+', '.join(catalog[h]['name'] for h in row['team'])+f" | {row['mean_value']:.3f} |")
    audit=result['numerical_audit']
    lines += ['', '## Numerical timestep audit', '', f"All {audit['basic_scenarios']} basic scenarios repeated at dt={audit['fine_dt']} versus default {audit['default_dt']}: {len(audit['outcome_direction_flips'])} outcome-direction flips; maximum index change {audit['max_index_delta']:.2f} points; maximum individual scenario-value difference {audit['max_scenario_value_delta']:.3f}. Team scenarios were not repeated at the finer step. Values are coarse scenario indices, not a gameplay ranking."]
    lines += ['', '## Sensitivity to incomplete ability coverage', '', '| Hero | No passives | Supported passives | Experimental limited kit |', '|---|---:|---:|---:|']
    for h,m in result['mode_sensitivity'].items():lines.append(f"| {catalog[h]['name']} | {m['basic_engagement']:.1f} | {m['supported_passives']:.1f} | {m['limited_kit']:.1f} |")
    lines += ['', 'These are model-variant sensitivity values, not statistical confidence intervals. Role utility stays separate: effective healing received, ally healing generated, shield absorbed, damage and range uptime appear per scenario, not as arbitrary designer weights.', '', 'The primary index uses ONLY basic attacks, with no passives or abilities; Brakk intrinsic 5% damage modifier remains part of basic damage. Contact/ranged starts and hold/retreat policies are weighted equally. It is a basic-engagement comparison, NOT an overall hero score, and MUST NOT be used to recommend buffs/nerfs. Review individual scenarios and covered abilities first.', '', 'Team swap results are in results.json; each core is held fixed. The --all-teams option covers 210 compositions against three disclosed reference opponents; this is not an all-versus-all tournament. Cross-team same heroes are allowed; team-internal duplicates are excluded.', '', '## Coverage and limits', '', 'Covered: source-extracted HP, base shields, attack range/damage/interval/windup/speed; 0.88 attack movement multiplier (Timmy exempt); basic targeting lowest HP; expiring nonadditive shields capped at 40% HP; base-shield recharge after 15 seconds; Auren 2 HP/s regeneration and Brakk conditional 12% HP lifesteal; W shield/heal for Auren/Brakk/Tahla; uncomplicated Q damage for Zhaev/Sivra/Tahla/Traveler; mobility distance/cooldown and entry shields for five heroes.', '', 'Unsupported: other damaging skills and all ultimates, full passive kits (including Void Resonance trigger on SKILL hit, Timmy movement passive, Sivra dash speed), roots/slows, acceleration/deceleration, turning/facing, cover, arena walls, projectile visuals, bot policies, collision, animation cancellation, dash duration (modeled instantaneous), ranged facing/cone-angle changes during windup, objective damage buffs, networking. One-dimensional open lane; no simultaneous cast locks; fixed policy, not optimized human skill. This creates substantial asymmetric kit bias, particularly against Void Knight/Timmy and area heroes. Support contribution includes healing, but only part of support kit.', '', 'Time integration uses dt=0.00625 seconds. Near threshold outcomes can depend on discrete attack/movement ordering; this is numerical uncertainty, not a confidence interval. Auren/Sivra and Ione/Tahla kite regressions are checked at dt=0.00625 and 0.003125, but all matchups have not been proven timestep-converged. Treat marginal outcomes cautiously.', '', 'No gameplay changes. Source hashes in JSON allow checking exactly which snapshot was modeled. Next phase: implement remaining skills/status timelines against Unity regression fixtures before interpreting aggregate balance indices.']
    (args.output/'report.md').write_text('\n'.join(lines)+'\n'); print(json.dumps(dict(duels=len(result['duels']),team_swaps=len(result['team_swaps']),scores=result['basic_engagement_index_reference_mean_100']),indent=2))
if __name__=='__main__':main()
