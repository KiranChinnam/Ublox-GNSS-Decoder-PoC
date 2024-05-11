using System.IO.Ports;

namespace Ubx_Query;

enum UbxDecoderState
{
    RxSyncCharOne,
    RxSyncCharTwo,
    RxMsgClass,
    RxMsgId,
    RxLenOne,
    RxLenTwo,
    RxPayload,
    RxChecksumOne,
    RxChecksumTwo
}

public class UbxMessage
{
    public byte MessageClass { get; set; }
    public byte MessageId { get; set; }
    public required byte[] Payload { get; set; }
}

public class UbloxDecoder
{
    private const byte SyncCharOne = 0xb5;
    private const byte SyncCharTwo = 0x62;

    private Queue<UbxMessage> messageQueue = new Queue<UbxMessage>();

    public UbloxDecoder()
    {
        if (BitConverter.IsLittleEndian) return;
        Console.WriteLine("Warning, your system is big endian - this is unsupported.");
        throw new NotSupportedException("Big Endian Arch detected.");
    }
    
    private UbxDecoderState _decoderState = UbxDecoderState.RxSyncCharOne;
    private byte _cMsgClass;
    private byte _cMsgId;
    private byte _cLenPartOne;
    private byte[] _cBuffer = Array.Empty<byte>();
    private int _buffPos;
    private byte _cChecksumOne;

    private static byte[] CalculateUbxChecksum(byte msgClass, byte msgId, IReadOnlyCollection<byte> payload)
    {
        byte a = 0;
        byte b = 0;

        a += msgClass;
        b += a;

        a += msgId;
        b += a;

        //Extract the payload len
        var len = payload.Count;
        //len is little endian
        a += (byte)((len >> 0) & 0xff);
        b += a;
        
        a += (byte)((len >> 8) & 0xff);
        b += a;

        foreach (var buff in payload)
        {
            a += buff;
            b += a;
        }

        return [a, b];
    }

    public void IngestBytes(byte[] bytes)
    {
        var currentByte = 0;
        while (currentByte < bytes.Length)
        {
            switch (_decoderState)
            {
                case UbxDecoderState.RxSyncCharOne:
                    if (bytes[currentByte++] == SyncCharOne)
                    {
                        _decoderState = UbxDecoderState.RxSyncCharTwo;
                    }
                    break;
                case UbxDecoderState.RxSyncCharTwo:
                    _decoderState = bytes[currentByte++] == SyncCharTwo ? UbxDecoderState.RxMsgClass : UbxDecoderState.RxChecksumOne;
                    break;
                case UbxDecoderState.RxMsgClass:
                    _cMsgClass = bytes[currentByte++];
                    _decoderState = UbxDecoderState.RxMsgId;
                    break;
                case UbxDecoderState.RxMsgId:
                    _cMsgId = bytes[currentByte++];
                    _decoderState = UbxDecoderState.RxLenOne;
                    break;
                case UbxDecoderState.RxLenOne:
                    _cLenPartOne = bytes[currentByte++];
                    _decoderState = UbxDecoderState.RxLenTwo;
                    break;
                case UbxDecoderState.RxLenTwo:
                    //Calculate the payload length and allocate the buffer for it
                    var len = _cLenPartOne | bytes[currentByte++] << 8;
                    _cBuffer = new byte[len];
                    _decoderState = len>0?UbxDecoderState.RxPayload:UbxDecoderState.RxChecksumOne;
                    _buffPos = 0;
                    break;
                case UbxDecoderState.RxPayload:
                    var bytesRemaining = _cBuffer.Length - _buffPos;
                    var availableBytes = bytes.Length - currentByte;
                    var bytesToRead = availableBytes >= bytesRemaining ? bytesRemaining : availableBytes;
                    Array.Copy(bytes,currentByte,_cBuffer,_buffPos,bytesToRead);
                    currentByte += bytesToRead;
                    _buffPos += bytesToRead;
                    if (_cBuffer.Length == _buffPos) _decoderState = UbxDecoderState.RxChecksumOne;
                    break;
                case UbxDecoderState.RxChecksumOne:
                    _cChecksumOne = bytes[currentByte++];
                    _decoderState = UbxDecoderState.RxChecksumTwo;
                    break;
                case UbxDecoderState.RxChecksumTwo:
                    var checksumTwo = bytes[currentByte++];
                    var calcChecksum = CalculateUbxChecksum(_cMsgClass, _cMsgId, _cBuffer);
                    if (calcChecksum[0] == _cChecksumOne && calcChecksum[1] == checksumTwo)
                    {
                        //Checksum is valid, compose the message and queue it.
                        var newMessage = new UbxMessage()
                        {
                            MessageClass = _cMsgClass,
                            MessageId = _cMsgId,
                            Payload = _cBuffer
                        };
                        messageQueue.Enqueue(newMessage);
                    }
                    else
                    {
                        Console.WriteLine("Checksum fail");
                    }
                    _decoderState = UbxDecoderState.RxSyncCharOne;
                    
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }

    public bool MessagesAvailable() => messageQueue.Count > 0;

    public UbxMessage GetOneMessage() => messageQueue.Dequeue();

    public UbxNavPvt? DecodeNavPvt(byte[] payload)
    {
        // TODO: Logger warn 92 bytes
        if (payload.Length != 92) return null;
        var message = new UbxNavPvt();
        var year = BitConverter.ToUInt16(payload, 4);
        var month = payload[6];
        var day = payload[7];
        var hour = payload[8];
        var min = payload[9];
        var sec = payload[10];
        var nano = BitConverter.ToInt32(payload, 16);
        message.UtcTime = new DateTimeOffset(year, month, day, hour, min, sec, (int)(nano / 1e6), TimeSpan.Zero);
        message.ValidDate = (payload[11] & 1) == 1;
        message.ValidTime = ((payload[11] >> 1) & 1) == 1;
        message.TimeFullyResolved = ((payload[11] >> 2) & 1) == 1;
        message.ValidMagneticDeclination = ((payload[11] >> 3) & 1) == 1;
        message.TimeAccuracy = BitConverter.ToUInt32(payload, 12);
        message.FixType = (UbxNavPvtFixType)payload[20];
        message.GnssFixOk = ((payload[21] >> 0) & 1) == 1;
        message.DifferentialCorrectionsApplied = ((payload[21] >> 1) & 1) == 1;
        message.SatellitesUsed = payload[23];
        message.Longitude = (float)(BitConverter.ToInt32(payload, 24) * 1e-7);
        message.Latitude = (float)(BitConverter.ToInt32(payload, 28) * 1e-7);
        message.HorizontalAccuracyMm = BitConverter.ToUInt32(payload, 40);
        message.VerticalAccuracyMm = BitConverter.ToUInt32(payload, 44);
        
        
        

        return message;
    }
}