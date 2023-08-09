using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
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
        private IRubberBand pRubberBand = new RubberPolygonClass();
        private IGeometry geometry;
        private IFeatureSelection featureSelection;
        private IFeature SelectedLineFeature;
        private int divideCount = 0;

        /// <summary>
        /// 测量结果显示对话框
        /// </summary>
        FrmLayerResult _frmLayerResult;

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
            // 获取当前地图
            GetCurrentMap();

            // 检查地图是否为空
            if (currentMap == null)
            {
                MessageBox.Show("地图未加载，请先加载地图。", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            //打开提示框
            showResultForm();
        }

        private void PerformMultipleSelection()
        {
            geometry = pRubberBand.TrackNew(currentMapControl.ActiveView.ScreenDisplay, null);
            if (geometry != null && !geometry.IsEmpty)
            {
                ISpatialFilter spatialFilter = new SpatialFilter();
                spatialFilter.Geometry = geometry;
                spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                featureSelection.SelectFeatures(spatialFilter, esriSelectionResultEnum.esriSelectionResultAdd, false);
            }
        }

        private void PerformClearAndSelection()
        {
            // 清空选择集
            featureSelection.Clear();

            // 根据拉框绘制的多边形重新创建选择集
            geometry = pRubberBand.TrackNew(currentMapControl.ActiveView.ScreenDisplay, null);
            if (geometry != null && !geometry.IsEmpty)
            {
                ISpatialFilter spatialFilter = new SpatialFilter();
                spatialFilter.Geometry = geometry;
                spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                featureSelection.SelectFeatures(spatialFilter, esriSelectionResultEnum.esriSelectionResultNew, false);
            }
        }

        private void PerformRightClickAction()
        {
            try
            {
                if (featureSelection.SelectionSet.Count == 0)
                {
                    MessageBox.Show("请先选择单个线要素");
                    clearResources();
                    return;
                }
                else if (featureSelection.SelectionSet.Count == 1)
                {
                    SelectedLineFeature = GetSelectedLineFeature(featureSelection);

                    if (SelectedLineFeature == null)
                    {
                        clearResources();
                        return;
                    }

                    divideCount = 2;
                    SplitAndCreateNewFeatures(SelectedLineFeature, divideCount);
                    MessageBox.Show("线段已等分为" + divideCount + "份");

                    // 刷新地图显示
                    currentMapControl.Refresh(esriViewDrawPhase.esriViewGeoSelection, null, null);

                }
                else if (featureSelection.SelectionSet.Count > 1)
                {
                    MessageBox.Show("请先选择单个线要素");
                    clearResources();
                    return;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                throw;
            }
        }

        public override void OnMouseDown(int button, int shift, int x, int y)
        {

            // 确保选定了一个要素图层
            IFeatureLayer selectedLayer = _frmLayerResult.TargetFeatureLayer;
            if (selectedLayer != null)
            {
                if (_frmLayerResult.TargetFeatureLayer == null)
                {
                    MessageBox.Show("请先通过窗体工具选择目标图层!");
                    return;
                }

                featureSelection = selectedLayer as IFeatureSelection;

                if (button == 1) // 鼠标左键按下
                {
                    try
                    {
                        if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
                        {
                            // 弹出确认对话框
                            DialogResult result = MessageBox.Show("已按住Control键，是否继续进行选择操作？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            
                            if (result == DialogResult.No) return;

                            PerformMultipleSelection();
                        }
                        else
                        {
                            PerformClearAndSelection();
                        }

                        if (featureSelection.SelectionSet.Count != 0)
                        {
                            MessageBox.Show("已选中" + featureSelection.SelectionSet.Count + "个" + selectedLayer.FeatureClass.ShapeType + "元素");
                        }
                        else
                        {
                            MessageBox.Show("未选中任何与图层" + _frmLayerResult.TargetFeatureLayer.Name + "类型匹配的元素");
                        }

                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                        throw;
                    }
                    // 右键按下
                }
                else if (button == 2)
                {
                    PerformRightClickAction();
                }
            }
        }

        public override void OnMouseMove(int button, int shift, int x, int y)
        {
          
        }

        public void ExitTool()
        {
            currentMapControl.CurrentTool = null;

            clearResources();

            MessageBox.Show("工具已退出");
        }

        public override void OnDblClick()
        {
            
        }

        private void ShowSelectionInTreeView()
        {
            // 检查选择集是否有要素
            if (featureSelection != null && featureSelection.SelectionSet != null && featureSelection.SelectionSet.Count > 0)
            {
                // 创建一个窗体
                Form selectionForm = new Form();
                selectionForm.Text = "选择集中的要素";

                // 创建 TreeView 控件
                TreeView treeView = new TreeView();
                treeView.Dock = DockStyle.Fill;

                // 遍历选择集中的要素，将每个 OID 添加为节点
                IEnumIDs enumIDs = featureSelection.SelectionSet.IDs;
                int oid;
                while ((oid = enumIDs.Next()) != -1)
                {
                    TreeNode node = new TreeNode("OID: " + oid.ToString());
                    treeView.Nodes.Add(node);
                }

                // 将 TreeView 控件添加到窗体
                selectionForm.Controls.Add(treeView);

                // 显示窗体
                selectionForm.ShowDialog();
            }
            else
            {
                // 如果选择集中没有要素，则不作响应
                MessageBox.Show("选择集中没有要素。");
            }
        }


        public override void OnKeyDown(int keyCode, int shift)
        {
            base.OnKeyDown(keyCode, shift);

            if ((shift & (int)Keys.Control) != 0)
            {
                MessageBox.Show("Control");

            }else if (keyCode == (int)Keys.Space) // 按下的是空格键
            {
                if (featureSelection != null && featureSelection.SelectionSet.Count > 0)
                {
                    ShowSelectionInTreeView();
                }
            }
            else if (keyCode == (int)Keys.Escape) // 按下的是 ESC 键
            {
                MessageBox.Show("退出工具");
                ExitTool(); // 退出工具
            }
        }

        public void clearResources()
        {
            // 清空选择集
            if (featureSelection != null)
            {
                featureSelection.Clear();
            }

            // 刷新地图显示
            currentMapControl.Refresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
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
                    _frmLayerResult.currentMapControl = currentMapControl;
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

        public IFeature GetSelectedLineFeature(IFeatureSelection featureSelection)
        {
            ISelectionSet selectionSet = featureSelection.SelectionSet;


                ICursor cursor;
                selectionSet.Search(null, false, out cursor);

                IFeatureCursor featureCursor = cursor as IFeatureCursor;
                if (featureCursor != null)
                {
                    IFeature selectedFeature = featureCursor.NextFeature();
                    if (selectedFeature.Shape.GeometryType == esriGeometryType.esriGeometryPolyline)
                    {
                        MessageBox.Show("已获取被选中的唯一一个线要素");
                        return selectedFeature; // 返回唯一的线要素
                    }
                    else
                    {
                        MessageBox.Show("被选中的唯一一个要素为"+selectedFeature.Shape.GeometryType + ", 并非线要素，请重新选择!");
                        clearResources();
                        return null;
                    }
                }

                return null;

        }

        // 假设你已经有一个IFeature对象表示原始要素
        public void SplitAndCreateNewFeatures(IFeature originalFeature, int segments)
        {
            IFeatureClass featureClass = originalFeature.Class as IFeatureClass;
            IWorkspaceEdit workspaceEdit = (featureClass as IDataset).Workspace as IWorkspaceEdit;

            try
            {
                workspaceEdit.StartEditing(true);
                workspaceEdit.StartEditOperation();

                IGeometry originalGeometry = originalFeature.ShapeCopy;

                ICurve curve = originalGeometry as ICurve;
                if (curve != null)
                {
                    double length = curve.Length;
                    double segmentLength = length / segments;

                    IPointCollection pointCollection = curve as IPointCollection;

                    IFeatureCursor cursor = featureClass.Insert(true);
                    for (int i = 0; i < segments; i++)
                    {
                        double distanceAlongCurve = i * segmentLength;

                        IPoint startPoint = new PointClass();
                        curve.QueryPoint(esriSegmentExtension.esriNoExtension, distanceAlongCurve, false, startPoint);

                        IPoint endPoint = new PointClass();
                        curve.QueryPoint(esriSegmentExtension.esriNoExtension, distanceAlongCurve + segmentLength, false, endPoint);

                        ILine newSegment = new LineClass();
                        newSegment.PutCoords(startPoint, endPoint);

                        IPointCollection newPointCollection = new PolylineClass();
                        newPointCollection.AddPoint(startPoint);
                        newPointCollection.AddPoint(endPoint);

                        ISegmentCollection newSegmentCollection = new PolylineClass();
                        newSegmentCollection.AddSegment(newSegment as ISegment);

                        IGeometry newGeometry = newSegmentCollection as IGeometry;

                        IFeatureBuffer newFeatureBuffer = featureClass.CreateFeatureBuffer();
                        newFeatureBuffer.Shape = newGeometry;

                        // 复制其他属性值
                        for (int fieldIndex = 0; fieldIndex < originalFeature.Fields.FieldCount; fieldIndex++)
                        {
                            IField field = originalFeature.Fields.Field[fieldIndex];
                            if (field.Type != esriFieldType.esriFieldTypeGeometry)
                            {
                                newFeatureBuffer.set_Value(fieldIndex, originalFeature.get_Value(fieldIndex));
                            }
                        }

                        cursor.InsertFeature(newFeatureBuffer);
                    }

                    cursor.Flush();

                    // 提交事务后删除原始要素
                    workspaceEdit.StopEditOperation();
                    workspaceEdit.StopEditing(true);

                    // 删除原始要素
                    originalFeature.Delete();
                }
            }
            catch (Exception ex)
            {
                workspaceEdit.StopEditOperation();
                workspaceEdit.StopEditing(false);
                // 处理异常
            }
        }
    }
}
