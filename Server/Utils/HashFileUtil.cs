using System;
using System.IO;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace Calendare.Server.Utils;

public static class HashFileUtil
{
    public static async Task<string> PrettyMD5Hash(this FileInfo fileInfo, CancellationToken cancellationToken)
    {
        using FileStream stream = new FileStream(fileInfo.FullName, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, useAsync: true);
        var hashBytes = await MD5.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexStringLower(hashBytes);
    }
}
