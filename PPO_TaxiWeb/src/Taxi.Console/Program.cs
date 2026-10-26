using Taxi.Domain;
var calculator=new QuoteService();
foreach(var q in calculator.Compare(new[]{new Tariff("economy","Эконом",100,20),new Tariff("comfort","Комфорт",150,30),new Tariff("business","Бизнес",250,50)},4))
    System.Console.WriteLine($"{q.Name}: база {q.BasePrice:F2}, переменная часть {q.VariablePrice:F2}, итог {q.Total:F2} RUB");
