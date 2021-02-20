using System.Drawing;

namespace NodeGraphControl.Elements {
    public interface IElement {
        RectangleF BoundsFull { get; }

        PointF Pivot { get; }
    }
}