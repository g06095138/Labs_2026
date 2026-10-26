using System.Security.Cryptography;
using Taxi.Application;
namespace Taxi.Infrastructure;
public sealed class UserStore(SqliteStore store)
{
    private const int Iterations=100000;
    private static string Hash(string password)
    {
        byte[] salt=RandomNumberGenerator.GetBytes(16);
        byte[] hash=Rfc2898DeriveBytes.Pbkdf2(password,salt,Iterations,HashAlgorithmName.SHA256,32);
        return Convert.ToBase64String(salt)+":"+Convert.ToBase64String(hash);
    }
    public void SeedDemoUsers()
    {
        using var c=store.Open(); using var tx=c.BeginTransaction();
        foreach(var (login,role,client,driver) in new (string,Role,string?,string?)[] {
            ("client",Role.Client,"client-1",null),("client2",Role.Client,"client-2",null),
            ("dispatcher",Role.Dispatcher,null,null),("driver1",Role.Driver,null,"driver-1"),
            ("driver2",Role.Driver,null,"driver-2"),("driver3",Role.Driver,null,"driver-3")})
        {
            using var exists=SqliteStore.Cmd(c,"SELECT count(*) FROM Users WHERE login=@login",tx,("@login",login));
            if(Convert.ToInt64(exists.ExecuteScalar())>0) continue;
            using var cmd=SqliteStore.Cmd(c,"INSERT INTO Users VALUES(@id,@login,@hash,@role,@client,@driver)",tx,
                ("@id",Guid.NewGuid().ToString("N")),("@login",login),("@hash",Hash("TaxiDemo!2026")),("@role",role.ToString()),("@client",client),("@driver",driver));
            cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
    public Actor? Authenticate(string login,string password)
    {
        using var c=store.Open(); using var cmd=SqliteStore.Cmd(c,"SELECT id,password_hash,role,client_id,driver_id FROM Users WHERE login=@login",null,("@login",login));
        using var r=cmd.ExecuteReader();
        if(!r.Read()) { _=Rfc2898DeriveBytes.Pbkdf2(password,new byte[16],Iterations,HashAlgorithmName.SHA256,32); return null; }
        var parts=r.GetString(1).Split(':'); var salt=Convert.FromBase64String(parts[0]); var expected=Convert.FromBase64String(parts[1]);
        var actual=Rfc2898DeriveBytes.Pbkdf2(password,salt,Iterations,HashAlgorithmName.SHA256,32);
        if(!CryptographicOperations.FixedTimeEquals(expected,actual)) return null;
        return new(r.GetString(0),Enum.Parse<Role>(r.GetString(2)),r.IsDBNull(3)?null:r.GetString(3),r.IsDBNull(4)?null:r.GetString(4));
    }
}
