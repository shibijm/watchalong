using System;
using System.Security.Cryptography;
using System.Text;

namespace WatchAlong.Utils;

public static class Aes {

	private static readonly System.Security.Cryptography.Aes aes;
	private static readonly byte[] key = [191, 209, 30, 99, 7, 238, 55, 211, 7, 233, 147, 30, 17, 47, 94, 166, 125, 232, 15, 211, 48, 143, 138, 21, 43, 226, 30, 71, 123, 103, 10, 162];

	static Aes() {
		aes = System.Security.Cryptography.Aes.Create();
		aes.Mode = CipherMode.CBC;
		aes.Padding = PaddingMode.PKCS7;
		aes.BlockSize = 128;
		aes.KeySize = 256;
		aes.Key = key;
	}

	public static string CbcDecrypt(byte[] data) {
		byte[] IV = new byte[16];
		Array.Copy(data, IV, IV.Length);
		aes.IV = IV;
		ICryptoTransform decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
		byte[] cipherBytes = new byte[data.Length - IV.Length];
		Array.Copy(data, 16, cipherBytes, 0, cipherBytes.Length);
		byte[] decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);
		return Encoding.UTF8.GetString(decryptedBytes);
	}

	public static byte[] CbcEncrypt(string data) {
		aes.GenerateIV();
		ICryptoTransform encryptor = aes.CreateEncryptor(aes.Key, aes.IV);
		byte[] dataBytes = Encoding.UTF8.GetBytes(data);
		byte[] encryptedBytes = encryptor.TransformFinalBlock(dataBytes, 0, dataBytes.Length);
		byte[] encryptedData = new byte[encryptedBytes.Length + aes.IV.Length];
		Array.Copy(aes.IV, encryptedData, aes.IV.Length);
		Array.Copy(encryptedBytes, 0, encryptedData, aes.IV.Length, encryptedBytes.Length);
		return encryptedData;
	}

}
