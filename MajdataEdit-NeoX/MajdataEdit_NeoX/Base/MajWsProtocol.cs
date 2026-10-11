namespace MajdataEdit_NeoX.Base;

/// <summary>
/// MajProtocol 不含传输层定义，留给调用方决定 URL/port/path。
/// 这些常量必须与 MajdataViewX 端 <c>WsServer.Endpoint</c> 完全一致。
/// </summary>
internal static class MajWsProtocol
{
    public const int Port = 8083;
    public const string ServicePath = "/majdata";
    public static string ServerUrl => CreateServerUrl("127.0.0.1");

    public static string CreateServerUrl(string ip) => $"ws://{ip}:{Port}{ServicePath}";
}