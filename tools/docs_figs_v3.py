import json, math
import matplotlib
matplotlib.use('Agg')
import matplotlib.pyplot as plt
from matplotlib import font_manager as fm
from matplotlib.patches import FancyBboxPatch, FancyArrowPatch
from PIL import Image, ImageFilter, ImageOps, ImageDraw, ImageChops

SP=__import__('os').environ.get('KUCA_FONT_DIR','.')   # nanum.ttf 가 있는 폴더
import os; R=os.path.dirname(os.path.dirname(os.path.abspath(__file__))); OUT=R+'/docs/img/v3/'
fm.fontManager.addfont(SP+'/nanum.ttf')
fam=fm.FontProperties(fname=SP+'/nanum.ttf').get_name()
plt.rcParams.update({'font.family':fam,'font.size':11,'axes.edgecolor':'#c9c8c0','axes.labelcolor':'#52514e',
  'xtick.color':'#52514e','ytick.color':'#52514e','figure.facecolor':'#fcfcfb','axes.facecolor':'#fcfcfb','savefig.facecolor':'#fcfcfb'})
INK='#0b0b0b'; INK2='#52514e'; GRID='#e1e0d9'; SURF='#fcfcfb'
SEQ={'C':'#9ec5f4','B':'#5598e7','A':'#256abf','S':'#0d366b'}
BLUE='#2a78d6'; ORANGE='#eb6834'; AQUA='#1baf7a'; GRAY='#a9a8a0'

def box(ax,x,y,w,h,text,fc='#ffffff',ec='#c9c8c0',tc=INK,fs=11,bold=False):
    ax.add_patch(FancyBboxPatch((x-w/2,y-h/2),w,h,boxstyle='round,pad=0.02,rounding_size=0.06',fc=fc,ec=ec,lw=1.2))
    ax.text(x,y,text,ha='center',va='center',color=tc,fontsize=fs,fontweight='bold' if bold else 'normal',linespacing=1.35)
def arrow(ax,a,b,c=INK2,rad=0.0,lw=1.4):
    ax.add_patch(FancyArrowPatch(a,b,arrowstyle='-|>',mutation_scale=13,color=c,lw=lw,connectionstyle=f'arc3,rad={rad}',shrinkA=4,shrinkB=4))
def blank(w,h):
    f,ax=plt.subplots(figsize=(w,h)); ax.set_xlim(0,10); ax.set_ylim(0,10*h/w); ax.axis('off'); return f,ax

# ---- 1. 방향: v1 고리 vs v2 고리
f,ax=blank(11,4.6); ax.set_xlim(0,10.2)
ax.text(2.4,4.35,'v1  잡는 게임',ha='center',color=INK2,fontsize=13,fontweight='bold')
ax.text(7.4,4.35,'v2  같이 걷고 키우는 게임',ha='center',color=INK,fontsize=13,fontweight='bold')
v1=[(2.4,3.5,'지도에 야생 경희몬 출현'),(2.4,2.5,'다가가서 AR 로 잡기'),(2.4,1.5,'도감 채우기'),(2.4,0.5,'혼자 반복')]
for x,y,t in v1: box(ax,x,y,3.1,0.62,t,fc='#f1f0ec',tc=INK2)
for i in range(3): arrow(ax,(2.4,v1[i][1]-0.31),(2.4,v1[i+1][1]+0.31))
ax.plot([4.55,4.55],[0.2,4.1],color=GRID,lw=1.2)
names=['걷기 (혼자 / 함께)','티켓','뽑기  C·B·A·S','파트너 진화\n아기 → 이름 → 빛나는','AI 매칭\n만남 스팟 · 스몰토크','AI 추천 코스']
cx,cy,rx,ry=7.35,2.0,2.05,1.55
angs=[90,30,-30,-90,-150,150]
for n,t in zip(angs,names):
    x=cx+rx*math.cos(math.radians(t if False else n)); y=cy+ry*math.sin(math.radians(n))
    box(ax,x,y,1.95 if n not in(90,-90) else 2.3,0.66,t,fc='#ffffff',ec=BLUE,fs=10)
for i in range(6):
    a0=angs[i]-(26 if angs[i] in (90,-90) else 14)
    a1=angs[i]-60+(26 if angs[i]-60 in (90,-90,-150+0) and angs[i]-60 in (90,-90) else 14)
    p0=(cx+rx*1.0*math.cos(math.radians(a0)),cy+ry*1.0*math.sin(math.radians(a0)))
    p1=(cx+rx*1.0*math.cos(math.radians(a1)),cy+ry*1.0*math.sin(math.radians(a1)))
    arrow(ax,p0,p1,c=BLUE,rad=-0.15)
ax.text(7.4,2.0,'걸을수록\n자라고\n사람과\n이어짐',ha='center',va='center',color=BLUE,fontsize=11,fontweight='bold')
f.savefig(OUT+'f1_direction.png',dpi=170,bbox_inches='tight'); plt.close(f)

# ---- 2. AI 역할 지도
f,ax=blank(11,5.6)
rows=[('산책 추천','취향 · 15/30/60분 · 위치\n시간대 · 계절 · 날씨\n스팟 소개글·태그 (A)','코스 + 장소마다 이유\n거리 · 시간 · 칼로리','C · 서버'),
      ('산책 매칭','가까움 · 같은 산책 시간\n취향 6문항 · 성별 조건','짝 + 만남 스팟 (A 10곳)\n스몰토크 3개 (A 예시 20개)','C · 서버'),
      ('안전한 공유','쪽지 · 댓글 · 사진\n인상착의 · 리뷰','공개 / 보류 / 거절\n(실패 시 금지어 규칙)','C · 서버'),
      ('공지 → 장소','운영자가 붙인 학교 공지 원문','관련 경희스팟 + 요약','C · 서버')]
for i,(name,inp,out,who) in enumerate(rows):
    y=4.25-i*1.12
    box(ax,1.9,y,3.4,0.95,inp,fs=9.5,tc=INK2)
    box(ax,5.0,y,2.0,0.75,'AI\n'+name,fc=BLUE,ec=BLUE,tc='white',fs=10.5,bold=True)
    box(ax,8.1,y,3.4,0.95,out,fs=9.5)
    arrow(ax,(3.6,y),(4.0,y)); arrow(ax,(6.0,y),(6.4,y))
ax.text(1.9,4.95,'입력',ha='center',color=INK2,fontsize=11,fontweight='bold'); ax.text(8.1,4.95,'결과',ha='center',color=INK2,fontsize=11,fontweight='bold')
f.savefig(OUT+'f2_ai_roles.png',dpi=170,bbox_inches='tight'); plt.close(f)

# ---- 3. 뽑기 확률 (100% 누적 가로 막대)
odds={'1장 뽑기':[('C',70),('B',25),('A',5)],'5장 뽑기':[('B',60),('A',30),('S',10)]}
f,ax=plt.subplots(figsize=(9,2.6))
for i,(k,v) in enumerate(odds.items()):
    left=0
    for g,p in v:
        ax.barh(i,p-0.6,left=left+0.3,color=SEQ[g],height=0.52)
        tc='white' if g in('A','S') else INK
        if p>=8: ax.text(left+p/2,i,f'{g}  {p}%',ha='center',va='center',color=tc,fontsize=11,fontweight='bold')
        else: ax.text(left+p/2,i+0.42,f'{g} {p}%',ha='center',va='bottom',color=INK,fontsize=10)
        left+=p
ax.set_yticks([0,1]); ax.set_yticklabels(list(odds)); ax.invert_yaxis(); ax.set_xlim(0,100); ax.set_xticks([0,25,50,75,100]); ax.set_xticklabels(['0','25','50','75','100%'])
for s in ('top','right','left'): ax.spines[s].set_visible(False)
ax.tick_params(left=False); ax.grid(axis='x',color=GRID,lw=0.8); ax.set_axisbelow(True)
ax.set_title('등급 확률 — 진할수록 높은 등급 (C → S)',loc='left',color=INK,fontsize=12,pad=10)
f.savefig(OUT+'f3_odds.png',dpi=170,bbox_inches='tight'); plt.close(f)

# ---- 4. 진화: 걸은 거리 → 단계 + 액세서리 열림
f,ax=plt.subplots(figsize=(9.5,3.6))
xs=[0,5,5,20,20,26]; ys=[0,0,1,1,2,2]
ax.plot(xs,ys,color=BLUE,lw=2)
for x,y in [(0,0),(5,1),(20,2)]: ax.plot(x,y,'o',ms=8,color=BLUE,mec=SURF,mew=2)
labels=[(0,0,'아기 올빼미\n크기 1 · 테두리 빛 약\n머리띠 · 목걸이'),(5,1,'중앙도서관 올빼미\n크기 1.15 · 빛 중간\n+ 선글라스'),(20,2,'빛나는 중앙도서관 올빼미\n크기 1.3 · 빛 강\n+ 귀걸이')]
for x,y,t in labels: ax.text(x+0.5,y+0.12,t,va='bottom',ha='left',color=INK,fontsize=9.5,linespacing=1.3)
ax.set_xlim(-0.5,27); ax.set_ylim(-0.3,3.0); ax.set_yticks([0,1,2]); ax.set_yticklabels(['0단계','1단계','2단계'])
ax.set_xticks([0,5,10,15,20,25]); ax.set_xlabel('파트너와 함께 걸은 거리 (km) · 100m = 진화 경험치 1 · 중복 뽑기 C+10 B+20 A+30 S+40')
for s in ('top','right'): ax.spines[s].set_visible(False)
ax.grid(axis='y',color=GRID,lw=0.8); ax.set_axisbelow(True)
f.savefig(OUT+'f4_evolution.png',dpi=170,bbox_inches='tight'); plt.close(f)

# ---- 5. 진화 모습 모식도 (크기 + 테두리 빛)
src=Image.open(R+'/Art/Concept/05_Characters/images/3_Yellow_Legendary/10_LibraryOwl_model.jpg').convert('RGB')
# 배경 = 가장자리에서 이어진 흰색 영역 (얼굴의 흰 부분은 남는다)
near=src.convert('L').point(lambda v:255 if v>=236 else 0)
bgm=near.copy(); W0,H0=bgm.size
for seed in [(0,0),(W0-1,0),(0,H0-1),(W0-1,H0-1)]:
    ImageDraw.floodfill(bgm,seed,128)
mask=bgm.point(lambda v:0 if v==128 else 255).filter(ImageFilter.MedianFilter(5))
# 세 방향 시트 중 첫 번째(정면) 덩어리만: 비어 있는 세로줄에서 자른다
cols=[any(mask.getpixel((x,y)) for y in range(0,H0,4)) for x in range(W0)]
x0=cols.index(True); x1=x0
while x1<W0 and cols[x1]: x1+=1
src=src.crop((x0,0,x1,H0)); mask=mask.crop((x0,0,x1,H0))
bb=mask.getbbox(); src=src.crop(bb); mask=mask.crop(bb)
k=420/max(src.size); src=src.resize((int(src.width*k),int(src.height*k))); mask=mask.resize(src.size)
canvas=Image.new('RGB',(1500,620),SURF); d=ImageDraw.Draw(canvas)
from PIL import ImageFont
ft=ImageFont.truetype(SP+'/nanum.ttf',30); fs=ImageFont.truetype(SP+'/nanum.ttf',24)
for i,(sc,glow,name,sub) in enumerate([(0.77,0,'아기 올빼미','0단계 · 크기 1'),(0.885,10,'중앙도서관 올빼미','1단계 · 크기 1.15'),(1.0,22,'빛나는 중앙도서관 올빼미','2단계 · 크기 1.3')]):
    sw,sh=int(src.width*sc),int(src.height*sc); im=src.resize((sw,sh)); m=mask.resize((sw,sh))
    cx=250+i*500; base=470
    if glow:
        pad=glow*3; big=Image.new('L',(sw+2*pad,sh+2*pad),0); big.paste(m,(pad,pad))
        g=big.filter(ImageFilter.MaxFilter(9)).filter(ImageFilter.GaussianBlur(glow))
        layer=Image.new('RGB',big.size,(255,214,120)); canvas.paste(layer,(cx-sw//2-pad,base-sh-pad),g.point(lambda v:min(255,int(v*1.5))))
    canvas.paste(im,(cx-sw//2,base-sh),m)
    tw=d.textlength(name,font=ft); d.text((cx-tw/2,base+20),name,fill=INK,font=ft)
    tw=d.textlength(sub,font=fs); d.text((cx-tw/2,base+62),sub,fill=INK2,font=fs)
canvas.save(OUT+'f5_stage_look.png')

# ---- 6. 캠퍼스 지도: 경희스팟 25 · 만남 스팟 10
B=json.load(open(R+'/server/KucaMemoServer/Data/buildings.json')); S=json.load(open(R+'/server/KucaMemoServer/Data/spots.json'))
pos={b['id']:(b['lon'],b['lat']) for b in B}
NAMES={'gate_magpie':'정문 까치','library_owl':'도서관 올빼미','pigeon':'비둘기','space_rabbit':'공학관 토끼','amphitheater_frog':'노천극장 개구리','clock_rooster':'선승관 수탉','language_parrot':'외대 앵무새','lion_cub':'아기 사자','multimedia_meerkat':'미어캣','art_chameleon':'카멜레온'}
lat0=sum(p[1] for p in pos.values())/len(pos); kx=math.cos(math.radians(lat0))
f,ax=plt.subplots(figsize=(9,9))
ax.scatter([p[0]*kx for p in pos.values()],[p[1] for p in pos.values()],s=6,color='#d6d5cd',lw=0)
sp=[(s,pos[s['id']]) for s in S if s['id'] in pos]
ax.scatter([p[0]*kx for s,p in sp if 'meetSpecies' not in s],[p[1] for s,p in sp if 'meetSpecies' not in s],s=46,color='#ffffff',edgecolor=BLUE,lw=1.6,zorder=3,label='경희스팟 (15)')
ms=[(s,p) for s,p in sp if 'meetSpecies' in s]
ax.scatter([p[0]*kx for s,p in ms],[p[1] for s,p in ms],s=120,color=BLUE,edgecolor=SURF,lw=2,zorder=4,label='만남 스팟 + 경희몬 (10)')
for s,p in ms:
    off={'선승관':(-62,12),'멀티미디어교육관':(8,-8)}.get(s['name'],(7,5))
    ax.annotate(f"{s['aliases'][0] if s['aliases'] else s['name']}\n{NAMES[s['meetSpecies']]}",(p[0]*kx,p[1]),xytext=off,textcoords='offset points',fontsize=8.5,color=INK,linespacing=1.15,zorder=5)
xs=[p[0]*kx for s,p in sp]; ys=[p[1] for s,p in sp]; pad=0.0016
ax.set_xlim(min(xs)-pad,max(xs)+pad*3); ax.set_ylim(min(ys)-pad,max(ys)+pad); ax.set_aspect('equal'); ax.set_xticks([]); ax.set_yticks([])
for s in ax.spines.values(): s.set_visible(False)
ax.legend(loc='lower left',frameon=False,fontsize=10)
missing=[s['name'] for s in S if s['id'] not in pos]
f.savefig(OUT+'f6_meet_map.png',dpi=170,bbox_inches='tight'); plt.close(f)
print('map missing',missing)

# ---- 7. 스팟 태그 표 (계절·시간대·실내)
cols=['봄','여름','가을','겨울','아침','낮','저녁','밤','실내']
keys=['spring','summer','autumn','winter','morning','day','evening','night']
f,ax=plt.subplots(figsize=(7.6,9.2))
for r,s in enumerate(S):
    vals=[k in s['seasons'] for k in keys[:4]]+[k in s['times'] for k in keys[4:]]+[s['indoor']]
    for c,v in enumerate(vals):
        ax.add_patch(plt.Rectangle((c+0.06,r+0.08),0.88,0.84,color=('#3987e5' if v else '#efeee9'),lw=0))
    nm=s['name'].replace('경희대학교 국제캠퍼스 ','')+(' *' if '확인 필요' in s['intro'] else '')
    ax.text(-0.2,r+0.5,nm,ha='right',va='center',fontsize=9.5,color=INK)
for c,n in enumerate(cols): ax.text(c+0.5,-0.35,n,ha='center',va='bottom',fontsize=10,color=INK2)
ax.plot([4,4],[0,len(S)],color=SURF,lw=4); ax.plot([8,8],[0,len(S)],color=SURF,lw=4)
ax.set_xlim(-6.2,9.2); ax.set_ylim(len(S)+0.2,-1.0); ax.axis('off')
ax.text(-6.1,len(S)+0.9,'파랑 = 추천에 어울림 · * = 소개글에 [확인 필요] 표시가 있는 곳',fontsize=9.5,color=INK2,va='top')
f.savefig(OUT+'f7_spot_tags.png',dpi=170,bbox_inches='tight'); plt.close(f)

# ---- 8. 파트너 사진 흐름
f,ax=blank(14,3.0)
steps=['산책 완료 화면\n"파트너와 사진 찍기"','WalkCompleteScreen\n.PartnerPhoto (B)','CatchCameraScreen\n.OpenPartnerPhoto (A)','파트너 + 진화 단계\n+ 액세서리 그대로','셔터','첫 사진 → 카드 사진\n앨범 저장 선택']
for i,t in enumerate(steps):
    x=0.85+i*1.66; box(ax,x,1.4,1.52,0.95,t,fs=8.6,ec=(BLUE if '(A)' in t or i in(3,5) else '#c9c8c0'))
    if i: arrow(ax,(x-1.66+0.75,1.4),(x-0.75,1.4))
ax.text(5.0,0.45,'잡기·점수 없음 · 사진 화면이 산책 완료 화면(52) 위에 뜨도록 캔버스 순서 25 → 55',ha='center',fontsize=9.5,color=INK2)
f.savefig(OUT+'f8_partner_photo.png',dpi=170,bbox_inches='tight'); plt.close(f)

# ---- 9. 코드 연결 지도 (A 가 바꾼 것 → B·C 가 쓰는 곳)
f,ax=blank(11,6.2)
A=[('CreatureLibrary','등급 · 확률 · 진화 거리 · 단계 이름'),('SpawnConditions','PickForGacha: 지금 어울리는 종'),('PartnerBuddy','SetStage: 크기 · 빛 · 이름'),('CatchCameraScreen','OpenPartnerPhoto'),('CreatureAccessories','UnlockStage: 부위 열림'),('MeetSpotCatalog / Figures','만남 스팟 10 · 지붕 위 경희몬 · 스몰토크 20'),('spots.json · smalltalk.json','소개글 · 태그 · meetSpecies')]
Bs=[('GachaRules (B)',[0,1]),('KhuMenus · EvolutionScreen (B)',[2,4]),('WalkCompleteScreen (B)',[3]),('MatchScreen · MockMatch (B)',[5]),('추천 v2 · 매칭 서버 (C)',[6])]
ya=[5.25-i*0.78 for i in range(len(A))]; ax.set_ylim(0,6.3); yb=[5.1-i*1.08 for i in range(len(Bs))]
for (n,t),y in zip(A,ya): box(ax,2.5,y,4.6,0.62,f'{n}\n{t}',fc='#ffffff',ec=BLUE,fs=8.6)
for (n,links),y in zip(Bs,yb):
    c=AQUA if '(C)' in n else ORANGE
    box(ax,8.4,y,3.0,0.6,n,fc='#ffffff',ec=c,fs=9.2)
    for k in links: arrow(ax,(4.8,ya[k]),(6.9,y),c=c,lw=1.1)
ax.text(2.5,6.05,'A 가 바꾼 파일',ha='center',fontsize=11,fontweight='bold',color=BLUE)
ax.text(8.4,6.05,'쓰는 쪽',ha='center',fontsize=11,fontweight='bold',color=INK2)
f.savefig(OUT+'f9_code_map.png',dpi=170,bbox_inches='tight'); plt.close(f)

# ---- 10. 뽑기 가중치 예시
f,ax=plt.subplots(figsize=(9,1.7))
ex=[('같은 S 등급 안: 도서관 올빼미',78),('정문 까치',22)]
ax.barh(0,77.4,left=0.3,color=BLUE,height=0.5); ax.barh(0,21.4,left=78.3,color=GRAY,height=0.5)
ax.text(39,0,'도서관 올빼미 78%  (밤 ×3 → 가중치 √3 = 약 1.73)',ha='center',va='center',color='white',fontsize=10.5,fontweight='bold')
ax.text(89,0,'정문 까치 22%\n(밤 ×0.2 → 하한 0.5)',ha='center',va='center',color=INK,fontsize=9)
ax.set_xlim(0,100); ax.set_yticks([]); ax.set_xticks([])
for s in ax.spines.values(): s.set_visible(False)
ax.set_title('가을 · 맑은 · 밤에 두 종 중 하나를 뽑을 때 (테스트와 같은 예)',loc='left',fontsize=11.5,color=INK)
f.savefig(OUT+'f10_weight.png',dpi=170,bbox_inches='tight'); plt.close(f)
print('done')
