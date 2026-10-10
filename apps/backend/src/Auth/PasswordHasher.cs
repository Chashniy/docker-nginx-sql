namespace Backend.Auth;

public class PasswordHasher
{
    public string Hash(string password)
    {
        return BCrypt.Net.BCrypt.HashPassword(password, workFactor: 11);
    }

    public bool Verify(string password, string hash)
    {
        try
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
        catch
        {
            // Если хеш повреждён или невалидный — считаем, что пароль не подходит
            return false;
        }
    }
}