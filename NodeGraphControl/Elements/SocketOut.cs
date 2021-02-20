using System;
using System.Collections.Generic;

namespace NodeGraphControl.Elements {
    public class SocketOut : AbstractSocket {
        public readonly List<Wire> OutputConnections = new List<Wire>();

        public SocketOut(Type valueType, string name, AbstractNode owner) : base(valueType, owner) {
            SocketName = name;
        }

        public override void UpdateValue(object value) {
            if(value.Equals(Value))
                return;
            
            if (value != null && value.GetType() != ValueType) {
                throw new Exception("Incompatible Type: " + value.GetType() + " ! Expected: " + ValueType + ".");
            } else {
                Value = value;
                foreach (var connection in OutputConnections) {
                    connection.Flow();
                }
            }
        }

        public override void Disconnect() {
            for (var i = OutputConnections.Count - 1; i >= 0; i--) {
                OutputConnections[i].Disconnect();
            }
        }
    }
}