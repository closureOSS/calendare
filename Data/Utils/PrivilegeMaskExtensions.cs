using System;
using System.Collections;
using Calendare.Data.Models;

namespace Calendare.Data.Utils;

public static class PrivilegeMaskExtensions
{
    public static BitArray ToBitArray(this PrivilegeMask privilege)
    {
        var bytes = BitConverter.GetBytes((ushort)privilege);
        return new BitArray(bytes);
    }

    public static PrivilegeMask FromBitArray(this BitArray bitArray)
    {
        uint[] array = new uint[1];
        bitArray.CopyTo(array, 0);
        return (PrivilegeMask)array[0];
    }

    public static bool IsEqual(this PrivilegeMask privilege, BitArray bitArray)
    {
        var mask = bitArray.FromBitArray();
        return privilege == mask;
    }
}
