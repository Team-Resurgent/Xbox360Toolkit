using System;
using System.Runtime.InteropServices;

internal static unsafe partial class UnpackNative
{
    [DllImport("libXexUnpack", CallingConvention = CallingConvention.Cdecl)]
    public static extern void UnpackXexData(byte* inputData, uint inputSize, byte* outputData, uint outputDataSize, uint windowSize, uint firstDataSize, ref uint error);
}

