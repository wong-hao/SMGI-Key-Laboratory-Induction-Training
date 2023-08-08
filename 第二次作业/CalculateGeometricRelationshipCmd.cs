using System;
using System.Drawing;
using System.Collections.Generic;
using System.IO;
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
using Cursor = ESRI.ArcGIS.Geodatabase.Cursor;

namespace SMGI.Plugin.CartoExt
{
    public class CalculateGeometricRelationshipCmd : SMGICommand
    {
        public CalculateGeometricRelationshipCmd()
        {
            m_caption = "CalculateGeometricRelationshipCmd"; // 扩展的显示名称
        }

        public override bool Enabled
        {
            get { return true; }
        }

        private AxMapControl currentMapControl;
        private IMap currentMap; //当前MapControl控件中的Map对象    

        private static readonly string SourceLayerName = "polygon"; // 面图层名
        private IFeatureLayer SourceFeatureLayer; // 面图层

        private int FeatureCount; // 面图层所含要素个数
        private String[] featureFieldNamesArray = new string[] { "polygon1", "polygon2", "polygon3" }; // 存储面要素名的数组
        private IFeature[] SourceFeatureArray; // 存储面要素的数组
        Dictionary<string, IFeature> featureDictionary = new Dictionary<string, IFeature>(); //用于存储要素的名称和对应的要素对象的字典

        private String SourceFieldName = "name"; // 面要素名称字段
        private int SourceFieldIndex; // 面要素名称字段索引

        int fieldIndex; // 字段索引

        static string filePath = "output.txt"; // 输出文件名

        // 计算几何关系
        public static void GetGeometricRelationship(string polygonName1, string polygonName2, IGeometry geometry1, IGeometry geometry2)
        {
            // 使用 IRelationalOperator 接口来计算两个几何对象之间的空间关系
            IRelationalOperator relationalOperator = geometry1 as IRelationalOperator;

            bool intersects = !relationalOperator.Disjoint(geometry2);
            bool crosses = relationalOperator.Crosses(geometry2);
            bool overlaps = relationalOperator.Overlaps(geometry2);
            bool contains = relationalOperator.Contains(geometry2);
            bool within = relationalOperator.Within(geometry2);

            string result = polygonName1 + "与" + polygonName2 + "空间相交关系为" + intersects + "\n" +
                            polygonName1 + "与" + polygonName2 + "空间重叠关系为" + (crosses || overlaps) + "\n" +
                            polygonName1 + "与" + polygonName2 + "空间包含关系为" + contains + "\n" +
                            polygonName1 + "与" + polygonName2 + "空间属于关系为" + within;

            // 将结果追加到txt文件，并在末尾添加换行符
            File.AppendAllText(filePath, result + Environment.NewLine);
        }

        // 获取所有面要素，并存储在面要素数组中
        private void ExtractFeatureArray()
        {
            // 创建字典，用于将名称与要素进行关联
            Dictionary<string, IFeature> featureDictionary = new Dictionary<string, IFeature>();

            // 使用null作为查询过滤器得到图层中所有要素的游标
            IFeatureCursor featureCursor = SourceFeatureLayer.Search(null, false);
            IFeature feature = featureCursor.NextFeature();

            try
            {
                while (feature != null)
                {
                    // 获取“name”字段的内容
                    string nameValue = GetFeatureFieldValue(feature, SourceFieldName);

                    // 将要素按照名称存储到字典中
                    featureDictionary[nameValue] = feature;

                    feature = featureCursor.NextFeature();
                }
            }
            finally
            {
                // 在 finally 块中确保释放游标资源
                ReleaseFeatureCursor(featureCursor);
            }

            // 根据字段名数组的顺序，将对应的要素存储到要素数组中
            SourceFeatureArray = new IFeature[featureFieldNamesArray.Length];
            for (int i = 0; i < featureFieldNamesArray.Length; i++)
            {
                string nameValue = featureFieldNamesArray[i];
                if (featureDictionary.ContainsKey(nameValue))
                {
                    SourceFeatureArray[i] = featureDictionary[nameValue];
                }
                else
                {
                    // 处理图层中不存在字段名数组中名称的情况。
                    MessageBox.Show("未找到名为" + nameValue+"的要素。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }
        }

        // 执行几何关系查询并将结果保存到文件
        public void PerformGeometricRelationshipQueries()
        {
            for (int i = 1; i <= FeatureCount - 1; i++)
            {
                GetGeometricRelationship(featureFieldNamesArray[0], featureFieldNamesArray[i], SourceFeatureArray[0].Shape, SourceFeatureArray[i].Shape);
            }
        }

        /// <Date>2023/7/28</Date>
        /// <Author>HaoWong</Author>
        /// <summary>
        /// 点击扩展按钮时执行的操作
        /// </summary>
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
                SourceFeatureLayer = GetFeatureLayerByName(currentMap, SourceLayerName);

                // 检查是否找到了图层
                if (SourceFeatureLayer == null )
                {
                    MessageBox.Show(
                        "未找到所需的图层，请确保地图中包含名为" + SourceLayerName + "的图层。", "错误", MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                // 检查是否找到了面图层
                if (SourceFeatureLayer.FeatureClass.ShapeType != esriGeometryType.esriGeometryPolygon)
                {
                    MessageBox.Show("寻找到的图层并非面图层！");
                        return;
                }

                // 获得所有面要素的个数
                FeatureCount = SourceFeatureLayer.FeatureClass.FeatureCount(null);

                // 初始化面要素数组
                SourceFeatureArray = new IFeature[FeatureCount];

                ExtractFeatureArray();

                PerformGeometricRelationshipQueries();

                MessageBox.Show("要素间的空间关系已判断完成，并保存在" + filePath + "文件中");

            }
            catch (Exception ex)
            {
                MessageBox.Show("发生错误：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <Date>2023/7/28</Date>
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

        /// <Date>2023/7/28</Date>
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


        /// <Date>2023/7/28</Date>
        /// <Author>HaoWong</Author>
        /// <summary>
        /// 子函数，获取要素的指定字段值
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="fieldName"></param>
        private string GetFeatureFieldValue(IFeature feature, string fieldName)
        {
            // 根据字段名获取到字段索引
            int FieledIndex = GetFieldIndex(feature, fieldName);

            // 若索引非空
            if (FieledIndex >= 0)
            {
                // 根据字段索引获取到字段值
                object fieldValueObj = feature.get_Value(FieledIndex);

                // 对获取到的字段值进行判空处理
                if (fieldValueObj != null && fieldValueObj != DBNull.Value)
                {
                    return fieldValueObj.ToString();
                }
            }

            return string.Empty;
        }

        /// <Date>2023/7/28</Date>
        /// <Author>HaoWong</Author>
        /// <summary>
        /// 子函数，获取要素类中指定字段的索引
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="fieldName"></param>
        private int GetFieldIndex(IFeature feature, string fieldName)
        {
            return feature.Fields.FindField(fieldName);
        }

        /// <Date>2023/7/28</Date>
        /// <Author>HaoWong</Author>
        /// <summary>
        /// 子函数，封装释放IFeatureCursor资源
        /// </summary>
        /// <param name="cursor"></param>
        private void ReleaseFeatureCursor(IFeatureCursor cursor)
        {
            if (cursor != null)
            {
                Marshal.ReleaseComObject(cursor);
            }
        }
    }
}