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
            m_caption = "ProcessCross"; // 扩展的显示名称
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
        int SourcelyrFlag = 0; // 源图层标志
        int TargetlyrFlag = 1; // 目标图层标志

        static IFeatureLayer[] featureLayersArray; // 存储要素图层的数组
        static IFeatureClass[] featureClassesArray; // 存储要素类的数组
        static IFeatureCursor[] featureCursorsArray; // 存储要素游标的数组
        IFeature featureTarget; // 存储目标要素
        static String[] featureFieldNamesArray; // 存储要素字段名的数组
        String featureFieldValue = string.Empty; // 存储填充到目标字段的源图层元素的源字段值

        int fieldIndex; // 字段索引

        List<string> crossingFieldValuesList = new List<string>(); // 存储与目标图层相交的源图层要素字段值的列表作为缓冲区
        int crossingFeatureCount; // 记录穿过目标图层中元素的源图层中元素的数量
        private IFeatureSelection crossingFeatureSelection; // 记录穿过目标图层中元素的源图层中元素的选择集
        bool isModified; // 标记是否进行了赋值操作

        // 点击扩展按钮时执行的操作
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

                length = currentMap.LayerCount; // 获取地图图层数量
                featureLayersArray = new IFeatureLayer[length]; // 初始化要素图层数组
                featureLayersArray[SourcelyrFlag] = GetFeatureLayerByName(currentMap, "RESA"); // 获取源图层
                featureLayersArray[TargetlyrFlag] = GetFeatureLayerByName(currentMap, "LRDL"); // 获取目标图层

                // 检查是否找到了源图层和目标图层
                if (featureLayersArray[SourcelyrFlag] == null || featureLayersArray[TargetlyrFlag] == null)
                {
                    MessageBox.Show("未找到所需的图层，请确保地图中包含名为" + featureLayersArray[SourcelyrFlag].Name + "和 " + featureLayersArray[TargetlyrFlag].Name + "的图层。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                featureFieldNamesArray = new String[length]; // 初始化要素字段名数组 
                featureFieldNamesArray[SourcelyrFlag] = "name"; // 设置源图层字段名
                featureFieldNamesArray[TargetlyrFlag] = "class1"; // 设置目标图层字段名

                ProcessCrossing(); // 处理图层穿过操作

                // 刷新地图以显示选择的要素
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
                // 如果图层不是图层组类型，则直接进行判断
                else
                {
                    if (map.get_Layer(i).Name == layerName)
                        return (IFeatureLayer)map.get_Layer(i);
                }
            }
            return null;
        }

        // 主函数，用于处理源图层与目标图层的穿过情况并赋值目标字段值
        public void ProcessCrossing()
        {
            featureClassesArray = new IFeatureClass[length]; // 初始化要素类数组
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
                featureTarget = featureCursorsArray[TargetlyrFlag].NextFeature(); // 获取目标图层中的第一个要素，用以初始化遍历循环

                // 遍历目标图层中的所有要素
                while (featureTarget != null)
                {
                    // 对于该目标图层元素，获取需要填充到目标字段的源图层元素的源字段值集合作为缓冲区
                    GetCrossingFeatureFieldValues();

                    // 在初始化后，根据缓冲区是否为空判断该目标图层要素是否与源图层元素相交
                    if (crossingFieldValuesList.Count != 0)
                    {
                        featureFieldValue = string.Join(",", crossingFieldValuesList);
                        crossingFeatureSelection.Add(featureTarget); // 添加到选择集
                        crossingFeatureCount++; // 相交元素数量增加
                    }
                    else
                    {
                        featureFieldValue = "未穿过居民地面"; // 设置填充字段值为“未穿过居民地面”
                    }

                    // 将填充字段值填充到该目标图层要素的目标字段值
                    UpdateFeatureFieldValues();

                    // 查询目标图层中的下一个要素
                    featureTarget = featureCursorsArray[TargetlyrFlag].NextFeature();
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
            crossingFieldValuesList.Clear(); // 清空存储相交要素字段值的列表

            // 创建空间过滤器，查找与当前目标图层要素穿过的源图层要素
            ISpatialFilter pSpatialFilter = new SpatialFilterClass
            {
                Geometry = featureTarget.Shape, // 获取目标图层要素的几何形状 
                SpatialRel = esriSpatialRelEnum.esriSpatialRelCrosses // 设置空间关系为穿过
            };

            // 使用游标遍历与当前目标图层要素穿过的源图层要素
            featureCursorsArray[SourcelyrFlag] = featureClassesArray[SourcelyrFlag].Search(pSpatialFilter, false);

            // 查询第一个与当前目标图层穿过的源图层要素，用以初始化遍历循环
            IFeature pFeatureCross = featureCursorsArray[SourcelyrFlag].NextFeature();

            try
            {
                // 遍历所有与当前目标图层穿过的源图层要素
                while (pFeatureCross != null)
                {
                    // 获取该源图层元素的源字段值
                    string fieldValue = GetFeatureFieldValue(pFeatureCross, featureFieldNamesArray[SourcelyrFlag]);

                    // 获取非重复的源字段值添加到列表
                    if (!string.IsNullOrEmpty(fieldValue) && !crossingFieldValuesList.Contains(fieldValue))
                    {
                        crossingFieldValuesList.Add(fieldValue);
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

        // 子函数，对于目标图层中的当前要素，赋值其目标字段值
        private void UpdateFeatureFieldValues()
        {
            // 获取当前目标图层要素的目标字段值
            string fieldValue = GetFeatureFieldValue(featureTarget, featureFieldNamesArray[TargetlyrFlag]);

            // 检查目标字段是否为空，如果为空则赋值字段
            if (string.IsNullOrEmpty(fieldValue))
            {
                // 获取目标字段的索引
                GetFieldIndex(featureTarget, featureFieldNamesArray[TargetlyrFlag]);

                // 若索引非空
                if (fieldIndex >= 0)
                {
                    // 填充目标字段
                    featureTarget.set_Value(fieldIndex, featureFieldValue);
                    featureCursorsArray[TargetlyrFlag].UpdateFeature(featureTarget);
                    isModified = true; // 标记已进行字段赋值操作
                }
            }
            else
            {
                isModified = false; // 目标字段非空，无需赋值
            }
        }

        // 获取要素的指定字段值
        private string GetFeatureFieldValue(IFeature feature, string fieldName)
        {
            GetFieldIndex(feature, fieldName);

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

        // 获取要素类中指定字段的索引的辅助函数
        private void GetFieldIndex(IFeature feature, string fieldName)
        {
            fieldIndex = feature.Fields.FindField(fieldName);
            return;
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
