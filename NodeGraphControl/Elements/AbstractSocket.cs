using System;
using System.Drawing;

namespace NodeGraphControl.Elements {
    public abstract class AbstractSocket : IElement {
        public string SocketName { get; protected set; }

        public string SocketDescription { get; set; }

        public readonly Type ValueType;

        protected readonly AbstractNode Owner;

        private RectangleF _bounds;

        public RectangleF BoundsFull {
            get => _bounds;
            set => _bounds = value;
        }

        protected PointF _pivot; 
        public PointF Pivot {
            get { return _pivot; }
        }
        
        protected object Value { get; set; }

        public AbstractSocket(Type valueType, AbstractNode owner) {
            ValueType = valueType;
            Owner = owner;
        }

        public abstract void UpdateValue(object value);

        public abstract void Disconnect();

        public object GetValue() {
            return Value;
        }

        public void SetPivot(PointF pivot) {
            _pivot = pivot;
        } 
    }
}