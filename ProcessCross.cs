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

        int length;
        static IFeatureLayer[] featureLayersArray;
        static IFeatureClass[] featureClassesArray;
        static IFeatureCursor[] featureCursorsArray;
        static IFeature[] featuresArray;
        static String[] fieldNamesArray;
        static int[] fieldIndexArray;
        static String[] fieldValuesArray;

        List<string> CrossingFieldValuesList = new List<string>();

        public override void OnClick()
        {
            GetCurrentMap();

            length = currentMap.LayerCount;
            featureLayersArray = new IFeatureLayer[length];
            featureLayersArray[0] = GetFeatureLayerByName(currentMap, "RESA");
            featureLayersArray[1] = GetFeatureLayerByName(currentMap, "LRDL");

            fieldNamesArray = new String[length];
            fieldNamesArray[0] = "name";
            fieldNamesArray[1] = "class1";

            ProcessCrossing();
            MessageBox.Show("属性表赋值完成");
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

            for (int i = 0; i <= length - 1; i++)
            {

                featureClassesArray[i] = featureLayersArray[i].FeatureClass;
            }

            // 记录与 A 图层穿过的 B 图层中元素的数量
            int crossingBFeatureCount = 0;

            // 遍历 B 图层要素并处理穿过情况
            featureCursorsArray = new IFeatureCursor[length];
            featureCursorsArray[1] = featureClassesArray[1].Update(null, false);

            // 获取 B 图层中的第一个要素
            featuresArray = new IFeature[length];
            featuresArray[1] = featureCursorsArray[1].NextFeature();

            // 遍历 B 图层中的所有要素
            while (featuresArray[1] != null)
            {
                GetCrossingFieldValues();

                // 设置未与 A 图层穿过的 B 图层要素的 "b" 字段值为“未穿过居民地面”
                fieldValuesArray[1] = string.Empty;
                if (CrossingFieldValuesList.Count == 0)
                {
                    fieldValuesArray[1] = "未穿过居民地面";
                }
                else
                {
                    fieldValuesArray[1] = string.Join(",", CrossingFieldValuesList);
                    crossingBFeatureCount++; // 直接在这里统计与 A 图层穿过的 B 图层要素数量
                }

                // 更新 B 图层要素的 "b" 字段值
                UpdateFieldValues();
                
                // 查询 B 图层中的下一个要素
                featuresArray[1] = featureCursorsArray[1].NextFeature();
            }

            // 获取与 A 图层穿过的 B 图层中元素的数量
            MessageBox.Show("与 " + featureLayersArray[0].Name + " 图层穿过的 " + featureLayersArray[1].Name + " 图层中元素的总数：" + crossingBFeatureCount, "统计数据", MessageBoxButtons.OK, MessageBoxIcon.Information);

        }

        // 子函数，对于 B 图层中的单个要素，获取其穿过的 A 图层中的所有要素的 "a" 字段值列表
        private void GetCrossingFieldValues()
        {
            CrossingFieldValuesList.Clear();

            // 创建空间过滤器，查找与当前 B 图层要素穿过的 A 图层要素
            ISpatialFilter pSpatialFilter = new SpatialFilterClass
            {
                Geometry = featuresArray[1].Shape,
                SpatialRel = esriSpatialRelEnum.esriSpatialRelCrosses
            };

            // 使用游标遍历与当前 B 图层要素穿过的 A 图层要素
            featureCursorsArray[0] = featureClassesArray[0].Search(pSpatialFilter, false);

            // 查询第一个与当前 B 图层穿过的 A 图层要素
            IFeature pFeatureCross = featureCursorsArray[0].NextFeature();

            // 遍历所有与当前 B 图层穿过的 A 图层要素
            while (pFeatureCross != null)
            {
                fieldIndexArray = new int[length];
                fieldIndexArray[0] = pFeatureCross.Fields.FindField(fieldNamesArray[0]);

                fieldValuesArray = new String[length];
                fieldValuesArray[0] = pFeatureCross.get_Value(fieldIndexArray[0]).ToString();

                // 如果 "a" 字段值在列表中不存在，则添加到列表
                if (!CrossingFieldValuesList.Contains(fieldValuesArray[0]))
                {
                    CrossingFieldValuesList.Add(fieldValuesArray[0]);
                }

                // 查询下一个与当前 B 图层穿过的 A 图层要素
                pFeatureCross = featureCursorsArray[0].NextFeature();
            };
        }

        // 子函数，对于 B 图层中的单个要素，更新其 "b" 字段值
        private void UpdateFieldValues()
        {
            fieldIndexArray[1] = featuresArray[1].Fields.FindField(fieldNamesArray[1]);
            featuresArray[1].set_Value(fieldIndexArray[1], fieldValuesArray[1]);
            featureCursorsArray[1].UpdateFeature(featuresArray[1]);
        }

    }
}
