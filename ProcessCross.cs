using System;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web;
using System.Windows.Forms;
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
        int RESAlyrFlag = 0;
        int LRDLlyrFlag = 1;

        static IFeatureLayer[] featureLayersArray;
        static IFeatureClass[] featureClassesArray;
        static IFeatureCursor[] featureCursorsArray;
        static IFeature[] featuresArray;
        static String[] featureFieldNamesArray;
        static int[] featureFieldIndexsArray;
        static String[] featureFieldValuesArray;

        List<string> CrossingFieldValuesList = new List<string>();

        int crossingFeatureCount; // 记录穿过 B 图层中元素的 A 图层中元素的数量
        private IFeatureSelection crossingFeatureSelection; // 记录穿过 B 图层中元素的 A 图层中元素的选择集
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
                featureLayersArray[RESAlyrFlag] = GetFeatureLayerByName(currentMap, "RESA");
                featureLayersArray[LRDLlyrFlag] = GetFeatureLayerByName(currentMap, "LRDL");

                if (featureLayersArray[RESAlyrFlag] == null || featureLayersArray[LRDLlyrFlag] == null)
                {
                    MessageBox.Show("未找到所需的图层，请确保地图中包含名为" + featureLayersArray[RESAlyrFlag].Name + "和 " + featureLayersArray[LRDLlyrFlag].Name + "的图层。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                featureFieldNamesArray = new String[length];
                featureFieldNamesArray[RESAlyrFlag] = "name";
                featureFieldNamesArray[LRDLlyrFlag] = "class1";

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

        // 主函数，用于处理 A 图层与 B 图层的穿过情况并更新 "b" 字段值
        public void ProcessCrossing()
        {
            featureClassesArray = new IFeatureClass[length];
            crossingFeatureSelection = (IFeatureSelection)featureLayersArray[LRDLlyrFlag]; // 初始化选择集
            crossingFeatureCount = 0; // 初始化选择元素数量

            for (int i = 0; i <= length - 1; i++)
            {
                featureClassesArray[i] = featureLayersArray[i].FeatureClass;
            }

            // 遍历 B 图层要素并处理穿过情况
            featureCursorsArray = new IFeatureCursor[length];
            featureCursorsArray[LRDLlyrFlag] = featureClassesArray[LRDLlyrFlag].Update(null, false);

            // 获取 B 图层中的第一个要素
            featuresArray = new IFeature[length];
            featuresArray[LRDLlyrFlag] = featureCursorsArray[LRDLlyrFlag].NextFeature();

            // 遍历 B 图层中的所有要素
            while (featuresArray[LRDLlyrFlag] != null)
            {
                GetCrossingFeatureFieldValues();

                // 初始化字段值
                featureFieldValuesArray[LRDLlyrFlag] = string.Empty;

                // 如果该要素与 A 图层相交
                if (CrossingFieldValuesList.Count != 0)
                {
                    featureFieldValuesArray[LRDLlyrFlag] = string.Join(",", CrossingFieldValuesList);
                    crossingFeatureSelection.Add(featuresArray[LRDLlyrFlag]); //添加到选择集
                    crossingFeatureCount++; //相交元素数量增加
                }
                else
                {
                    featureFieldValuesArray[LRDLlyrFlag] = "未穿过居民地面"; // 设置未与 A 图层穿过的 B 图层要素的 "b" 字段值为“未穿过居民地面”
                }

                // 更新 B 图层要素的 "b" 字段值
                UpdateFeatureFieldValues();
                
                // 查询 B 图层中的下一个要素
                featuresArray[LRDLlyrFlag] = featureCursorsArray[LRDLlyrFlag].NextFeature();
            }

            // 获取与 A 图层穿过的 B 图层中元素的数量
            MessageBox.Show("图层" + featureLayersArray[LRDLlyrFlag].Name + "中与图层"+featureLayersArray[LRDLlyrFlag].Name+"穿过的元素的总数：" + crossingFeatureCount, "统计数据", MessageBoxButtons.OK, MessageBoxIcon.Information);

            // 在循环结束后，只有进行了赋值操作才输出提示信息
            if (isModified)
            {
                // 输出字段赋值信息到弹出窗口
                MessageBox.Show("图层 " + featureLayersArray[LRDLlyrFlag].Name + " 的字段 " + featureFieldNamesArray[LRDLlyrFlag] + " 已被修改");
            }
            else
            {
                MessageBox.Show("图层 " + featureLayersArray[LRDLlyrFlag].Name + " 的字段 " + featureFieldNamesArray[LRDLlyrFlag] + " 非空，未进行修改");
            }
        }

        // 子函数，对于 B 图层中的单个要素，获取其穿过的 A 图层中的所有要素的 "a" 字段值组成的列表
        private void GetCrossingFeatureFieldValues()
        {
            CrossingFieldValuesList.Clear();

            // 创建空间过滤器，查找与当前 B 图层要素穿过的 A 图层要素
            ISpatialFilter pSpatialFilter = new SpatialFilterClass
            {
                Geometry = featuresArray[LRDLlyrFlag].Shape,
                SpatialRel = esriSpatialRelEnum.esriSpatialRelCrosses
            };

            // 使用游标遍历与当前 B 图层要素穿过的 A 图层要素
            featureCursorsArray[RESAlyrFlag] = featureClassesArray[RESAlyrFlag].Search(pSpatialFilter, false);

            // 查询第一个与当前 B 图层穿过的 A 图层要素
            IFeature pFeatureCross = featureCursorsArray[RESAlyrFlag].NextFeature();

            // 遍历所有与当前 B 图层穿过的 A 图层要素
            while (pFeatureCross != null)
            {
                featureFieldIndexsArray = new int[length];
                featureFieldIndexsArray[RESAlyrFlag] = pFeatureCross.Fields.FindField(featureFieldNamesArray[RESAlyrFlag]);

                featureFieldValuesArray = new String[length];
                featureFieldValuesArray[RESAlyrFlag] = pFeatureCross.get_Value(featureFieldIndexsArray[RESAlyrFlag]).ToString();

                // 如果 "a" 字段值在列表中不存在，则添加到列表
                if (!CrossingFieldValuesList.Contains(featureFieldValuesArray[RESAlyrFlag]))
                {
                    CrossingFieldValuesList.Add(featureFieldValuesArray[RESAlyrFlag]);
                }

                // 查询下一个与当前 B 图层穿过的 A 图层要素
                pFeatureCross = featureCursorsArray[RESAlyrFlag].NextFeature();
            };
        }

        // 子函数，对于 B 图层中的单个要素，更新其 "b" 字段值
        private void UpdateFeatureFieldValues()
        {
            featureFieldIndexsArray[LRDLlyrFlag] = featuresArray[LRDLlyrFlag].Fields.FindField(featureFieldNamesArray[LRDLlyrFlag]);
            object fieldValueObj = featuresArray[LRDLlyrFlag].get_Value(featureFieldIndexsArray[LRDLlyrFlag]);

            // 检查 "b" 字段是否为空，如果为空则更新字段
            if (fieldValueObj == null || DBNull.Value.Equals(fieldValueObj) || string.IsNullOrEmpty(fieldValueObj.ToString()))
            {
                string fieldName = featureFieldNamesArray[LRDLlyrFlag];
                string fieldValue = featureFieldValuesArray[LRDLlyrFlag];
                featuresArray[LRDLlyrFlag].set_Value(featureFieldIndexsArray[LRDLlyrFlag], fieldValue);
                featureCursorsArray[LRDLlyrFlag].UpdateFeature(featuresArray[LRDLlyrFlag]);
                isModified = true;
            } else{
                isModified = false;
            }
        }
    }
}
