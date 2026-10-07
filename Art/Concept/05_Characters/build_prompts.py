"""
캐릭터 기획서(캐릭터_기획_및_제미나이_프롬프트.md)의 블록들로 제미나이에 붙여 넣을 프롬프트 파일을 다시 만든다.
기획서의 블록을 고친 뒤 실행:  python3 build_prompts.py

만드는 파일
  제미나이_프롬프트_통합본.txt   한 번에 붙여 넣는 하나짜리 (24종 x 카드·3D 시트 = 48장, 멈추지 말고 연속 생성)
  제미나이_프롬프트_단체시트.txt  24종을 한 장에 모은 라인업 (빠른 전체 확인용)
  제미나이_프롬프트_전체.md       4단계로 나눈 48개 (한 장씩 확실하게 만들 때)
"""
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
doc = open(os.path.join(HERE, '캐릭터_기획_및_제미나이_프롬프트.md'), encoding='utf-8').read()
blocks = re.findall(r'```\n(.*?)```', doc, re.S)


def get(prefix):
    return [b for b in blocks if b.startswith(prefix)][0].strip()


style = get('STYLE').replace('STYLE (keep identical for every character):', '').strip()
rar = {k: get('RARITY = ' + k) for k in ('COMMON', 'RARE', 'LEGENDARY')}
out_a = get('OUTPUT = COLLECTION')
out_b = get('OUTPUT = 3D')
sec6 = doc[doc.index('## 6.'):doc.index('## 8.')]
chars = re.findall(r'\*\*(.+?)\*\* — (.+?)\n```\n(CHARACTER = .*?)```', sec6, re.S)
assert len(chars) == 24, len(chars)
first = [c for c in chars if c[0].startswith('공학관 토끼')][0]
order = [first] + [c for c in chars if c is not first]

ICON = {'공학관': '[캠퍼스 내 건물] - [공학관]_아이콘_기본', '중앙도서관': '[캠퍼스 내 건물] - [중앙도서관(대학본부)]_아이콘_기본',
        '사색의 광장': '[캠퍼스 내 조경] - [사색의 광장(오벨리스크 2기)]_아이콘_기본',
        '정문': '[캠퍼스 내 조경] - [정문(새천년기념탑:네오르네상스문)]_아이콘_기본', '체육대학관': '[캠퍼스 내 건물] - [체육대학관]_아이콘_기본',
        '선승관': '[캠퍼스 내 건물] - [선승관(시계탑)]_아이콘_기본', '예술·디자인대학': '[캠퍼스 내 건물] - [예술·디자인대학관]_아이콘_기본',
        '도예관': '[캠퍼스 내 건물] - [도예관]_아이콘_기본', '천문대': '[캠퍼스 내 조경] - [천문대]_아이콘_밤_맑음',
        '평화노천극장': '[캠퍼스 내 조경] - [평화노천극장(연못·무대배경)]_아이콘_기본', '대운동장': '[캠퍼스 내 조경] - [대운동장(벚꽃길)]_아이콘_기본',
        '외국어대학관': '[캠퍼스 내 건물] - [외국어대학관]_아이콘_기본', '우정원': '[캠퍼스 내 건물] - [우정원]_아이콘_기본',
        '전자정보대학관': '[캠퍼스 내 건물] - [전자정보·응용과학대학관]_아이콘_기본', '국제·경영대학관': '[캠퍼스 내 건물] - [국제·경영대학관]_아이콘_기본',
        '멀티미디어·글로벌관': '[캠퍼스 내 건물] - [멀티미디어교육관·글로벌관]_아이콘_기본'}


def icon_of(name):
    for k, v in ICON.items():
        if name.startswith(k):
            return v
    return None


def rarity(c):
    return re.search(r'RARITY = (\w+)', c).group(1)


def no_pose(c):
    return '\n'.join(l for l in c.strip().split('\n') if not l.startswith(('POSE:', 'LANDMARK:')))


# ---------- 1) 통합본: 한 번에 붙여 넣기, 48장을 멈추지 않고 연속 생성 ----------
parts = ['''You are generating a full set of collectible character art for a cheerful campus map game (Kyung Hee University Global Campus).
There are 24 characters. For EACH character make TWO images: (A) a collection card and (B) a 3D model reference sheet. 48 images total.

BATCH MODE - THIS IS ONE SINGLE JOB FOR ALL 24 CHARACTERS
- Treat all 24 characters as one batch and produce ALL 48 images in this response, back to back.
- Do NOT stop after one or two images. Do NOT ask me for confirmation, do NOT ask which one to do next, do NOT summarize between images.
- Keep generating continuously until image #24B is finished. Only stop earlier if you hit a hard output limit;
  in that case end with exactly one line "NEXT: #<number><A or B>", and when I reply "continue" resume from that image and again keep going to #24B without pausing.

HOW TO WORK
1. One image per step in this order: #1A, #1B, #2A, #2B, ... #24A, #24B.
2. Character #1 (Space Engineering Rabbit) defines the art style. Every other character must match #1's rendering style,
   proportion, face style and diorama base design exactly (different animal, same art style).
3. Each (B) sheet shows exactly the same character as its (A) card: same face, colors, outfit, accessories and base.
4. Above each image write only its number and name, e.g. "#1A Space Engineering Rabbit - card". Nothing written inside the images.
''', 'SHARED STYLE (applies to every image)\n' + style,
         'RARITY RULES (each character lists its rarity)\n' + '\n\n'.join(rar.values()),
         'IMAGE TYPE (A)\n' + out_a.replace('OUTPUT = COLLECTION CARD:', 'COLLECTION CARD:'),
         'IMAGE TYPE (B)\n' + out_b.replace('OUTPUT = 3D MODEL REFERENCE SHEET:', '3D MODEL REFERENCE SHEET:')
         + '\nFor (B), ignore the POSE and LANDMARK lines; use a neutral A-pose and a closed-mouth calm expression.',
         'CHARACTER LIST']
for i, (_, _, c) in enumerate(order, 1):
    parts.append(f'#{i}\n' + c.strip())
parts.append('Start now with #1A and continue through all 48 images to #24B in this same response without stopping.')
open(os.path.join(HERE, '제미나이_프롬프트_통합본.txt'), 'w', encoding='utf-8').write('\n\n'.join(parts) + '\n')

# ---------- 2) 단체 시트: 24종을 한 장에 ----------
lineup = ['''Create ONE single image: a character lineup poster showing ALL 24 characters listed below together at once,
arranged in a neat grid of 6 columns x 4 rows on a clean light cream background, every character at the same size,
each standing on its own small round diorama base, all in one consistent art style.
Row 1 = the gold (legendary) landmark characters #1-#6, row 2 = gold #7-#12, row 3 = gold #13-#16 plus blue #17-#18,
row 4 = blue #19-#20 and green #21-#24. Each character in a cheerful idle pose holding its tool or accessory.
Horizontal 16:9. No text, labels, numbers or logos.''', 'SHARED STYLE\n' + style,
          'RARITY RULES\n' + '\n\n'.join(rar.values()), 'CHARACTERS']
gold = [c for c in order if rarity(c[2]) == 'LEGENDARY']
blue = [c for c in order if rarity(c[2]) == 'RARE']
green = [c for c in order if rarity(c[2]) == 'COMMON']
for i, (_, _, c) in enumerate(gold + blue + green, 1):
    lineup.append(f'#{i}\n' + no_pose(c))
open(os.path.join(HERE, '제미나이_프롬프트_단체시트.txt'), 'w', encoding='utf-8').write('\n\n'.join(lineup) + '\n')

# ---------- 3) 4단계 48개 ----------
REF = ('Match the exact rendering style, proportion, face style and diorama base design of the attached character image '
       '(a different animal, same art style).\n\n')
SAME = 'This is the SAME character as the attached image: keep its face, colors, outfit, accessories and base exactly the same.\n\n'


def card(c, ref):
    return (REF if ref else '') + 'STYLE:\n' + style + '\n\n' + rar[rarity(c)] + '\n\n' + out_a + '\n\n' + c.strip()


def sheet(c):
    return SAME + 'STYLE:\n' + style + '\n\n' + rar[rarity(c)] + '\n\n' + out_b + '\n\n' + no_pose(c)


md = ['# 제미나이 프롬프트 전체 (복사용, 4단계)', '',
      '`build_prompts.py` 가 기획서에서 자동으로 만든 파일입니다. 각 회색 상자 하나가 프롬프트 하나입니다.', '',
      '| 단계 | 하는 일 | 개수 | 첨부 |', '| --- | --- | --- | --- |',
      '| 1 | 그림체 확정: 공학관 토끼 도감 카드 | 1 | 공학관 아이콘 |',
      '| 2 | 공학관 토끼 3D 레퍼런스 | 1 | 1단계 토끼 카드 |',
      '| 3 | 나머지 23종 도감 카드 | 23 | 1단계 토끼 카드 (+ 노랑은 랜드마크 아이콘) |',
      '| 4 | 나머지 23종 3D 레퍼런스 | 23 | 3단계의 그 캐릭터 카드 |', '']


def emit(title, attach, prompt):
    md.extend([f'### {title}', f'첨부: {attach}', '', '```', prompt, '```', ''])


n = 1
nm, bl, c = order[0]
md.append('## 1단계 · 그림체 확정')
emit(f'{n:02d}. {nm} — 도감 카드', f'`{icon_of(nm)}.png`', card(c, False)); n += 1
md.append('## 2단계 · 공학관 토끼 3D 레퍼런스')
emit(f'{n:02d}. {nm} — 3D 레퍼런스', '1단계 토끼 카드', sheet(c)); n += 1
md.append('## 3단계 · 나머지 23종 도감 카드')
for nm, bl, c in order[1:]:
    ic = icon_of(nm) if rarity(c) == 'LEGENDARY' else None
    emit(f'{n:02d}. {nm} — 도감 카드 · {bl}', '1단계 토끼 카드' + (f' + `{ic}.png`' if ic else ''), card(c, True)); n += 1
md.append('## 4단계 · 나머지 23종 3D 레퍼런스')
for nm, bl, c in order[1:]:
    emit(f'{n:02d}. {nm} — 3D 레퍼런스', f'3단계 {nm} 카드', sheet(c)); n += 1
open(os.path.join(HERE, '제미나이_프롬프트_전체.md'), 'w', encoding='utf-8').write('\n'.join(md))
print('ok', n - 1, 'prompts')
