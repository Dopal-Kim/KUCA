"""
제미나이 이미지 생성 (Art/Concept/06_A라운드_제미나이_프롬프트.md 의 프롬프트용).
  python tools/keyart/gemini_image.py <out.png> <prompt.txt> [레퍼런스 이미지 ...]
키: 환경 변수 GEMINI_API_KEY (Google AI Studio). 저장소에 넣지 않는다.
"""
import base64, json, os, sys, urllib.request, mimetypes, pathlib
MODEL = 'gemini-3-pro-image'
out, ptxt, refs = sys.argv[1], pathlib.Path(sys.argv[2]).read_text(), sys.argv[3:]
parts = []
for r in refs:
    mt = mimetypes.guess_type(r)[0] or 'image/png'
    parts.append({'inline_data': {'mime_type': mt, 'data': base64.b64encode(open(r, 'rb').read()).decode()}})
parts.append({'text': ptxt})
body = {'contents': [{'parts': parts}], 'generationConfig': {'responseModalities': ['IMAGE', 'TEXT']}}
req = urllib.request.Request(f'https://generativelanguage.googleapis.com/v1beta/models/{MODEL}:generateContent',
                             data=json.dumps(body).encode(), headers={'Content-Type': 'application/json',
                                      **({'x-goog-api-key': os.environ['GEMINI_API_KEY']} if os.environ.get('GEMINI_API_KEY') else {})})
d = json.load(urllib.request.urlopen(req, timeout=300))
for c in d.get('candidates', []):
    for p in c.get('content', {}).get('parts', []):
        if 'inlineData' in p or 'inline_data' in p:
            data = (p.get('inlineData') or p.get('inline_data'))['data']
            open(out, 'wb').write(base64.b64decode(data)); print('saved', out); sys.exit()
        if 'text' in p: print('text:', p['text'][:300])
print('no image', json.dumps(d)[:500])
