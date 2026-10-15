using System.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using UnityEngine;

public class NetMessage
{
    public OpCode Code {set; get; }

    public virtual void Serialize (ref DataStreamWriter writer) {
        writer.WriteByte((byte)Code);
    }
    public virtual void Deserialize (DataStreamReader reader) {
        //asd
    }
    public virtual void ReceivedOnClient () {
        //asd
    }
    public virtual void ReceivedOnServer (NetworkConnection cnn) {
        //asd
    }
}
