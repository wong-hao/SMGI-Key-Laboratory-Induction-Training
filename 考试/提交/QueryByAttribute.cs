using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Windows.Forms;
using System.Runtime.InteropServices;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.esriSystem;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.SystemUI;
using SMGI.Common;
using ESRI.ArcGIS.DataSourcesFile;

namespace SMGI.Plugin.CartoExt
{
    public class QueryByAttribute : SMGICommand
    {
        public QueryByAttribute()
        {
            m_caption = "QueryByAttribute"; // 扩展的显示名称
        }

        public override bool Enabled
        {
            get { return true; }
        }

        private AxMapControl currentMapControl;
        private IMap currentMap; //当前MapControl控件中的Map对象   

        private static readonly string SourceLayerName = "HYDL"; // 源图层名

        private IFeatureLayer SourceLayer;

        /// <Date>2023/8/4</Date>
        /// <Author>HaoWong</Author>
        /// <summary>
        /// 子函数，得到目前图层
        /// </summary>
        private void GetCurrentMap()
        {
            currentMapControl = m_Application.MapControl;

            // 确保当前地图控件和地图对象不为空
            if (currentMapControl != null)
            {
                currentMap = currentMapControl.Map;
            }
        }

        /// <Date>2023/8/4</Date>
        /// <Author>HaoWong</Author>
        /// <summary>
        /// 在地图中根据图层名称获得矢量图层
        /// </summary>
        /// <param name="map"></param>
        /// <param name="layerName"></param>
        private IFeatureLayer GetFeatureLayerByName(IMap map, string layerName)
        {
            //对地图中的图层进行遍历
            for (int i = 0; i < map.LayerCount; i++)
            {
                //如果该图层为图层组类型，则分别对所包含的每个图层进行操作
                if (map.get_Layer(i) is GroupLayer)
                {
                    //使用ICompositeLayer接口进行遍历操作
                    ICompositeLayer compositeLayer = map.get_Layer(i) as ICompositeLayer;
                    for (int j = 0; j < compositeLayer.Count; j++)
                    {
                        //如果图层名称为所要查询的图层名称，则返回IFeatureLayer接口的矢量图层对象
                        if (compositeLayer.get_Layer(j).Name == layerName)
                            return (IFeatureLayer)compositeLayer.get_Layer(j);
                    }
                }
                // 如果图层不是图层组类型，则直接进行判断
                else
                {
                    if (map.get_Layer(i).Name == layerName)
                        return (IFeatureLayer)map.get_Layer(i);
                }
            }

            return null;
        }

        // 定义一个函数来统计数据类型及其数量
        private void CountDataTypesInDatabase()
        {
            IEnumLayer enumLayer = currentMap.Layers;
            ILayer layer;

            // 创建一个字典来存储数据类型及其数量
            Dictionary<string, int> dataTypeCounts = new Dictionary<string, int>();

            // 遍历地图中的每个图层
            while ((layer = enumLayer.Next()) != null)
            {
                // 判断图层是否为要素图层
                if (layer is IFeatureLayer)
                {
                    IFeatureLayer featureLayer = (IFeatureLayer)layer; // 将图层转换为要素图层

                    IFeatureClass featureClass = featureLayer.FeatureClass;

                    // 遍历当前图层的每个字段
                    IFields fields = featureClass.Fields;
                    for (int i = 0; i < fields.FieldCount; i++)
                    {
                        IField field = fields.get_Field(i);

                        // 排除几个常见的不需要统计的字段，如 OID、Shape 等
                        if (field.Name.Equals("OID", StringComparison.OrdinalIgnoreCase) || field.Type == esriFieldType.esriFieldTypeOID ||
                            field.Name.Equals("Shape", StringComparison.OrdinalIgnoreCase) || field.Type == esriFieldType.esriFieldTypeGeometry)
                            continue;

                        string dataType = field.Type.ToString(); // 获取字段的数据类型

                        // 更新数据类型计数
                        if (!string.IsNullOrEmpty(dataType))
                        {
                            if (dataTypeCounts.ContainsKey(dataType))
                                dataTypeCounts[dataType]++;
                            else
                                dataTypeCounts[dataType] = 1;
                        }
                    }
                }
            }

            // 输出统计结果
            foreach (var entry in dataTypeCounts)
            {
                string dataType = entry.Key;
                int count = entry.Value;
                MessageBox.Show("数据类型: " + dataType + ", 数量: " + count);
            }
        }

        // 定义一个函数来统计点线面要素、栅格数据和要素数据集的数量
        private void CountFeatureTypesInDatabase()
        {
            IEnumLayer enumLayer = currentMap.Layers;
            ILayer layer;

            int pointCount = 0;
            int polylineCount = 0;
            int polygonCount = 0;
            int rasterCount = 0;
            int featureDatasetCount = 0;

            // 遍历地图中的每个图层
            while ((layer = enumLayer.Next()) != null)
            {
                // 判断图层是否为要素图层
                if (layer is IFeatureLayer)
                {
                    IFeatureLayer featureLayer = (IFeatureLayer)layer; // 将图层转换为要素图层

                    IFeatureClass featureClass = featureLayer.FeatureClass;

                    // 获取要素类型
                    if (featureClass.ShapeType == esriGeometryType.esriGeometryPoint)
                    {
                        pointCount += featureClass.FeatureCount(null);
                    }
                    else if (featureClass.ShapeType == esriGeometryType.esriGeometryPolyline)
                    {
                        polylineCount += featureClass.FeatureCount(null);
                    }
                    else if (featureClass.ShapeType == esriGeometryType.esriGeometryPolygon)
                    {
                        polygonCount += featureClass.FeatureCount(null);
                    }
                }
                else if (layer is IRasterLayer)
                {
                    // 栅格数据图层
                    rasterCount++;
                }
                else if (layer is IFeatureDataset)
                {
                    // 要素数据集
                    featureDatasetCount++;
                }
            }

            // 输出统计结果
            MessageBox.Show("点要素个数: " + pointCount + ", 线要素个数: " + polylineCount + ", 面要素个数: " + polygonCount + ", 栅格数据个数: " + rasterCount + ", 要素数据集个数: " + featureDatasetCount);
        }

        public override void OnClick()
        {
            try
            {
                GetCurrentMap(); // 获取当前地图

                // 检查地图是否为空
                if (currentMap == null)
                {
                    MessageBox.Show("地图未加载，请先加载地图。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }


                // 获取点线面个数
                CountFeatureTypesInDatabase();
                // 获取当前地图中所有图层的数据类型及其数量

                CountDataTypesInDatabase();

                SourceLayer = GetFeatureLayerByName(currentMap, SourceLayerName);

                // 检查是否找到了源图层
                if (SourceLayer == null)
                {
                    MessageBox.Show(
                        "未找到所需的图层，请确保地图中包含名为" + SourceLayer.Name + "的图层。", "错误", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                // 创建查询过滤器以选择 GB = "210400" 的要素
                IQueryFilter queryFilter = new QueryFilterClass
                {
                    WhereClause = "GB = 210400"
                };

                try
                {
                    // 获取所选要素的要素游标
                    IFeatureCursor featureCursor = SourceLayer.Search(queryFilter, false);

                    // 创建要素选择集
                    IFeatureSelection featureSelection = (IFeatureSelection)SourceLayer;
                    featureSelection.Clear(); // 清空之前的选择

                    if (featureCursor != null)
                    {
                        // 初始化变量以存储最长要素的信息
                        double maxFeatureLength = 0.0;
                        int maxFeatureOID = -1;

                        // 循环遍历所选要素
                        IFeature feature;
                        while ((feature = featureCursor.NextFeature()) != null)
                        {
                            // 添加筛选的要素到选择集中
                            featureSelection.Add(feature);

                            // 获取 "Name" 字段的字段索引，不区分大小写
                            int nameFieldIndex = feature.Fields.FindField("name");

                            // 获取 "RuleID" 字段的字段索引，不区分大小写
                            int ruleIDFieldIndex = feature.Fields.FindField("RuleID");

                            // 获取 "Name" 字段的值并进行 Trim() 操作
                            string nameFieldValue = feature.get_Value(nameFieldIndex).ToString().Trim();

                            // 获取 "RuleID" 字段的值并进行 Trim() 操作
                            string ruleIDFieldValue = feature.get_Value(ruleIDFieldIndex).ToString().Trim();

                            Console.WriteLine("nameFieldValue: " + nameFieldValue + "ruleIDFieldValue " + ruleIDFieldValue);

                            // 如果 "Name" 字段为空，则将其赋值为 "RuleID" 字段的值
                            if (string.IsNullOrEmpty(nameFieldValue))
                            {
                                // 将 "RuleID" 字段的值赋给 "Name" 字段
                                feature.set_Value(feature.Fields.FindField("name"), ruleIDFieldValue);
                                feature.Store(); // 保存要素更改
                            }

                            // 检查并更新最长要素信息
                            IGeometry featureGeometry = feature.Shape;
                            double featureLength = ((IPolyline)featureGeometry).Length;

                            if (featureLength > maxFeatureLength)
                            {
                                maxFeatureLength = featureLength;
                                maxFeatureOID = feature.OID;
                            }
                        }

                        // 输出最长要素的信息
                        MessageBox.Show("最长要素的长度为：" + maxFeatureLength.ToString());
                        MessageBox.Show("最长要素的OBJECTID为：" + maxFeatureOID.ToString());

                        // 刷新地图以显示选择集中的要素
                        currentMapControl.Refresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
                    }
                    else
                    {
                        MessageBox.Show("没有找到符合条件的要素");
                    }
                }
                catch (Exception ex)
                {
                    // 处理异常，可以输出异常信息提示用户
                    MessageBox.Show("发生异常：" + ex.Message);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

    }
}
