using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace NameCrypto
{
    public class MedStormCrypto
    {
        string m_password = "Mer_Storm_";
        Aes m_AesAlgorithme;

        public MedStormCrypto()
        {
            m_AesAlgorithme = GetAlgorithme();
        }

        private Aes GetAlgorithme()
        {
            Aes myAlg = Aes.Create();
            byte[] salt = Encoding.ASCII.GetBytes("MyLongSalt");
            Rfc2898DeriveBytes key =
                new Rfc2898DeriveBytes(m_password, salt);
            myAlg.Key = key.GetBytes(myAlg.KeySize / 8);
            myAlg.IV = key.GetBytes(myAlg.BlockSize / 8);
            return myAlg;
        }

        public byte[] EncryptStringToBytes_Aes(string plainText)
        {
            byte[] encrypted;

            // Create an encryptor to perform the stream transform.
            ICryptoTransform encryptor = m_AesAlgorithme.CreateEncryptor();

            // Create the streams used for encryption.
            using (MemoryStream msEncrypt = new MemoryStream())
            {
                using (CryptoStream csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                {
                    using (StreamWriter swEncrypt = new StreamWriter(csEncrypt))
                    {
                        //Write all data to the stream.
                        swEncrypt.Write(plainText);
                    }
                    encrypted = msEncrypt.ToArray();
                }
            }
            return encrypted;
        }

        public string? DecryptStringFromBytes_Aes(byte[] cipherText)
        {
            string? plaintext = null;

            // Create a decryptor to perform the stream transform.
            ICryptoTransform decryptor = m_AesAlgorithme.CreateDecryptor();

            // Create the streams used for decryption.
            using (MemoryStream msDecrypt = new MemoryStream(cipherText))
            {
                using (CryptoStream csDecrypt = new CryptoStream(msDecrypt, decryptor, CryptoStreamMode.Read))
                {
                    using (StreamReader srDecrypt = new StreamReader(csDecrypt))
                    {

                        // Read the decrypted bytes from the decrypting stream
                        // and place them in a string.
                        plaintext = srDecrypt.ReadToEnd();
                    }
                }
            }
            return plaintext;
        }
    }
}
