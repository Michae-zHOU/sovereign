# 大清管理区示意数据

`qing-provinces-schematic.geojson` 由本项目原创低精度多边形生成，供检查或替换数据管线使用。它不是CHGIS数据、不是现代中国省界、也不是精确1836疆界。十八行省与九个边疆管理区分别注明制度类型。

运行时以 `Simulation/QingProvinceCatalog.cs` 为唯一数据源。修改后运行 `python Assets/provinces/export_geojson.py` 更新此审查用 GeoJSON；避免只改派生文件导致地图与命中检测不一致。

资料、精度、实际控制差异、人口假设及存档迁移见 `QING-PROVINCE-SOURCES.md`。
