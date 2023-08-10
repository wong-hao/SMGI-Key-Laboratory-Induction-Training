﻿using System;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;

namespace SMGI.Plugin.CartoExt
{
    public partial class FrmLayerResult : Form
    {
        public IMap currentMap; //当前MapControl控件中的Map对象
        public IFeatureLayer TargetFeatureLayer; // 要选择的图层

        public FrmLayerResult()
        {
            InitializeComponent();
        }

        // 检查是否已选择图层
        private bool check()
        {
            if (cmbSelLayerName.SelectedIndex == -1)
            {
                MessageBox.Show("请选择图层！", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return false;
            }

            return true;
        }

        //在图层名称下拉框控件中所选择图层发生改变时触发事件，执行本函数
        private void cmbSelLayerName_SelectedIndexChanged(object sender, EventArgs e)
        {
            for (var i = 0; i < currentMap.LayerCount; i++)
                if (currentMap.get_Layer(i) is GroupLayer)
                {
                    var compositeLayer = currentMap.get_Layer(i) as ICompositeLayer;
                    for (var j = 0; j < compositeLayer.Count; j++)
                        //判断图层的名称是否与控件中选择的图层名称相同
                        if (compositeLayer.get_Layer(j).Name == cmbSelLayerName.SelectedItem.ToString())
                        {
                            //如果相同则设置为整个窗体所使用的IFeatureLayer接口对象
                            TargetFeatureLayer = compositeLayer.get_Layer(j) as IFeatureLayer;
                            break;
                        }
                }
                else
                {
                    //判断图层的名称是否与控件中选择的图层名称相同
                    if (currentMap.get_Layer(i).Name == cmbSelLayerName.SelectedItem.ToString())
                    {
                        //如果相同则设置为整个窗体所使用的IFeatureLayer接口对象
                        TargetFeatureLayer = currentMap.get_Layer(i) as IFeatureLayer;
                        break;
                    }
                }
        }

        private void button_ok_Click(object sender, EventArgs e)
        {
            if (!check()) return;

            cmbSelLayerName.Items.Clear();
            cmbSelLayerName.Text = "";
            MessageBox.Show("选择的图层为" + TargetFeatureLayer.Name);

            Close();
        }

        private void button_cancel_Click(object sender, EventArgs e)
        {
            MessageBox.Show("用户取消了选择图层操作，请使用Esc键退出工具。", "取消操作", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }

        private void FrmLayerResult_Load(object sender, EventArgs e)
        {
            try
            {
                //将当前图层列表清空
                cmbSelLayerName.Items.Clear();

                string layerName; //设置临时变量存储图层名称

                //对Map中的每个图层进行判断并加载名称
                for (var i = 0; i < currentMap.LayerCount; i++)
                    //如果该图层为图层组类型，则分别对所包含的每个图层进行操作
                    if (currentMap.get_Layer(i) is GroupLayer)
                    {
                        //使用ICompositeLayer接口进行遍历操作
                        var compositeLayer = currentMap.get_Layer(i) as ICompositeLayer;
                        for (var j = 0; j < compositeLayer.Count; j++)
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

                //将comboBoxLayerName控件的默认选项设置为空
                cmbSelLayerName.SelectedIndex = -1;
                //将comboBoxSelectMethod控件的默认选项设置为空
                cmbSelLayerName.SelectedIndex = -1;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }
    }
}