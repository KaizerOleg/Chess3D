using System.Collections;
using System.Collections.Generic;
using Unity.Networking.Transport;
using UnityEngine;

public class NetRematch : NetMessage
{
    public int teamID;
    public byte wantRematch;

    public NetRematch(){
        Code = OpCode.REMATCH;
    }
    public NetRematch(DataStreamReader reader){
        Code = OpCode.REMATCH;
        Deserialize(reader);
    }
    public override void Serialize (ref DataStreamWriter writer) {
        writer.WriteByte((byte)Code);
        writer.WriteInt(teamID);
        writer.WriteByte(wantRematch);
    }

    public override void Deserialize (DataStreamReader reader) {
        teamID = reader.ReadInt();
        wantRematch = reader.ReadByte();
    }
    public override void ReceivedOnClient () {
        NetUtility.C_REMATCH?.Invoke(this);
    }
    public override void ReceivedOnServer (NetworkConnection cnn) {
        NetUtility.S_REMATCH?.Invoke(this, cnn);
    }
}
