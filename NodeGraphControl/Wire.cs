using System;
using NodeGraphControl.Elements;

namespace NodeGraphControl {
    public class Wire {
        private SocketOut _from;

        public SocketOut From {
            get => _from;
            set {
                if (value == null) {
                    _from = null;
                } else {
                    if (_to != null 
                        && value.ValueType != _to.ValueType 
                        && !value.ValueType.IsSubclassOf(_to.ValueType))
                        throw new Exception("Type mismatch!");

                    if (value.GetType() == typeof(SocketOut))
                        _from = value;
                    else {
                        throw new Exception("Can't connect output to output!");
                    }
                }
            }
        }

        private SocketIn _to;

        public SocketIn To {
            get => _to;
            set {
                if (value == null) {
                    _to = null;
                } else {
                    if (_from != null
                        && value.ValueType != _from.ValueType
                        && !_from.ValueType.IsSubclassOf(value.ValueType))
                        throw new Exception("Type mismatch!");

                    if (value.GetType() == typeof(SocketIn))
                        _to = value;
                    else {
                        throw new Exception("Can't connect input to input!");
                    }
                }
            }
        }

        public Wire() {
        }

        public Wire(SocketOut from, SocketIn to) {
            From = from;
            To = to;
        }

        public void Flow() {
            _to.UpdateValue(_from.GetValue());
        }

        public void Disconnect() {
            _from.OutputConnections.Remove(this);
            _to.Disconnect();
        }
    }
}