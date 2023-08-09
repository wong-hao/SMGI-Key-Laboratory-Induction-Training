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
    public class MeasureTool : SMGITool
    {
        /// <summary>
        /// 测量结果显示对话框
        /// </summary>
        FrmMeasureResult _frmMeasureResult;


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

        public MeasureTool()
        {
            m_caption = "测量工具";       
        }

        public override bool Enabled
        {
            get
            {
                return m_Application != null
                    && m_Application.Workspace != null
                    && m_Application.LayoutState == Common.LayoutState.MapControl
                    && m_Application.MapControl.SpatialReference is IProjectedCoordinateSystem;//只适用于平面坐标系
            }
        }

        public override void OnClick()
        {
            _frmMeasureResult = null;
            _ptCollection = new MultipointClass();

            _polygonFeedBack = null;
            _lineFeedback = null;

            _lineSymbol = new SimpleLineSymbolClass
            {
                Color = new RgbColorClass { Red = 85, Green = 255, Blue = 0 },
                Style = esriSimpleLineStyle.esriSLSSolid,
                ROP2 = esriRasterOpCode.esriROPNotXOrPen,
                Width = 2.0
            };
            

            //打开提示框
            showResultForm();
        }

        public override void OnMouseDown(int button, int shift, int x, int y)
        {
            if (button != 1)
            {
                return;
            }

            //打开提示框
            showResultForm();

            switch (_frmMeasureResult.CurMeasureType)
            {
                case FrmMeasureResult.MeasureType.AREA:
                    {
                        if (null == _polygonFeedBack)
                        {
                            _polygonFeedBack = new NewPolygonFeedbackClass { Display = m_Application.ActiveView.ScreenDisplay, Symbol = _lineSymbol };

                            _ptCollection.RemovePoints(0, _ptCollection.PointCount);
                            if (_lineFeedback != null)
                            {
                                _lineFeedback.Stop();
                                _lineFeedback = null;
                            }

                            IPoint pt = ToSnapedMapPoint(x, y);

                            _polygonFeedBack.Start(pt);
                            _ptCollection.AddPoint(pt);
                        }
                        else
                        {
                            IPoint pt = ToSnapedMapPoint(x, y);

                            _polygonFeedBack.AddPoint(pt);
                            _ptCollection.AddPoint(pt);
                        }

                        break;
                    }
                case FrmMeasureResult.MeasureType.LINE:
                    {
                        if (null == _lineFeedback)
                        {
                            _lineFeedback = new NewLineFeedbackClass { Display = m_Application.ActiveView.ScreenDisplay, Symbol = _lineSymbol };

                            _ptCollection.RemovePoints(0, _ptCollection.PointCount);
                            if (_polygonFeedBack != null)
                            {
                                _polygonFeedBack.Stop();
                                _polygonFeedBack = null;
                            }

                            IPoint pt = ToSnapedMapPoint(x, y);

                            _lineFeedback.Start(pt);
                            _ptCollection.AddPoint(pt);
                        }
                        else
                        {
                            IPoint pt = ToSnapedMapPoint(x, y);

                            _lineFeedback.AddPoint(pt);
                            _ptCollection.AddPoint(pt);
                        }

                        break;
                    }
            }

            
        }

        public override void OnMouseMove(int button, int shift, int x, int y)
        {
            if (_polygonFeedBack != null)
            {
                #region 面积测量
                IPoint pt = ToSnapedMapPoint(x, y);
                _polygonFeedBack.MoveTo(pt);

                //更新点集信息
                IPointCollection pts = new PolygonClass();
                for (int i = 0; i < _ptCollection.PointCount; ++i)
                {
                    pts.AddPoint(_ptCollection.get_Point(i));
                }
                pts.AddPoint(pt);

                if (pts.PointCount < 3) return;

                IPolygon geo = pts as IPolygon; 
                //geo.Close();

                (geo as ITopologicalOperator).Simplify();//使几何图形的拓扑正确
                geo.Project(m_Application.MapControl.SpatialReference);

                //实际面积(平方米)
                double area = (geo as IArea).Area * getUnitConvertScale(m_Application.MapControl.Map.MapUnits) * getUnitConvertScale(m_Application.MapControl.Map.MapUnits);
                
                //图上面积(平方毫米)
                double mapScale = m_Application.MapControl.Map.ReferenceScale;
                double mapArea = area * 1.0e6 / (mapScale * mapScale);//平方米=》平方毫米=》图面面积

                _frmMeasureResult.ResultText = string.Format("面积测量(当前地图的参考比例尺为1∶{0})\n实地面积：{1:.##}平方米\n图上面积：{2:.##}平方毫米", mapScale, area, mapArea);

                #endregion
            }
            else if (_lineFeedback != null)
            {
                IPoint pt = ToSnapedMapPoint(x, y);
                _lineFeedback.MoveTo(pt);

                //更新点集信息
                IPointCollection pts = new PolylineClass();
                for (int i = 0; i < _ptCollection.PointCount; ++i)
                {
                    pts.AddPoint(_ptCollection.get_Point(i));
                }
                pts.AddPoint(pt);

                if (pts.PointCount < 2) return;

                IPolyline geo = pts as IPolyline; 

                (geo as ITopologicalOperator).Simplify();//使几何图形的拓扑正确
                geo.Project(m_Application.MapControl.SpatialReference);

                //实际长度（米)
                double len = geo.Length * getUnitConvertScale(m_Application.MapControl.Map.MapUnits);

                //图上长度(毫米)
                double mapScale = m_Application.MapControl.Map.ReferenceScale;
                double mapLen = len * 1.0e3 / mapScale ;//米=》毫米=》图面长度

                _frmMeasureResult.ResultText = string.Format("距离测量(当前地图的参考比例尺为1∶{0})\n实地距离：{1:.##}米\n图上距离：{2:.##}毫米", mapScale, len, mapLen);
            }
        }

        public override void OnDblClick()
        {
            clear();
        }

        public override bool Deactivate()
        {
            //关闭提示框
            _frmMeasureResult.Close();

            return base.Deactivate();
        }

        

        /// <summary>
        /// 清空成员
        /// </summary>
        private void clear()
        {
            MessageBox.Show("asd");

            if (_polygonFeedBack != null)
            {
                _polygonFeedBack.Stop();
                _polygonFeedBack = null;
            }

            if (_lineFeedback != null)
            {
                _lineFeedback.Stop();
                _lineFeedback = null;
            }

            _ptCollection.RemovePoints(0, _ptCollection.PointCount);
            m_Application.ActiveView.PartialRefresh(esriViewDrawPhase.esriViewForeground, null, null);
            
        }

        /// <summary>
        /// 显示结果框
        /// </summary>
        private void showResultForm()
        {
            if (null == _frmMeasureResult || _frmMeasureResult.IsDisposed)
            {
                _frmMeasureResult = new FrmMeasureResult();
                _frmMeasureResult.frmClosed += new FrmMeasureResult.FrmClosedEventHandler(clear);
                _frmMeasureResult.ResultText = "0";

                _frmMeasureResult.Show();
            }
            else
            {
                _frmMeasureResult.Activate();
            }
        }


        /// <summary>
        /// 1输入长度单位转为标准长度单位（米）的转换比例
        /// </summary>
        /// <param name="unit"></param>
        /// <returns></returns>
        private double getUnitConvertScale(esriUnits unit)
        {
            UnitConverterClass unitConverter = new UnitConverterClass();
            return unitConverter.ConvertUnits(1.0, unit, ESRI.ArcGIS.esriSystem.esriUnits.esriMeters);
        }
    }
}
