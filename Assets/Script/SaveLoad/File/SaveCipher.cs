using System;
using System.Security.Cryptography;
using System.Text;

// 평문 JSON을 암호화·서명해서 파일 바이트로 만들고, 파일 바이트를 검사·복호화해서 평문 JSON으로 되돌린다.
public class SaveCipher
{
    private const int IvSize = 16;
    private const int SignSize = 32;

    private readonly SaveKey saveKey;

    // 열쐐 보관소를 받는다.
    public SaveCipher(SaveKey saveKey)
    {
        this.saveKey = saveKey;
    }

    // 평문 JSON을 암호화하고 서명을 붙여 파일 바이트로 조립한다.
    public byte[] ToFileBytes(string json)
    {
        byte[] iv;
        byte[] cipherText = Encrypt(json, out iv);
        byte[] ivAndCipher = Combine(iv, cipherText);
        byte[] sign = Sign(ivAndCipher);

        return Combine(ivAndCipher, sign);
    }

    // 파일 바이트의 서명을 먼저 검사하고, 통과하면 복호화해서 평문 JSON으로 되돌린다. 서명이 안 맞으면 CryptographicException을 던진다.
    public string ToJson(byte[] fileBytes)
    {
        Verify(fileBytes);

        byte[] ivAndCipher = TakeIvAndCipher(fileBytes);
        byte[] iv = TakeIv(ivAndCipher);
        byte[] cipherText = TakeCipherText(ivAndCipher);

        return Decrypt(cipherText, iv);
    }

    // 평문 JSON을 AES-256-CBC로 암호화하고, 이번에 쓴 IV를 함께 내보낸다.
    private byte[] Encrypt(string json, out byte[] iv)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = saveKey.CipherKey;
            aes.GenerateIV();
            iv = aes.IV;

            byte[] plainBytes = Encoding.UTF8.GetBytes(json);
            using (ICryptoTransform encryptor = aes.CreateEncryptor())
            {
                return encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);
            }
        }
    }

    // 암호문 바이트를 같은 IV로 복호화해서 평문 JSON으로 되돌린다.
    private string Decrypt(byte[] cipherText, byte[] iv)
    {
        using (Aes aes = Aes.Create())
        {
            aes.Key = saveKey.CipherKey;
            aes.IV = iv;

            using (ICryptoTransform decryptor = aes.CreateDecryptor())
            {
                byte[] plainBytes = decryptor.TransformFinalBlock(cipherText, 0, cipherText.Length);
                return Encoding.UTF8.GetString(plainBytes);
            }
        }
    }

    // IV와 암호문을 합친 바이트에 대한 서명을 계산한다.
    private byte[] Sign(byte[] ivAndCipher)
    {
        using (HMACSHA256 hmac = new HMACSHA256(saveKey.SignKey))
        {
            return hmac.ComputeHash(ivAndCipher);
        }
    }

    // 파일 바이트 뒤 32바이트를 서명으로 보고, 앞부분으로 다시 계산한 값과 같은지 검사한다. 다르면 CryptographicException을 던진다.
    private void Verify(byte[] fileBytes)
    {
        if (fileBytes.Length <= IvSize + SignSize)
        {
            throw new CryptographicException("파일 길이가 IV+서명보다 짧습니다.");
        }

        byte[] ivAndCipher = TakeIvAndCipher(fileBytes);
        byte[] storedSign = TakeSign(fileBytes);
        byte[] expectedSign = Sign(ivAndCipher);

        if (!CryptographicOperations.FixedTimeEquals(storedSign, expectedSign))
        {
            throw new CryptographicException("서명이 일치하지 않습니다.");
        }
    }

    // 파일 바이트에서 서명을 뗀 나머지(IV+암호문)를 잘라낸다.
    private static byte[] TakeIvAndCipher(byte[] fileBytes)
    {
        int length = fileBytes.Length - SignSize;
        byte[] result = new byte[length];
        Buffer.BlockCopy(fileBytes, 0, result, 0, length);
        return result;
    }

    // 파일 바이트 뒤 32바이트(서명)를 잘라낸다.
    private static byte[] TakeSign(byte[] fileBytes)
    {
        byte[] result = new byte[SignSize];
        Buffer.BlockCopy(fileBytes, fileBytes.Length - SignSize, result, 0, SignSize);
        return result;
    }

    // IV+암호문에서 앞 16바이트(IV)를 잘라낸다.
    private static byte[] TakeIv(byte[] ivAndCipher)
    {
        byte[] result = new byte[IvSize];
        Buffer.BlockCopy(ivAndCipher, 0, result, 0, IvSize);
        return result;
    }

    // IV+암호문에서 IV 뒤(암호문)를 잘라낸다.
    private static byte[] TakeCipherText(byte[] ivAndCipher)
    {
        int length = ivAndCipher.Length - IvSize;
        byte[] result = new byte[length];
        Buffer.BlockCopy(ivAndCipher, IvSize, result, 0, length);
        return result;
    }

    // 두 바이트 배열을 순서대로 이어붙인다.
    private static byte[] Combine(byte[] first, byte[] second)
    {
        byte[] result = new byte[first.Length + second.Length];
        Buffer.BlockCopy(first, 0, result, 0, first.Length);
        Buffer.BlockCopy(second, 0, result, first.Length, second.Length);
        return result;
    }
}
