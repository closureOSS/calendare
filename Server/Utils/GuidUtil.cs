using System;
using System.Buffers.Text;
using System.Linq;
using System.Text;
using Serilog;

namespace Calendare.Server.Utils;

public static class GuidUtil
{
    public const int GuidLength = 16;

    public static string ToBase64Url(this Guid guid)
    {
        return Base64Url.EncodeToString(guid.ToByteArray());
    }

    public static bool TryGuidFromBase64Url(string guid64url, out Guid guid)
    {
        guid = Guid.Empty;
        var bytesUtf8Raw = new byte[64];
        if (!Encoding.UTF8.TryGetBytes(guid64url, bytesUtf8Raw, out var lengthUtf8))
        {
            return false;
        }
        var bytesUtf8 = bytesUtf8Raw.Take(lengthUtf8).ToArray();
        var bytesBase64 = new byte[GuidLength];
        try
        {
            if (!Base64Url.TryDecodeFromUtf8(bytesUtf8, bytesBase64, out var lengthBase64))
            {
                return false;
            }
            if (lengthBase64 != GuidLength)
            {
                return false;
            }
            guid = new Guid(bytesBase64);
        }
        catch (FormatException fe)
        {
            Log.Error(fe, "GUID invalid {guid64url}", guid64url);
            return false;
        }
        return true;
    }
}
