# ЛР2 / ЛР4. Модель данных и SQLite
## Концептуальная модель
Клиент создаёт 0…N заказов, каждый заказ имеет одного клиента. Тариф применяется к 0…N заказов, заказ имеет один тариф. Водитель выполняет 0…N заказов за всё время, заказ имеет 0…1 исполнителя. Сущность Users добавлена для серверных ролей; исходная курсовая авторизации не содержит.

## Физическая модель
| Таблица | Поля |
|---|---|
| Clients | id TEXT PK; full_name TEXT NOT NULL; phone TEXT NOT NULL |
| Drivers | id TEXT PK; full_name, car_name, car_number TEXT NOT NULL; is_available INTEGER NOT NULL CHECK 0/1 |
| Tariffs | id TEXT PK; name TEXT NOT NULL UNIQUE; base_kopecks, rate_kopecks INTEGER NOT NULL CHECK >=0; is_active INTEGER NOT NULL CHECK 0/1 |
| Orders | id TEXT PK; client_id TEXT NOT NULL FK RESTRICT; driver_id TEXT NULL FK RESTRICT; tariff_id TEXT NOT NULL FK RESTRICT; pickup,destination TEXT NOT NULL; distance_hundredths INTEGER CHECK 1…100000; base_kopecks,rate_kopecks,cost_kopecks INTEGER NOT NULL CHECK >=0; status TEXT CHECK; created_at TEXT UTC ISO8601 |
| Users | id TEXT PK; login TEXT UNIQUE; password_hash TEXT NOT NULL; role TEXT CHECK; client_id или driver_id FK, UNIQUE; диспетчер без обоих FK |

Деньги хранятся INTEGER копеек, расстояние — INTEGER сотых километра. Конвертация точная, ToEven не используется. Цена и компоненты сохраняются как исторический снимок. Вычисляемая таблица сравнения не хранится. Даты ISO8601 UTC.

FOREIGN KEY включается в каждой строке подключения (Foreign Keys=True). Создание схемы и сидов — одна транзакция; повторный запуск не перезаписывает заказы. Статусы: Created, Assigned, InProgress, Completed. CHECK согласует наличие водителя со статусом. Частичный UNIQUE Orders(driver_id) WHERE status IN ('Assigned','InProgress') запрещает два активных заказа.

SQLite не обеспечивает авторизацию пользователей приложения: её выполняет Application. Файл БД размещён вне wwwroot. Пароли: PBKDF2-SHA256, случайная соль, 100000 итераций; сравнение в фиксированное время. Демонстрационные пароли не предназначены для внешнего размещения.

Сохранение заказа — Create; изменение существующего ID — Assign/Start/Complete. Редактирования уже оформленного маршрута и произвольного UPSERT нет: адаптация не должна позволять обойти жизненный цикл. Отдельный пример повторного Create создаёт самостоятельный заказ, в отличие от обновления состояния существующего.
