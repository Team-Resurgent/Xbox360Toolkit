using System.Runtime.InteropServices;

namespace Xbox360Toolkit
{
    internal unsafe static class XexUnpack
    {
        [DllImport("libXexUnpack", CallingConvention = CallingConvention.Cdecl)]
        private static extern void UnpackXexData(byte* inputData, uint inputSize, byte* outputData, uint outputDataSize, uint windowSize, uint firstDataSize, ref uint error);

        public unsafe static bool UnpackXexData(byte[] input, uint imageSize, uint windowSize, uint firstSize, out byte[] output)
        {
            output = new byte[imageSize];
            fixed (byte* outputArray = output)
            {
                fixed (byte* inputArray = input)
                {
                    uint error = 0xffffff;
                    UnpackXexData(inputArray, (uint)input.Length, outputArray, imageSize, windowSize, firstSize, ref error);
                    return error == 0;
                }
            }
        }
    }
}