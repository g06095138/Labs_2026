# ЛР2. Проектирование
## Слои и зависимости
Domain: QuoteService, Tariff, Order, TripInput, OrderStatus; формула и валидация входа. Не зависит от других слоёв.
Application: TaxiService и ITaxiStore; роли, сценарии, проверки прав. Зависит от Domain.
Infrastructure: SqliteStore, схема, транзакции, UserStore. Зависит от Application и Domain.
UI: Blazor Web App (Interactive Server), cookie-вход; вызывает Application. Ссылка на Infrastructure нужна только для composition root Program.cs.
Консоль — отдельный адаптер демонстрации расчёта. Тесты — xUnit, изолированные временные БД.

## Контракты операций
| Операция | Вход | Результат | Ошибки |
|---|---|---|---|
| Compare | distance decimal; список активных тарифов | независимые Quote с base, variable, total | DistanceKm, Tariff, список пуст |
| Create | серверный Actor(Client), TripInput | новый ID, статус Created, цена-снимок | роль, поле, неактивный тариф |
| List/Get | Actor, при открытии ID | записи в границах роли | неизвестный ID, запрет доступа |
| Assign | Actor(Dispatcher), orderId, driverId | Assigned, IsAvailable=false | не существует, не Created, водитель занят |
| Start | Actor(Driver), orderId | InProgress | чужой заказ, не Assigned |
| Complete | Actor(Driver), orderId | Completed и IsAvailable=true | чужой заказ, не InProgress |

Ошибки имеют Field и Message. Ошибки бизнес-валидации показываются пользователю; внутренние ошибки логируются, без строк подключения в UI. Цена рассчитывается заново при Create, не берётся из браузера. Actor получается из подписанной cookie, не из editable-поля. Идентификатор пользователя связывается с client_id или driver_id через таблицу Users.

## Атомарность и конкурентность
Назначение берёт BEGIN IMMEDIATE: проверяет заказ, условно меняет доступность водителя, затем условно меняет заказ. Неуспех откатывает транзакцию. Дополнительный частичный UNIQUE-индекс по driver_id для активных заказов предотвращает двойное назначение. Start/Complete проверяют текущий статус и driver_id в транзакции. Complete освобождает только исполнителя завершённого заказа. SQLite busy_timeout=5000; сетевые повторения команды не считаются новой операцией.

## SRP и DIP
SRP: QuoteService отвечает за расчёт; Razor-компоненты — за ввод/представление. Изменение формулы не требует копировать её в UI. Application связывает шаги сценария, Infrastructure обеспечивает атомарную запись.
DIP: Application объявляет ITaxiStore и не знает SQLite. Реализация подключается через DI. Транзакционные доменные операции представлены методами интерфейса, чтобы между отдельными вызовами чтения/записи не возникали гонки.

## Ручной проход сценария
Комфорт: base=150, rate=30, distance=4 → 270.00. После Create: Created, driver=null. Assign(driver-1): Assigned, available=false. Start(driver-1): InProgress, 270.00. Complete(driver-1): Completed, available=true, 270.00. Попытка второго Complete отклоняется; изменений нет.
Аналог исходного прохода трёх месяцев — три перехода одного заказа: при каждом переносится идентичность заказа и фиксированная стоимость, а не финансовые остатки.
