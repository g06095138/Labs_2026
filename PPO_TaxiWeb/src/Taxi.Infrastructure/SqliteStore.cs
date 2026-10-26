using System.Globalization;
using Microsoft.Data.Sqlite;
using Taxi.Application;
using Taxi.Domain;
namespace Taxi.Infrastructure;
public sealed class SqliteStore
    : ITaxiStore
{
    private readonly string connectionString;
    public SqliteStore(string path)
    {
        string absolute = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(absolute)!);
        connectionString = new SqliteConnectionStringBuilder { DataSource = absolute, ForeignKeys = true, DefaultTimeout = 5 }.ToString();
    }
    internal SqliteConnection Open() { var c = new SqliteConnection(connectionString); c.Open(); return c; }
    internal static SqliteCommand Cmd(SqliteConnection c, string sql, SqliteTransaction? tx = null, params (string, object?)[] args)
    {
        var cmd = c.CreateCommand(); cmd.CommandText = sql; cmd.Transaction = tx;
        foreach (var (key,value) in args) cmd.Parameters.AddWithValue(key, value ?? DBNull.Value);
        return cmd;
    }
    public void Initialize()
    {
        using var c = Open(); using var tx = c.BeginTransaction();
        using var cmd = Cmd(c, """
        CREATE TABLE IF NOT EXISTS Clients(id TEXT PRIMARY KEY,full_name TEXT NOT NULL,phone TEXT NOT NULL);
        CREATE TABLE IF NOT EXISTS Drivers(id TEXT PRIMARY KEY,full_name TEXT NOT NULL,car_name TEXT NOT NULL,car_number TEXT NOT NULL,is_available INTEGER NOT NULL CHECK(is_available IN(0,1)));
        CREATE TABLE IF NOT EXISTS Tariffs(id TEXT PRIMARY KEY,name TEXT NOT NULL UNIQUE,base_kopecks INTEGER NOT NULL CHECK(base_kopecks>=0),rate_kopecks INTEGER NOT NULL CHECK(rate_kopecks>=0),is_active INTEGER NOT NULL CHECK(is_active IN(0,1)));
        CREATE TABLE IF NOT EXISTS Orders(
          id TEXT PRIMARY KEY,client_id TEXT NOT NULL REFERENCES Clients(id) ON DELETE RESTRICT,
          driver_id TEXT REFERENCES Drivers(id) ON DELETE RESTRICT,tariff_id TEXT NOT NULL REFERENCES Tariffs(id) ON DELETE RESTRICT,
          pickup TEXT NOT NULL,destination TEXT NOT NULL,distance_hundredths INTEGER NOT NULL CHECK(distance_hundredths BETWEEN 1 AND 100000),
          base_kopecks INTEGER NOT NULL CHECK(base_kopecks>=0),rate_kopecks INTEGER NOT NULL CHECK(rate_kopecks>=0),cost_kopecks INTEGER NOT NULL CHECK(cost_kopecks>=0),
          status TEXT NOT NULL CHECK(status IN('Created','Assigned','InProgress','Completed')),created_at TEXT NOT NULL,
          CHECK((status='Created' AND driver_id IS NULL) OR (status<>'Created' AND driver_id IS NOT NULL)));
        CREATE UNIQUE INDEX IF NOT EXISTS one_active_per_driver ON Orders(driver_id) WHERE status IN('Assigned','InProgress');
        CREATE TABLE IF NOT EXISTS Users(id TEXT PRIMARY KEY,login TEXT NOT NULL UNIQUE,password_hash TEXT NOT NULL,
          role TEXT NOT NULL CHECK(role IN('Client','Dispatcher','Driver')),
          client_id TEXT UNIQUE REFERENCES Clients(id),driver_id TEXT UNIQUE REFERENCES Drivers(id),
          CHECK((role='Client' AND client_id IS NOT NULL AND driver_id IS NULL) OR (role='Driver' AND driver_id IS NOT NULL AND client_id IS NULL) OR (role='Dispatcher' AND driver_id IS NULL AND client_id IS NULL)));
        INSERT OR IGNORE INTO Clients VALUES('client-1','Демонстрационный клиент','+70000000000');
        INSERT OR IGNORE INTO Clients VALUES('client-2','Второй клиент','+70000000001');
        INSERT OR IGNORE INTO Drivers VALUES('driver-1','Пётр Водитель','Kia Rio','А001АА',1);
        INSERT OR IGNORE INTO Drivers VALUES('driver-2','Иван Водитель','Hyundai Solaris','А002АА',1);
        INSERT OR IGNORE INTO Drivers VALUES('driver-3','Сергей Водитель','Toyota Camry','А003АА',1);
        INSERT OR IGNORE INTO Tariffs VALUES('economy','Эконом',10000,2000,1);
        INSERT OR IGNORE INTO Tariffs VALUES('comfort','Комфорт',15000,3000,1);
        INSERT OR IGNORE INTO Tariffs VALUES('business','Бизнес',25000,5000,1);
        """, tx); cmd.ExecuteNonQuery(); tx.Commit();
    }
    public IReadOnlyList<Tariff> Tariffs()
    {
        using var c = Open(); using var cmd = Cmd(c,"SELECT id,name,base_kopecks,rate_kopecks,is_active FROM Tariffs ORDER BY base_kopecks");
        using var r = cmd.ExecuteReader(); var result = new List<Tariff>();
        while(r.Read()) result.Add(new(r.GetString(0),r.GetString(1),r.GetInt64(2)/100m,r.GetInt64(3)/100m,r.GetBoolean(4)));
        return result;
    }
    public IReadOnlyList<Driver> Drivers()
    {
        using var c=Open(); using var cmd=Cmd(c,"SELECT * FROM Drivers ORDER BY id"); using var r=cmd.ExecuteReader(); var result=new List<Driver>();
        while(r.Read()) result.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetBoolean(4))); return result;
    }
    public IReadOnlyList<Order> Orders()
    {
        using var c=Open(); using var cmd=Cmd(c,"SELECT * FROM Orders ORDER BY created_at DESC,id"); using var r=cmd.ExecuteReader(); var result=new List<Order>();
        while(r.Read()) result.Add(Read(r)); return result;
    }
    public Order? Get(string id)
    { using var c=Open(); return Get(c,null,id); }
    private static Order? Get(SqliteConnection c,SqliteTransaction? tx,string id)
    { using var cmd=Cmd(c,"SELECT * FROM Orders WHERE id=@id",tx,("@id",id)); using var r=cmd.ExecuteReader(); return r.Read()?Read(r):null; }
    private static Order Read(SqliteDataReader r) => new(r.GetString(0),r.GetString(1),r.IsDBNull(2)?null:r.GetString(2),r.GetString(3),r.GetString(4),r.GetString(5),r.GetInt64(6)/100m,r.GetInt64(7)/100m,r.GetInt64(8)/100m,r.GetInt64(9)/100m,Enum.Parse<OrderStatus>(r.GetString(10)),DateTimeOffset.Parse(r.GetString(11),CultureInfo.InvariantCulture));
    public Order Create(string clientId,TripInput input)
    {
        input.Validate(); using var c=Open(); using var tx=c.BeginTransaction(deferred:false);
        using var tariffCmd=Cmd(c,"SELECT name,base_kopecks,rate_kopecks,is_active FROM Tariffs WHERE id=@id",tx,("@id",input.TariffId));
        Tariff tariff;
        using(var r=tariffCmd.ExecuteReader()) {
            if(!r.Read()) throw new RuleException("Tariff","Тариф не найден.");
            tariff=new(input.TariffId,r.GetString(0),r.GetInt64(1)/100m,r.GetInt64(2)/100m,r.GetBoolean(3));
        }
        var quote=new QuoteService().Calculate(tariff,input.DistanceKm);
        using(var client=Cmd(c,"UPDATE Clients SET full_name=@name,phone=@phone WHERE id=@id",tx,("@name",input.FullName.Trim()),("@phone",input.Phone.Trim()),("@id",clientId)))
            if(client.ExecuteNonQuery()!=1) throw new RuleException("Client","Клиент не найден.");
        var id=Guid.NewGuid().ToString("N");
        using(var cmd=Cmd(c,"""INSERT INTO Orders VALUES(@id,@client,NULL,@tariff,@pickup,@destination,@distance,@base,@rate,@cost,'Created',@at)""",tx,
          ("@id",id),("@client",clientId),("@tariff",tariff.Id),("@pickup",input.Pickup.Trim()),("@destination",input.Destination.Trim()),("@distance",(long)(input.DistanceKm*100)),("@base",(long)(tariff.BasePrice*100)),("@rate",(long)(tariff.Rate*100)),("@cost",(long)(quote.Total*100)),("@at",DateTimeOffset.UtcNow.ToString("O")))) cmd.ExecuteNonQuery();
        var result=Get(c,tx,id)!; tx.Commit(); return result;
    }
    public void Assign(string orderId,string driverId)
    {
        using var c=Open(); using var tx=c.BeginTransaction(deferred:false);
        var order=Get(c,tx,orderId)??throw new RuleException("Order","Заказ не найден.");
        if(order.Status!=OrderStatus.Created) throw new RuleException("Status","Назначить можно только созданный заказ.");
        using(var cmd=Cmd(c,"UPDATE Drivers SET is_available=0 WHERE id=@id AND is_available=1",tx,("@id",driverId)))
            if(cmd.ExecuteNonQuery()!=1) throw new RuleException("Driver","Водитель недоступен.");
        using(var cmd=Cmd(c,"UPDATE Orders SET driver_id=@driver,status='Assigned' WHERE id=@id AND status='Created'",tx,("@driver",driverId),("@id",orderId)))
            if(cmd.ExecuteNonQuery()!=1) throw new RuleException("Status","Заказ уже изменён.");
        tx.Commit();
    }
    public void Transition(string orderId,string driverId,OrderStatus from,OrderStatus to)
    {
        if(!((from==OrderStatus.Assigned && to==OrderStatus.InProgress)||(from==OrderStatus.InProgress && to==OrderStatus.Completed))) throw new RuleException("Status","Недопустимый переход.");
        using var c=Open(); using var tx=c.BeginTransaction(deferred:false);
        var order=Get(c,tx,orderId)??throw new RuleException("Order","Заказ не найден.");
        if(order.DriverId!=driverId) throw new RuleException("Role","Нет доступа к заказу.");
        if(order.Status!=from) throw new RuleException("Status","Недопустимый статус заказа.");
        using(var cmd=Cmd(c,"UPDATE Orders SET status=@status WHERE id=@id AND status=@from AND driver_id=@driver",tx,("@status",to.ToString()),("@id",orderId),("@from",from.ToString()),("@driver",driverId)))
            if(cmd.ExecuteNonQuery()!=1) throw new RuleException("Status","Заказ уже изменён.");
        if(to==OrderStatus.Completed) {
            using var cmd=Cmd(c,"UPDATE Drivers SET is_available=1 WHERE id=@id",tx,("@id",driverId)); cmd.ExecuteNonQuery();
        }
        tx.Commit();
    }
}
