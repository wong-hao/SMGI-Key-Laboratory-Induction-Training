using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;

namespace SMGI.Plugin.CartoExt
{
    public partial class FrmLayerResult : Form
    {
        public IMap currentMap;    //当前MapControl控件中的Map对象

        public FrmLayerResult()
        {
            InitializeComponent();
        }

        /// <summary>
        /// 获得当前MapControl控件中的Map对象。
        /// </summary>
        public IMap CurrentMap
        {
            get { return currentMap; }
            set { currentMap = value; }
        }

        public void initUI()
        {
            try
            {
                //将当前图层列表清空
                cmbSelLayerName.Items.Clear();

                string layerName;   //设置临时变量存储图层名称

                //对Map中的每个图层进行判断并加载名称
                for (int i = 0; i < currentMap.LayerCount; i++)
                {
                    //如果该图层为图层组类型，则分别对所包含的每个图层进行操作
                    if (currentMap.get_Layer(i) is GroupLayer)
                    {
                        //使用ICompositeLayer接口进行遍历操作
                        ICompositeLayer compositeLayer = currentMap.get_Layer(i) as ICompositeLayer;
                        for (int j = 0; j < compositeLayer.Count; j++)
                        {
                            //将图层的名称添加到comboBoxLayerName控件中
                            layerName = compositeLayer.get_Layer(j).Name;
                            cmbSelLayerName.Items.Add(layerName);
                        }
                    }
                    //如果图层不是图层组类型，则直接添加名称
                    else
                    {
                        layerName = currentMap.get_Layer(i).Name;
                        cmbSelLayerName.Items.Add(layerName);
                    }
                }

                //将控件的默认选项设置为第一个图层的名称
                cmbSelLayerName.SelectedIndex = 0;
            }
            catch { }
        }
    }
}
