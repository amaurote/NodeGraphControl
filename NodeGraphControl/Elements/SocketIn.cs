using System;

namespace NodeGraphControl.Elements {
    public class SocketIn : AbstractSocket {
        public Wire InputConnection = null;

        public SocketIn(Type valueType, string name, AbstractNode owner) : base(valueType, owner) {
            SocketName = name;
        }

        public override void UpdateValue(object value) {
            if(value != Value) {
                Value = value;
                Owner.Execute();
            }
        }

        public override void Disconnect() {
            InputConnection = null;
            Value = null;
            Owner.Execute();
        }
    }
}