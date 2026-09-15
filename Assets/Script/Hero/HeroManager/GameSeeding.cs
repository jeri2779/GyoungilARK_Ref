using System;
using System.Security.Cryptography;
using System.Text;

// 시드 문자열+순번을 안정적인 정수 하나로 합친다 (같은 입력이면 항상 같은 정수).
// 영웅 뽑기/합성처럼 "고정 시드 + 소비할 때마다 늘어나는 순번"으로 결과를 재현해야 하는 곳에서 공용으로 쓴다.
public static class GameSeeding
{
    public static int Derive(string seed, int count)
    {
        byte[] bytes = Encoding.UTF8.GetBytes($"{seed}:{count}");
        using (SHA256 sha256 = SHA256.Create())
        {
            byte[] hash = sha256.ComputeHash(bytes);
            return BitConverter.ToInt32(hash, 0);
        }
    }
}
