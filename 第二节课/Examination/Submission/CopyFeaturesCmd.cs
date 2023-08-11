using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Geodatabase;
using SMGI.Common;

/* 技术路线
1. 获取当前地图：
通过调用GetCurrentMap()方法，获取当前的GIS地图对象。
 
2. 初始化图层：
根据预定义的图层名称，使用GetFeatureLayerByName()方法获取源面图层、目标面图层以及查询线图层的引用。
检查是否找到了所需的图层。

3. 复制要素：
使用CopyFeatures方法进行复制操作。
首先，获取源面图层的要素类和目标面图层的要素类。
创建游标遍历源面图层中的要素。
检查目标面图层是否为空，如果不为空，则不执行复制操作。
遍历源面图层中的每个要素，为每个要素创建一个新的要素，并使用CopyFeature方法将几何和属性复制到新要素中：

- 复制几何形状：
通过 sourceFeature.ShapeCopy 获得源要素的几何形状（Geometry）的副本。
将这个几何形状赋值给目标要素的几何形状（targetFeature.Shape），这样目标要素的空间位置就与源要素相同。

- 复制属性：
通过循环遍历源要素的字段（Fields），获取每个可编辑字段的值，并将其赋值给目标要素对应的字段。
这确保了目标要素的属性与源要素的属性保持一致。

- 插入新要素：
调用 targetFeature.Store() 方法，将目标要素插入到目标要素类中。
这样新的目标要素就成功地复制了源要素的几何和属性信息。
 
- 将新要素存储到目标面图层。

4. 错误处理：
在各个步骤中，使用异常处理来捕获可能发生的错误，并在错误发生时显示相应的错误消息。

5. 资源释放：
在完成复制操作后，通过调用ReleaseFeatureCursor()方法释放要素游标的资源，确保资源正确释放。
 */
namespace SMGI.Plugin.CartoExt
{
    public class CopyFeaturesCmd : SMGICommand
    {
        private static readonly string HYDLLayerName = "HYDL"; // 源面图层名
        private static readonly string HYDLEmptyLayerName = "HYDL_Empty"; // 目标面图层名
        private static readonly string LRDLLayerName = "LRDL"; // 查询线图层名

        private IMap currentMap; //当前MapControl控件中的Map对象    

        private AxMapControl currentMapControl;

        private IFeatureClass featureClassLRDL; // 查询线图层要素类

        private IFeatureCursor featureCursorHYDL;

        private Dictionary<string, IFeature>
            featureDictionary = new Dictionary<string, IFeature>(); //用于存储要素的名称和对应的要素对象的字典

        private IFeature featureHYDL;

        private IFeatureLayer HYDLEmptyFeatureLayer; // 目标面图层
        private IFeatureLayer HYDLFeatureLayer; // 源面图层
        private IFeatureLayer LRDLFeatureLayer; // 查询线图层

        public CopyFeaturesCmd()
        {
            m_caption = "QueryBySpatialCmd"; // 扩展的显示名称
        }

        public override bool Enabled
        {
            get { return true; }
        }


        // 在 QueryBySpatialCmd 类中添加以下私有方法
        private void CopyFeature(IFeature sourceFeature, IFeature targetFeature)
        {
            // 复制几何
            targetFeature.Shape = sourceFeature.ShapeCopy;

            // 复制属性
            for (var i = 0; i < sourceFeature.Fields.FieldCount; i++)
                if (sourceFeature.Fields.get_Field(i).Editable)
                    targetFeature.set_Value(i, sourceFeature.get_Value(i));

            // 插入新要素
            targetFeature.Store();
        }

        private void CopyFeatures()
        {
            // 获取要素类
            var featureClassHYDL = HYDLFeatureLayer.FeatureClass;
            var featureClassHYDLEmpty = HYDLEmptyFeatureLayer.FeatureClass;

            // 创建游标遍历源图层中的要素
            featureCursorHYDL = featureClassHYDL.Search(null, false);
            featureHYDL = featureCursorHYDL.NextFeature();

            // 检查是否已经有要素在目标图层  中
            if (HYDLEmptyFeatureLayer.FeatureClass.FeatureCount(null) > 0)
            {
                MessageBox.Show("目标图层" + HYDLEmptyLayerName + "非空，无需复制!", "提示", MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                return; // 退出方法，不执行复制操作
            }

            // 开始复制
            while (featureHYDL != null)
            {
                // 创建新要素
                var newFeature = featureClassHYDLEmpty.CreateFeature();

                // 使用 CopyFeature 方法复制要素
                CopyFeature(featureHYDL, newFeature);

                // 获取下一个要素
                featureHYDL = featureCursorHYDL.NextFeature();
            }

            MessageBox.Show("已将源图层" + HYDLLayerName + "中的所有要素全部复制到" + HYDLEmptyLayerName + "中!", "复制操作",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                HYDLFeatureLayer = GetFeatureLayerByName(currentMap, HYDLLayerName);
                HYDLEmptyFeatureLayer = GetFeatureLayerByName(currentMap, HYDLEmptyLayerName);
                LRDLFeatureLayer = GetFeatureLayerByName(currentMap, LRDLLayerName);

                // 检查是否找到了图层
                if (HYDLFeatureLayer == null || HYDLEmptyFeatureLayer == null || LRDLFeatureLayer == null)
                {
                    MessageBox.Show(
                        "未找到所需的图层，请确保地图中包含名为" + HYDLLayerName + "," + HYDLEmptyLayerName + "和" + LRDLFeatureLayer +
                        "的图层。", "错误",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                CopyFeatures();
            }
            catch (Exception ex)
            {
                MessageBox.Show("发生错误：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            finally
            {
                // 释放资源
                ReleaseFeatureCursor(featureCursorHYDL);
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