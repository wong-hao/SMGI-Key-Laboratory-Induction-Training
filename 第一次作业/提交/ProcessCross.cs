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
    public class ProcessCross : SMGICommand
    {
        public ProcessCross()
        {
            m_caption = "ProcessCross";
        }

        public override bool Enabled
        {
	        get 
	        { 
		         return true;
	        }
        }

        private AxMapControl currentMapControl;
        private IMap currentMap;    //当前MapControl控件中的Map对象    

        int length; //图层数量
        int SourcelyrFlag = 0;
        int TargetlyrFlag = 1;

        static IFeatureLayer[] featureLayersArray;
        static IFeatureClass[] featureClassesArray;
        static IFeatureCursor[] featureCursorsArray;
        static IFeature[] featuresArray;
        static String[] featureFieldNamesArray;
        static String[] featureFieldValuesArray;

        List<string> CrossingFieldValuesList = new List<string>();

        int crossingFeatureCount; // 记录穿过目标图层中元素的源图层中元素的数量
        private IFeatureSelection crossingFeatureSelection; // 记录穿过目标图层中元素的源图层中元素的选择集
        bool isModified; // 标记是否进行了赋值操作

        public override void OnClick()
        {
            try
            {
                GetCurrentMap();

                // 检查地图是否为空
                if (currentMap == null)
                {
                    MessageBox.Show("地图未加载，请先加载地图。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                length = currentMap.LayerCount;
                featureLayersArray = new IFeatureLayer[length];
                featureLayersArray[SourcelyrFlag] = GetFeatureLayerByName(currentMap, "RESA");
                featureLayersArray[TargetlyrFlag] = GetFeatureLayerByName(currentMap, "LRDL");

                if (featureLayersArray[SourcelyrFlag] == null || featureLayersArray[TargetlyrFlag] == null)
                {
                    MessageBox.Show("未找到所需的图层，请确保地图中包含名为" + featureLayersArray[SourcelyrFlag].Name + "和 " + featureLayersArray[TargetlyrFlag].Name + "的图层。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                featureFieldNamesArray = new String[length];
                featureFieldNamesArray[SourcelyrFlag] = "name";
                featureFieldNamesArray[TargetlyrFlag] = "class1";

                ProcessCrossing();

                // Refresh the map to show selected features
                currentMapControl.Refresh(esriViewDrawPhase.esriViewGeoSelection, null, null);

            }
            catch (Exception ex)
            {
                MessageBox.Show("发生错误：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void GetCurrentMap()
        {
            currentMapControl = m_Application.MapControl;
            currentMap = currentMapControl.Map;
        }

        // 在地图中根据图层名称获得矢量图层。
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
                //如果图层不是图层组类型，则直接进行判断
                else
                {
                    if (map.get_Layer(i).Name == layerName)
                        return (IFeatureLayer)map.get_Layer(i);
                }
            }
            return null;
        }

        // 主函数，用于处理源图层与目标图层的穿过情况并更新目标字段值
        public void ProcessCrossing()
        {
            featureClassesArray = new IFeatureClass[length];
            crossingFeatureSelection = (IFeatureSelection)featureLayersArray[TargetlyrFlag]; // 初始化选择集
            crossingFeatureCount = 0; // 初始化选择元素数量

            for (int i = 0; i <= length - 1; i++)
            {
                featureClassesArray[i] = featureLayersArray[i].FeatureClass;
            }

            // 遍历目标图层要素并处理穿过情况
            featureCursorsArray = new IFeatureCursor[length];
            featureCursorsArray[TargetlyrFlag] = featureClassesArray[TargetlyrFlag].Update(null, false);

            try
            {
                // 获取目标图层中的第一个要素
                featuresArray = new IFeature[length];
                featuresArray[TargetlyrFlag] = featureCursorsArray[TargetlyrFlag].NextFeature();

                // 遍历目标图层中的所有要素
                while (featuresArray[TargetlyrFlag] != null)
                {
                    // 对于该目标图层元素，获取需要填充到目标字段的源图层元素的源字段值集合
                    GetCrossingFeatureFieldValues();

                    // 初始化填充到目标字段的源图层元素的源字段值集合
                    featureFieldValuesArray = new String[length];
                    featureFieldValuesArray[TargetlyrFlag] = string.Empty;

                    // 根据填充到目标字段的源图层元素的源字段值集合判断该目标图层要素是否与源图层元素相交
                    if (CrossingFieldValuesList.Count != 0)
                    {
                        featureFieldValuesArray[TargetlyrFlag] = string.Join(",", CrossingFieldValuesList);
                        crossingFeatureSelection.Add(featuresArray[TargetlyrFlag]); // 添加到选择集
                        crossingFeatureCount++; // 相交元素数量增加
                    }
                    else
                    {
                        featureFieldValuesArray[TargetlyrFlag] = "未穿过居民地面"; // 设置未与源图层穿过的目标图层要素的目标字段值为“未穿过居民地面”
                    }

                    // 将填充到目标字段的源图层元素的源字段值填充到该目标图层要素的目标字段值
                    UpdateFeatureFieldValues();

                    // 查询目标图层中的下一个要素
                    featuresArray[TargetlyrFlag] = featureCursorsArray[TargetlyrFlag].NextFeature();
                }
            }
            finally
            {
                // 在 finally 块中确保释放游标资源
                ReleaseFeatureCursor(featureCursorsArray[TargetlyrFlag]);
            }

            // 获取与源图层穿过的目标图层中元素的数量
            MessageBox.Show("图层" + featureLayersArray[TargetlyrFlag].Name + "中与图层" + featureLayersArray[SourcelyrFlag].Name + "穿过的元素的总数：" + crossingFeatureCount, "统计数据", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 在循环结束后，只有进行了赋值操作才输出提示信息
            if (isModified)
            {
                // 输出字段赋值信息到弹出窗口
                MessageBox.Show("图层 " + featureLayersArray[TargetlyrFlag].Name + " 的字段 " + featureFieldNamesArray[TargetlyrFlag] + " 已被填充");
            }
            else
            {
                MessageBox.Show("图层 " + featureLayersArray[TargetlyrFlag].Name + " 的字段 " + featureFieldNamesArray[TargetlyrFlag] + " 非空，未进行填充");
            }
        }

        // 子函数，对于目标图层中的单个要素，获取其穿过的源图层中的所有要素的源字段值组成的列表
        private void GetCrossingFeatureFieldValues()
        {
            CrossingFieldValuesList.Clear();

            // 创建空间过滤器，查找与当前目标图层要素穿过的源图层要素
            ISpatialFilter pSpatialFilter = new SpatialFilterClass
            {
                Geometry = featuresArray[TargetlyrFlag].Shape,
                SpatialRel = esriSpatialRelEnum.esriSpatialRelCrosses
            };

            // 使用游标遍历与当前目标图层要素穿过的源图层要素
            featureCursorsArray[SourcelyrFlag] = featureClassesArray[SourcelyrFlag].Search(pSpatialFilter, false);

            // 查询第一个与当前目标图层穿过的源图层要素
            IFeature pFeatureCross = featureCursorsArray[SourcelyrFlag].NextFeature();

            try
            {
                // 遍历所有与当前目标图层穿过的源图层要素
                while (pFeatureCross != null)
                {
                    // 获取该源图层元素的源字段值
                    string fieldValue = GetFeatureFieldValue(pFeatureCross, featureFieldNamesArray[SourcelyrFlag]);

                    // 获取非重复的源字段值添加到列表
                    if (!string.IsNullOrEmpty(fieldValue) && !CrossingFieldValuesList.Contains(fieldValue))
                    {
                        CrossingFieldValuesList.Add(fieldValue);
                    }

                    // 查询下一个与当前目标图层穿过的源图层要素
                    pFeatureCross = featureCursorsArray[SourcelyrFlag].NextFeature();
                }
            }
            finally
            {
                // 在 finally 块中确保释放游标资源
                ReleaseFeatureCursor(featureCursorsArray[SourcelyrFlag]);
            }
        }

        // 子函数，对于目标图层中的单个要素，更新其目标字段值
        private void UpdateFeatureFieldValues()
        {
            // 获取目标字段的当前值
            string fieldValue = GetFeatureFieldValue(featuresArray[TargetlyrFlag], featureFieldNamesArray[TargetlyrFlag]);

            // 检查目标字段是否为空，如果为空则更新字段
            if (string.IsNullOrEmpty(fieldValue))
            {
                // 获取目标字段的索引
                int fieldIndex = featuresArray[TargetlyrFlag].Fields.FindField(featureFieldNamesArray[TargetlyrFlag]);

                // 若索引非空
                if (fieldIndex >= 0)
                {
                    // 对填充目标字段
                    featuresArray[TargetlyrFlag].set_Value(fieldIndex, featureFieldValuesArray[TargetlyrFlag]);
                    featureCursorsArray[TargetlyrFlag].UpdateFeature(featuresArray[TargetlyrFlag]);
                    isModified = true;
                }
            }
            else
            {
                isModified = false;
            }
        }

        // 获取要素的指定字段值
        private string GetFeatureFieldValue(IFeature feature, string fieldName)
        {
            int fieldIndex = feature.Fields.FindField(fieldName);

            if (fieldIndex >= 0)
            {
                object fieldValueObj = feature.get_Value(fieldIndex);
                if (fieldValueObj != null && fieldValueObj != DBNull.Value)
                {
                    return fieldValueObj.ToString();
                }
            }

            return string.Empty;
        }

        // 封装释放 IFeatureCursor 资源的子函数
        private void ReleaseFeatureCursor(IFeatureCursor cursor)
        {
            if (cursor != null)
            {
                Marshal.ReleaseComObject(cursor);
            }
        }
    }
}
