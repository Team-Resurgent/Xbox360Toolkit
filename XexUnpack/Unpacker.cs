public static class Unpacker
{
    public unsafe static bool UnpackXexData(byte[] input, uint imageSize, uint windowSize, uint firstSize, out byte[] output)
    {
        output = new byte[imageSize];
        fixed (byte* outputArray = output)
        {
            fixed (byte* inputArray = input)
            {
                uint error = 0xffffff;
                UnpackNative.UnpackXexData(inputArray, (uint)input.Length, outputArray, imageSize, windowSize, firstSize, ref error);
                return error == 0;
            }
        }
    }
}