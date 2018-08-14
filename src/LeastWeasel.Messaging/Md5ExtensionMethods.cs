using System;
using System.Security.Cryptography;

namespace LeastWeasel.Messaging
{
    public static class Md5ExtensionMethods
    {

        public static long ComputeMD5HashAsInt64(this string value)
        {
            var md5 = new MD5CryptoServiceProvider();
            var valueBytes = System.Text.Encoding.Unicode.GetBytes(value);
            var hash = md5.ComputeHash(valueBytes);
            var result = BitConverter.ToInt64(hash, 0);
            return result;
        }
    }
}