using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SMGI.Common;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using System.Windows.Forms;
using ESRI.ArcGIS.Geometry;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.esriSystem;

namespace SMGI.Plugin.CartoExt
{
    public class SelectFeaturesTool : SMGITool
    {
        private AxMapControl currentMapControl;
        private IMap currentMap; //当前MapControl控件中的Map对象   

        /// <summary>
        /// 测量结果显示对话框
        /// </summary>
        FrmLayerResult _frmLayerResult;


        /// <summary>
        /// 面积测量
        /// </summary>
        INewPolygonFeedback _polygonFeedBack;

        /// <summary>
        /// 距离测量
        /// </summary>
        INewLineFeedback _lineFeedback;

        ISymbol _lineSymbol;


        /// <summary>
        /// 点集
        /// </summary>
        IPointCollection _ptCollection;

        public SelectFeaturesTool()
        {
            m_caption = "测量工具";      
        }

        public override bool Enabled
        {
            get
            {
                return true;
            }
        }

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

        public override void OnClick()
        {
            GetCurrentMap();

            //打开提示框
            showResultForm();

        }

        public override void OnMouseDown(int button, int shift, int x, int y)
        {
            if (button != 1)
            {
                return;
            }


            
        }

        public override void OnMouseMove(int button, int shift, int x, int y)
        {
          
        }

        /// <summary>
        /// 显示结果框
        /// </summary>
        private void showResultForm()
        {
            try
            {
                if (_frmLayerResult == null || _frmLayerResult.IsDisposed)
                {
                    _frmLayerResult = new FrmLayerResult();

                    _frmLayerResult.Show();
                    _frmLayerResult.currentMap = currentMap;
                    _frmLayerResult.initUI();
                    //_frmLayerResult.ShowDialog();
                }
                else
                {
                    _frmLayerResult.Activate();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex);
                throw;
            }
        }

    }
}
