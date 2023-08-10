using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using SMGI.Common;

namespace SMGI.Plugin.CartoExt
{
    public class QueryGeometricRelationshipCmd : SMGICommand
    {
        private static readonly string SourceLayerName = "polygon"; // 面图层名

        private static string filePath; // 输出文件名

        private readonly string[] featureFieldNamesArray = { "polygon1", "polygon2", "polygon3" }; // 存储面要素名的数组

        private readonly string SourceFieldName = "name"; // 面要素名称字段
        private IMap currentMap; //当前MapControl控件中的Map对象    

        private AxMapControl currentMapControl;

        private int FeatureCount; // 面图层所含要素个数

        private Dictionary<string, IFeature>
            featureDictionary = new Dictionary<string, IFeature>(); //用于存储要素的名称和对应的要素对象的字典

        private int fieldIndex; // 字段索引
        private IFeature[] SourceFeatureArray; // 存储面要素的数组
        private IFeatureLayer SourceFeatureLayer; // 面图层
        private int SourceFieldIndex; // 面要素名称字段索引

        public QueryGeometricRelationshipCmd()
        {
            m_caption = "QueryGeometricRelationshipCmd"; // 扩展的显示名称
        }

        public override bool Enabled
        {
            get { return true; }
        }

        // 指定的文本文件中追加一个字符串
        public void AppendResult(string result)
        {
            MessageBox.Show(result, "结果", MessageBoxButtons.OK, MessageBoxIcon.Information);
            File.AppendAllText(filePath, result + Environment.NewLine);
        }

        // 计算两个几何体之间的关系并存储进文本
        public void CalculateAndAppendRelation(IGeometry geometry1, IGeometry geometry2, string polygonName1,
            string polygonName2, Func<IRelationalOperator, bool> relationFunc, string relationName)
        {
            var relationalOperator = geometry1 as IRelationalOperator;
            var relationResult = relationFunc(relationalOperator);

            var result = polygonName1 + "与" + polygonName2 + "空间相交关系为" + relationResult;

            AppendResult(result);
        }

        // 计算两个几何体的不同关系（相交、重叠、包含、属于）
        public void GetGeometricRelationship(string polygonName1, string polygonName2, IGeometry geometry1,
            IGeometry geometry2)
        {
            CalculateAndAppendRelation(geometry1, geometry2, polygonName1, polygonName2, op => !op.Disjoint(geometry2),
                "相交");
            CalculateAndAppendRelation(geometry1, geometry2, polygonName1, polygonName2, op => op.Overlaps(geometry2),
                "重叠");
            CalculateAndAppendRelation(geometry1, geometry2, polygonName1, polygonName2, op => op.Contains(geometry2),
                "包含");
            CalculateAndAppendRelation(geometry1, geometry2, polygonName1, polygonName2, op => op.Contains(geometry2),
                "属于");
        }

        /// <summary>
        ///     从指定要素图层中提取要素并存储到字典中，以“name”字段的值作为键。
        /// </summary>
        /// <param name="featureLayer">要素图层</param>
        /// <returns>以“name”字段的值为键的要素字典</returns>
        private Dictionary<string, IFeature> ExtractFeatureDictionary(IFeatureLayer featureLayer)
        {
            var featureDictionary = new Dictionary<string, IFeature>();

            // 获取要素游标以遍历要素图层
            var featureCursor = featureLayer.Search(null, false);
            var feature = featureCursor.NextFeature();

            try
            {
                while (feature != null)
                {
                    // 获取要素的“name”字段的值
                    var nameValue = GetFeatureFieldValue(feature, SourceFieldName);

                    // 将要素添加到字典，以“name”字段的值作为键
                    featureDictionary[nameValue] = feature;

                    feature = featureCursor.NextFeature();
                }
            }
            finally
            {
                // 释放要素游标资源
                ReleaseFeatureCursor(featureCursor);
            }

            return featureDictionary;
        }

        /// <summary>
        ///     根据字段名数组的顺序构建要素数组。
        /// </summary>
        /// <param name="featureDictionary">以“name”字段的值为键的要素字典</param>
        /// <returns>构建的要素数组</returns>
        private IFeature[] BuildSourceFeatureArray(Dictionary<string, IFeature> featureDictionary)
        {
            var sourceFeatureArray = new IFeature[featureFieldNamesArray.Length];

            for (var i = 0; i < featureFieldNamesArray.Length; i++)
            {
                var nameValue = featureFieldNamesArray[i];

                if (featureDictionary.ContainsKey(nameValue))
                {
                    sourceFeatureArray[i] = featureDictionary[nameValue];
                }
                else
                {
                    // 如果未找到指定名称的要素，显示警告
                    MessageBox.Show("未找到名为" + nameValue + "的要素。", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                    // 返回空数组，表示未成功构建要素数组
                    return null;
                }
            }

            return sourceFeatureArray;
        }

        /// <Date>2023/8/08</Date>
        /// <Author>HaoWong</Author>
        /// <summary>
        ///     执行几何关系查询并将结果保存到文件
        /// </summary>
        /// <returns></returns>
        public void QueryGeometricRelationship()
        {
            for (var i = 1; i <= FeatureCount - 1; i++)
                GetGeometricRelationship(featureFieldNamesArray[0], featureFieldNamesArray[i],
                    SourceFeatureArray[0].Shape, SourceFeatureArray[i].Shape);
        }

        /// <Date>2023/8/08</Date>
        /// <Author>HaoWong</Author>
        /// <summary>
        ///     文件操作
        /// </summary>
        /// <returns></returns>
        public void SaveFileWithDialog()
        {
            // 创建 SaveFileDialog 对象
            var saveFileDialog = new SaveFileDialog();

            // 设置对话框的标题
            saveFileDialog.Title = "选择存储文件的路径和名称";

            // 设置对话框的过滤器，以限制用户可以保存的文件类型
            saveFileDialog.Filter = "文本文件 (*.txt)|*.txt";

            // 显示对话框，等待用户选择文件路径和名称
            if (saveFileDialog.ShowDialog() == DialogResult.OK)
                // 获取用户选择的文件路径和名称
                filePath = saveFileDialog.FileName;
            else
                MessageBox.Show("用户取消了保存操作。", "取消保存", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <Date>2023/7/28</Date>
        /// <Author>HaoWong</Author>
        /// <summary>
        ///     点击扩展按钮时执行的操作
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
                if (SourceFeatureLayer == null)
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

                // 提取面要素并存储
                SourceFeatureArray = BuildSourceFeatureArray(ExtractFeatureDictionary(SourceFeatureLayer));

                // 获取文件保存路径
                SaveFileWithDialog();

                // 执行几何关系查询并将结果保存到文件
                QueryGeometricRelationship();

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
        ///     子函数，得到目前图层
        /// </summary>
        private void GetCurrentMap()
        {
            currentMapControl = m_Application.MapControl;

            // 确保当前地图控件和地图对象不为空
            if (currentMapControl != null) currentMap = currentMapControl.Map;
        }

        /// <Date>2023/7/28</Date>
        /// <Author>HaoWong</Author>
        /// <summary>
        ///     在地图中根据图层名称获得矢量图层
        /// </summary>
        /// <param name="map"></param>
        /// <param name="layerName"></param>
        private IFeatureLayer GetFeatureLayerByName(IMap map, string layerName)
        {
            //对地图中的图层进行遍历
            for (var i = 0; i < map.LayerCount; i++)
                //如果该图层为图层组类型，则分别对所包含的每个图层进行操作
                if (map.get_Layer(i) is GroupLayer)
                {
                    //使用ICompositeLayer接口进行遍历操作
                    var compositeLayer = map.get_Layer(i) as ICompositeLayer;
                    for (var j = 0; j < compositeLayer.Count; j++)
                        //如果图层名称为所要查询的图层名称，则返回IFeatureLayer接口的矢量图层对象
                        if (compositeLayer.get_Layer(j).Name == layerName)
                            return (IFeatureLayer)compositeLayer.get_Layer(j);
                }
                // 如果图层不是图层组类型，则直接进行判断
                else
                {
                    if (map.get_Layer(i).Name == layerName)
                        return (IFeatureLayer)map.get_Layer(i);
                }

            return null;
        }

        /// <Date>2023/7/28</Date>
        /// <Author>HaoWong</Author>
        /// <summary>
        ///     子函数，获取要素的指定字段值
        /// </summary>
        /// <param name="feature"></param>
        /// <param name="fieldName"></param>
        private string GetFeatureFieldValue(IFeature feature, string fieldName)
        {
            // 根据字段名获取到字段索引
            var FieledIndex = GetFieldIndex(feature, fieldName);

            // 若索引非空
            if (FieledIndex >= 0)
            {
                // 根据字段索引获取到字段值
                var fieldValueObj = feature.get_Value(FieledIndex);

                // 对获取到的字段值进行判空处理
                if (fieldValueObj != null && fieldValueObj != DBNull.Value) return fieldValueObj.ToString();
            }

            return string.Empty;
        }

        /// <Date>2023/7/28</Date>
        /// <Author>HaoWong</Author>
        /// <summary>
        ///     子函数，获取要素类中指定字段的索引
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
        ///     子函数，封装释放IFeatureCursor资源
        /// </summary>
        /// <param name="cursor"></param>
        private void ReleaseFeatureCursor(IFeatureCursor cursor)
        {
            if (cursor != null) Marshal.ReleaseComObject(cursor);
        }
    }
}