using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using NodeGraphControl.Elements;

namespace NodeGraphControl {
    public partial class NodeGraphControl : Control {
        #region Constructor

        public NodeGraphControl() {
            InitializeComponent();
            this.SetStyle(
                ControlStyles.AllPaintingInWmPaint | ControlStyles.Opaque | ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw | ControlStyles.Selectable | ControlStyles.UserPaint, true);
        }

        #endregion

        #region Interface

        public void Run() {
            foreach (var abstractNode in _graphNodes.Where(abstractNode => abstractNode.StartNode)) {
                abstractNode.Execute();
            }
        }

        private readonly List<ContextNode> _contextNodeList = new List<ContextNode>();

        public void AddContextNodeType<T>(string contextName, string contextDescription, string contextCategory)
            where T : AbstractNode {
            var cn = new ContextNode();

            const string defaultDescription = "No Description";
            const string defaultCategory = "";

            // create name from type
            var typeStr = typeof(T).ToString().Split('.').Last();
            // insert space before Capital letter and ignore acronyms
            var nameFromTypeStr = Regex.Replace(typeStr, @"((?<=\p{Ll})\p{Lu})|((?!\A)\p{Lu}(?>\p{Ll}))", " $0");

            // add context node data
            cn.NodeType = typeof(T);

            cn.NodeName = (!string.IsNullOrEmpty(contextName)) ? contextName : nameFromTypeStr;
            cn.NodeDescription = (!string.IsNullOrEmpty(contextDescription)) ? contextDescription : defaultDescription;
            cn.NodeCategory = (!string.IsNullOrEmpty(contextCategory)) ? contextCategory : defaultCategory;

            _contextNodeList.Add(cn);
        }

        public void AddNode(AbstractNode node) {
            node.InvokeRepaint += n_InvokeRepaint;
            _graphNodes.Add(node);
            node.Calculate();
        }

        public void DeleteNode(AbstractNode node) {
            node.Disconnect();
            _graphNodes.Remove(node);
            ValidateConnections();
            HandleSelection();
            Refresh();
        }

        public void DeleteSelectedNodes() {
            for (var i = _graphNodes.Count - 1; i >= 0; i--) {
                if (_graphNodes[i].Selected) {
                    DeleteNode(_graphNodes[i]);
                }
            }

            HandleSelection();
        }

        public void Connect(SocketOut from, SocketIn to) {
            try {
                var wire = new Wire(from, to);

                from.OutputConnections.Add(wire);
                to.InputConnection = wire;

                _connections.Add(wire);
                wire.Flow();
            } catch (Exception e) {
                Console.WriteLine(e);
            }
        }

        private void Disconnect(Wire wire) {
            if (wire == null)
                return;

            wire.From?.OutputConnections.Remove(wire);
            wire.To?.InputConnection?.Disconnect();

            _connections.Remove(wire);
        }

        private void ValidateConnections() {
            for (var i = _connections.Count - 1; i >= 0; i--) {
                var con = _connections[i];

                if (con.From == null || con.To == null
                                     || !con.From.OutputConnections.Contains(con)
                                     || con.To.InputConnection != con) {
                    Disconnect(con);
                }
            }
        }

        public void SetNodeSelected(bool selected) {
            foreach (var node in _graphNodes) {
                node.Selected = selected;
            }

            HandleSelection();
        }

        #endregion

        #region Events

        public event EventHandler<List<AbstractNode>> SelectionChanged;

        #endregion

        #region GridSettings

        // grid style
        public enum EGridStyle {
            Grid,
            Dots,
            None
        }

        private EGridStyle _eGridStyleStyle = EGridStyle.Grid;

        [Description("The type of rendered grid"), Category("Appearance"), DisplayName("Grid Style")]
        public EGridStyle GridStyle {
            get { return _eGridStyleStyle; }
            set {
                if (_eGridStyleStyle == value)
                    return;

                _eGridStyleStyle = value;
                this.Invalidate();
            }
        }

        // grid step
        private int _gridStep = 16 * 8;

        [Description("The distance between the largest grid lines"), Category("Appearance")]
        public int GridStep {
            get { return _gridStep; }
            set {
                if (_gridStep == value)
                    return;

                _gridStep = value;
                this.Invalidate();
            }
        }

        // grid color
        private Color _gridColor = Color.LightGray;
        private Pen _gridPen = new Pen(Color.LightGray);
        private Brush _gridBrush = new SolidBrush(Color.LightGray);

        [Description("The color for the grid lines with the largest gap between them"), Category("Appearance")]
        public Color GridColor {
            get { return _gridColor; }
            set {
                if (_gridColor == value)
                    return;

                _gridColor = value;
                _gridPen = new Pen(_gridColor);
                _gridBrush = new SolidBrush(_gridColor);
                this.Invalidate();
            }
        }

        #endregion

        #region Elements

        private readonly List<AbstractNode> _graphNodes = new List<AbstractNode>();
        private readonly List<Wire> _connections = new List<Wire>();
        private Wire _tempWire = null;

        #endregion

        #region regionToBeSorted

        private enum CommandMode {
            Edit,
            MarqueSelection,
            MoveSelection,
            Wiring,
            TranslateView,
            ScaleView
        }

        private CommandMode _command = CommandMode.Edit;

        // IElement internalDragOverElement;
        bool mouseMoved = false;
        bool dragging = false;
        bool abortDrag = false;

        Point lastLocation;
        PointF snappedLocation;
        PointF originalLocation;
        Point originalMouseLocation;

        #endregion

        #region UpdateMatrices

        PointF translation;
        float zoom = 1.0f;

        readonly Matrix transformation = new Matrix();
        readonly Matrix inverse_transformation = new Matrix();

        private void UpdateMatrices() {
            zoom = Utils.Clamp(0.25f, 4.00f, zoom);
            // zoom = Utils.Clamp(1f, 1f, zoom); // locked

            var center = new PointF(this.Width / 2.0f, this.Height / 2.0f);
            transformation.Reset();
            transformation.Translate(translation.X, translation.Y);
            // transformation.Translate(center.X, center.Y);
            transformation.Scale(zoom, zoom);
            // transformation.Translate(-center.X, -center.Y);

            inverse_transformation.Reset();
            // inverse_transformation.Translate(center.X, center.Y);
            inverse_transformation.Scale(1.0f / zoom, 1.0f / zoom);
            // inverse_transformation.Translate(-center.X, -center.Y);
            inverse_transformation.Translate(-translation.X, -translation.Y);
        }

        #endregion

        #region GetTransformedLocation

        private PointF GetTransformedLocation() {
            var points = new[] {snappedLocation};
            inverse_transformation.TransformPoints(points);
            var transformed_location = points[0];

            if (abortDrag) {
                transformed_location = originalLocation;
            }

            return transformed_location;
        }

        #endregion

        #region Events

        private void n_InvokeRepaint(object sender, EventArgs e) {
            Refresh();
        }

        #endregion

        #region OnPaint

        // temp
        private bool _renderBounds = false;

        protected override void OnPaint(PaintEventArgs e) {
            base.OnPaint(e);

            Graphics g = e.Graphics;

            g.PageUnit = GraphicsUnit.Pixel;
            g.CompositingQuality = CompositingQuality.GammaCorrected;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;

            UpdateMatrices();
            g.Transform = transformation;

            OnDrawBackground(e);

            // temp corsair
            var pW = new Pen(Color.Gray, 3f);
            g.DrawLine(pW, -_gridStep, 0, _gridStep, 0);
            g.DrawLine(pW, 0, -_gridStep, 0, _gridStep);

            if (_graphNodes.Count == 0)
                return;

            g.SmoothingMode = SmoothingMode.HighQuality;

            foreach (var wire in _connections) {
                var xFrom = wire.From.BoundsFull.X + wire.From.BoundsFull.Width / 2f;
                var yFrom = wire.From.BoundsFull.Y + wire.From.BoundsFull.Height / 2f;
                var xTo = wire.To.BoundsFull.X + wire.To.BoundsFull.Width / 2;
                var yTo = wire.To.BoundsFull.Y + wire.To.BoundsFull.Height / 2;

                DrawWire(g, new Pen(Color.Fuchsia, 2f), xFrom, yFrom, xTo, yTo);
            }

            foreach (var node in _graphNodes) {
                node.Draw(g);
            }

            // temp render bounds
            if (_renderBounds) {
                foreach (var node in _graphNodes) {
                    foreach (var socket in node.Sockets) {
                        g.DrawRectangle(new Pen(Color.Aqua), socket.BoundsFull.X, socket.BoundsFull.Y,
                            socket.BoundsFull.Width,
                            socket.BoundsFull.Height);
                    }

                    g.DrawRectangle(new Pen(Color.Aqua), node.BoundsHeader.X, node.BoundsHeader.Y,
                        node.BoundsHeader.Width, node.BoundsHeader.Height);
                    g.DrawRectangle(new Pen(Color.Aqua), node.BoundsBase.X, node.BoundsBase.Y, node.BoundsBase.Width,
                        node.BoundsBase.Height);
                    g.DrawRectangle(new Pen(Color.Aqua), node.BoundsFooter.X, node.BoundsFooter.Y,
                        node.BoundsFooter.Width, node.BoundsFooter.Height);
                }
            }

            if (_command == CommandMode.Wiring && _tempWire != null) {
                float xFrom, yFrom, xTo, yTo;

                var cursorPoint = GetTranslatedPosition(PointToClient(Cursor.Position));

                if (_tempWire.From != null) {
                    xFrom = _tempWire.From.BoundsFull.X + _tempWire.From.BoundsFull.Width / 2f;
                    yFrom = _tempWire.From.BoundsFull.Y + _tempWire.From.BoundsFull.Height / 2f;
                    xTo = cursorPoint.X;
                    yTo = cursorPoint.Y;

                    DrawWire(g, new Pen(Color.White, 2f), xFrom, yFrom, xTo, yTo);
                } else if (_tempWire.To != null) {
                    xFrom = cursorPoint.X;
                    yFrom = cursorPoint.Y;
                    xTo = _tempWire.To.BoundsFull.X + _tempWire.To.BoundsFull.Width / 2;
                    yTo = _tempWire.To.BoundsFull.Y + _tempWire.To.BoundsFull.Height / 2;

                    DrawWire(g, new Pen(Color.White, 2f), xFrom, yFrom, xTo, yTo);
                }
            }

            if (_command == CommandMode.MarqueSelection) {
                var marqueRectangle = GetMarqueRectangle();
                g.FillRectangle(new SolidBrush(Color.FromArgb(15, 64, 64, 127)), marqueRectangle);
                g.DrawRectangle(Pens.DarkGray, marqueRectangle.X, marqueRectangle.Y, marqueRectangle.Width,
                    marqueRectangle.Height);
            }
        }

        private void DrawWire(Graphics g, Pen pen, float xFrom, float yFrom, float xTo, float yTo) {
            var from = new PointF(xFrom, yFrom);
            var to = new PointF(xTo, yTo);

            var distance = to.X - from.X;

            var fromHalf = new PointF(from.X + distance / 2, from.Y);
            var toHalf = new PointF(from.X + distance / 2, to.Y);

            var path = new GraphicsPath();
            PointF[] bezierPoints = {from, fromHalf, toHalf, to};
            path.AddBeziers(bezierPoints);
            g.DrawPath(pen, path);
            var points = path.PathPoints;
        }

        #endregion

        #region OnDrawBackground

        private void OnDrawBackground(PaintEventArgs e) {
            Graphics g = e.Graphics;

            e.Graphics.Clear(Color.FromArgb(23, 25, 31));

            if (_eGridStyleStyle == EGridStyle.None)
                return;

            var points = new PointF[] {
                new PointF(e.ClipRectangle.Left, e.ClipRectangle.Top),
                new PointF(e.ClipRectangle.Right, e.ClipRectangle.Bottom)
            };

            inverse_transformation.TransformPoints(points);

            var left = points[0].X;
            var right = points[1].X;
            var top = points[0].Y;
            var bottom = points[1].Y;

            var largeXOffset = ((float) Math.Round(left / _gridStep) * _gridStep);
            var largeYOffset = ((float) Math.Round(top / _gridStep) * _gridStep);

            // grid
            if (_eGridStyleStyle == EGridStyle.Grid) {
                for (var x = largeXOffset; x < right; x += _gridStep)
                    g.DrawLine(_gridPen, x, top, x, bottom);

                for (var y = largeYOffset; y < bottom; y += _gridStep)
                    g.DrawLine(_gridPen, left, y, right, y);
            }

            // dots
            if (_eGridStyleStyle == EGridStyle.Dots) {
                for (var x = largeXOffset; x < right; x += _gridStep)
                for (var y = largeYOffset; y < bottom; y += _gridStep)
                    g.FillRectangle(_gridBrush, x, y, 2, 2);
            }
        }

        #endregion

        #region GetMarqueRectangle

        private RectangleF GetMarqueRectangle() {
            var transformedLocation = GetTransformedLocation();
            var x1 = transformedLocation.X;
            var y1 = transformedLocation.Y;
            var x2 = originalLocation.X;
            var y2 = originalLocation.Y;
            var x = Math.Min(x1, x2);
            var y = Math.Min(y1, y2);
            var width = Math.Max(x1, x2) - x;
            var height = Math.Max(y1, y2) - y;
            return new RectangleF(x, y, width, height);
        }

        #endregion

        #region OnMouseWheel

        protected override void OnMouseWheel(MouseEventArgs e) {
            base.OnMouseWheel(e);

            // zoom *= (float) Math.Pow(2, e.Delta / 480.0f);

            float centeredMousePositionX = e.X - (this.Size.Width / 2.0f);
            float centeredMousePositionY = e.Y - (this.Size.Height / 2.0f);

            // zoom in (mouse wheel ↑)
            if (e.Delta > 0) {
                zoom += 0.25f;

                // var deltaX = (centeredMousePositionX) / zoom;
                // var deltaY = (centeredMousePositionY) / zoom;
                // translation.X -= deltaX * zoom;
                // translation.Y -= deltaY * zoom;

                // attemp 1
                // translation.X += centeredMousePositionX * zoom;
                // translation.Y += centeredMousePositionY * zoom;
            }

            // zoom out (mouse wheel ↓)
            if (e.Delta < 0) {
                zoom -= 0.25f;

                // attemp 2
                // var deltaX = (centeredMousePositionX) / zoom;
                // var deltaY = (centeredMousePositionY) / zoom;
                // translation.X += -deltaX * zoom;
                // translation.Y += -deltaY * zoom;

                // attemp 1
                // translation.X -= centeredMousePositionX * zoom;
                // translation.Y -= centeredMousePositionY * zoom;
            }

            Refresh();
        }

        #endregion

        #region MouseProperties

        private bool leftMouseButton = false;
        private bool rightMouseButton = false;

        private void UpdateOriginalLocation(Point location) {
            var points = new PointF[] {location};
            inverse_transformation.TransformPoints(points);
            var transformed_location = points[0];

            originalLocation = transformed_location;

            snappedLocation = lastLocation = location;
        }

        #endregion

        #region OnMouseDown

        protected override void OnMouseDown(MouseEventArgs e) {
            base.OnMouseDown(e);
            this.Focus();

            UpdateOriginalLocation(e.Location);

            var element = FindElementAtOriginal(originalLocation);

            if ((e.Button & MouseButtons.Left) != 0) {
                leftMouseButton = true;

                if (_command == CommandMode.Edit) {
                    if (element == null && Control.ModifierKeys != Keys.Shift) {
                        SetNodeSelected(false);
                        Refresh();
                    }

                    if (element is AbstractNode abstractNode) {
                        if (Control.ModifierKeys != Keys.Shift && !abstractNode.Selected) {
                            SetNodeSelected(false);
                        }

                        if (Control.ModifierKeys == Keys.Shift) {
                            abstractNode.Selected = !abstractNode.Selected;
                        } else {
                            abstractNode.Selected = true;
                        }

                        HandleSelection();
                        Refresh();
                    }
                }

                if (leftMouseButton && rightMouseButton) {
                    _command = CommandMode.ScaleView;
                    return;
                }

                if (element == null) {
                    _command = CommandMode.MarqueSelection;
                    return;
                }

                if (element is SocketIn socketIn) {
                    _command = CommandMode.Wiring;

                    Wire connection = socketIn.InputConnection;

                    if (connection != null) {
                        _tempWire = new Wire {From = connection.From, To = null};
                        Disconnect(connection);
                    } else {
                        _tempWire = new Wire {From = null, To = socketIn};
                    }

                    return;
                }

                if (element is SocketOut socketOut) {
                    _command = CommandMode.Wiring;
                    _tempWire = new Wire {From = socketOut};
                    return;
                }

                if (element is AbstractNode node) {
                    if (node.GetBoundsArea(originalLocation) == NodeBoundsArea.HEADER) {
                        _command = CommandMode.MoveSelection;
                        BringNodeToFront(node);
                    } else {
                        // rest of node
                    }
                }
            }

            if ((e.Button & MouseButtons.Right) != 0) {
                rightMouseButton = true;

                if (leftMouseButton && rightMouseButton) {
                    _command = CommandMode.ScaleView;
                    return;
                }

                if (_command == CommandMode.Edit && FindElementAtMousePoint(e.Location) == null) {
                    rightMouseButton = false;
                    OpenContextMenu(e.Location);
                    return;
                }
            }

            if (e.Button == MouseButtons.Middle) {
                _command = CommandMode.TranslateView;
            }

            var points = new[] {originalLocation};
            transformation.TransformPoints(points);
            originalMouseLocation = this.PointToScreen(new Point((int) points[0].X, (int) points[0].Y));
        }

        private void BringNodeToFront(AbstractNode node) {
            if (_graphNodes.Remove(node))
                _graphNodes.Add(node);

            Refresh();
        }

        #endregion

        #region OnMouseMove

        protected override void OnMouseMove(MouseEventArgs e) {
            base.OnMouseMove(e);

            Point currentLocation;
            PointF transformed_location;
            if (abortDrag) {
                transformed_location = originalLocation;

                var points = new PointF[] {originalLocation};
                transformation.TransformPoints(points);
                currentLocation = new Point((int) points[0].X, (int) points[0].Y);
            } else {
                currentLocation = e.Location;

                var points = new PointF[] {currentLocation};
                inverse_transformation.TransformPoints(points);
                transformed_location = points[0];
            }

            var deltaX = (lastLocation.X - currentLocation.X) / zoom;
            var deltaY = (lastLocation.Y - currentLocation.Y) / zoom;

            switch (_command) {
                case CommandMode.ScaleView:
                    if (!mouseMoved) {
                        if ((Math.Abs(deltaY) > 1))
                            mouseMoved = true;
                    }

                    if (mouseMoved &&
                        (Math.Abs(deltaY) > 0)) {
                        zoom *= (float) Math.Pow(2, deltaY / 100.0f);
                        Cursor.Position = this.PointToScreen(lastLocation);
                        snappedLocation = //lastLocation = 
                            currentLocation;
                        Invalidate();
                    }

                    return;
                case CommandMode.TranslateView: {
                    if (!mouseMoved) {
                        if ((Math.Abs(deltaX) > 1) ||
                            (Math.Abs(deltaY) > 1))
                            mouseMoved = true;
                    }

                    if (mouseMoved &&
                        (Math.Abs(deltaX) > 0) ||
                        (Math.Abs(deltaY) > 0)) {
                        translation.X -= deltaX * zoom;
                        translation.Y -= deltaY * zoom;
                        snappedLocation = lastLocation = currentLocation;
                        Invalidate();
                    }

                    return;
                }
                case CommandMode.MoveSelection: {
                    foreach (var node in _graphNodes.Where(node => node.Selected)) {
                        node.Location = new Point((int) Math.Round(node.Location.X - deltaX),
                            (int) Math.Round(node.Location.Y - deltaY));
                        node.Calculate();
                    }

                    snappedLocation = lastLocation = currentLocation;
                    Refresh();
                    return;
                }
                case CommandMode.Wiring: {
                    Invalidate();
                    return;
                }
                case CommandMode.MarqueSelection:
                    if (!mouseMoved) {
                        if ((Math.Abs(deltaX) > 1) ||
                            (Math.Abs(deltaY) > 1))
                            mouseMoved = true;
                    }

                    if (mouseMoved &&
                        (Math.Abs(deltaX) > 0) ||
                        (Math.Abs(deltaY) > 0)) {
                        var marque_rectangle = GetMarqueRectangle();

                        foreach (var node in _graphNodes) {
                            bool contains = marque_rectangle.Contains(node.Pivot);
                            node.Selected = contains || (Control.ModifierKeys == Keys.Shift && node.Selected);
                        }

                        snappedLocation = lastLocation = currentLocation;
                        Invalidate();
                    }

                    return;

                default:
                case CommandMode.Edit:
                    break;
            }
        }

        #endregion

        #region OnMouseUp

        protected override void OnMouseUp(MouseEventArgs e) {
            base.OnMouseUp(e);

            UpdateOriginalLocation(e.Location);

            var element = FindElementAtOriginal(originalLocation);

            if ((e.Button & MouseButtons.Left) != 0) {
                leftMouseButton = false;

                if (_command == CommandMode.ScaleView) {
                    _command = CommandMode.Edit;
                    return;
                }

                if (_command == CommandMode.MarqueSelection) {
                    _command = CommandMode.Edit;
                    HandleSelection();
                    Refresh();
                    return;
                }

                if (_command == CommandMode.MoveSelection) {
                    _command = CommandMode.Edit;
                    return;
                }

                if (_command == CommandMode.Wiring && _tempWire != null) {
                    if (_tempWire.From != null && element is SocketIn @socketIn) {
                        var lastConnection = @socketIn.InputConnection;
                        Disconnect(lastConnection);

                        Connect(_tempWire.From, @socketIn);
                    }

                    if (_tempWire.To != null && element is SocketOut @socketOut) {
                        Connect(@socketOut, _tempWire.To);
                    }

                    _tempWire = null;
                    _command = CommandMode.Edit;
                    Refresh();
                    return;
                }
            }

            if ((e.Button & MouseButtons.Right) != 0) {
                rightMouseButton = false;
                if (_command == CommandMode.ScaleView) {
                    _command = CommandMode.Edit;
                    return;
                }
            }

            if ((e.Button & MouseButtons.Middle) != 0) {
                if (_command == CommandMode.TranslateView) {
                    _command = CommandMode.Edit;
                    return;
                }
            }

            if (!dragging)
                return;

            try {
                Point currentLocation;
                PointF transformed_location;
                if (abortDrag) {
                    transformed_location = originalLocation;

                    var points = new PointF[] {originalLocation};
                    transformation.TransformPoints(points);
                    currentLocation = new Point((int) points[0].X, (int) points[0].Y);
                } else {
                    currentLocation = e.Location;

                    var points = new PointF[] {currentLocation};
                    inverse_transformation.TransformPoints(points);
                    transformed_location = points[0];
                }


                switch (_command) {
                    case CommandMode.MarqueSelection:
                        this.Invalidate();
                        return;
                    case CommandMode.ScaleView:
                        return;
                    case CommandMode.TranslateView:
                        return;

                    default:
                    case CommandMode.Edit:
                        break;
                }
            } finally {
                dragging = false;
                _command = CommandMode.Edit;

                base.OnMouseUp(e);
            }
        }

        #endregion

        #region OnMouseClick

        protected override void OnMouseClick(MouseEventArgs e) {
            base.OnMouseClick(e);

            if (e.Button == MouseButtons.Left) {
            }

            if (e.Button == MouseButtons.Right) {
                // TODO context menu
            }
        }

        #endregion

        /*
         * This translates mouse click real position to relative position in matrix
         */
        private PointF GetTranslatedPosition(Point mouseClick) {
            var points = new PointF[] {mouseClick};
            inverse_transformation.TransformPoints(points);
            return points[0];
        }

        private PointF GetOriginalPosition(PointF transformed) {
            var points = new PointF[] {transformed};
            transformation.TransformPoints(points);
            return points[0];
        }

        #region OnKeyDown

        protected override void OnKeyDown(KeyEventArgs e) {
            base.OnKeyDown(e);

            // translate to center
            if (e.KeyCode == Keys.Space) {
                translation.X = (Width / 2f);
                translation.Y = (Height / 2f);
                Refresh();
            }

            // switch name -> type
            if ((e.KeyData & Keys.Alt) == Keys.Alt) {
                CommonStates.SocketCaptionTypeToggle = true;
                Refresh();
            }

            // show bounds (dev)
            if ((e.KeyData & Keys.X) == Keys.X) {
                _renderBounds = true;
                Refresh();
            }

            // back to edit mode
            if ((e.KeyData & Keys.Escape) == Keys.Escape) {
                _command = CommandMode.Edit;
            }

            // select all
            if (e.Control && e.KeyCode == Keys.A) {
                SetNodeSelected(true);
                Refresh();
            }

            // deselect all
            if (e.Control && e.Shift && e.KeyCode == Keys.A) {
                SetNodeSelected(false);
                Refresh();
            }

            // delete selected
            if ((e.KeyData & Keys.Delete) == Keys.Delete) {
                DeleteSelectedNodes();
            }
        }

        #endregion

        #region OnKeyUp

        protected override void OnKeyUp(KeyEventArgs e) {
            base.OnKeyUp(e);

            // switch type -> name
            if ((e.KeyData & Keys.Menu) == Keys.Menu) {
                CommonStates.SocketCaptionTypeToggle = false;
                Refresh();
            }

            // hide bounds (dev)
            if ((e.KeyData & Keys.X) == Keys.X) {
                _renderBounds = false;
                Refresh();
            }
        }

        #endregion

        private IElement FindElementAtOriginal(PointF point) {
            foreach (var node in _graphNodes) {
                // find socket    
                foreach (var socket in node.Sockets.Where(socket => socket.BoundsFull.Contains(point))) {
                    if (socket.GetType() == typeof(SocketIn))
                        return (SocketIn) socket;
                    if (socket.GetType() == typeof(SocketOut))
                        return (SocketOut) socket;
                }

                // find node
                if (node.BoundsFull.Contains(point)) {
                    return node;
                }
            }

            return null;
        }

        private IElement FindElementAtMousePoint(Point mouseClickPosition) {
            var position = GetTranslatedPosition(mouseClickPosition);

            foreach (var node in _graphNodes) {
                // try to find socket
                foreach (var socket in node.Sockets.Where(socket => socket.BoundsFull.Contains(position))) {
                    return socket;
                }

                // try to find node
                if (node.BoundsFull.Contains(position)) {
                    return node;
                }
            }

            return null;
        }

        #region ContextMenu

        private Point _contextMenuMouseClick;

        private void OpenContextMenu(Point location) {
            _contextMenuMouseClick = location;

            var contextMenu = new ContextMenu();
            var categories = new List<MenuItem>();

            var menuItemAdd = new MenuItem("Add Node");
            var menuItemExit = new MenuItem("Exit", MenuItemClickExit); // TODO remove temp exit item

            foreach (var contextNode in _contextNodeList) {
                var cnName = contextNode.NodeName;
                var cnCat = contextNode.NodeCategory;

                if (cnCat.Equals(""))
                    menuItemAdd.MenuItems.Add(cnName, MenuItemClickNode);
                else {
                    var categoryMenuItem = categories.Find(item => item.Text.Equals(cnCat));
                    if (categoryMenuItem != null) {
                        categoryMenuItem.MenuItems.Add(new MenuItem(cnName, MenuItemClickNode));
                    } else {
                        categoryMenuItem = new MenuItem(cnCat);
                        categoryMenuItem.MenuItems.Add(new MenuItem(cnName, MenuItemClickNode));
                        categories.Add(categoryMenuItem);
                    }
                }
            }

            foreach (var category in categories) {
                menuItemAdd.MenuItems.Add(category);
            }

            contextMenu.MenuItems.AddRange(new[] {
                menuItemAdd, menuItemExit
            });

            contextMenu.Show(this, location);
        }

        // TODO remove this... exit will be handled by application
        private void MenuItemClickExit(object sender, EventArgs e) {
            Application.Exit();
        }

        private void MenuItemClickNode(object sender, EventArgs e) {
            if (!(sender is MenuItem item)) return;

            var contextNode = _contextNodeList.Find(context => context.NodeName.Equals(item.Text));
            var nodeType = contextNode.NodeType;

            var newNodeObj = (AbstractNode) Activator.CreateInstance(nodeType);

            var locationTranslated = GetTranslatedPosition(_contextMenuMouseClick);

            newNodeObj.Location = new Point((int) locationTranslated.X, (int) locationTranslated.Y);
            newNodeObj.Calculate();
            newNodeObj.Execute();

            AddNode(newNodeObj);

            Refresh();
        }

        #endregion

        #region NodeSelection

        private List<AbstractNode> lastSelected = new List<AbstractNode>();

        private void HandleSelection() {
            var selected = _graphNodes.Where(node => node.Selected).ToList();

            if (lastSelected.Count != selected.Count) {
                SelectionChanged?.Invoke(this, selected);
                lastSelected = selected;
            } else if (!lastSelected.SequenceEqual(selected)) {
                SelectionChanged?.Invoke(this, selected);
                lastSelected = selected;
            }
        }

        #endregion
    }
}