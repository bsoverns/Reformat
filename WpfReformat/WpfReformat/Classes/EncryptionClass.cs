using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Windows;

namespace WpfReformat.Classes
{
    class EncryptionClass
    {
        //private static string Key = "alskd2dcghh5v;d'eoerkkemwertgxzj"; //32 CHARACTERS
        //private static string IV = "chae3tgffnkldfsk"; //16 CHARACTERS
        private static string return_data = "";

        public static string Encrypt(string text, string Key, string IV)
        {
            try
            {
                byte[] plaintextbytes = System.Text.ASCIIEncoding.ASCII.GetBytes(text);
                AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
                aes.BlockSize = 128;
                aes.KeySize = 256;
                aes.Key = System.Text.ASCIIEncoding.ASCII.GetBytes(Key);
                aes.IV = System.Text.ASCIIEncoding.ASCII.GetBytes(IV);
                aes.Padding = PaddingMode.PKCS7;
                aes.Mode = CipherMode.CBC;
                ICryptoTransform crypto = aes.CreateEncryptor(aes.Key, aes.IV);
                byte[] encrypted = crypto.TransformFinalBlock(plaintextbytes, 0, plaintextbytes.Length);
                crypto.Dispose();

                return_data = Convert.ToBase64String(encrypted);
                return return_data;
            }

            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
                return return_data;
            }

            finally { }
        }

        public static string Decrypt(string encrypted, string Key, string IV)
        {
            try
            {
                byte[] encryptedbytes = Convert.FromBase64String(encrypted);
                AesCryptoServiceProvider aes = new AesCryptoServiceProvider();
                aes.BlockSize = 128;
                aes.KeySize = 256;
                aes.Key = System.Text.ASCIIEncoding.ASCII.GetBytes(Key);
                aes.IV = System.Text.ASCIIEncoding.ASCII.GetBytes(IV);
                aes.Padding = PaddingMode.PKCS7;
                aes.Mode = CipherMode.CBC;
                ICryptoTransform crypto = aes.CreateDecryptor(aes.Key, aes.IV);
                byte[] secret = crypto.TransformFinalBlock(encryptedbytes, 0, encryptedbytes.Length);
                crypto.Dispose();

                return_data = System.Text.ASCIIEncoding.ASCII.GetString(secret);
                return return_data;
            }

            catch (Exception ex)
            {
                return_data = "The password that you entered is incorrect.";
                return return_data;
            }

            finally { }
        }
    }
}
