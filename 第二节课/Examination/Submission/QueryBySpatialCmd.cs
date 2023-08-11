using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using SMGI.Common;

/* 技术路线
1. 遍历每个线要素：
使用游标（Cursor）或类似机制遍历每个线要素。
对于每个线要素，获取其几何形状（Shape）。

2. 判断是否是线要素：
确保要素的几何形状是线（Polyline）。
 
3. 获取起点和终点：
对于每个线要素，获取其起点和终点坐标。
通常可以使用几何对象的 FromPoint 和 ToPoint 属性来获取线要素的起点和终点。

4. 判断是否是端点：
对于每个起点和终点，检查是否是两端线要素的端点。
可以使用一个自定义的方法（比如 IsConnectedToOnlyOneLine）来判断：
IsConnectedToOnlyOneLine 方法的目标是判断一个点是否连接到且仅连接一条线要素。为了实现这个目标，该方法会在给定的点周围创建一个缓冲区，然后进行空间查询以查找与该缓冲区相交的线要素。如果找到的线要素数量为1，且该线要素与给定的 OID（Object ID，线要素的唯一标识符）不同

5. 记录端点的 OID：
如果起点或终点被判断为是两端线要素的端点，将其 OID（Object ID）记录下来。
返回结果：

6. 返回记录下来的起点和终点的 OID。
 */

namespace SMGI.Plugin.CartoExt
{
    public class QueryBySpatialCmd : SMGICommand
    {
        private static readonly string LRDLLayerName = "LRDL"; // 查询线图层名

        private IMap currentMap; // 当前MapControl控件中的Map对象
        private AxMapControl currentMapControl;
        private IFeatureClass featureClassLRDL; // 查询线图层要素类
        private IFeatureLayer LRDLFeatureLayer; // 查询线图层

        public QueryBySpatialCmd()
        {
            m_caption = "QueryBySpatialCmd"; // 扩展的显示名称
        }

        public override bool Enabled
        {
            get { return true; }
        }

        private void SetLineFeatureIndexes(IFeatureClass lineFeatureClass, Dictionary<int, int> featureIndexMap)
        {
            IFeatureCursor featureCursor = null;
            try
            {
                // 构建空间查询
                ISpatialFilter spatialFilter = new SpatialFilterClass();
                spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;

                featureCursor = lineFeatureClass.Search(null, true);
                IFeature feature = featureCursor.NextFeature();

                while (feature != null)
                {
                    int featureOID = feature.OID;
                    if (featureIndexMap.ContainsKey(featureOID))
                    {
                        int index = featureIndexMap[featureOID];
                        if (index >= 0)
                        {
                            // 更新要素的 "index" 字段
                            feature.Value[feature.Fields.FindField("index")] = index;
                            feature.Store();  // 保存更新
                        }
                    }

                    feature = featureCursor.NextFeature();
                }
            }
            finally
            {
                if (featureCursor != null)
                {
                    Marshal.ReleaseComObject(featureCursor);
                }
            }
        }

        private List<int> FindPathEndpoints(IFeatureClass lineFeatureClass)
        {
            List<int> endpointsOIDs = new List<int>();
            Dictionary<int, int> featureIndexMap = new Dictionary<int, int>(); // 存储要素的序号

            // 构建空间查询
            ISpatialFilter spatialFilter = new SpatialFilterClass();
            spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;

            // 使用游标遍历要素
            IFeatureCursor featureCursor = null;
            try
            {
                featureCursor = lineFeatureClass.Search(null, true);
                IFeature feature = featureCursor.NextFeature();

                int index = 0;
                // 遍历要素并记录起点和终点的关系
                while (feature != null)
                {
                    IGeometry geometry = feature.Shape;

                    if (geometry is IPolyline)
                    {
                        // 获取起点和终点
                        IPolyline polyline = geometry as IPolyline;
                        IPoint start = polyline.FromPoint;
                        IPoint end = polyline.ToPoint;

                        // 检查起点和终点是否只连接一条线
                        if (IsConnectedToOnlyOneLine(start, lineFeatureClass, feature.OID))
                        {
                            // 记录起点的 OID 和序号
                            endpointsOIDs.Add(feature.OID);
                            featureIndexMap[feature.OID] = index;
                        }
                        else if (IsConnectedToOnlyOneLine(end, lineFeatureClass, feature.OID))
                        {
                            // 记录终点的 OID 和序号
                            endpointsOIDs.Add(feature.OID);
                            featureIndexMap[feature.OID] = index;
                        }
                    }
                    else
                    {
                        MessageBox.Show("不是线要素");
                    }

                    index++; // 每遍历一个要素，序号增加
                    feature = featureCursor.NextFeature();
                }

                // 调用方法更新 "index" 字段
                SetLineFeatureIndexes(lineFeatureClass, featureIndexMap);
            }
            finally
            {
                if (featureCursor != null)
                {
                    Marshal.ReleaseComObject(featureCursor);
                }
            }

            return endpointsOIDs;
        }


        // 检查点是否连接到且仅连接一条线
        private bool IsConnectedToOnlyOneLine(IPoint point, IFeatureClass lineFeatureClass, int ignoreOID)
        {
            var topoOperator = point as ITopologicalOperator;
            var buffer = topoOperator.Buffer(0.001); // 使用缓冲区以处理拓扑关系问题

            // 构建空间查询
            ISpatialFilter spatialFilter = new SpatialFilterClass();
            spatialFilter.Geometry = buffer;
            spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;

            // 使用游标遍历要素
            IFeatureCursor featureCursor = null;
            try
            {
                featureCursor = lineFeatureClass.Search(spatialFilter, true);
                var feature = featureCursor.NextFeature();
                var count = 0;

                // 遍历要素，检查是否只连接一条线
                while (feature != null)
                {
                    // 这里只需要增加计数，不需要输出消息
                    count++;
                    if (count > 1)
                        // 如果连接了多于一条线，则不满足条件
                        return false;

                    feature = featureCursor.NextFeature();
                }
            }
            finally
            {
                if (featureCursor != null) Marshal.ReleaseComObject(featureCursor);
            }

            // 连接了且仅连接一条线
            return true;
        }


        // 点击扩展按钮时执行的操作
        public override void OnClick()
        {
            try
            {
                // 获取当前地图
                GetCurrentMap();

                // 检查地图是否为空
                if (currentMap == null)
                {
                    MessageBox.Show("地图未加载，请先加载地图。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // 初始化图层
                LRDLFeatureLayer = GetFeatureLayerByName(currentMap, LRDLLayerName);

                // 检查是否找到了图层
                if (LRDLFeatureLayer == null)
                {
                    MessageBox.Show(
                        "未找到所需的图层，请确保地图中包含名为" + LRDLFeatureLayer +
                        "的图层。", "错误",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                featureClassLRDL = LRDLFeatureLayer.FeatureClass;

                // 查询起始端和末端线要素的 OID
                var endpointsOIDs = FindPathEndpoints(featureClassLRDL);

                // 弹出消息框显示 OID
                // 弹出消息框显示 OID
                if (endpointsOIDs.Count == 2)
                {
                    var message = "起始端 OID: " + endpointsOIDs[0] + ", 末端 OID: " + endpointsOIDs[1];
                    MessageBox.Show(message);
                }
                else if (endpointsOIDs.Count == 1)
                {
                    var message = "起始端 OID: " + endpointsOIDs[0];
                    MessageBox.Show(message);
                }
                else if (endpointsOIDs.Count == 0)
                {
                    MessageBox.Show("未找到合适的起始端和末端线要素。");
                }
                else
                {
                    // 输出所有异常情况下的 OID
                    var message = "找到的起始端和末端线要素数量异常，OID 列表：";
                    foreach (var oid in endpointsOIDs) message += " " + oid;
                    MessageBox.Show(message);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("发生错误：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // 获取当前地图
        private void GetCurrentMap()
        {
            currentMapControl = m_Application.MapControl;

            // 确保当前地图控件和地图对象不为空
            if (currentMapControl != null) currentMap = currentMapControl.Map;
        }

        // 在地图中根据图层名称获得矢量图层
        private IFeatureLayer GetFeatureLayerByName(IMap map, string layerName)
        {
            // 对地图中的图层进行遍历
            for (var i = 0; i < map.LayerCount; i++)
            {
                var layer = map.get_Layer(i);
                if (layer.Name == layerName && layer is IFeatureLayer) return (IFeatureLayer)layer;
            }

            return null;
        }
    }
}