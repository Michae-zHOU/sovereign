from pathlib import Path
import math,random
from PIL import Image,ImageDraw
ROOT=Path(__file__).resolve().parent
W,H=1024,3072
random.seed(1837)
im=Image.new('RGB',(W,H));p=im.load()
for y in range(H):
 for x in range(W):
  n=random.randint(-3,3)+(1 if (x+y)%3==0 else 0);p[x,y]=(171+n,125+n,42+n)
d=ImageDraw.Draw(im)

def bezier(points,n=120):
 out=[]
 for i in range(n+1):
  t=i/n;u=1-t
  out.append(tuple(u*u*u*points[0][j]+3*u*u*t*points[1][j]+3*u*t*t*points[2][j]+t*t*t*points[3][j] for j in range(2)))
 return out

def cloud(x,y,r=24,color=(203,160,74)):
 for k in range(3):
  xx=x+(k-1)*r*.8;yy=y+(0 if k==1 else r*.35)
  d.arc((xx-r,yy-r*.6,xx+r,yy+r*.6),175,520,fill=color,width=2)
 d.line([(x-r*2,y+r*.7),(x,y+r),(x+r*2,y+r*.7)],fill=color,width=2)
for y in range(110,2750,205):
 for x in range(-60,1100,230):
  xx=x+(115 if y//205%2 else 0)
  if (xx-512)**2+(y-755)**2>330**2:cloud(xx,y,23,(185,139,54))

def dragon(cx,cy,s=1):
 def T(p):return (cx+p[0]*s,cy+p[1]*s)
 def line(ps,col,w):d.line([T(x) for x in ps],fill=col,width=max(1,int(w*s)),joint='curve')
 ink=(77,54,21);gold=(235,187,83);bright=(255,215,118);shadow=(131,91,26)
 body=bezier([(40,-170),(-195,-170),(-150,-40),(5,-10)])+bezier([(5,-10),(200,30),(155,175),(-32,165)])[1:]+bezier([(-32,165),(-170,166),(-144,94),(-92,112)])[1:]
 line(body,ink,38);line(body,gold,31);line(body,shadow,2)
 for i in range(12,len(body)-10,10):
  a=body[i-1];b=body[i+1];ang=math.atan2(b[1]-a[1],b[0]-a[0]);n=(-math.sin(ang),math.cos(ang));t=(math.cos(ang),math.sin(ang));c=body[i]
  for sign in [-1,1]:
   pts=[(c[0]+n[0]*sign*14-t[0]*5,c[1]+n[1]*sign*14-t[1]*5),(c[0]+n[0]*sign*5+t[0]*5,c[1]+n[1]*sign*5+t[1]*5),(c[0]-t[0]*5,c[1]-t[1]*5)]
   line(pts,ink,1.5)
 # Four limbs with five individual talons.
 for sx,sy,dx,dy in [(-98,-109,-193,-62),(-22,-45,100,-117),(112,64,198,120),(58,156,-25,221)]:
  limb=bezier([(sx,sy),((sx+dx)/2,sy-28),((sx+dx)/2,dy+5),(dx,dy)],35)
  line(limb,ink,14);line(limb,gold,9)
  for i in range(5):
   a=-1.4+i*.55;tx=dx+math.cos(a)*27;ty=dy+math.sin(a)*28
   line([(dx,dy),(tx,ty),(tx+9,ty-8)],ink,4);line([(dx,dy),(tx,ty),(tx+9,ty-8)],bright,2)
 # Head, muzzle, mane, antler horns and long fine whiskers.
 d.ellipse([T((4,-209)),T((77,-147))],fill=gold,outline=ink,width=max(1,int(3*s)))
 d.polygon([T((57,-191)),T((108,-184)),T((112,-170)),T((67,-156)),T((59,-172))],fill=gold,outline=ink)
 line([(40,-204),(25,-231),(11,-244),(18,-220),(4,-216)],gold,5)
 line([(62,-202),(65,-229),(79,-247),(77,-220),(91,-227)],gold,5)
 for i in range(7):
  a=-2.7+i*.30;line([(35,-177),(35+math.cos(a)*51,-177+math.sin(a)*48),(35+math.cos(a+.22)*62,-177+math.sin(a+.22)*55)],bright,3)
 d.ellipse([T((57,-190)),T((68,-181))],fill=ink);d.ellipse([T((61,-188)),T((65,-184))],fill=bright)
 line([(84,-170),(99,-175)],ink,2)
 line(bezier([(92,-170),(130,-139),(170,-188),(204,-158)],50),bright,2)
 line(bezier([(83,-166),(121,-114),(178,-124),(194,-91)],50),bright,2)
 line([(61,-155),(48,-132),(36,-125),(40,-144)],bright,3)
 # Irregular flame wisps and cloud scrolls around the dragon, embroidered in fine thread.
 for ang in [-2.9,-2.4,-1.1,-.5,.15,.7,1.45,2.4]:
  x=math.cos(ang)*235;y=math.sin(ang)*230;cloud(cx+x*s,cy+y*s,18*s,(222,174,70))
 # Twin fine cord edge, not a thick badge.
 d.ellipse((cx-269*s,cy-269*s,cx+269*s,cy+269*s),outline=(199,150,55),width=2)
 d.ellipse((cx-275*s,cy-275*s,cx+275*s,cy+275*s),outline=(209,164,65),width=1)

dragon(512,770,.92)
# Smaller lower-skirt dragon roundels.
dragon(270,2125,.46);dragon(760,2390,.42)
# Sea-wave hem and diagonal mountain-water bands.
for x in range(-120,W+150,75):
 for yy in range(0,5):
  pts=bezier([(x,2760+yy*14),(x+65,2660+yy*14),(x+100,2840+yy*14),(x+150,2740+yy*14)],50)
  d.line(pts,fill=[(94,78,40),(208,166,77),(61,87,81),(212,174,86),(146,103,31)][yy],width=7)
for x in range(-200,W+200,34):
 d.line((x,2900,x+155,3072),fill=(225,184,91),width=9)
 d.line((x+13,2900,x+168,3072),fill=(73,97,90),width=6)
for y in [2853,2861,2875,2895]:d.line((0,y,W,y),fill=(238,196,107),width=3)
im.save(ROOT/'qing_robe_embroidery_07.png')
print('Original imperial dragon embroidery',ROOT/'qing_robe_embroidery_07.png')
