# 大清新增聚落：资料与场景边界

核对日期：2026-09-19。本批为大清场景增加 32 个选定聚落，与原有 8 城合计 40 城。它仍不是全部清代聚落名录。

## 取样与坐标

选择的是 1836 年以前已存在的城、县治或商业聚落。游戏中的“城市”是统一的模拟实体，不要求它在 1836 年已经具有现代“市”的行政建制。汉口与武昌分别表示两处历史聚落，不合并为后来形成的武汉市。盛京使用时代地名；登州表示今蓬莱旧城，不是河南邓州。太原点位表示太原府城，不是西南方向另有其址的太原县城；洛阳点位表示金元明清旧城，不是隋唐全城的中心。

下表经纬度是作者为世界地图手工选取的**旧城附近近似定位点**，采用通常经纬度表示，保留三位小数以避免相邻标记重叠；这不代表有百米级历史测绘精度。没有把现代行政区的面积、全部辖境人口或现代市政府的位置作为清代城市边界。来源列核对聚落历史存在和旧城身份，不声称这些网页直接提供了表内全部坐标。

`north`、`yangtze`、`south` 仅沿用现有模拟分组，分别表示广义北方、长江沿线及流域、广义南方，**不是清代省界**。宁波、绍兴编入 `south`；原版杭州的 `yangtze` 归属保留兼容性。城市点不表示国家或省份边界。

`P` 是程序化河岸／港口场景标志，涵盖部分内河码头，不表示当时的条约口岸资格、外国租界、自由贸易、海军基地等级或现代港区。空白也不表示聚落在历史上完全没有水运。1836 年上海、宁波、厦门等的港口场景不得预先出现 1842 年以后建立的条约口岸制度和租界街区。

**所有初始人口均为游戏作者的平衡估计，单位为人，不是 1836 年人口普查或地方志户口数字。** 来源只支持历史选址，不能用于给人口、行业等级、GDP 或财政数字背书。产业和市民数据来自统一模拟；程序化三维街区不是准确复原的历史街巷。

## 新增 32 个聚落

| ID 后缀 | 中文／内部英文名 | 分组 | 纬度 | 经度 | 初始人口估计 | 港口场景 | 历史选址依据 |
| --- | --- | --- | ---: | ---: | ---: | :---: | --- |
| jinan | 济南 / Jinan | north | 36.664 | 117.022 | 180000 | | 明代已有省级行政中心。[济南史][jinan] |
| taiyuan | 太原 / Taiyuan | north | 37.872 | 112.558 | 140000 | | 太原府城与晋阳遗址须区分。[太原研究][taiyuan] |
| kaifeng | 开封 / Kaifeng | north | 34.797 | 114.308 | 160000 | | 旧城及明清城墙。[开封保护名录][kaifeng] |
| luoyang | 洛阳 / Luoyang | north | 34.683 | 112.477 | 100000 | | 金以后旧城承续。[洛阳沿革][luoyang] |
| baoding | 保定 / Baoding | north | 38.857 | 115.491 | 100000 | | 历史府城。[国务院名城简介][gazette] |
| zhengding | 正定 / Zhengding | north | 38.149 | 114.572 | 65000 | | 清代城址有复原研究。[国博研究][zhengding] |
| chengde | 承德 / Chengde | north | 40.980 | 117.939 | 60000 | | 清代行宫及寺庙群所在城镇。[UNESCO][chengde] |
| shengjing | 盛京 / Shengjing | north | 41.795 | 123.449 | 130000 | | 入关前清宫与都城所在。[UNESCO][shengjing] |
| dengzhou | 登州 / Dengzhou | north | 37.824 | 120.752 | 45000 | P | 蓬莱旧城与明清水城港湾。[蓬莱历史][dengzhou] |
| chengdu | 成都 / Chengdu | yangtze | 30.664 | 104.066 | 350000 | | 长期延续的旧城。[成都保护规划][chengdu] |
| chongqing | 重庆 / Chongqing | yangtze | 29.558 | 106.578 | 220000 | P | 江河汇合处的古城。[国务院名城简介][gazette] |
| wuchang | 武昌 / Wuchang | yangtze | 30.546 | 114.301 | 180000 | P | 与汉口分列的历史城镇。[国务院名城简介][gazette] |
| hankou | 汉口 / Hankou | yangtze | 30.583 | 114.288 | 300000 | P | 明清商业名镇。[广佛历史介绍][foshan] |
| jiujiang | 九江 / Jiujiang | yangtze | 29.729 | 115.993 | 120000 | P | 1450 年已有商船征税机构。[九江钞关史][jiujiang] |
| nanchang | 南昌 / Nanchang | yangtze | 28.678 | 115.893 | 150000 | | 古代行政中心延续至明清。[国务院名城简介][gazette] |
| anqing | 安庆 / Anqing | yangtze | 30.505 | 117.047 | 120000 | P | 1760 年起为安徽省会。[安庆概况][anqing] |
| wuhu | 芜湖 / Wuhu | yangtze | 31.326 | 118.376 | 80000 | P | 古县城延续，具有水陆商贸功能。[芜湖古城][wuhu] |
| yangzhou | 扬州 / Yangzhou | yangtze | 32.395 | 119.440 | 300000 | | 明清古城仍有街区遗存。[扬州介绍][yangzhou] |
| zhenjiang | 镇江 / Zhenjiang | yangtze | 32.212 | 119.449 | 140000 | P | 宋代起使用镇江府名。[国务院名城简介][gazette] |
| shanghai | 上海 / Shanghai | yangtze | 31.227 | 121.491 | 220000 | P | 宋代镇、元代县的旧城。[国务院名城简介][gazette] |
| changsha | 长沙 / Changsha | yangtze | 28.194 | 112.974 | 180000 | | 明代府志已有城与属县记录。[湖南方志院][changsha] |
| ningbo | 宁波 / Ningbo | south | 29.873 | 121.551 | 250000 | P | 古代港城及行政中心。[国务院名城简介][gazette] |
| shaoxing | 绍兴 / Shaoxing | south | 30.000 | 120.582 | 180000 | | 山阴、会稽同城而治。[浙江数字方志][shaoxing] |
| wenzhou | 温州 / Wenzhou | south | 28.015 | 120.655 | 100000 | P | 晋代永嘉郡城的历史承续。[浙江省英文门户][wenzhou] |
| xiamen | 厦门 / Xiamen | south | 24.455 | 118.077 | 140000 | P | 1394 年筑城；现代设市晚得多。[福建民政厅][xiamen] |
| quanzhou | 泉州 / Quanzhou | south | 24.908 | 118.587 | 160000 | P | 宋元海贸城市与内陆联系。[UNESCO][quanzhou] |
| zhangzhou | 漳州 / Zhangzhou | south | 24.511 | 117.649 | 120000 | | 唐以后州郡治所。[国务院名城简介][gazette] |
| chaozhou | 潮州 / Chaozhou | south | 23.666 | 116.641 | 150000 | | 宋城及明清街区。[国务院名城简介][gazette] |
| foshan | 佛山 / Foshan | south | 23.031 | 113.112 | 250000 | | 明清手工业商业聚落。[广佛历史介绍][foshan] |
| guilin | 桂林 / Guilin | south | 25.281 | 110.296 | 100000 | | 明代靖江王府所在旧城。[王城介绍][guilin] |
| kunming | 昆明 / Kunming | south | 25.043 | 102.708 | 140000 | | 明清云南府城与昆明县城址。[云南民政厅][kunming] |
| guiyang | 贵阳 / Guiyang | south | 26.579 | 106.713 | 90000 | | 明代旧城门格局及清代城图。[贵阳市政府][guiyang] |

## 资料使用说明

国务院第二批国家历史文化名城文件的“简介”用于确认若干聚落的古代身份，不把 1986 年的名城范围当作 1836 年城界。官方公报为原始发布文件；附列维基文库转录方便检索文字。已核对转录中的对应城市段落。其他来源为地方政府、地方志机构、大学／博物馆研究、遗产机构或遗址管理方的历史介绍。现代旅游设施、修复建筑和现代经济数据没有转为 1836 年场景事实。

后续完善顺序：逐城核对 1836 年前后地方志及舆图；记录异名和有效日期；为人口保存原始统计口径及不确定区间；标注航道、码头、城墙和街区的时间范围；再制作有史料依据的单城三维模型。当前 40 城只是一批可玩内容，不代替用户要求的全部历史聚落数据库。

[gazette]: https://www.gov.cn/gongbao/shuju/1986/gwyb198635.pdf
[jinan]: https://www.jinan.gov.cn/col24703/art/2024/art_24703_1685067.html
[taiyuan]: https://qkzx.tyut.edu.cn/__local/0/91/E5/EED800B1ABFC546AAA4C6B37736_30DB2AF0_D33AE.pdf
[kaifeng]: https://www.kaifeng.gov.cn/kfsrmzfwz/tzgg/1768115858696634368/EWVE1D0L.pdf
[luoyang]: https://lysrd.henanrd.gov.cn/2025/07-16/223000.html
[zhengding]: https://www.chnmuseum.cn/yj/xscg/xslw/201812/t20181224_36605.shtml
[chengde]: https://whc.unesco.org/en/list/703
[shengjing]: https://whc.unesco.org/en/list/439/
[dengzhou]: https://www.penglai.gov.cn/art/2020/6/16/art_13313_975837.html
[chengdu]: https://www.sc.gov.cn/10462/c108551/2021/12/24/8eceb42022834db6a494ed30f439a4a2/files/0c7b9d26dc5540e09a31373d23844149.pdf
[jiujiang]: https://szfzsb.jiujiang.gov.cn/ftrq_233/jjts/202301/t20230131_5920790.html
[anqing]: https://tzcjj.anqing.gov.cn/zspt/2003708301.html
[wuhu]: https://www.wuhu.gov.cn/mlwh/lywh/rw/23530171.html
[yangzhou]: https://www.miit.gov.cn/ztzl/lszt/gjgyyxzdlxcs/csmd/art/2020/art_6b713aeeb6324e57a20111d0405561f4.html
[changsha]: https://dfz.hunan.gov.cn/tslm_71564/dfzs/jz/202503/t20250314_33612703.html
[shaoxing]: https://dfz.zj.gov.cn/zlyz/ossfs/h5/ZS-K-330603-2013-001-0401/files/basic-html/page118.html
[wenzhou]: https://www.ezhejiang.gov.cn/2020-04/22/c_56089.htm
[xiamen]: https://mzt.fujian.gov.cn/gk/wpldq/202509/t20250901_6998776.htm
[quanzhou]: https://whc.unesco.org/en/list/1561
[foshan]: https://www.gz.gov.cn/zlgz/whgz/content/post_8814877.html
[guilin]: https://www.glwangcheng.com/cp/
[kunming]: https://ynmz.yn.gov.cn/cms/dimingfengcai/11216.html
[guiyang]: https://english.guiyang.gov.cn/sl/2025-03/04/c_1075541.htm

公报文字检索副本：[维基文库：国务院批转建设部、文化部关于请公布第二批国家历史文化名城名单报告的通知](https://zh.wikisource.org/wiki/国务院批转建设部、文化部关于请公布第二批国家历史文化名城名单报告的通知)。
