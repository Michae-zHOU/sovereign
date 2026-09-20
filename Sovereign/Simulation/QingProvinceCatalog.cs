using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Sovereign.Simulation;

public readonly record struct QingGeoPoint(double Longitude, double Latitude);
public sealed record QingProvinceDefinition(string Id, string Name, string Administration, string Capital,
    double Longitude, double Latitude, int RuralWeight,
    IReadOnlyList<IReadOnlyList<QingGeoPoint>> BoundaryPolygons, string Note)
{
    public bool IsProvince => Administration == "行省";
}

/// <summary>
/// Original coarse gameplay polygons, not survey data or a transcription of CHGIS. The eighteen provinces
/// are distinguished from frontier administrations; their territory is an approximation. Longitude precedes latitude.
/// No present-day PRC province geometry is used. Source and uncertainty: QING-PROVINCE-SOURCES.md.
/// Population weights are authored scenario allocations, not province-level historical census observations.
/// </summary>
public static class QingProvinceCatalog
{
    public const string BoundaryNotice = "大清省界为地理示意，非1836年实测疆界";
    public static IReadOnlyList<QingProvinceDefinition> Provinces { get; } = new[]
    {
        P("zhili", "直隶", "行省", "保定", 115.49, 38.86, 27,
            "113.5,36.1 115.4,36.1 116.1,37.2 117.7,38.2 119.8,39.9 119.1,40.6 116.2,40.6 114.1,41.1 113.7,39.1 114.2,37.3", "京师及天津归入直隶；热河旗地另列管理区。"),
        P("shandong", "山东", "行省", "济南", 117.02, 36.66, 28,
            "115.4,34.6 118.2,34.6 119.5,35.2 121.0,36.5 122.8,37.5 120.8,38.2 118.7,37.9 117.7,38.2 116.1,37.2 115.4,36.1", "登州、济南等城属山东。"),
        P("shanxi", "山西", "行省", "太原", 112.56, 37.87, 16,
            "110.2,34.6 111.7,34.5 113.5,36.1 114.2,37.3 113.7,39.1 114.1,41.1 112.0,40.4 110.4,40.6 111.2,38.8 110.5,36.7", "边界为黄河、太行山地的简化示意。"),
        P("henan", "河南", "行省", "开封", 114.31, 34.80, 25,
            "110.2,34.6 111.7,34.5 113.5,36.1 115.4,36.1 115.4,34.6 116.5,33.0 115.2,31.5 113.2,31.8 111.0,33.3", "开封与洛阳归属河南。"),
        P("shaanxi", "陕西", "行省", "西安", 108.94, 34.34, 10,
            "105.7,32.4 108.5,31.8 111.0,33.3 110.2,34.6 110.5,36.7 111.2,38.8 110.4,40.6 107.4,39.3 107.0,37.0 106.0,35.0", "陕北、关中、汉中合为陕西。"),
        P("gansu", "甘肃", "行省", "兰州", 103.82, 36.06, 14,
            "92.5,39.0 94.0,41.2 97.7,42.8 101.0,41.7 104.2,41.8 107.4,39.3 107.0,37.0 106.0,35.0 105.7,32.4 102.5,32.4 101.8,34.0 102.4,36.0 100.0,37.5 97.0,38.2", "包括宁夏府与西宁府一带；新疆此时尚未建省。"),
        P("jiangsu", "江苏", "行省", "苏州／江宁", 119.44, 32.21, 38,
            "116.5,33.0 115.4,34.6 118.2,34.6 119.5,35.2 120.4,34.2 121.0,32.5 122.0,31.4 121.0,30.7 119.2,31.2 118.3,32.5", "江宁、苏州、扬州与上海均在江苏范围内。"),
        P("anhui", "安徽", "行省", "安庆", 117.05, 30.51, 32,
            "115.2,31.5 116.5,33.0 118.3,32.5 119.2,31.2 118.9,30.0 117.8,29.4 116.1,29.8 115.5,30.5", "安庆、芜湖归属安徽。"),
        P("zhejiang", "浙江", "行省", "杭州", 120.15, 30.27, 27,
            "117.8,29.4 118.9,30.0 119.2,31.2 121.0,30.7 122.0,30.4 122.1,29.7 121.6,28.3 120.5,27.0 119.4,27.4 118.4,28.4", "杭州、宁波、绍兴、温州均归浙江。"),
        P("jiangxi", "江西", "行省", "南昌", 115.89, 28.68, 28,
            "113.9,24.5 115.9,24.5 116.4,25.5 116.8,27.2 118.4,28.4 117.8,29.4 116.1,29.8 114.8,29.4 113.6,28.0 113.8,26.0", "九江和南昌归属江西。"),
        P("hubei", "湖北", "行省", "武昌", 114.30, 30.55, 28,
            "108.5,31.8 111.0,33.3 113.2,31.8 115.2,31.5 115.5,30.5 116.1,29.8 114.8,29.4 112.0,29.5 109.8,29.2 108.5,29.4", "武昌、汉口按两处历史聚落记录。"),
        P("hunan", "湖南", "行省", "长沙", 112.97, 28.19, 23,
            "108.8,26.0 110.2,26.0 111.7,25.0 113.9,24.5 113.8,26.0 113.6,28.0 114.8,29.4 112.0,29.5 109.8,29.2 109.0,28.4", "长沙为本情景已建模城市，其余居民计入乡村人口。"),
        P("sichuan", "四川", "行省", "成都", 104.07, 30.66, 33,
            "97.5,29.0 99.0,31.5 99.0,33.0 102.5,32.4 105.7,32.4 108.5,31.8 108.5,29.4 109.0,28.4 107.2,28.2 105.7,28.2 104.5,28.6 103.5,26.5 101.5,26.8 100.0,28.0", "重庆属于四川；川边与藏区接界仅作示意。"),
        P("fujian", "福建", "行省", "福州", 119.30, 26.07, 18,
            "115.9,24.5 116.9,24.0 117.6,23.3 119.2,24.8 120.2,26.0 120.5,27.0 119.4,27.4 118.4,28.4 116.8,27.2 116.4,25.5;120.0,22.0 120.8,21.9 121.5,23.1 122.0,25.1 121.4,25.4 120.3,23.8", "台湾府当时隶福建；图形不表示清廷对岛上全部地区的同等实际控制。"),
        P("guangdong", "广东", "行省", "广州", 113.26, 23.13, 22,
            "109.2,21.0 110.6,20.2 111.8,21.5 114.5,22.3 116.8,22.9 117.6,23.3 116.9,24.0 115.9,24.5 113.9,24.5 111.7,25.0 111.0,23.6 109.8,22.6;108.5,19.5 109.2,18.2 110.0,18.1 111.1,19.7 110.6,20.2 109.4,20.2", "含琼州府及今雷州、钦州一带；没有独立的海南省。"),
        P("guangxi", "广西", "行省", "桂林", 110.30, 25.28, 8,
            "104.5,23.5 105.8,22.7 106.8,22.0 108.0,21.7 109.2,21.0 109.8,22.6 111.0,23.6 111.7,25.0 110.2,26.0 108.8,26.0 106.7,25.5 105.7,24.8", "桂林为省城；沿海边界按清代省制概略处理。"),
        P("yunnan", "云南", "行省", "云南府", 102.71, 25.04, 7,
            "97.5,29.0 100.0,28.0 101.5,26.8 103.5,26.5 104.5,26.5 104.5,23.5 105.8,22.7 104.0,22.8 102.5,22.3 101.0,21.0 100.0,21.5 99.0,23.0 97.5,24.0 98.8,25.5", "城市数据中的昆明对应云南府城。"),
        P("guizhou", "贵州", "行省", "贵阳", 106.71, 26.58, 6,
            "104.5,23.5 105.7,24.8 106.7,25.5 108.8,26.0 109.0,28.4 107.2,28.2 105.7,28.2 104.5,28.6 103.5,26.5 104.5,26.5", "省界为概略地理分区。"),
        P("shengjing", "盛京", "将军辖区", "盛京", 123.45, 41.80, 2,
            "119.1,40.6 119.8,39.9 121.5,38.6 123.2,39.3 125.1,40.4 125.0,42.3 123.5,43.2 121.5,43.2 120.0,42.0", "奉天尚未改设近代行省；与吉林、黑龙江将军辖区分别显示。"),
        P("jilin", "吉林", "将军辖区", "吉林", 126.55, 43.84, 1,
            "121.5,43.2 123.5,43.2 125.0,42.3 125.1,40.4 127.2,41.2 130.7,42.3 133.0,44.0 136.5,45.5 140.5,48.3 140.0,51.0 136.0,49.7 132.0,47.4 128.0,45.5 125.0,45.4 122.5,45.0", "包括黑龙江以南、乌苏里江以东的历史辖境示意；不是现代吉林省。"),
        P("heilongjiang", "黑龙江", "将军辖区", "齐齐哈尔", 123.92, 47.35, 1,
            "118.0,49.0 119.0,52.0 122.5,53.8 125.5,55.0 131.0,55.5 136.0,54.0 140.0,51.0 136.0,49.7 132.0,47.4 128.0,45.5 125.0,45.4 122.5,45.0 120.0,46.5", "外兴安岭、黑龙江流域仅以简化历史辖境表示。"),
        P("rehe", "热河", "都统及旗地管理区", "承德", 117.94, 40.98, 1,
            "114.1,41.1 116.2,40.6 119.1,40.6 120.0,42.0 119.4,43.0 116.0,43.0 114.0,42.0", "承德府的民政与直隶、热河都统的旗地管辖并存；此处为游戏管理区，不称热河省。"),
        P("inner_mongolia", "内蒙古盟旗", "盟旗合并管理区", "归化城", 111.67, 40.82, 2,
            "101.0,41.7 104.2,41.8 107.4,39.3 110.4,40.6 112.0,40.4 114.1,41.1 114.0,42.0 116.0,43.0 119.4,43.0 120.0,42.0 121.5,43.2 122.5,45.0 120.0,46.5 118.0,49.0 115.0,47.0 112.0,44.5 107.0,43.7 103.0,42.8", "多个盟旗合并为一处游戏管理区，非单一清代省级行政机构。"),
        P("outer_mongolia", "外蒙古盟旗", "盟旗及驻臣合并管理区", "库伦", 106.92, 47.92, 1,
            "87.5,49.0 90.0,50.5 94.5,50.0 98.0,52.5 102.5,51.5 106.0,50.5 110.0,50.0 115.0,49.8 118.0,49.0 115.0,47.0 112.0,44.5 107.0,43.7 103.0,42.8 101.0,41.7 97.7,42.8 94.0,44.0 91.0,46.0", "喀尔喀等盟旗及驻臣辖境的合并游戏分区，非行省。"),
        P("ili", "新疆军府", "伊犁将军等合并管理区", "惠远", 80.87, 44.03, 2,
            "73.0,39.0 74.0,36.5 78.0,35.0 82.0,35.0 87.0,36.0 90.0,37.0 92.5,39.0 94.0,41.2 97.7,42.8 94.0,44.0 91.0,46.0 87.5,49.0 83.0,48.0 81.0,45.5 78.0,43.5 74.0,42.0", "1836年采用军府制度；新疆于1884年建省，因此此处不称新疆省。"),
        P("tibet", "西藏", "驻藏大臣与噶厦辖境", "拉萨", 91.13, 29.65, 2,
            "78.0,35.0 79.0,32.0 81.0,30.0 84.0,28.5 87.0,27.5 89.0,28.0 92.0,27.5 95.0,28.0 97.5,29.0 99.0,31.5 96.0,33.0 93.0,33.5 89.0,35.0 87.0,36.0 82.0,35.0", "驻藏大臣与地方政务制度另列；图形不等同逐地行政控制。"),
        P("qinghai", "青海辖境", "办事大臣及旗地管理区", "西宁驻臣", 99.2, 35.5, 1,
            "87.0,36.0 89.0,35.0 93.0,33.5 96.0,33.0 99.0,31.5 99.0,33.0 102.5,32.4 101.8,34.0 102.4,36.0 100.0,37.5 97.0,38.2 92.5,39.0 90.0,37.0", "青海蒙古旗地等的游戏管理区；西宁府民政仍计甘肃，并无青海行省。")
    };

    private static readonly Dictionary<string, string> CityRegions = BuildCityRegions();
    public static QingProvinceDefinition Province(string id) => Provinces.Single(p => p.Id == id);
    public static string RegionForCity(string cityId) => CityRegions.TryGetValue(cityId, out var id)
        ? id : throw new ArgumentException("Unknown Qing city.", nameof(cityId));

    public static QingProvinceDefinition? ProvinceAt(double longitude, double latitude)
    {
        if (!double.IsFinite(longitude) || !double.IsFinite(latitude) || longitude < 70 || longitude > 142 || latitude < 18 || latitude > 57) return null;
        foreach (var province in Provinces)
            if (province.BoundaryPolygons.Any(polygon => Contains(polygon, longitude, latitude))) return province;
        return null;
    }

    private static bool Contains(IReadOnlyList<QingGeoPoint> points, double x, double y)
    {
        bool inside = false;
        for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
        {
            var a = points[j]; var b = points[i];
            double cross = (x - a.Longitude) * (b.Latitude - a.Latitude) - (y - a.Latitude) * (b.Longitude - a.Longitude);
            if (Math.Abs(cross) < 1e-10 && x >= Math.Min(a.Longitude, b.Longitude) && x <= Math.Max(a.Longitude, b.Longitude) &&
                y >= Math.Min(a.Latitude, b.Latitude) && y <= Math.Max(a.Latitude, b.Latitude)) return true;
            if ((a.Latitude > y) != (b.Latitude > y) && x < (b.Longitude - a.Longitude) * (y - a.Latitude) / (b.Latitude - a.Latitude) + a.Longitude)
                inside = !inside;
        }
        return inside;
    }

    private static QingProvinceDefinition P(string id, string name, string administration, string capital,
        double longitude, double latitude, int weight, string polygons, string note) =>
        new("QNG_" + id, name, administration, capital, longitude, latitude, weight,
            polygons.Split(';').Select(polygon => (IReadOnlyList<QingGeoPoint>)polygon.Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => pair.Split(',')).Select(p => new QingGeoPoint(double.Parse(p[0], CultureInfo.InvariantCulture),
                    double.Parse(p[1], CultureInfo.InvariantCulture))).ToArray()).ToArray(), note);

    private static Dictionary<string, string> BuildCityRegions()
    {
        const string data = """
zhili|beijing tianjin baoding zhengding
shandong|jinan dengzhou
shanxi|taiyuan
henan|kaifeng luoyang
shaanxi|xian
jiangsu|nanjing suzhou yangzhou zhenjiang shanghai
anhui|anqing wuhu
zhejiang|hangzhou ningbo shaoxing wenzhou
jiangxi|jiujiang nanchang
hubei|wuchang hankou
hunan|changsha
sichuan|chengdu chongqing
fujian|fuzhou xiamen quanzhou zhangzhou
guangdong|canton chaozhou foshan
guangxi|guilin
yunnan|kunming
guizhou|guiyang
shengjing|shengjing
rehe|chengde
""";
        return data.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(line => line.Trim().Split('|'))
            .SelectMany(parts => parts[1].Split(' ').Select(city => new KeyValuePair<string, string>("QNG_" + city, "QNG_" + parts[0])))
            .ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
    }
}
