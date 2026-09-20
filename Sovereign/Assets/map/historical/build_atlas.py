"""Build a compact, attributed 1815 REFERENCE atlas and GPU index texture.

Coordinates and rings are preserved apart from longitude unwrapping and 6-decimal
rounding. This deliberately does not claim to reconstruct 1836 borders.
"""
import colorsys
import hashlib
import json
import math
from pathlib import Path
from PIL import Image, ImageDraw

ROOT = Path(__file__).resolve().parent
OUT = ROOT
source = json.loads((OUT / 'world_1815.geojson').read_text(encoding='utf-8'))

translations = dict(line.split('|', 1) for line in '''Afghanistan|阿富汗
Algiers|阿尔及尔
Angola|安哥拉
Anguilla|安圭拉
Anhalt|安哈尔特
Annam|安南
Antarctica|南极洲
Antigua and Barbuda|安提瓜和巴布达
Arakan|若开
Asante|阿散蒂
Assam|阿萨姆
Australian aboriginal hunter-gatherers|澳大利亚原住民族地区
Austrian Empire|奥地利帝国
Baden|巴登
Barbados|巴巴多斯
Bavaria|巴伐利亚
Bhutan|不丹
Bremen|不来梅
British East India Company|英国东印度公司辖区
Brunei|文莱
Brunswick|不伦瑞克
Buganda|布干达
Bunyoro|布尼奥罗
Burma|缅甸
Burundi|布隆迪
Cambodia|柬埔寨
Canada|加拿大地区
Cape Colony|开普殖民地
Ceylon|锡兰
Cochin China|交趾支那
Congo|刚果地区
Cuxhaven|库克斯港
Cyrenaica|昔兰尼加
Dangbon|达贡巴
Delagoa Bay|德拉戈阿湾
Denmark|丹麦
Dominica|多米尼克
Dutch East Indies|荷属东印度
Egypt|埃及
Electoral Hesse|黑森选侯国
Ethiopia|埃塞俄比亚
Fante|芳蒂
Fivizzano|菲维扎诺
France|法国
Fulani Empire|富拉尼帝国
Goa|果阿
Grand Duchy of Hesse|黑森大公国
Grenada|格林纳达
Guadeloupe|瓜德罗普
Guanches|关切人地区
Guiana|圭亚那地区
Haiti|海地
Hamburg|汉堡
Hanover|汉诺威
Hohenzollern|霍亨索伦
Holstein|荷尔斯泰因
Hong Kong|香港地区
Imbangala|伊姆班加拉
Japan|日本
Kaarta|卡尔塔
Kanem-Bornu|卡涅姆—博尔努
Kazembe|卡泽姆贝
Kingdom of Hawaii|夏威夷王国
Kingdom of Sardinia|撒丁王国
Kingdom of the Two Sicilies|两西西里王国
Korea|朝鲜
Kuril Islands|千岛群岛
Lippe-Detmold|利珀—代特莫尔德
Lombardy|伦巴第
Lozi|洛齐
Lucca|卢卡
Lunda|隆达
Luxembourg|卢森堡
Lübeck|吕贝克
Malaya|马来亚地区
Manchu Empire|大清
Maori|毛利人地区
Maratha Confederacy|马拉塔邦联
Martinique|马提尼克
Massa|马萨
Maya|玛雅人地区
Mecklenburg-Schwerin|梅克伦堡—什未林
Mecklenburg-Strelitz|梅克伦堡—施特雷利茨
Merina Kingdom|梅里纳王国
Modena|摩德纳
Montserrat|蒙特塞拉特
Morocco|摩洛哥
Mossi States|莫西诸邦
Mysore (Indian princely state)|迈索尔
Nassau|拿骚
Nejd|内志
Nepal|尼泊尔
Netherlands Antilles|荷属安的列斯
New South Wales|新南威尔士
Nkore|恩科雷
Oldenburg|奥尔登堡
Oman|阿曼
Ottoman Empire|奥斯曼帝国
Oudh|奥德
Oyo|奥约
Palatinate|普法尔茨
Pampas cultures|潘帕斯原住民族地区
Papal States|教皇国
Papuans|巴布亚原住民族地区
Paraguay|巴拉圭
Parma|帕尔马
Patagonian shellfish and marine mammal hunters|巴塔哥尼亚沿岸原住民族地区
Persia|波斯
Philippines|菲律宾地区
Polynesians|波利尼西亚原住民族地区
Pontremoli|蓬特雷莫利
Portugal|葡萄牙
Portuguese East Africa|葡属东非
Portuguese Guinea|葡属几内亚
Prussia|普鲁士
Rattanakosin Kingdom|暹罗拉达那哥欣王国
Republic of Kraków|克拉科夫共和国
Russian Empire|俄罗斯帝国
Rwanda|卢旺达
Saint Barthelemy|圣巴泰勒米
Saint Kitts and Nevis|圣基茨和尼维斯
Saint Lucia|圣卢西亚
Saint Martin|圣马丁
Saint Vincent and the Grenadines|圣文森特和格林纳丁斯
San Marino|圣马力诺
Saxony|萨克森
Schaumburg-Lippe|绍姆堡—利珀
Schleswig|石勒苏益格
Senegal|塞内加尔地区
Shuar|舒阿尔人地区
Sierra Leone|塞拉利昂
Sikkim (Indian princely state)|锡金
Somalia|索马里地区
Sotho|索托人地区
Spain|西班牙
Sweden–Norway|瑞典—挪威
Switzerland|瑞士
Thuringia|图林根
Travancore|特拉凡哥尔
Trinidad|特立尼达
Tripolitania|的黎波里塔尼亚
Tunis|突尼斯
Turan|图兰地区
Tuscany|托斯卡纳
Tuʻi Tonga Empire|图伊汤加
United Kingdom|英国
United Kingdom of Great Britain and Ireland|英国
United Kingdom of Netherlands|尼德兰联合王国
United Provinces of the Río de la Plata|拉普拉塔联合省
United States|美国
Venetia|威尼托
Viceroyalty of Brazil|巴西总督区
Viceroyalty of New Granada|新格拉纳达总督区
Viceroyalty of New Spain|新西班牙总督区
Viceroyalty of Peru|秘鲁总督区
Waldeck|瓦尔德克
Wetzlar|韦茨拉尔
Württemberg|符腾堡
Xhosa|科萨人地区
Yemen|也门
Zanzibar|桑给巴尔
Zulu|祖鲁人地区
central Asian khanates|中亚诸汗国'''.splitlines())
scenario = {'United Kingdom':'GBR','United Kingdom of Great Britain and Ireland':'GBR','Prussia':'PRU','Japan':'JAP','France':'FRA','Austrian Empire':'AUS','Russian Empire':'RUS','United States':'USA','Manchu Empire':'QNG','Hong Kong':'QNG','Ottoman Empire':'OTT','Spain':'SPA','Portugal':'POR'}
colors = {'QNG':'cfb764','GBR':'b27978','FRA':'849eaa','PRU':'758691','RUS':'91a078','AUS':'c4b8a3','JAP':'cbaea8','USA':'93a8b8','OTT':'a0a67b','SPA':'c3ad7b','POR':'82a58a'}

def source_name(props):
    for key in ('NAME','SUBJECTO','ABBREVN'):
        value = (props.get(key) or '').strip()
        if value: return value
    return ''

def ring_inside(ring,x,y):
    inside=False
    for a,b in zip(ring,ring[-1:]+ring[:-1]):
        if (a[1]>y)!=(b[1]>y) and x < (b[0]-a[0])*(y-a[1])/(b[1]-a[1])+a[0]: inside=not inside
    return inside

def inside(poly,x,y):
    return ring_inside(poly['Rings'][0],x,y) and not any(ring_inside(r,x,y) for r in poly['Rings'][1:])

def area(ring):
    return abs(sum(a[0]*b[1]-b[0]*a[1] for a,b in zip(ring,ring[-1:]+ring[:-1])))/2

def clean_polygon(rings):
    cleaned=[]
    for ring in rings:
        result=[]
        for lon,lat,*_ in ring:
            if result:
                while lon-result[-1][0]>180: lon-=360
                while lon-result[-1][0]<-180: lon+=360
            point=[round(lon,6),round(lat,6)]
            if not result or point!=result[-1]: result.append(point)
        if result and result[0]==result[-1]:result.pop()
        if len(result)>=3: cleaned.append(result)
    if not cleaned:return []
    points=cleaned[0]
    low=min(p[0] for p in points); high=max(p[0] for p in points)
    result=[]
    for shift in (-360,0,360):
        if high+shift < -180 or low+shift>180: continue
        moved=[[[p[0]+shift,p[1]] for p in ring] for ring in cleaned]
        result.append({'Rings':moved,'Bounds':[max(-180,low+shift),min(p[1] for p in points),min(180,high+shift),max(p[1] for p in points)]})
    return result

grouped={}
unnamed=0
for feature in source['features']:
    name=source_name(feature['properties'])
    if not name:unnamed+=1
    if name=='United Kingdom of Great Britain and Ireland': name='United Kingdom'
    geom=feature['geometry']
    polys = [geom['coordinates']] if geom['type']=='Polygon' else geom['coordinates'] if geom['type']=='MultiPolygon' else []
    for rings in polys:grouped.setdefault(name,[]).extend(clean_polygon(rings))

countries=[]
for index,name in enumerate(sorted(grouped),1):
    polys=grouped[name]
    if not polys: continue
    selected=max(polys,key=lambda p:area(p['Rings'][0]))
    b=selected['Bounds']; x=(b[0]+b[2])/2;y=(b[1]+b[3])/2
    if not inside(selected,x,y):
        candidates=[(b[0]+(b[2]-b[0])*i/30,b[1]+(b[3]-b[1])*j/30) for i in range(1,30) for j in range(1,30)]
        valid=[p for p in candidates if inside(selected,*p)]
        if valid: x,y=min(valid,key=lambda p:(p[0]-x)**2+(p[1]-y)**2)
        else:x,y=selected['Rings'][0][0]
    sid=scenario.get(name)
    translated=translations.get(name, f'历史地区（{index:03d}号）') if name else '历史归属未标注地区'
    digest=hashlib.sha256(name.encode()).digest()
    rgb=colorsys.hsv_to_rgb(int.from_bytes(digest[:2],'big')/65535,.22+digest[2]/255*.15,.62+digest[3]/255*.19)
    color=colors.get(sid,''.join(f'{round(v*255):02x}' for v in rgb)) if name else '8b8c83'
    desc='本图层依据1815年历史底图，含国家、属地与文化地区，不代表1836年或当前游戏日期的精确疆域。'
    if not name:desc+='原始资料未标注此处政治归属，不自动分配给邻近国家。'
    elif name not in translations:desc+='该地区专名尚待中文史料校译，暂用档案编号；原始名称保留在随附源数据中。'
    if name=='Hong Kong':desc+='原底图将香港单列；此处只作为地理地区展示，并关联大清场景，非独立国家。'
    desc+= '已关联本开发版国家模拟。' if sid else '本地区仅供地图查阅，尚未接入人口、财政、生产或可玩国家模拟。'
    countries.append({'Id':'H1815_'+hashlib.sha256(name.encode()).hexdigest()[:12].upper() if name else 'H1815_UNASSIGNED','Name':translated,'SourceName':name,'SourceYear':1815,'Description':desc,'ScenarioCountryId':sid,'Longitude':round(x,5),'Latitude':round(y,5),'ColorIndex':index,'ColorHex':color,'Area':round(sum(area(p['Rings'][0]) for p in polys),4),'Polygons':polys})

precedence=sorted(countries,key=lambda c:(c['Id']=='H1815_UNASSIGNED',c['Area']))
def winner(x,y):
    return next((c for c in precedence if any(p['Bounds'][0]<=x<=p['Bounds'][2] and p['Bounds'][1]<=y<=p['Bounds'][3] and inside(p,x,y) for p in c['Polygons'])),None)
overlaps=[]
for c in countries:
    if winner(c['Longitude'],c['Latitude']) is c:continue
    found=False
    for p in sorted(c['Polygons'],key=lambda p:-area(p['Rings'][0])):
        b=p['Bounds']
        for j in range(1,40):
            if found:break
            for i in range(1,40):
                x=b[0]+(b[2]-b[0])*i/40;y=b[1]+(b[3]-b[1])*j/40
                if inside(p,x,y) and winner(x,y) is c:
                    c['Longitude']=round(x,5);c['Latitude']=round(y,5);found=True;break
        if found:break
    if not found:
        overlaps.append(c['SourceName']);c['Description']+='此处与原资料其他区域重叠，点击时优先显示较小的已命名区域。'
(OUT/'atlas_1815.json').write_text(json.dumps(countries,ensure_ascii=False,separators=(',',':')),encoding='utf-8')
width,height=4096,2048
im=Image.new('RGB',(width,height),(0,0,0)); draw=ImageDraw.Draw(im)
def projected(ring):return [((p[0]+180)/360*(width-1),(90-p[1])/180*(height-1)) for p in ring]
# Match CPU precedence: named polygons smaller in area win; unnamed regions render first.
for c in sorted(countries,key=lambda c:(c['Id']!='H1815_UNASSIGNED',-c['Area'])):
    color=(c['ColorIndex']%256,c['ColorIndex']//256,0)
    for poly in c['Polygons']:
        draw.polygon(projected(poly['Rings'][0]),fill=color)
        for hole in poly['Rings'][1:]:draw.polygon(projected(hole),fill=(0,0,0))
im.save(OUT/'territory_ids.png',optimize=True)
manifest={'SourceUrl':'https://github.com/aourednik/historical-basemaps','Commit':'da7a4b735ecef70aebdc9c73e409d8a2500d50f3','SourceYear':1815,'License':'GPL-3.0','RawFeatures':len(source['features']),'NamedAtlasEntries':sum(bool(c['SourceName']) for c in countries),'UnassignedSourceFeatures':unnamed,'ChineseNames':len(translations),'UntranslatedNames':sum(bool(c['SourceName']) and c['SourceName'] not in translations for c in countries),'TextureSize':[width,height],'SourceSha256':hashlib.sha256((OUT/'world_1815.geojson').read_bytes()).hexdigest()}
manifest['FullyOverlappedEntries']=overlaps
(OUT/'provenance.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(manifest,ensure_ascii=False))

