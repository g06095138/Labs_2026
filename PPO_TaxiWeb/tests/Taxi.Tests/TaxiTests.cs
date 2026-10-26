using Taxi.Domain;
using Taxi.Application;
using Taxi.Infrastructure;
using Microsoft.Data.Sqlite;
using Xunit;
namespace Taxi.Tests;
public sealed class QuoteTests
{
    private readonly QuoteService calc=new();
    [Fact] public void Quote_ComfortFourKm(){var q=calc.Calculate(new("c","Комфорт",150,30),4);Assert.Equal(270m,q.Total);Assert.Equal(120m,q.VariablePrice);}
    [Fact] public void Quote_RoundsHalfKopeck(){Assert.Equal(100.01m,calc.Calculate(new("e","Эконом",100,0.50m),0.01m).Total);}
    [Fact] public void Quote_FreeTariff(){Assert.Equal(0m,calc.Calculate(new("f","Бесплатно",0,0),4).Total);}
    [Fact] public void Quote_RejectsNegativeDistance(){Assert.Equal("DistanceKm",Assert.Throws<RuleException>(()=>calc.Calculate(new("e","Эконом",100,20),-1)).Field);}
    [Theory][InlineData("0")][InlineData("1000.01")][InlineData("0.001")]
    public void Quote_InvalidDistance(string s){Assert.Throws<RuleException>(()=>calc.Calculate(new("e","Эконом",100,20),decimal.Parse(s,System.Globalization.CultureInfo.InvariantCulture)));}
    [Fact] public void Quote_Comparison(){var qs=calc.Compare(new[]{new Tariff("e","Эконом",100,20),new Tariff("c","Комфорт",150,30),new Tariff("b","Бизнес",250,50)},4);Assert.Equal(new[]{180m,270m,450m},qs.Select(q=>q.Total));}
    [Fact] public void Quote_RepeatDoesNotMutate(){var t=new Tariff("e","Эконом",100,20);var before=t with{};Assert.Equal(calc.Calculate(t,4),calc.Calculate(t,4));Assert.Equal(300m,calc.Calculate(t,10).Total);Assert.Equal(before,t);}
    [Theory][InlineData(-1,20)][InlineData(100,-1)][InlineData(1000001,20)] public void Quote_InvalidTariff(int b,int r){Assert.Throws<RuleException>(()=>calc.Calculate(new("x","Тариф",b,r),4));}
    [Fact] public void Quote_InactiveOrEmpty(){Assert.Throws<RuleException>(()=>calc.Calculate(new("x","Тариф",100,20,false),4));Assert.Throws<RuleException>(()=>calc.Compare([],4));}
}
public sealed class StoreTests:IDisposable
{
    private readonly string directory=Path.Combine(Path.GetTempPath(),"taxi-tests-"+Guid.NewGuid().ToString("N"));
    private readonly SqliteStore store;
    private readonly TaxiService service;
    private readonly Actor client=new("u1",Role.Client,"client-1");
    private readonly Actor client2=new("u2",Role.Client,"client-2");
    private readonly Actor dispatcher=new("ud",Role.Dispatcher);
    private readonly Actor driver=new("uv",Role.Driver,DriverId:"driver-1");
    private string Db=>Path.Combine(directory,"test.db");
    public StoreTests(){store=new(Db);store.Initialize();service=new(store,new());}
    private static TripInput Input()=>new("Глеб Преснов","+79991234567","Вокзал","Университет",4,"comfort");
    [Fact] public void Create_ValidatesAndStores(){var o=service.Create(client,Input());Assert.Equal(270m,o.Cost);Assert.Equal(OrderStatus.Created,o.Status);Assert.Null(o.DriverId);Assert.Equal("client-1",o.ClientId);Assert.Equal(o,service.Get(client,o.Id));}
    [Theory][InlineData("")][InlineData(" ")]
    public void Create_RejectsEmptyFields(string field){Assert.Throws<RuleException>(()=>service.Create(client,Input() with{Pickup=field}));Assert.Empty(store.Orders());}
    [Theory][InlineData("0")][InlineData("-1")][InlineData("1000.01")]
    public void Create_RejectsInvalidDistance(string s){Assert.Throws<RuleException>(()=>service.Create(client,Input() with{DistanceKm=decimal.Parse(s,System.Globalization.CultureInfo.InvariantCulture)}));Assert.Empty(store.Orders());}
    [Fact] public void Create_UnknownTariffRollsBack(){Assert.Throws<RuleException>(()=>service.Create(client,Input() with{TariffId="missing"}));Assert.Empty(store.Orders());}
    [Fact] public void Storage_RoundTripAndRestart(){var o=service.Create(client,Input() with{DistanceKm=4.01m});var second=new SqliteStore(Db);second.Initialize();Assert.Equal(o,second.Get(o.Id));Assert.Equal(270.30m,second.Get(o.Id)!.Cost);Assert.Single(second.Orders());}
    [Fact] public void Storage_RoundsAndRetainsKopeck(){using(var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Db,ForeignKeys=true}.ToString())){c.Open();using var cmd=c.CreateCommand();cmd.CommandText="INSERT INTO Tariffs VALUES('half','Половина',10000,50,1)";cmd.ExecuteNonQuery();}var o=service.Create(client,Input() with{TariffId="half",DistanceKm=0.01m});Assert.Equal(100.01m,new SqliteStore(Db).Get(o.Id)!.Cost);}
    [Fact] public void Workflow_Completes(){var o=service.Create(client,Input());var other=service.Create(client2,Input());service.Assign(dispatcher,o.Id,"driver-1");Assert.False(store.Drivers().Single(d=>d.Id=="driver-1").Available);Assert.Equal(OrderStatus.Assigned,store.Get(o.Id)!.Status);service.Start(driver,o.Id);Assert.Equal(OrderStatus.InProgress,store.Get(o.Id)!.Status);service.Complete(driver,o.Id);var reopened=new SqliteStore(Db);Assert.Equal(OrderStatus.Completed,reopened.Get(o.Id)!.Status);Assert.Equal(270m,reopened.Get(o.Id)!.Cost);Assert.True(reopened.Drivers().Single(d=>d.Id=="driver-1").Available);Assert.Equal(other,reopened.Get(other.Id));Assert.Equal(2,reopened.Orders().Count);Assert.Throws<RuleException>(()=>service.Complete(driver,o.Id));}
    [Fact] public void Assignment_IsAtomic(){var a=service.Create(client,Input());var b=service.Create(client,Input());service.Assign(dispatcher,a.Id,"driver-1");Assert.Throws<RuleException>(()=>service.Assign(dispatcher,b.Id,"driver-1"));Assert.Equal(OrderStatus.Created,store.Get(b.Id)!.Status);Assert.Null(store.Get(b.Id)!.DriverId);Assert.Throws<RuleException>(()=>service.Assign(dispatcher,a.Id,"driver-2"));Assert.True(store.Drivers().Single(d=>d.Id=="driver-2").Available);}
    [Fact] public async Task Assignment_ConcurrentRequests(){var a=service.Create(client,Input());var b=service.Create(client,Input());int success=0;var tasks=new[]{a,b}.Select(o=>Task.Run(()=>{try{new SqliteStore(Db).Assign(o.Id,"driver-1");Interlocked.Increment(ref success);}catch(RuleException){}}));await Task.WhenAll(tasks);Assert.Equal(1,success);Assert.Single(store.Orders(),o=>o.Status==OrderStatus.Assigned);}
    [Fact] public void Roles_EnforceOwnership(){var o=service.Create(client,Input());Assert.Empty(service.List(client2));Assert.Throws<RuleException>(()=>service.Get(client2,o.Id));Assert.Throws<RuleException>(()=>service.Assign(client,o.Id,"driver-1"));Assert.Throws<RuleException>(()=>service.Create(driver,Input()));service.Assign(dispatcher,o.Id,"driver-1");var driver2=new Actor("v2",Role.Driver,DriverId:"driver-2");Assert.Empty(service.List(driver2));Assert.Throws<RuleException>(()=>service.Start(driver2,o.Id));Assert.Equal(OrderStatus.Assigned,store.Get(o.Id)!.Status);Assert.Single(service.List(driver));}
    [Fact] public void Workflow_RejectsInvalidTransitions(){var o=service.Create(client,Input());Assert.Throws<RuleException>(()=>service.Start(driver,o.Id));service.Assign(dispatcher,o.Id,"driver-1");Assert.Throws<RuleException>(()=>service.Complete(driver,o.Id));service.Start(driver,o.Id);Assert.Throws<RuleException>(()=>service.Start(driver,o.Id));}
    [Fact] public void Storage_ForeignKeysAndUniqueActive(){var a=service.Create(client,Input());var b=service.Create(client,Input());service.Assign(dispatcher,a.Id,"driver-1");using var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=Db,ForeignKeys=true}.ToString());c.Open();using var cmd=c.CreateCommand();cmd.CommandText="UPDATE Orders SET driver_id='driver-1',status='Assigned' WHERE id=@id";cmd.Parameters.AddWithValue("@id",b.Id);Assert.Throws<SqliteException>(()=>cmd.ExecuteNonQuery());cmd.Parameters.Clear();cmd.CommandText="DELETE FROM Clients WHERE id='client-1'";Assert.Throws<SqliteException>(()=>cmd.ExecuteNonQuery());}
    [Fact] public void Auth_ValidAndInvalid(){var users=new UserStore(store);users.SeedDemoUsers();users.SeedDemoUsers();Assert.Equal(Role.Dispatcher,users.Authenticate("dispatcher","TaxiDemo!2026")!.Role);Assert.Equal("driver-1",users.Authenticate("driver1","TaxiDemo!2026")!.DriverId);Assert.Null(users.Authenticate("client","wrong"));Assert.Null(users.Authenticate("unknown","TaxiDemo!2026"));}
    public void Dispose(){SqliteConnection.ClearAllPools();if(Directory.Exists(directory))Directory.Delete(directory,true);}
}
