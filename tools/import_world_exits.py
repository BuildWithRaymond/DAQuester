"""Normalize read-only Excalibur WorldLogs world-map exits into directed route edges."""
import argparse,json
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('world_logs',type=Path);args=p.parse_args()
root=Path(__file__).resolve().parents[1]
target=root/'content/routes.json';records=json.loads(target.read_text(encoding='utf-8-sig'))
added=0
for record in records:
    path=args.world_logs/f"{record['id']}.json"
    if not path.exists():continue
    source=json.loads(path.read_text(encoding='utf-8-sig'))
    for point,destinations in source.get('WorldMaps',{}).items():
        x,y=map(int,point.split(','))
        for destination,landing in destinations.items():
            destination=int(destination)
            if destination==record['id']:continue
            tx,ty=map(int,landing.split(','))
            if any(e['map']==destination and e['from']=={'X':x,'Y':y} for e in record['portals']):continue
            record['portals'].append({'from':{'X':x,'Y':y},'map':destination,'to':{'X':tx,'Y':ty},'worldMap':True});added+=1
target.write_text(json.dumps(records,indent=2),encoding='utf-8')
print(f'Imported {added} world-map edges; source unchanged.')
