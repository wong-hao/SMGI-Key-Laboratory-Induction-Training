using System;
using System.Windows.Forms;
using ESRI.ArcGIS.Carto;
using ESRI.ArcGIS.Controls;
using ESRI.ArcGIS.Display;
using ESRI.ArcGIS.Geodatabase;
using ESRI.ArcGIS.Geometry;
using SMGI.Common;

namespace SMGI.Plugin.CartoExt
{
    public class SelectFeaturesTool : SMGITool
    {
        /// <summary>
        ///     测量结果显示对话框
        /// </summary>
        private FrmLayerResult _frmLayerResult;

        /// <summary>
        ///     点集
        /// </summary>
        private IPointCollection _ptCollection;

        private IMap currentMap; // 当前MapControl控件中的Map对象   
        private AxMapControl currentMapControl; // 当前的MapControl控件
        private int divideCount; // 等分的份数
        private IFeatureSelection featureSelection; // 选择集
        private IGeometry geometry; // 框选出的几何图形
        private readonly IRubberBand pRubberBand = new RubberPolygonClass(); // 用于框选面要素
        private IFeature SelectedLineFeature; // 用于等分的线要素
        private IEngineEditor pEngineEditor = null; // 编辑器
        private IEngineEditTask pEngineEditTask = null;
        private IEngineEditLayers pEngineEditLayers = null;

        public SelectFeaturesTool()
        {
            m_caption = "SelectFeaturesTool";
        }

        public override bool Enabled
        {
            get { return true; }
        }

        /// <summary>
        /// 开始编辑
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartEditing()
        {
            try
            {
                pEngineEditor = new EngineEditorClass();
                pEngineEditTask = pEngineEditor as IEngineEditTask;
                pEngineEditLayers = pEngineEditor as IEngineEditLayers;

                //如果编辑已经开始，则直接退出
                if (pEngineEditor.EditState != esriEngineEditState.esriEngineStateNotEditing)
                    return;
                //获取当前编辑图层工作空间
                IDataset pDataSet = _frmLayerResult.TargetFeatureLayer.FeatureClass as IDataset;
                IWorkspace pWs = pDataSet.Workspace;
                //设置编辑模式，如果是ArcSDE采用版本模式
                if (pWs.Type == esriWorkspaceType.esriRemoteDatabaseWorkspace)
                {
                    pEngineEditor.EditSessionMode = esriEngineEditSessionMode.esriEngineEditSessionModeVersioned;
                }
                else
                {
                    pEngineEditor.EditSessionMode = esriEngineEditSessionMode.esriEngineEditSessionModeNonVersioned;
                }
                //设置编辑任务
                pEngineEditTask = pEngineEditor.GetTaskByUniqueName("ControlToolsEditing_CreateNewFeatureTask");
                pEngineEditor.CurrentTask = pEngineEditTask;// 设置编辑任务
                pEngineEditor.EnableUndoRedo(true); //是否可以进行撤销、恢复操作
                pEngineEditor.StartEditing(pWs, currentMap); //开始编辑操作

                //设置编辑目标图层
                pEngineEditLayers.SetTargetLayer(_frmLayerResult.TargetFeatureLayer, 0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        /// <Date>2023/8/4</Date>
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
            ShowResultForm();
        }

        // 刷新地图
        private void RefreshMap()
        {
            currentMapControl.Refresh(esriViewDrawPhase.esriViewGeoSelection, null, null);
        }

        // 拉框选择
        private void TrackGeometry()
        {
            geometry = pRubberBand.TrackNew(currentMapControl.ActiveView.ScreenDisplay, null);
            if (geometry != null && !geometry.IsEmpty)
            {
                ISpatialFilter spatialFilter = new SpatialFilter();
                spatialFilter.Geometry = geometry;
                spatialFilter.SpatialRel = esriSpatialRelEnum.esriSpatialRelIntersects;
                featureSelection.SelectFeatures(spatialFilter, esriSelectionResultEnum.esriSelectionResultAdd, false);
            }

            // 刷新地图显示
            RefreshMap();
        }

        // 【ctrl】键按下：实现多选（现有选择集不清空）
        private void PerformMultipleSelection()
        {
            TrackGeometry();
        }

        // 【ctrl】没有按下：清空选择集，在根据拉框绘制的多边形重新创建选择集
        private void PerformClearAndSelection()
        {
            ClearResources();

            TrackGeometry();
        }

        // 根据选择集中线要素个数进行动态判断
        private void PerformRightClickAction()
        {
            try
            {
                if (featureSelection.SelectionSet.Count == 0)
                {
                    MessageBox.Show("请先选择单个线要素");
                    ClearResources();
                    return;
                }

                if (featureSelection.SelectionSet.Count == 1)
                {
                    SelectedLineFeature = GetSelectedLineFeature(featureSelection);

                    if (SelectedLineFeature == null)
                    {
                        ClearResources();
                        return;
                    }

                    divideCount = 2;
                    SplitAndCreateNewFeatures(SelectedLineFeature, divideCount);
                    MessageBox.Show("线段已等分为" + divideCount + "份");

                    // 刷新地图显示
                    RefreshMap();
                }
                else if (featureSelection.SelectionSet.Count > 1)
                {
                    MessageBox.Show("请先选择单个线要素");
                    ClearResources();
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
            var selectedLayer = _frmLayerResult.TargetFeatureLayer;
            if (selectedLayer != null)
            {
                if (_frmLayerResult.TargetFeatureLayer == null)
                {
                    MessageBox.Show("请先通过窗体工具选择目标图层!");
                    return;
                }

                featureSelection = selectedLayer as IFeatureSelection;

                if (button == 1) // 鼠标左键按下
                    try
                    {
                        if ((Control.ModifierKeys & Keys.Control) == Keys.Control)
                            // 弹出确认对话框
                            //DialogResult result = MessageBox.Show("已按住Control键，是否继续进行选择操作？", "确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                            //if (result == DialogResult.No) return;
                            PerformMultipleSelection();
                        else
                            PerformClearAndSelection();

                        if (featureSelection.SelectionSet.Count != 0)
                            MessageBox.Show("选择集中存在" + featureSelection.SelectionSet.Count + "个" +
                                            selectedLayer.FeatureClass.ShapeType + "元素");
                        else
                            MessageBox.Show("选择集中不存在任何与图层" + _frmLayerResult.TargetFeatureLayer.Name + "图形类型匹配的元素");
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.ToString());
                        throw;
                    }
                // 右键按下
                else if (button == 2)
                {
                    // 设置编辑器属性
                    // StartEditing();

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

            ClearResources();

            MessageBox.Show("工具已退出");
        }

        // 弹出窗体
        private void ShowSelectionInTreeView()
        {
            // 检查选择集是否有要素
            if (featureSelection != null && featureSelection.SelectionSet != null &&
                featureSelection.SelectionSet.Count > 0)
            {
                // 创建一个窗体
                var selectionForm = new Form();
                selectionForm.Text = "选择集中的要素";

                // 创建 TreeView 控件
                var treeView = new TreeView();
                treeView.Dock = DockStyle.Fill;

                // 遍历选择集中的要素，将每个 OID 添加为节点
                var enumIDs = featureSelection.SelectionSet.IDs;
                int oid;
                while ((oid = enumIDs.Next()) != -1)
                {
                    var node = new TreeNode("OID: " + oid);
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

            if (keyCode == (int)Keys.Space) // 按下的是空格键
            {
                if (featureSelection != null && featureSelection.SelectionSet.Count > 0) ShowSelectionInTreeView();
            }
            else if (keyCode == (int)Keys.Escape) // 按下的是 ESC 键
            {
                MessageBox.Show("退出工具");

                // 退出工具
                ExitTool();
            }
        }

        // 清空资源
        public void ClearResources()
        {
            // 清空选择集
            if (featureSelection != null) featureSelection.Clear();

            // 刷新地图显示
            RefreshMap();
        }


        /// <summary>
        ///     显示窗体
        /// </summary>
        private void ShowResultForm()
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

        // 获取选择集中的线要素
        public IFeature GetSelectedLineFeature(IFeatureSelection featureSelection)
        {
            var selectionSet = featureSelection.SelectionSet;


            ICursor cursor;
            selectionSet.Search(null, false, out cursor);

            var featureCursor = cursor as IFeatureCursor;
            if (featureCursor != null)
            {
                var selectedFeature = featureCursor.NextFeature();
                if (selectedFeature.Shape.GeometryType == esriGeometryType.esriGeometryPolyline)
                {
                    MessageBox.Show("已获取被选中的唯一一个线要素");
                    return selectedFeature; // 返回唯一的线要素
                }

                MessageBox.Show("被选中的唯一一个要素为" + selectedFeature.Shape.GeometryType + ", 并非线要素，请重新选择!");
                ClearResources();
                return null;
            }

            return null;
        }

        // 根据线要素进行等分
        public void SplitAndCreateNewFeatures(IFeature originalFeature, int segments)
        {
            var featureClass = originalFeature.Class as IFeatureClass;
            var workspaceEdit = (featureClass as IDataset).Workspace as IWorkspaceEdit;

            try
            {
                workspaceEdit.StartEditing(true);
                workspaceEdit.StartEditOperation();

                var originalGeometry = originalFeature.ShapeCopy;

                var curve = originalGeometry as ICurve;
                if (curve != null)
                {
                    var length = curve.Length;
                    var segmentLength = length / segments;

                    var pointCollection = curve as IPointCollection;

                    var cursor = featureClass.Insert(true);
                    for (var i = 0; i < segments; i++)
                    {
                        var distanceAlongCurve = i * segmentLength;

                        IPoint startPoint = new PointClass();
                        curve.QueryPoint(esriSegmentExtension.esriNoExtension, distanceAlongCurve, false, startPoint);

                        IPoint endPoint = new PointClass();
                        curve.QueryPoint(esriSegmentExtension.esriNoExtension, distanceAlongCurve + segmentLength,
                            false, endPoint);

                        ILine newSegment = new LineClass();
                        newSegment.PutCoords(startPoint, endPoint);

                        IPointCollection newPointCollection = new PolylineClass();
                        newPointCollection.AddPoint(startPoint);
                        newPointCollection.AddPoint(endPoint);

                        ISegmentCollection newSegmentCollection = new PolylineClass();
                        newSegmentCollection.AddSegment(newSegment as ISegment);

                        var newGeometry = newSegmentCollection as IGeometry;

                        var newFeatureBuffer = featureClass.CreateFeatureBuffer();
                        newFeatureBuffer.Shape = newGeometry;

                        // 复制其他属性值
                        for (var fieldIndex = 0; fieldIndex < originalFeature.Fields.FieldCount; fieldIndex++)
                        {
                            var field = originalFeature.Fields.Field[fieldIndex];
                            if (field.Type != esriFieldType.esriFieldTypeGeometry)
                                newFeatureBuffer.set_Value(fieldIndex, originalFeature.get_Value(fieldIndex));
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