"""
캠퍼스 보행 길 그래프 만들기 → My project/Assets/Resources/KUCA/WalkGraph.json

    python3 tools/route/build_walk_graph.py

지도 바닥을 만든 OSM 데이터(tools/keyart/osm_ground.json)에서 사람이 걸을 수 있는 길만 골라
교차점 · 꺾이는 점을 노드로, 이어진 구간을 간선으로 저장한다. 앱의 WalkGraph 가 읽어서 길 찾기를 한다.
  lon[i] · lat[i]: 노드 i 의 경도 · 위도,  a[k] · b[k]: 간선 k 가 잇는 두 노드 번호 (양방향)
  (Unity JsonUtility 가 그대로 읽게 평평한 배열로)
가장 큰 연결 덩어리만 남긴다 (따로 떨어진 짧은 길로 길 찾기가 끊기지 않게).
"""
import json
import os
from collections import defaultdict

HERE = os.path.dirname(os.path.abspath(__file__))
SRC = os.path.join(HERE, '..', 'keyart', 'osm_ground.json')
OUT = os.path.join(HERE, '..', '..', 'My project', 'Assets', 'Resources', 'KUCA', 'WalkGraph.json')

WALKABLE = {'footway', 'path', 'pedestrian', 'steps', 'service', 'residential', 'living_street', 'tertiary',
            'secondary', 'track', 'unclassified', 'cycleway', 'corridor', 'primary'}

data = json.load(open(SRC, encoding='utf-8'))
coord = {}
adj = defaultdict(set)
for e in data['elements']:
    tags = e.get('tags', {})
    if e.get('type') != 'way' or tags.get('highway') not in WALKABLE or tags.get('foot') == 'no' or tags.get('access') == 'no':
        continue
    nodes, geom = e.get('nodes'), e.get('geometry')
    if not nodes or not geom or len(nodes) != len(geom):
        continue
    for nid, g in zip(nodes, geom):
        coord[nid] = (round(g['lon'], 7), round(g['lat'], 7))
    for a, b in zip(nodes, nodes[1:]):
        if a != b:
            adj[a].add(b)
            adj[b].add(a)

# 가장 큰 연결 덩어리
seen, best = set(), []
for start in adj:
    if start in seen:
        continue
    comp, stack = [], [start]
    seen.add(start)
    while stack:
        n = stack.pop()
        comp.append(n)
        for m in adj[n]:
            if m not in seen:
                seen.add(m)
                stack.append(m)
    if len(comp) > len(best):
        best = comp
keep = set(best)
index = {nid: i for i, nid in enumerate(sorted(keep))}
nodes = [list(coord[nid]) for nid in sorted(keep)]
edges = sorted({tuple(sorted((index[a], index[b]))) for a in keep for b in adj[a] if b in keep})
os.makedirs(os.path.dirname(OUT), exist_ok=True)
json.dump({'lon': [n[0] for n in nodes], 'lat': [n[1] for n in nodes],
           'a': [e[0] for e in edges], 'b': [e[1] for e in edges]}, open(OUT, 'w'), separators=(',', ':'))
print(f'nodes {len(nodes)} (of {len(adj)}), edges {len(edges)} -> {os.path.normpath(OUT)} ({os.path.getsize(OUT)//1024} KB)')
