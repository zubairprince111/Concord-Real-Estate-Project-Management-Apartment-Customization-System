using System.Security.Cryptography;
using System.Text;

namespace Final.Security
{
    public static class PasswordHasher
    {
        public static string Hash(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password)); //text k byte array,SHA algo diye fixed size
                var sb = new StringBuilder(bytes.Length * 2);
                foreach (byte b in bytes)
                    sb.Append(b.ToString("x2")); //2 digit hexa te, 64 bit
                return sb.ToString();
            }
        }

        public static bool Verify(string password, string hash)
        {
            return Hash(password) == hash;
        }
    }
}
