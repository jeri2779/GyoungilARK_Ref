using System.Security.Cryptography;
using System.Text;

// 암호용 열쇠와 서명용 열쇠를 각각 32바이트로 보관한다. 암호화·서명 계산은 하지 않는다.
public class SaveKey
{
    private static readonly string[] cipherFragments = { "K9x2vQ", "mPz7Lr", "Tq4Wsb1" };
    private static readonly string[] signFragments = { "Hd8Yfn", "Bx3Ckt", "Ln6Rpe2" };

    public byte[] CipherKey { get; }
    public byte[] SignKey { get; }

    // 조각 문자열을 실행 시점에 합쳐 암호용 열쇠와 서명용 열쇠를 만든다.
    public SaveKey()
    {
        CipherKey = BuildKey(cipherFragments);
        SignKey = BuildKey(signFragments);
    }

    // 조각 문자열을 이어붙여 SHA-256으로 고정 32바이트 열쇠를 만든다.
    private static byte[] BuildKey(string[] fragments)
    {
        string combined = string.Concat(fragments);
        byte[] combinedBytes = Encoding.UTF8.GetBytes(combined);

        using (SHA256 sha256 = SHA256.Create())
        {
            return sha256.ComputeHash(combinedBytes);
        }
    }
}
